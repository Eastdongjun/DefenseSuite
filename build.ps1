# ============================================================
# DefenseSuite — Build Script
# Packages all components into a standalone EXE installer
# ============================================================

param(
    [string]$OutputDir = "$PSScriptRoot\dist",
    [switch]$SkipEXE = $false
)

$ErrorActionPreference = "Stop"
$BuildDir = $PSScriptRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DefenseSuite Build System" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
Write-Host "[1/5] Output: $OutputDir"

# Copy all source files to dist
Write-Host "[2/5] Copying source files..."
$distDir = "$OutputDir\DefenseSuite"
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
Get-ChildItem $BuildDir -Exclude "dist","logs" | Copy-Item -Destination $distDir -Recurse -Force
Write-Host "       Source copied to $distDir"

# Create a master installer PowerShell script
Write-Host "[3/5] Creating installer script..."
$installerScript = @'
<#
.SYNOPSIS
    DefenseSuite v1.0 — Windows Server Attack Defense System
.DESCRIPTION
    Installs complete defense system: port honeypots, web traps,
    failed-login monitoring, and auto-banning firewall rules.
.PARAMETER InstallDir
    Installation directory (default: C:\Program Files\DefenseSuite)
.PARAMETER WhitelistIPs
    Comma-separated list of IPs that should never be blocked
.PARAMETER Silent
    Run without interactive prompts
.EXAMPLE
    DefenseSuite-Installer.ps1 -WhitelistIPs "1.2.3.4,5.6.7.8"
    DefenseSuite-Installer.ps1 -Silent -WhitelistIPs "1.2.3.4"
#>

param(
    [string]$InstallDir = "C:\Program Files\DefenseSuite",
    [string]$WhitelistIPs = "",
    [switch]$Silent = $false
)

$ErrorActionPreference = "Stop"
$SourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DefenseSuite v1.0 Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Admin check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: Must run as Administrator!" -ForegroundColor Red
    if (-not $Silent) { Read-Host "Press Enter to exit" }
    exit 1
}

# Parse whitelist
$ips = @()
if ($WhitelistIPs) {
    $ips = $WhitelistIPs -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
}

# Import and install
Import-Module "$SourceDir\DefenseSuite.psm1" -Force

if ($Silent) {
    Install-DefenseSuite -InstallDir $InstallDir -WhitelistIPs $ips
} else {
    # Interactive mode: ask for whitelist
    if ($ips.Count -eq 0) {
        Write-Host "Enter whitelist IPs (comma-separated, or press Enter for none):" -ForegroundColor Yellow
        $input = Read-Host "> "
        if ($input) { $ips = $input -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ } }
    }
    Install-DefenseSuite -InstallDir $InstallDir -WhitelistIPs $ips
}

if (-not $Silent) { Read-Host "Press Enter to exit" }
'@

$installerScript | Set-Content "$distDir\DefenseSuite-Installer.ps1" -Encoding UTF8
Write-Host "       Installer script created"

# Create IExpress SED file for self-extracting EXE
Write-Host "[4/5] Creating IExpress packaging..."

# Build a self-extracting archive using the built-in IExpress
$iexpressSED = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%AdminQuietInstCmd%
[Strings]
InstallPrompt="This will install DefenseSuite v1.0 on your server. Continue?"
DisplayLicense=""
FinishMessage="DefenseSuite has been installed successfully. The server is now protected."
TargetName="$OutputDir\DefenseSuite-Setup.exe"
FriendlyName="DefenseSuite Windows Server Protection"
AppLaunched="powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File DefenseSuite-Installer.ps1"
PostInstallCmd=""
AdminQuietInstCmd="powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File DefenseSuite-Installer.ps1 -Silent"
[SourceFiles]
SourceFiles=
"@

# List all source files
$sourceFiles = Get-ChildItem $distDir -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Replace($distDir + "\", "")
    "$SourceFiles=$relative`r`n"
}

$iexpressSED += $sourceFiles
$iexpressSED | Set-Content "$OutputDir\DefenseSuite.sed" -Encoding ASCII

Write-Host "       IExpress SED file created"

# Try to build EXE with IExpress (built into Windows)
Write-Host "[5/5] Building EXE..."
$iexpressPath = "$env:SystemRoot\System32\iexpress.exe"
if (Test-Path $iexpressPath) {
    Push-Location $distDir
    try {
        & $iexpressPath /N /Q "$OutputDir\DefenseSuite.sed" 2>&1
        if (Test-Path "$OutputDir\DefenseSuite-Setup.exe") {
            $size = [math]::Round((Get-Item "$OutputDir\DefenseSuite-Setup.exe").Length / 1MB, 1)
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Green
            Write-Host " BUILD SUCCESS" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Green
            Write-Host " Output: $OutputDir\DefenseSuite-Setup.exe"
            Write-Host " Size:   $size MB"
            Write-Host ""
            Write-Host " Usage:" -ForegroundColor Yellow
            Write-Host "   DefenseSuite-Setup.exe           — Interactive install"
            Write-Host "   DefenseSuite-Setup.exe /S        — Silent install"
            Write-Host "========================================" -ForegroundColor Green
        } else {
            Write-Host "IExpress build may have failed — using direct script instead"
            Write-Host "Run: $distDir\DefenseSuite-Installer.ps1"
        }
    } finally { Pop-Location }
} else {
    Write-Host "IExpress not available on this system"
    Write-Host "Manual install: powershell -File $distDir\DefenseSuite-Installer.ps1"
}

Write-Host ""
Write-Host "Alternative: Install directly with PowerShell:" -ForegroundColor Yellow
Write-Host "  powershell -ExecutionPolicy Bypass -File `"$distDir\DefenseSuite-Installer.ps1`" -WhitelistIPs `"1.2.3.4`""
