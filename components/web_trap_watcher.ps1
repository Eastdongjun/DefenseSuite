# ============================================================
# DefenseSuite Component — Web Trap Watcher
# Monitors HTTP access logs for scanner paths, bans detected IPs
# Works standalone (not dependent on Nginx — can watch any log)
# ============================================================

param(
    [string]$ConfigPath = $null
)

if (-not $ConfigPath) {
    $ConfigPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\config.json"
}
if (-not (Test-Path $ConfigPath)) { throw "Config not found: $ConfigPath" }

$cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$LogDir = if ($cfg.log_dir) { $cfg.log_dir } else { Join-Path (Split-Path $ConfigPath) "logs" }
if (-not (Test-Path $LogDir)) { New-Item $LogDir -ItemType Directory -Force | Out-Null }

$WhitelistIPs = $cfg.whitelist_ips
$TrapPaths = $cfg.web_trap_paths
$TrapPorts = $cfg.web_trap_ports
$BanLog = Join-Path $LogDir "webtrap.log"

$SeenIPs = @{}

function Write-Log($Level, $Msg) {
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$ts | $Level | $Msg" | Add-Content $BanLog -Encoding UTF8
}

function Ban-WebAttacker($IP) {
    if ($IP -in $WhitelistIPs) { return }
    if ($IP -match "^(10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|127\.)") { return }
    if ($SeenIPs.ContainsKey($IP)) { return }
    $SeenIPs[$IP] = $true

    $octets = $IP -split '\.'
    $subnet = "$($octets[0]).$($octets[1]).$($octets[2]).0"

    # Ban IP
    $ruleIP = "WEBTRAP_IP_$IP"
    $exist = netsh advfirewall firewall show rule name="$ruleIP" 2>$null
    if ($exist -notmatch $ruleIP) {
        netsh advfirewall firewall add rule name="$ruleIP" dir=in action=block remoteip="$IP" >$null
        Write-Log "BAN" "IP: $IP"
    }

    # Ban /24 subnet
    $ruleSub = "WEBTRAP_SUBNET_$subnet"
    $exist2 = netsh advfirewall firewall show rule name="$ruleSub" 2>$null
    if ($exist2 -notmatch $ruleSub) {
        netsh advfirewall firewall add rule name="$ruleSub" dir=in action=block remoteip="$subnet/24" >$null
        Write-Log "BAN" "Subnet: $subnet/24"
    }
}

# Build path regex
$pathPattern = ($TrapPaths | ForEach-Object { [regex]::Escape($_) }) -join '|'
Write-Log "START" "WebTrap Watcher started, watching paths: $pathPattern"

# ===== Method 1: Watch Windows Firewall log for incoming web connections =====
$fwLog = "$env:systemroot\system32\LogFiles\Firewall\pfirewall.log"
$fwLastPos = 0

# ===== Method 2: Periodically scan IIS/W3C logs if available =====
$iisLogDir = "$env:SystemDrive\inetpub\logs\LogFiles"

while ($true) {
    # Scan IIS logs if they exist
    if (Test-Path $iisLogDir) {
        Get-ChildItem $iisLogDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            Get-ChildItem "$($_.FullName)\*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object {
                $lines = Get-Content $_.FullName -Tail 500 -ErrorAction SilentlyContinue
                foreach ($line in $lines) {
                    foreach ($path in $TrapPaths) {
                        if ($line -match "/$path" -and $line -match "(\d+\.\d+\.\d+\.\d+)") {
                            $foundIP = $Matches[1]
                            Write-Log "TRAP" "Path $path from $foundIP in $($_.Name)"
                            Ban-WebAttacker $foundIP
                            break
                        }
                    }
                }
            }
        }
    }

    # Also try watching default web server log locations
    $genericLogDirs = @(
        "C:\nginx\logs",
        "E:\nginx\logs",
        "D:\nginx\logs",
        "C:\Apache24\logs",
        "C:\xampp\apache\logs"
    )
    foreach ($dir in $genericLogDirs) {
        if (-not (Test-Path $dir)) { continue }
        Get-ChildItem $dir -Filter "access*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object {
            if ($_.LastWriteTime -lt (Get-Date).AddMinutes(-5)) { continue }
            Get-Content $_.FullName -Tail 200 | ForEach-Object {
                foreach ($path in $TrapPaths) {
                    if ($_ -match "/$path" -and $_ -match "(\d+\.\d+\.\d+\.\d+)") {
                        $foundIP = $Matches[1]
                        Write-Log "TRAP" "Path $path from $foundIP in $($_.Name)"
                        Ban-WebAttacker $foundIP
                        break
                    }
                }
            }
        }
    }

    Start-Sleep -Seconds 30
}
