<#
.SYNOPSIS
    Exercises signed llama.cpp runtime authorization without signing credentials.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$workflow = Get-Content -LiteralPath (Join-Path $repoRoot '.github\workflows\signed-llama-runtime-release.yml') -Raw
foreach ($requiredWorkflowToken in @(
        "github.ref == 'refs/heads/main'",
        'environment: release-signing',
        'id-token: write',
        'Reverify authorized bytes before Azure login',
        '$global:LASTEXITCODE = 0',
        'actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1',
        'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
        'actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c',
        'azure/login@a641126d1b8aa4d1fa005f4f92df94a3a4c4c906',
        'azure/artifact-signing-action@c7ab2a863ab5f9a846ddb8265964877ef296ee82',
        'files-folder-recurse: true',
        'files-folder-depth: 2',
        '--latest=false',
        'tagged_sha="$(git rev-list -n 1 "refs/tags/$RELEASE_TAG")"')) {
    if (-not $workflow.Contains($requiredWorkflowToken, [StringComparison]::Ordinal)) {
        throw "The signed llama.cpp workflow is missing '$requiredWorkflowToken'."
    }
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "openclaw-llama-release-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $downloadRoot = Join-Path $tempRoot 'downloads'
    New-Item -ItemType Directory -Path $downloadRoot | Out-Null
    $requiredFiles = @(
        'LICENSE-LLVM-OpenMP',
        'libomp.dll',
        'llama-server.exe',
        'llama-server-impl.dll',
        'llama-common.dll',
        'llama.dll',
        'mtmd.dll',
        'ggml.dll',
        'ggml-base.dll',
        'ggml-cuda.dll'
    )
    $licensePath = Join-Path $downloadRoot 'LICENSE'
    Set-Content -LiteralPath $licensePath -Value 'fixture llama.cpp license' -NoNewline
    $licenseFile = Get-Item -LiteralPath $licensePath
    $assets = New-Object System.Collections.Generic.List[object]
    foreach ($architecture in @('x64', 'arm64')) {
        $sourceName = "llama-b1-bin-win-cuda-13.4-$architecture.zip"
        $archivePath = Join-Path $downloadRoot $sourceName
        $archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($name in @($requiredFiles + 'ignored-tool.exe')) {
                $entry = $archive.CreateEntry($name)
                $writer = [System.IO.StreamWriter]::new($entry.Open())
                try { $writer.Write("$architecture-$name") } finally { $writer.Dispose() }
            }
        }
        finally {
            $archive.Dispose()
        }
        $archiveFile = Get-Item -LiteralPath $archivePath
        $assets.Add([ordered]@{
            architecture = $architecture
            sourceName = $sourceName
            sourceSize = $archiveFile.Length
            sourceSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
            outputName = "openclaw-llama-b1-bin-win-cuda-13.4-$architecture.zip"
        })
    }
    $manifestPath = Join-Path $tempRoot 'manifest.json'
    [ordered]@{
        schemaVersion = 1
        releaseTag = 'llama-b1-openclaw.1'
        upstream = [ordered]@{
            repository = 'ggml-org/llama.cpp'
            tag = 'b1'
            commit = 'a' * 40
        }
        license = [ordered]@{
            name = 'LICENSE'
            size = $licenseFile.Length
            sha256 = (Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        signerSubject = 'CN=OpenClaw Foundation'
        assets = $assets.ToArray()
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    $payloadRoot = Join-Path $tempRoot 'payload'
    & (Join-Path $PSScriptRoot 'Prepare-SignedLlamaRuntime.ps1') `
        -ManifestPath $manifestPath `
        -OutputPath $payloadRoot `
        -DownloadRoot $downloadRoot

    & (Join-Path $PSScriptRoot 'Test-SignedLlamaRuntimePayload.ps1') `
        -PayloadPath $payloadRoot `
        -AuthorizationPath (Join-Path $payloadRoot 'authorization.json') `
        -VerifyAuthorizedHashes

    if (Test-Path -LiteralPath (Join-Path $payloadRoot 'x64\ignored-tool.exe')) {
        throw 'The minimal runtime payload retained an unrelated upstream tool.'
    }

    Add-Content -LiteralPath (Join-Path $payloadRoot 'x64\llama-server-impl.dll') -Value 'tampered'
    $tamperRejected = $false
    try {
        & (Join-Path $PSScriptRoot 'Test-SignedLlamaRuntimePayload.ps1') `
            -PayloadPath $payloadRoot `
            -AuthorizationPath (Join-Path $payloadRoot 'authorization.json') `
            -VerifyAuthorizedHashes
    }
    catch {
        $tamperRejected = $_.Exception.Message -like '*modified before signing*'
    }
    if (-not $tamperRejected) {
        throw 'The pre-signing authorization check accepted a modified DLL.'
    }

    Write-Host 'Signed llama.cpp runtime release tests passed.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
