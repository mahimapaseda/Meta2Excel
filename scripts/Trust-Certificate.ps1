#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installs the Meta Receipts Lite Code Signing Certificate into the local Windows Trusted Root store.
  This marks Meta Receipts Lite releases as fully trusted and prevents SmartScreen warnings.
#>

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$cerPath = Join-Path $root "assets\MetaReceiptsLite-CodeSign.cer"
if (-not (Test-Path $cerPath)) {
    $cerPath = Join-Path $root "MetaReceiptsLite-CodeSign.cer"
}

if (-not (Test-Path $cerPath)) {
    Write-Error "Certificate file not found: $cerPath"
    exit 1
}

Write-Host "==> Installing Meta Receipts Lite certificate into Trusted Root Certification Authorities..." -ForegroundColor Cyan
Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\LocalMachine\TrustedPublisher" | Out-Null

Write-Host "Success! Meta Receipts Lite is now registered as a trusted publisher on this PC." -ForegroundColor Green
Write-Host "Windows Defender SmartScreen will no longer warn when running releases signed by this certificate." -ForegroundColor Green

