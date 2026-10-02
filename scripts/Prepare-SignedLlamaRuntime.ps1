<#
.SYNOPSIS
    Authorizes and stages the pinned upstream llama.cpp runtime for signing.

.DESCRIPTION
    Downloads or reads the exact archives declared by the reviewed release
    manifest, verifies their size and SHA-256, and extracts only the runtime
    closure used by OpenClaw's managed llama-server. The generated authorization
    manifest binds every staged file before the signing job requests credentials.
#>

[CmdletBinding()]
param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot '..\.github\llama-runtime-release.json'),

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$DownloadRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-LowerHex {
    param([Parameter(Mandatory = $true)][string]$Value, [Parameter(Mandatory = $true)][int]$Length)

    return $Value.Length -eq $Length -and $Value -cmatch "^[0-9a-f]{$Length}$"
}

function Assert-ReleaseManifest {
    param([Parameter(Mandatory = $true)]$Manifest)

    if ($Manifest.schemaVersion -ne 1) {
        throw 'The llama runtime release manifest schema version is unsupported.'
    }
    if ($Manifest.releaseTag -cnotmatch '^llama-b[0-9]+-openclaw\.[1-9][0-9]*$') {
        throw 'The llama runtime release tag is not canonical.'
    }
    if ($Manifest.upstream.repository -cne 'ggml-org/llama.cpp' -or
        $Manifest.upstream.tag -cnotmatch '^b[0-9]+$' -or
        -not (Test-LowerHex -Value $Manifest.upstream.commit -Length 40)) {
        throw 'The pinned upstream llama.cpp identity is invalid.'
    }
    if ([string]::IsNullOrWhiteSpace($Manifest.signerSubject)) {
        throw 'The expected signing subject is missing.'
    }
    if ($Manifest.license.name -cne 'LICENSE' -or
        [long]$Manifest.license.size -le 0 -or
        -not (Test-LowerHex -Value ([string]$Manifest.license.sha256) -Length 64)) {
        throw 'The pinned upstream llama.cpp license is invalid.'
    }

    $assets = @($Manifest.assets)
    if ($assets.Count -ne 2) {
        throw 'The release manifest must contain exactly x64 and ARM64 assets.'
    }
    $architectures = @($assets | ForEach-Object { [string]$_.architecture })
    if (@($architectures | Sort-Object -Unique).Count -ne 2 -or
        $architectures -notcontains 'x64' -or
        $architectures -notcontains 'arm64') {
        throw 'The release manifest must contain one x64 and one ARM64 asset.'
    }

    foreach ($asset in $assets) {
        $expectedName = "llama-$($Manifest.upstream.tag)-bin-win-cuda-13.4-$($asset.architecture).zip"
        $expectedOutput = "openclaw-llama-$($Manifest.upstream.tag)-bin-win-cuda-13.4-$($asset.architecture).zip"
        if ($asset.sourceName -cne $expectedName -or $asset.outputName -cne $expectedOutput) {
            throw "The $($asset.architecture) runtime asset names are not canonical."
        }
        if ([long]$asset.sourceSize -le 0 -or
            -not (Test-LowerHex -Value ([string]$asset.sourceSha256) -Length 64)) {
            throw "The $($asset.architecture) runtime asset pin is invalid."
        }
    }
}

function Test-IncludedRuntimeFile {
    param([Parameter(Mandatory = $true)][string]$Name)

    return $Name -ceq 'LICENSE-LLVM-OpenMP' -or
        $Name -ceq 'libomp.dll' -or
        $Name -ceq 'llama-server.exe' -or
        $Name -ceq 'llama-server-impl.dll' -or
        $Name -ceq 'llama-common.dll' -or
        $Name -ceq 'llama.dll' -or
        $Name -ceq 'mtmd.dll' -or
        $Name -cmatch '^ggml(?:-[A-Za-z0-9]+)*\.dll$'
}

$manifestFile = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
Assert-ReleaseManifest -Manifest $manifest

$outputRoot = [System.IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $outputRoot) {
    throw "The output directory already exists: $outputRoot"
}
New-Item -ItemType Directory -Path $outputRoot | Out-Null

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$authorizationAssets = New-Object System.Collections.Generic.List[object]
$licensePath = if ([string]::IsNullOrWhiteSpace($DownloadRoot)) {
    Join-Path $outputRoot $manifest.license.name
}
else {
    Join-Path ([System.IO.Path]::GetFullPath($DownloadRoot)) $manifest.license.name
}
if ([string]::IsNullOrWhiteSpace($DownloadRoot)) {
    $licenseUri = "https://raw.githubusercontent.com/$($manifest.upstream.repository)/$($manifest.upstream.commit)/LICENSE"
    Invoke-WebRequest -Uri $licenseUri -OutFile $licensePath
}
elseif (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    throw "The supplied upstream license does not exist: $licensePath"
}
$licenseFile = Get-Item -LiteralPath $licensePath
if ($licenseFile.Length -ne [long]$manifest.license.size -or
    (Get-Sha256 -Path $licensePath) -cne [string]$manifest.license.sha256) {
    throw 'The upstream llama.cpp license failed size or SHA-256 verification.'
}

