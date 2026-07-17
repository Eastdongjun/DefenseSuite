# ============================================================
# DefenseSuite v1.0 — Windows Server Attack Defense System
# Module: DefenseSuite.psm1
# ============================================================

$Script:Config = $null
$Script:BaseDir = $PSScriptRoot
$Script:LogDir = $null

# ========== Config ==========
function Load-Config {
    param([string]$ConfigPath = "$BaseDir\config.json")
    if (Test-Path $ConfigPath) {
        $Script:Config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $Script:LogDir = if ($Script:Config.log_dir) { $Script:Config.log_dir } else { "$BaseDir\logs" }
        if (-not (Test-Path $Script:LogDir)) { New-Item -ItemType Directory -Path $Script:LogDir -Force | Out-Null }
        return $Script:Config
    }
    throw "Config not found: $ConfigPath"
}

# ========== Logging ==========
function Write-DefenseLog {
    param([string]$Level, [string]$Message, [string]$Component = "CORE")
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logFile = Join-Path $Script:LogDir "defense_$(Get-Date -Format 'yyyyMMdd').log"
    "$ts | $Level | $Component | $Message" | Add-Content $logFile -Encoding UTF8
}

# ========== Firewall Helper ==========
function Add-DefenseFirewallRule {
    param([string]$RuleName, [string]$RemoteIP, [string]$Direction = "in", [string]$Action = "block")

    $exist = netsh advfirewall firewall show rule name="$RuleName" 2>$null
    if ($exist -notmatch $RuleName) {
        netsh advfirewall firewall add rule name="$RuleName" dir=$Direction action=$Action remoteip="$RemoteIP" >$null
        Write-DefenseLog "BAN" "Rule: $RuleName -> $RemoteIP" "FIREWALL"
        return $true
    }
    return $false
}

function Remove-DefenseFirewallRule {
    param([string]$RuleName)
    netsh advfirewall firewall delete rule name="$RuleName" >$null 2>&1
}

function Get-DefenseFirewallRules {
    $all = netsh advfirewall firewall show rule name=all 2>$null
    $prefixes = @("DEFENDER_BLOCK_", "HONEYPOT_IP_", "HONEYPOT_SUBNET_", "WEBTRAP_IP_", "WEBTRAP_SUBNET_")
    $result = @{}
    foreach ($p in $prefixes) {
        $result[$p] = ($all | Select-String $p).Count
    }
    $result["TOTAL"] = ($result.Values | Measure-Object -Sum).Sum
    return $result
}

# ========== IP Helpers ==========
function Test-IsWhitelisted {
    param([string]$IP)
    if (-not $Script:Config) { Load-Config }
    if ($IP -in $Script:Config.whitelist_ips) { return $true }
    if ($IP -match "^(10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|127\.|0\.)") { return $true }
    return $false
}

function Get-Subnet {
    param([string]$IP, [int]$Mask = 24)
    $octets = $IP -split '\.'
    return "$($octets[0]).$($octets[1]).$($octets[2]).0/$Mask"
}

