#Requires -Version 5.1
<#
.SYNOPSIS
  Build Meta Receipts Lite Release folder, portable ZIP, Setup.exe, and SHA256 checksums.

.PARAMETER Version
  Release version string (default 1.0.5). Passed into the Inno script as MyAppVersion.

.PARAMETER SignThumbprint
  Optional Authenticode certificate thumbprint. If set (or $env:MRL_SIGN_THUMBPRINT),
  signs Setup.exe and MetaReceiptsLite.exe when signtool is available.
#>
param(
    [string]$Version = "1.0.5",
    [string]$SignThumbprint = $env:MRL_SIGN_THUMBPRINT
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

# Standalone exe copy for GitHub Assets listing - cannot run alone without DLLs from the ZIP.
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

function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

if (-not [string]::IsNullOrWhiteSpace($SignThumbprint)) {
    $signtool = Find-SignTool
    if (-not $signtool) {
        Write-Warning "Sign thumbprint set but signtool.exe not found - skipping Authenticode."
    }
    else {
        Write-Host "==> Signing with thumbprint $SignThumbprint" -ForegroundColor Cyan
        foreach ($f in @($setupPath, $exePath, $exeCopy)) {
            & $signtool sign /sha1 $SignThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f
            if ($LASTEXITCODE -ne 0) { throw "signtool failed for $f" }
        }
        # Re-zip after signing the release-dir exe so portable ZIP contains signed binary
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zipPath -Force
    }
}
else {
    Write-Host "==> No sign thumbprint (MRL_SIGN_THUMBPRINT) - skipping Authenticode." -ForegroundColor DarkYellow
}

Write-Host "==> Writing SHA256SUMS.txt" -ForegroundColor Cyan
$sumsPath = Join-Path $dist "SHA256SUMS-v$Version.txt"
$targets = @($setupPath, $zipPath, $exeCopy) | Where-Object { Test-Path $_ }
$lines = foreach ($t in $targets) {
    $hash = (Get-FileHash -Algorithm SHA256 -Path $t).Hash.ToLowerInvariant()
    "{0}  {1}" -f $hash, (Split-Path $t -Leaf)
}
Set-Content -Path $sumsPath -Value $lines -Encoding ASCII

Write-Host ""
Write-Host "Done. Dist outputs:" -ForegroundColor Green
Get-ChildItem $dist -File | Where-Object {
    $_.Name -like "*$Version*" -or $_.Name -eq "MetaReceiptsLite.exe" -or $_.Name -like "SHA256SUMS*"
} | ForEach-Object {
    "{0,-46} {1,8:N2} MB" -f $_.Name, ($_.Length / 1MB)
}
Write-Host ""
Write-Host "Portable ZIP : $zipPath"
Write-Host "Setup.exe    : $setupPath"
Write-Host "Exe copy     : $exeCopy  (listing only - needs DLLs from ZIP to run)"
Write-Host "Checksums    : $sumsPath"
