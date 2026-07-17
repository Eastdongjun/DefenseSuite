# ============================================================
# DefenseSuite Component — AutoDefender
# Monitors failed logins (Event 4625) + auto-bans attackers
# ============================================================

param(
    [string]$ConfigPath = $null,
    [string]$LogDir = $null
)

# Try to locate config
if (-not $ConfigPath) {
    $ConfigPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\config.json"
}
if (-not (Test-Path $ConfigPath)) { throw "Config not found: $ConfigPath" }

$cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$LogDir = if ($cfg.log_dir) { $cfg.log_dir } else { Join-Path (Split-Path $ConfigPath) "logs" }
if (-not (Test-Path $LogDir)) { New-Item $LogDir -ItemType Directory -Force | Out-Null }

$WhitelistIPs = $cfg.whitelist_ips
$BanDurationHours = $cfg.ban_config.ban_duration_hours
$Tier1Threshold = $cfg.ban_config.tier1_threshold
$Tier2Threshold = $cfg.ban_config.tier2_threshold
$Tier3Threshold = $cfg.ban_config.tier3_threshold
$LookbackMinutes = $cfg.ban_config.lookback_minutes

$LogFile = Join-Path $LogDir "defender.log"
$BlacklistFile = Join-Path $LogDir "ip_blacklist.json"
$PermanentFile = Join-Path $LogDir "ip_permanent_blocks.json"

function Write-Log($Level, $Msg) {
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$ts | $Level | $Msg" | Add-Content $LogFile -Encoding UTF8
}

function Test-IsWhitelisted($IP) {
    if ($IP -in $WhitelistIPs) { return $true }
    if ($IP -match "^(10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|127\.)") { return $true }
    return $false
}

function Add-FirewallBan($IP, $Subnet, $IsPermanent) {
    $ruleName = "DEFENDER_BLOCK_$IP"
    $exist = netsh advfirewall firewall show rule name="$ruleName" 2>$null
    if ($exist -notmatch $ruleName) {
        netsh advfirewall firewall add rule name="$ruleName" dir=in action=block remoteip="$IP" >$null
        Write-Log "BAN" "IP: $IP"
    }

    $subnetRule = "DEFENDER_BLOCK_$subnet"
    $exist2 = netsh advfirewall firewall show rule name="$subnetRule" 2>$null
    if ($exist2 -notmatch $subnetRule) {
        netsh advfirewall firewall add rule name="$subnetRule" dir=in action=block remoteip="$subnet/24" >$null
        Write-Log "BAN" "Subnet: $subnet/24 (from $IP)"
    }
}

function Run-Scan {
    $since = (Get-Date).AddMinutes(-$LookbackMinutes)
    $failures = Get-EventLog Security -After $since -ErrorAction SilentlyContinue | Where-Object { $_.EventID -eq 4625 }

    $ipCounts = @{}
    foreach ($f in $failures) {
        if ($f.Message -match "Source Network Address:\s+(\S+)") {
            $ip = $Matches[1]
            if (Test-IsWhitelisted $ip) { continue }
            if (-not $ipCounts.ContainsKey($ip)) { $ipCounts[$ip] = 0 }
            $ipCounts[$ip]++
        }
    }

    if ($ipCounts.Count -eq 0) { return }

    # Load blacklist history
    $blacklist = @{}
    if (Test-Path $BlacklistFile) {
        try { $blacklist = Get-Content $BlacklistFile -Raw | ConvertFrom-Json } catch {}
    }

    $permanent = @{}
    if (Test-Path $PermanentFile) {
        try { $permanent = Get-Content $PermanentFile -Raw | ConvertFrom-Json } catch {}
    }

    foreach ($ip in $ipCounts.Keys) {
        $count = $ipCounts[$ip]
        $octets = $ip -split '\.'
        $subnet = "$($octets[0]).$($octets[1]).$($octets[2]).0"

        if ($count -ge $Tier3Threshold) {
            $permanent[$ip] = $true
            Add-FirewallBan -IP $ip -Subnet $subnet -IsPermanent $true
            try { Write-EventLog -LogName Application -Source "DefenseSuite" -EventId 999 -EntryType Error -Message "Tier3 permanent ban: $ip ($count failures)" } catch {}
        } elseif ($count -ge $Tier2Threshold) {
            Add-FirewallBan -IP $ip -Subnet $subnet -IsPermanent $false
        } elseif ($count -ge $Tier1Threshold) {
            Add-FirewallBan -IP $ip -Subnet $subnet -IsPermanent $false
        }

        $blacklist[$ip] = @{ Count = $count; Subnet = $subnet; LastSeen = (Get-Date -Format "yyyy-MM-dd HH:mm:ss") }
    }

    $blacklist | ConvertTo-Json -Depth 3 | Set-Content $BlacklistFile -Encoding UTF8
    $permanent | ConvertTo-Json | Set-Content $PermanentFile -Encoding UTF8
}

# Main loop
Write-Log "START" "AutoDefender started (lookback=${LookbackMinutes}m, T1=$Tier1Threshold, T2=$Tier2Threshold, T3=$Tier3Threshold)"

while ($true) {
    try {
        Run-Scan
    } catch {
        Write-Log "ERROR" $_.Exception.Message
    }
    Start-Sleep -Seconds 180
}
