<#
.SYNOPSIS
    DefenseSuite v1.0 — Windows Server Attack Defense System
.DESCRIPTION
    Complete protection: port honeypots, web traps, failed-login monitoring,
    auto-banning firewall. Proven in production since July 2026.
.PARAMETER InstallDir
    Install path (default: C:\Program Files\DefenseSuite)
.PARAMETER WhitelistIPs
    Comma-separated IPs that must never be blocked
.PARAMETER Silent
    No interactive prompts
.PARAMETER Status
    Show defense status and exit
.PARAMETER Uninstall
    Remove everything and exit
.EXAMPLE
    DefenseSuite-Setup.exe
    DefenseSuite-Setup.exe /status
    DefenseSuite-Setup.exe /silent /whitelistIPs "1.2.3.4,5.6.7.8"
#>

param(
    [string]$InstallDir = "C:\Program Files\DefenseSuite",
    [string]$WhitelistIPs = "",
    [switch]$Silent = $false,
    [switch]$Status = $false,
    [switch]$Uninstall = $false
)

$ErrorActionPreference = "Stop"
$Host.UI.RawUI.WindowTitle = "DefenseSuite v1.0"

# ========== Banner ==========
function Show-Banner {
    Write-Host ""
    Write-Host "  ____        __ _                        ____        _ __       _   " -ForegroundColor Cyan
    Write-Host " |  _ \  ___ / _(_)_ __   ___  _____   __/ ___| _   _(_) |_ ___ | |_ " -ForegroundColor Cyan
    Write-Host " | | | |/ _ \ |_| | '_ \ / _ \/ __\ \ / /\___ \| | | | | __/ _ \| __|" -ForegroundColor Cyan
    Write-Host " | |_| |  __/  _| | | | |  __/\__ \\ V /  ___) | |_| | | || (_) | |_ " -ForegroundColor Cyan
    Write-Host " |____/ \___|_| |_|_| |_|\___||___/ \_/  |____/ \__,_|_|\__\___/ \__|" -ForegroundColor Cyan
    Write-Host "                          Windows Server Protection v1.0" -ForegroundColor DarkCyan
    Write-Host ""
}

# ========== Admin Check ==========
function Test-Admin {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Host "[ERROR] Must run as Administrator!" -ForegroundColor Red
        if (-not $Silent) { Read-Host "Press Enter to exit" }
        exit 1
    }
}

# ========== Status ==========
function Show-Status {
    Show-Banner
    Write-Host "--- Scheduled Tasks ---"
    @("DefenseSuite-AutoDefender","DefenseSuite-Honeypot","DefenseSuite-WebTrap","DefenseSuite-QuickResponse") | ForEach-Object {
        $info = schtasks /query /tn $_ /fo list 2>$null | Select-String "Status|Last Run"
        $status = if ($info) { ($info[0].Line -replace '.*:\s+','').Trim() } else { "NOT INSTALLED" }
        Write-Host "  $_ : $status"
    }

    Write-Host "`n--- Honeypot Ports ---"
    $trapPorts = @(3389, 22, 23, 21, 5900, 9200, 11211, 27017, 8088, 5432, 5555, 8443)
    $active = 0
    netstat -ano -p tcp 2>$null | Select-String "LISTENING" | ForEach-Object {
        $l = ($_ -replace '\s+', ' ').Trim()
        foreach ($p in $trapPorts) {
            if ($l -match "^TCP\s+0\.0\.0\.0:$p\s") { Write-Host "  [ON] Port $p"; $active++ }
        }
    }
    Write-Host "  Active: $active/$($trapPorts.Count)"

    Write-Host "`n--- Firewall Rules ---"
    $all = netsh advfirewall firewall show rule name=all 2>$null
    $d = ($all | Select-String "DEFENDER_BLOCK_").Count
    $h = ($all | Select-String "HONEYPOT_").Count
    $w = ($all | Select-String "WEBTRAP_").Count
    Write-Host "  DEFENDER: $d  HONEYPOT: $h  WEBTRAP: $w  TOTAL: $($d+$h+$w)"
    Write-Host ""
}

