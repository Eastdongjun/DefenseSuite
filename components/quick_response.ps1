# ============================================================
# DefenseSuite Component — Quick Response
# Event-triggered: monitors Security log for 4625 in real-time
# Bans attackers within seconds (not waiting for 3-min scan)
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
$WhitelistIPs = $cfg.whitelist_ips
$LogFile = Join-Path $LogDir "quick_response.log"

function Write-Log($L, $M) {
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $L | $M" | Add-Content $LogFile -Encoding UTF8
}

Write-Log "START" "QuickResponse event watcher online"

# Subscribe to Security event 4625 (failed login)
$query = @"
<QueryList>
  <Query Id="0" Path="Security">
    <Select Path="Security">*[System[EventID=4625]]</Select>
  </Query>
</QueryList>
"@

$watcher = New-Object System.Diagnostics.Eventing.Reader.EventLogWatcher
$watcher.Query = New-Object System.Diagnostics.Eventing.Reader.EventLogQuery("Security", [System.Diagnostics.Eventing.Reader.PathType]::LogName, $query)

$action = {
    $event = $Event.SourceEventArgs.Event
    $xml = [xml]$event.ToXml()
    $data = @{}
    foreach ($d in $xml.Event.EventData.Data) {
        $data[$d.Name] = $d.'#text'
    }

    $ip = $data["IpAddress"]
    $user = $data["TargetUserName"]

    if (-not $ip -or $ip -in $WhitelistIPs) { return }
    if ($ip -match "^(10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|127\.)") { return }

    Write-Log "ALERT" "Failed login: $user from $ip"

    # Immediate ban
    $ruleName = "DEFENDER_BLOCK_$ip"
    $exist = netsh advfirewall firewall show rule name="$ruleName" 2>$null
    if ($exist -notmatch $ruleName) {
        netsh advfirewall firewall add rule name="$ruleName" dir=in action=block remoteip="$ip" >$null
        Write-Log "BAN" "Quick ban: $ip"
    }
}

Register-ObjectEvent -InputObject $watcher -EventName "EventRecordWritten" -Action $action | Out-Null
$watcher.Enabled = $true

Write-Log "READY" "Listening for Security 4625 events..."

# Keep alive
while ($true) {
    Start-Sleep -Seconds 3600
    Write-Log "HEARTBEAT" "QuickResponse still watching"
}
