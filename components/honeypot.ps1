# ============================================================
# DefenseSuite Component — Honeypot (TCP Port Traps)
# Listens on trap ports, bans anyone who connects
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
$TrapPorts = $cfg.trap_ports
$LogFile = Join-Path $LogDir "honeypot.log"
$BanHistoryFile = Join-Path $LogDir "honeypot_bans.json"

function Write-Log2($Level, $Msg) {
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$ts | $Level | $Msg" | Add-Content $LogFile -Encoding UTF8
}

# ========== Trap listener (runs in separate runspace) ==========
$trapScript = {
    param($Port, $LogFile, $BanHistoryFile, $WhitelistIPs)

    function Log($L,$M) {
        $t = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        "$t | $L | $M" | Add-Content $LogFile -Encoding UTF8
    }

    $listener = $null
    try {
        $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Any, $Port)
        $listener.Start()
        Log "READY" "Port $Port online"
    } catch {
        Log "ERROR" "Port $Port bind failed: $_"
        return
    }

    while ($true) {
        try {
            $client = $listener.AcceptTcpClient()
            $client.ReceiveTimeout = 3000
            $client.SendTimeout = 3000
            $ip = $client.Client.RemoteEndPoint.Address.ToString()

            Log "TRAP" "IP=$ip Port=$Port"

            if ($ip -notin $WhitelistIPs -and $ip -notmatch "^(10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|127\.)") {
                $octets = $ip -split '\.'
                $subnet = "$($octets[0]).$($octets[1]).$($octets[2]).0"

                $ruleIP = "HONEYPOT_IP_$ip"
                $exist = netsh advfirewall firewall show rule name="$ruleIP" 2>$null
                if ($exist -notmatch $ruleIP) {
                    netsh advfirewall firewall add rule name="$ruleIP" dir=in action=block remoteip="$ip" >$null
                    Log "BAN" "IP: $ip"
                }

                $ruleSub = "HONEYPOT_SUBNET_$subnet"
                $exist2 = netsh advfirewall firewall show rule name="$ruleSub" 2>$null
                if ($exist2 -notmatch $ruleSub) {
                    netsh advfirewall firewall add rule name="$ruleSub" dir=in action=block remoteip="$subnet/24" >$null
                    Log "BAN" "Subnet: $subnet/24"
                }

                try {
                    $entry = @{ IP = $ip; Subnet = "$subnet/24"; Port = $Port; Time = (Get-Date -Format "yyyy-MM-dd HH:mm:ss") }
                    $hist = @()
                    if (Test-Path $BanHistoryFile) {
                        $raw = Get-Content $BanHistoryFile -Raw
                        if ($raw) { $hist = $raw | ConvertFrom-Json }
                        if ($hist -isnot [array]) { $hist = @() }
                    }
                    $hist += $entry
                    $hist | ConvertTo-Json -Depth 2 | Set-Content $BanHistoryFile -Encoding UTF8
                } catch {}
            }

            # Fake banner
            $banner = ""
            switch ($Port) {
                22 { $banner = "SSH-2.0-OpenSSH_7.4`r`n" }
                21 { $banner = "220 ProFTPD 1.3.5`r`n" }
                23 { $banner = "`r`nUbuntu 18.04 LTS`r`nlocalhost login: " }
            }
            if ($banner) {
                try {
                    $s = $client.GetStream()
                    $b = [Text.Encoding]::ASCII.GetBytes($banner)
                    $s.Write($b, 0, $b.Length)
                    Start-Sleep -Milliseconds 300
                } catch {}
            }

            $client.Close()
            $client.Dispose()
        } catch {
            Start-Sleep -Seconds 5
        }
    }
}

# ========== Main ==========
Write-Log2 "START" "Honeypot v1.0 starting"
Write-Log2 "INFO" "Whitelist: $($WhitelistIPs -join ', ')"
Write-Log2 "INFO" "Trap ports: $($TrapPorts -join ', ')"

$readyPorts = @()
foreach ($p in $TrapPorts) {
    $used = netstat -ano -p tcp 2>$null | Select-String "0\.0\.0\.0:$p\s"
    if ($used) { Write-Log2 "SKIP" "Port $p in use"; continue }
    $readyPorts += $p
}

Write-Log2 "INFO" "Ready ports: $($readyPorts -join ', ')"

$pool = [RunspaceFactory]::CreateRunspacePool(1, [Math]::Max($readyPorts.Count + 5, 20))
$pool.Open()

$instances = @()
foreach ($port in $readyPorts) {
    $ps = [PowerShell]::Create()
    $ps.RunspacePool = $pool
    [void]$ps.AddScript($trapScript.ToString())
    [void]$ps.AddParameter("Port", $port)
    [void]$ps.AddParameter("LogFile", $LogFile)
    [void]$ps.AddParameter("BanHistoryFile", $BanHistoryFile)
    [void]$ps.AddParameter("WhitelistIPs", $WhitelistIPs)
    $handle = $ps.BeginInvoke()
    $instances += @{ PS = $ps; Handle = $handle; Port = $port }
    Write-Log2 "INFO" "Listener started: port $port"
    Start-Sleep -Milliseconds 500
}

Write-Log2 "READY" "$($instances.Count)/$($TrapPorts.Count) traps active"

while ($true) {
    Start-Sleep -Seconds 300
    $alive = 0
    foreach ($i in $instances) { if (-not $i.Handle.IsCompleted) { $alive++ } }
    if ($alive -lt $instances.Count) { Write-Log2 "WARN" "$alive/$($instances.Count) alive" }
}