# ========== Install ==========
function Install-DefenseSuite {
    param(
        [string]$InstallDir = "C:\Program Files\DefenseSuite",
        [string[]]$WhitelistIPs = @(),
        [int[]]$TrapPorts = $null,
        [int]$RdpPort = 0
    )
    $ErrorActionPreference = "Stop"

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " DefenseSuite v1.0 — Installation" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Admin check
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { Write-Host "ERROR: Run as Administrator" -ForegroundColor Red; return }

    # Copy files
    Write-Host "[1/5] Copying files to $InstallDir ..."
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    New-Item -ItemType Directory -Path "$InstallDir\components" -Force | Out-Null
    New-Item -ItemType Directory -Path "$InstallDir\logs" -Force | Out-Null
    Copy-Item "$BaseDir\*" -Destination $InstallDir -Recurse -Force

    # Update config
    Write-Host "[2/5] Configuring..."
    $cfg = Get-Content "$InstallDir\config.json" -Raw | ConvertFrom-Json
    $cfg.install_dir = $InstallDir
    $cfg.log_dir = "$InstallDir\logs"
    if ($WhitelistIPs.Count -gt 0) { $cfg.whitelist_ips = @($WhitelistIPs) }
    if ($TrapPorts) { $cfg.trap_ports = @($TrapPorts) }
    if ($RdpPort -gt 0) { $cfg.rdp_port = $RdpPort }
    $cfg | ConvertTo-Json -Depth 4 | Set-Content "$InstallDir\config.json" -Encoding UTF8
    Write-Host "       Whitelist: $($cfg.whitelist_ips -join ', ')"
    Write-Host "       Trap ports: $($cfg.trap_ports -join ', ')"

    # Scheduled Tasks
    Write-Host "[3/5] Creating scheduled tasks..."

    # AutoDefender (every 3 min)
    schtasks /create /tn "DefenseSuite-AutoDefender" /tr "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\components\auto_defender.ps1`"" /sc minute /mo 3 /ru SYSTEM /rl HIGHEST /f >$null
    Write-Host "       DefenseSuite-AutoDefender: every 3 min"

    # QuickResponse (event-triggered)
    schtasks /create /tn "DefenseSuite-QuickResponse" /tr "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\components\quick_response.ps1`"" /sc onstart /ru SYSTEM /rl HIGHEST /f /delay 0000:30 >$null
    Write-Host "       DefenseSuite-QuickResponse: on startup"

    # Honeypot (on startup)
    schtasks /create /tn "DefenseSuite-Honeypot" /tr "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\components\honeypot.ps1`"" /sc onstart /ru SYSTEM /rl HIGHEST /f /delay 0001:00 >$null
    Write-Host "       DefenseSuite-Honeypot: on startup"

    # WebTrapWatcher (on startup)
    schtasks /create /tn "DefenseSuite-WebTrap" /tr "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\components\web_trap_watcher.ps1`"" /sc onstart /ru SYSTEM /rl HIGHEST /f /delay 0002:00 >$null
    Write-Host "       DefenseSuite-WebTrap: on startup"

    # Start components
    Write-Host "[4/5] Starting defense components..."
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\components\auto_defender.ps1`"" -WindowStyle Hidden
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\components\honeypot.ps1`"" -WindowStyle Hidden
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\components\web_trap_watcher.ps1`"" -WindowStyle Hidden
    Start-Sleep -Seconds 20

    # Verify
    Write-Host "[5/5] Verifying..."
    $honeypotPorts = netstat -ano -p tcp 2>$null | Select-String "LISTENING" | ForEach-Object {
        $l = ($_ -replace '\s+', ' ').Trim()
        foreach ($p in $cfg.trap_ports) { if ($l -match "^TCP\s+0\.0\.0\.0:$p\s") { $p } }
    } | Measure-Object | Select-Object -ExpandProperty Count

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " Honeypot ports active: $honeypotPorts"
    Write-Host " Config: $InstallDir\config.json"
    Write-Host " Logs:   $InstallDir\logs"
    Write-Host ""
    Write-Host " Management commands:" -ForegroundColor Yellow
    Write-Host "   Get-DefenseStatus          — View status"
    Write-Host "   Uninstall-DefenseSuite     — Remove completely"
    Write-Host "========================================" -ForegroundColor Green
}

# ========== Uninstall ==========
function Uninstall-DefenseSuite {
    Write-Host ""
    Write-Host "Uninstalling DefenseSuite..." -ForegroundColor Yellow

    Write-Host "[1/4] Stopping components..."
    Get-Process powershell -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -match "defense|honeypot|web_trap" } | Stop-Process -Force -ErrorAction SilentlyContinue

    Write-Host "[2/4] Removing scheduled tasks..."
    @("DefenseSuite-AutoDefender","DefenseSuite-QuickResponse","DefenseSuite-Honeypot","DefenseSuite-WebTrap") | ForEach-Object {
        schtasks /delete /tn $_ /f 2>$null
    }

    Write-Host "[3/4] Removing firewall rules..."
    $rules = netsh advfirewall firewall show rule name=all 2>$null
    $rules | Select-String "Rule Name: (DEFENDER_BLOCK_|HONEYPOT_|WEBTRAP_)" | ForEach-Object {
        $name = ($_ -replace 'Rule Name:\s+', '').Trim()
        netsh advfirewall firewall delete rule name="$name" >$null
    }
    Write-Host "       Defense rules removed"

    Write-Host "[4/4] Removing files..."
    if ($Script:Config -and $Script:Config.install_dir) {
        Remove-Item -Recurse -Force $Script:Config.install_dir -ErrorAction SilentlyContinue
    }

    Write-Host "DefenseSuite uninstalled." -ForegroundColor Green
}

# ========== Status ==========
function Get-DefenseStatus {
    if (-not $Script:Config) { Load-Config }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " DefenseSuite v1.0 — Status" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    Write-Host "`n--- Scheduled Tasks ---"
    @("DefenseSuite-AutoDefender","DefenseSuite-QuickResponse","DefenseSuite-Honeypot","DefenseSuite-WebTrap") | ForEach-Object {
        $info = schtasks /query /tn $_ /fo list 2>$null | Select-String "Status|Last Run|Last Result"
        $status = ($info | Select-String "Status:").Line -replace ".*:\s+",""
        $lastRun = ($info | Select-String "Last Run" | Select-Object -First 1).Line -replace ".*:\s+",""
        $icon = if ($status -eq "Ready" -or $status -eq "Running") { "OK" } else { "!!" }
        Write-Host "  [$icon] $_ : $status (Last: $lastRun)"
    }

    Write-Host "`n--- Honeypot Ports ---"
    $trapPorts = $Script:Config.trap_ports
    $active = 0
    netstat -ano -p tcp 2>$null | Select-String "LISTENING" | ForEach-Object {
        $l = ($_ -replace '\s+', ' ').Trim()
        foreach ($p in $trapPorts) {
            if ($l -match "^TCP\s+0\.0\.0\.0:$p\s") {
                Write-Host "  [ACTIVE] Port $p"
                $active++
            }
        }
    }
    Write-Host "  Total: $active/$($trapPorts.Count)"

    Write-Host "`n--- Firewall Rules ---"
    $rules = Get-DefenseFirewallRules
    foreach ($k in $rules.Keys) { Write-Host "  $k : $($rules[$k])" }

    Write-Host "`n--- Recent Traps (24h) ---"
    $since = (Get-Date).AddDays(-1)
    $logPattern = Join-Path $Script:LogDir "defense_*.log"
    Get-ChildItem $logPattern -ErrorAction SilentlyContinue | ForEach-Object {
        Get-Content $_.FullName | Select-String "TRAP|BAN" | Select-Object -Last 10 | ForEach-Object { Write-Host "  $_" }
    }

    Write-Host "`n--- Whitelist ---"
    $Script:Config.whitelist_ips | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
}

# ========== Whitelist Management ==========
function Add-DefenseWhitelist {
    param([string[]]$IPs)
    if (-not $Script:Config) { Load-Config }
    $current = [System.Collections.ArrayList]@($Script:Config.whitelist_ips)
    foreach ($ip in $IPs) {
        if ($ip -notin $current) {
            $current.Add($ip) | Out-Null
            Write-Host "Added to whitelist: $ip" -ForegroundColor Green
        } else {
            Write-Host "Already whitelisted: $ip" -ForegroundColor Gray
        }
    }
    $Script:Config.whitelist_ips = @($current)
    $Script:Config | ConvertTo-Json -Depth 4 | Set-Content "$BaseDir\config.json" -Encoding UTF8
}

# ========== Auto-load config ==========
try { Load-Config } catch {}

Export-ModuleMember -Function Install-DefenseSuite, Uninstall-DefenseSuite, Get-DefenseStatus, Add-DefenseWhitelist, Load-Config, Get-DefenseFirewallRules, Add-DefenseFirewallRule, Write-DefenseLog, Test-IsWhitelisted
