<#
.SYNOPSIS
    Verifies authorized or signed llama.cpp runtime payloads.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PayloadPath,

    [Parameter(Mandatory = $true)]
    [string]$AuthorizationPath,

    [switch]$VerifyAuthorizedHashes,

    [switch]$RequireOpenClawSignatures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$payloadRoot = (Resolve-Path -LiteralPath $PayloadPath).Path
$authorization = Get-Content -LiteralPath (Resolve-Path -LiteralPath $AuthorizationPath).Path -Raw | ConvertFrom-Json
if ($authorization.schemaVersion -ne 1 -or @($authorization.assets).Count -ne 2) {
    throw 'The llama runtime authorization manifest is invalid.'
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

foreach ($asset in @($authorization.assets)) {
    $architectureRoot = Join-Path $payloadRoot $asset.architecture
    if (-not (Test-Path -LiteralPath $architectureRoot -PathType Container)) {
        throw "The $($asset.architecture) runtime directory is missing."
    }

    $expectedFiles = @($asset.files | ForEach-Object { [string]$_.name } | Sort-Object)
    $actualFiles = @(Get-ChildItem -LiteralPath $architectureRoot -File | ForEach-Object Name | Sort-Object)
    if ([string]::Join("`n", $actualFiles) -cne [string]::Join("`n", $expectedFiles)) {
        throw "The $($asset.architecture) runtime file inventory does not match its authorization manifest."
    }

    foreach ($file in @($asset.files)) {
        $path = Join-Path $architectureRoot $file.name
        if ($VerifyAuthorizedHashes) {
            $item = Get-Item -LiteralPath $path
            if ($item.Length -ne [long]$file.size -or (Get-Sha256 -Path $path) -cne [string]$file.sha256) {
                throw "Authorized runtime file '$($asset.architecture)/$($file.name)' was modified before signing."
            }
        }

        $isOpenClawSignable = $file.name -match '\.(exe|dll)$' -and $file.name -cne 'libomp.dll'
        if ($RequireOpenClawSignatures -and $isOpenClawSignable) {
            $signature = Get-AuthenticodeSignature -LiteralPath $path
            $subject = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { '' }
            if ($signature.Status -ne 'Valid' -or
                -not [string]::Equals($subject, $authorization.signerSubject, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Runtime file '$($asset.architecture)/$($file.name)' is not validly signed by the expected OpenClaw signer."
            }
        }
        elseif ($RequireOpenClawSignatures) {
            $item = Get-Item -LiteralPath $path
            if ($item.Length -ne [long]$file.size -or (Get-Sha256 -Path $path) -cne [string]$file.sha256) {
                throw "Unsigned runtime file '$($asset.architecture)/$($file.name)' changed after authorization."
            }
        }

        if ($RequireOpenClawSignatures -and $file.name -ceq 'libomp.dll') {
            $signature = Get-AuthenticodeSignature -LiteralPath $path
            $subject = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { '' }
            if ($signature.Status -eq 'Valid' -and
                [string]::Equals($subject, $authorization.signerSubject, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'The third-party LLVM OpenMP runtime must not be signed as OpenClaw-owned code.'
            }
        }
    }
}

Write-Host 'Signed llama.cpp runtime payload policy passed.' -ForegroundColor Green
