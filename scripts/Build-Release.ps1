#Requires -Version 5.1
<#
.SYNOPSIS
  Build Meta Receipts Lite Release folder, portable ZIP, and Inno Setup installer.

.PARAMETER Version
  Release version string (default 1.0.4). Passed into the Inno script as MyAppVersion.
#>
param(
    [string]$Version = "1.0.4"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
            [System.Environment]::GetEnvironmentVariable("Path", "User")

Write-Host "==> Building Release (net48)..." -ForegroundColor Cyan
$locked = Get-Process -Name "MetaReceiptsLite" -ErrorAction SilentlyContinue
if ($locked) {
    throw "MetaReceiptsLite.exe is running (PID $($locked.Id -join ', ')). Close it and retry."
}

dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

$releaseDir = Join-Path $root "MetaReceiptsLite\bin\Release\net48"
$exePath = Join-Path $releaseDir "MetaReceiptsLite.exe"
if (-not (Test-Path $exePath)) {
    throw "Missing $exePath"
}

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$zipName = "MetaReceiptsLite-v$Version.zip"
$zipPath = Join-Path $dist $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "==> Creating portable ZIP: $zipName" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zipPath -Force

$exeCopy = Join-Path $dist "MetaReceiptsLite.exe"
Copy-Item $exePath $exeCopy -Force

function Find-ISCC {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$iscc = Find-ISCC
if (-not $iscc) {
    throw @"
Inno Setup 6 (ISCC.exe) not found.
Install from https://jrsoftware.org/isinfo.php then re-run this script.
"@
}

$iss = Join-Path $root "installer\MetaReceiptsLite.iss"
Write-Host "==> Compiling installer with $iscc" -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$Version" $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed." }

$setupName = "MetaReceiptsLite-Setup-v$Version.exe"
$setupPath = Join-Path $dist $setupName
if (-not (Test-Path $setupPath)) {
    throw "Expected installer not found: $setupPath"
}

Write-Host ""
Write-Host "Done. Dist outputs:" -ForegroundColor Green
Get-ChildItem $dist -File | Where-Object {
    $_.Name -like "*$Version*" -or $_.Name -eq "MetaReceiptsLite.exe"
} | ForEach-Object {
    "{0,-42} {1,8:N2} MB" -f $_.Name, ($_.Length / 1MB)
}
Write-Host ""
Write-Host "Portable ZIP : $zipPath"
Write-Host "Setup.exe    : $setupPath"
Write-Host "Exe copy     : $exeCopy"