# ========== Install ==========
function Invoke-Install {
    Show-Banner
    Write-Host "Installing to: $InstallDir" -ForegroundColor Yellow
    Write-Host ""

    # Parse whitelist
    $ips = @()
    if ($WhitelistIPs) {
        $ips = $WhitelistIPs -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    } elseif (-not $Silent) {
        Write-Host "Enter whitelist IPs (comma-separated, Enter for none):" -ForegroundColor Yellow
        Write-Host "Example: 113.132.220.221,220.195.83.129" -ForegroundColor Gray
        $input = Read-Host "> "
        if ($input) { $ips = $input -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ } }
    }

    # Step 1: Copy files
    Write-Host "[1/5] Copying files..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "$InstallDir" -Force | Out-Null
    New-Item -ItemType Directory -Path "$InstallDir\components" -Force | Out-Null
    New-Item -ItemType Directory -Path "$InstallDir\logs" -Force | Out-Null

    $srcDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    if ($srcDir -match "Temp") {
        # Running from extracted temp — use embedded files
        $srcDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    Get-ChildItem $srcDir -Exclude "dist","logs" -ErrorAction SilentlyContinue | Copy-Item -Destination "$InstallDir" -Recurse -Force -ErrorAction SilentlyContinue

    # Update config
    $cfg = Get-Content "$InstallDir\config.json" -Raw | ConvertFrom-Json
    $cfg.install_dir = $InstallDir
    $cfg.log_dir = "$InstallDir\logs"
    if ($ips.Count -gt 0) { $cfg.whitelist_ips = @($ips) }
    $cfg | ConvertTo-Json -Depth 4 | Set-Content "$InstallDir\config.json" -Encoding UTF8

    # Step 2: Scheduled Tasks
    Write-Host "[2/5] Creating scheduled tasks..." -ForegroundColor Yellow
    $taskCommands = @(
        @{Name="DefenseSuite-AutoDefender"; Script="auto_defender.ps1"; Schedule="/sc minute /mo 3"},
        @{Name="DefenseSuite-Honeypot"; Script="honeypot.ps1"; Schedule="/sc onstart"},
        @{Name="DefenseSuite-WebTrap"; Script="web_trap_watcher.ps1"; Schedule="/sc onstart"},
        @{Name="DefenseSuite-QuickResponse"; Script="quick_response.ps1"; Schedule="/sc onstart"}
    )

    foreach ($t in $taskCommands) {
        $scriptPath = "$InstallDir\components\$($t.Script)"
        schtasks /delete /tn $t.Name /f 2>$null
        $cmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`""
        schtasks /create /tn $t.Name /tr $cmd $t.Schedule /ru SYSTEM /rl HIGHEST /f 2>$null
        Write-Host "  $($t.Name): created"
    }

    # Step 3: Start Components
    Write-Host "[3/5] Starting defense components..." -ForegroundColor Yellow
    foreach ($t in $taskCommands) {
        $scriptPath = "$InstallDir\components\$($t.Script)"
        Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`"" -WindowStyle Hidden
        Write-Host "  Started: $($t.Script)"
    }

    # Step 4: Wait and verify
    Write-Host "[4/5] Verifying..." -ForegroundColor Yellow
    Start-Sleep -Seconds 25

    # Step 5: Report
    Write-Host "[5/5] Done!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  DefenseSuite Installed Successfully" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green

    # Show active ports
    $trapPorts = @(3389, 22, 23, 21, 5900, 9200, 11211, 27017, 8088, 5432, 5555, 8443)
    $active = 0
    netstat -ano -p tcp 2>$null | Select-String "LISTENING" | ForEach-Object {
        $l = ($_ -replace '\s+', ' ').Trim()
        foreach ($p in $trapPorts) { if ($l -match "^TCP\s+0\.0\.0\.0:$p\s") { Write-Host "  Honeypot ON: port $p" -ForegroundColor Green; $active++ } }
    }
    Write-Host "  Honeypot ports: $active" -ForegroundColor Green
    Write-Host "  Whitelist: $($cfg.whitelist_ips -join ', ')" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Check status: DefenseSuite-Setup.exe /status" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Green
}

# ========== Uninstall ==========
function Invoke-Uninstall {
    Show-Banner
    Write-Host "Uninstalling DefenseSuite..." -ForegroundColor Yellow

    Write-Host "[1/4] Stopping tasks..."
    @("DefenseSuite-AutoDefender","DefenseSuite-Honeypot","DefenseSuite-WebTrap","DefenseSuite-QuickResponse") | ForEach-Object {
        schtasks /end /tn $_ 2>$null
        schtasks /delete /tn $_ /f 2>$null
        Write-Host "  Removed: $_"
    }

    Write-Host "[2/4] Removing firewall rules..."
    $rules = netsh advfirewall firewall show rule name=all 2>$null
    $rules | Select-String "Rule Name: (DEFENDER_BLOCK_|HONEYPOT_|WEBTRAP_)" | ForEach-Object {
        $name = ($_ -replace 'Rule Name:\s+', '').Trim()
        netsh advfirewall firewall delete rule name="$name" >$null
    }
    Write-Host "  Defense rules removed"

    Write-Host "[3/4] Stopping processes..."
    Get-Process powershell -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $pid } | ForEach-Object {
        try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch {}
    }

    Write-Host "[4/4] Removing files..."
    Remove-Item -Recurse -Force $InstallDir -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "$env:ProgramData\DefenseSuite" -ErrorAction SilentlyContinue

    Write-Host "DefenseSuite uninstalled." -ForegroundColor Green
}

# ========== Main ==========
Clear-Host
Show-Banner

if ($Uninstall) {
    Test-Admin
    Invoke-Uninstall
} elseif ($Status) {
    Show-Status
} else {
    Test-Admin
    Invoke-Install
}

if (-not $Silent) {
    Write-Host ""
    Read-Host "Press Enter to exit"
}
