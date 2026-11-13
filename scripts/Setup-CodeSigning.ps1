#Requires -Version 5.1
<#
.SYNOPSIS
  Creates an Authenticode Code Signing Certificate for Meta Receipts Lite
  and installs the public certificate into the CurrentUser Trusted Root / Trusted Publishers.
#>

$certSubject = "CN=Meta Receipts Lite, O=Meta2Excel"
$existing = Get-ChildItem "Cert:\CurrentUser\My" -CodeSigningCert | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1

if (-not $existing) {
    Write-Host "==> Generating new Code Signing Certificate ($certSubject)..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $certSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -HashAlgorithm "SHA256" `
        -KeyLength 2048 `
        -NotAfter (Get-Date).AddYears(5)
} else {
    Write-Host "==> Using existing Code Signing Certificate ($certSubject)..." -ForegroundColor Yellow
    $cert = $existing
}

Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Export public CER for distribution with releases
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$assetsDir = Join-Path $root "assets"
$cerPath = Join-Path $assetsDir "MetaReceiptsLite-CodeSign.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
Write-Host "Exported public cert: $cerPath" -ForegroundColor Green

# Also trust on local machine CurrentUser so SmartScreen/UAC verifies locally
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store "Root", "CurrentUser"
$rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
if (-not ($rootStore.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
    $rootStore.Add($cert)
    Write-Host "Installed in Cert:\CurrentUser\Root" -ForegroundColor Green
}
$rootStore.Close()

$pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store "TrustedPublisher", "CurrentUser"
$pubStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
if (-not ($pubStore.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
    $pubStore.Add($cert)
    Write-Host "Installed in Cert:\CurrentUser\TrustedPublisher" -ForegroundColor Green
}
$pubStore.Close()

# Set environment variable for the session
[System.Environment]::SetEnvironmentVariable("MRL_SIGN_THUMBPRINT", $cert.Thumbprint, "User")
Write-Host "Saved MRL_SIGN_THUMBPRINT to User Environment." -ForegroundColor Green