foreach ($asset in @($manifest.assets)) {
    $sourcePath = if ([string]::IsNullOrWhiteSpace($DownloadRoot)) {
        Join-Path $outputRoot $asset.sourceName
    }
    else {
        Join-Path ([System.IO.Path]::GetFullPath($DownloadRoot)) $asset.sourceName
    }

    if ([string]::IsNullOrWhiteSpace($DownloadRoot)) {
        $sourceUri = "https://github.com/$($manifest.upstream.repository)/releases/download/$($manifest.upstream.tag)/$($asset.sourceName)"
        Invoke-WebRequest -Uri $sourceUri -OutFile $sourcePath
    }
    elseif (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "The supplied runtime archive does not exist: $sourcePath"
    }

    $sourceFile = Get-Item -LiteralPath $sourcePath
    if ($sourceFile.Length -ne [long]$asset.sourceSize) {
        throw "The $($asset.architecture) runtime archive size was $($sourceFile.Length), expected $($asset.sourceSize)."
    }
    $sourceHash = Get-Sha256 -Path $sourcePath
    if ($sourceHash -cne [string]$asset.sourceSha256) {
        throw "The $($asset.architecture) runtime archive failed SHA-256 verification."
    }

    $architectureRoot = Join-Path $outputRoot $asset.architecture
    New-Item -ItemType Directory -Path $architectureRoot | Out-Null
    $authorizedFiles = New-Object System.Collections.Generic.List[object]
    $runtimeLicensePath = Join-Path $architectureRoot $manifest.license.name
    Copy-Item -LiteralPath $licensePath -Destination $runtimeLicensePath
    $authorizedFiles.Add([ordered]@{
        name = $manifest.license.name
        size = $licenseFile.Length
        sha256 = [string]$manifest.license.sha256
    })
    $seenNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $archive = [System.IO.Compression.ZipFile]::OpenRead($sourcePath)
    try {
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }
            if ($entry.FullName -cne $entry.Name -or
                [System.IO.Path]::GetFileName($entry.Name) -cne $entry.Name) {
                throw "The pinned archive contains a non-root entry: $($entry.FullName)"
            }
            if (-not $seenNames.Add($entry.Name)) {
                throw "The pinned archive contains a duplicate file name: $($entry.Name)"
            }
            if (-not (Test-IncludedRuntimeFile -Name $entry.Name)) {
                continue
            }

            $destination = Join-Path $architectureRoot $entry.Name
            $input = $entry.Open()
            $output = [System.IO.File]::Open($destination, [System.IO.FileMode]::CreateNew)
            try {
                $input.CopyTo($output)
            }
            finally {
                $output.Dispose()
                $input.Dispose()
            }
            $file = Get-Item -LiteralPath $destination
            $authorizedFiles.Add([ordered]@{
                name = $entry.Name
                size = $file.Length
                sha256 = Get-Sha256 -Path $destination
            })
        }
    }
    finally {
        $archive.Dispose()
    }

    foreach ($required in @(
            'LICENSE',
            'LICENSE-LLVM-OpenMP',
            'libomp.dll',
            'llama-server.exe',
            'llama-server-impl.dll',
            'llama-common.dll',
            'llama.dll',
            'mtmd.dll',
            'ggml.dll',
            'ggml-base.dll',
            'ggml-cuda.dll')) {
        if (-not ($authorizedFiles | Where-Object name -CEQ $required)) {
            throw "The $($asset.architecture) archive is missing required runtime file '$required'."
        }
    }

    $authorizationAssets.Add([ordered]@{
        architecture = $asset.architecture
        sourceName = $asset.sourceName
        sourceSize = $sourceFile.Length
        sourceSha256 = $sourceHash
        outputName = $asset.outputName
        files = @($authorizedFiles.ToArray() | Sort-Object name)
    })

    if ([string]::IsNullOrWhiteSpace($DownloadRoot)) {
        Remove-Item -LiteralPath $sourcePath -Force
    }
}

if ([string]::IsNullOrWhiteSpace($DownloadRoot)) {
    Remove-Item -LiteralPath $licensePath -Force
}

$authorization = [ordered]@{
    schemaVersion = 1
    releaseTag = $manifest.releaseTag
    upstream = $manifest.upstream
    license = $manifest.license
    signerSubject = $manifest.signerSubject
    assets = $authorizationAssets.ToArray()
}
$authorization | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'authorization.json') -Encoding utf8NoBOM
Write-Host "Authorized llama.cpp runtime inputs for $($manifest.releaseTag)."
