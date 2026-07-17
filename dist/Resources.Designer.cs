using System;

namespace DefenseSuite.Properties
{
    internal static class Resources
    {
        public static string InstallerScript = @"<# DefenseSuite Installer — generated #>
param([string]$InstallDir='C:\Program Files\DefenseSuite',[string]$WhitelistIPs='',[switch]$Silent,[switch]$Status,[switch]$Uninstall)
if($Uninstall){&{Get-ChildItem 'C:\Program Files\DefenseSuite\components\*.ps1'|%{schtasks /end /tn ('DefenseSuite-'+[IO.Path]::GetFileNameWithoutExtension($_.Name)) 2>$null;schtasks /delete /tn ('DefenseSuite-'+[IO.Path]::GetFileNameWithoutExtension($_.Name)) /f 2>$null};netsh advfirewall firewall show rule name=all 2>$null|Select-String 'DEFENDER_BLOCK_|HONEYPOT_|WEBTRAP_'|%{$n=($_ -replace 'Rule Name:\s+','').Trim();netsh advfirewall firewall delete rule name=""$n"" >$null};Remove-Item -Recurse -Force 'C:\Program Files\DefenseSuite' -EA 0;'Uninstalled.';if(!$Silent){Read-Host}};exit}
if($Status){&{Write-Host ""DefenseSuite Status"" -F Cyan;@('AutoDefender','Honeypot','WebTrap','QuickResponse')|%{$i=schtasks /query /tn ""DefenseSuite-$_"" /fo list 2>$null|Select-String ""Status|Last Run"";$s=if($i){($i[0].Line -replace '.*:\s+','').Trim()}else{'NOT INSTALLED'};Write-Host ""  DefenseSuite-$_ : $s""};$t=@(3389,22,23,21,5900,9200,11211,27017,8088,5432,5555,8443);$a=0;netstat -ano -p tcp 2>$null|Select-String LISTENING|%{$l=($_ -replace '\s+',' ').Trim();foreach($p in $t){if($l -match ""^TCP\s+0\.0\.0\.0:$p\s""){Write-Host ""  [ON] Port $p"" -F Green;$a++}}};Write-Host ""  Honeypots: $a/$($t.Count)"";$r=netsh advfirewall firewall show rule name=all 2>$null;$d=($r|Select-String DEFENDER_BLOCK_).Count;$h=($r|Select-String HONEYPOT_).Count;$w=($r|Select-String WEBTRAP_).Count;Write-Host ""  Firewall Rules: DEFENDER=$d HONEYPOT=$h WEBTRAP=$w TOTAL=$($d+$h+$w)"";if(!$Silent){Read-Host}};exit}
Write-Host ""Installing..."" -F Yellow
$ips=@();if($WhitelistIPs){$ips=$WhitelistIPs -split ','|%{$_.Trim()}|?{$_}}
$d='C:\Program Files\DefenseSuite';mkdir $d,$d\components,$d\logs -Force|Out-Null
Get-ChildItem ""$d\components"" | ForEach-Object { $src = ""$d\components\$($_.Name)""; if(Test-Path $src){Copy-Item $src ""$d\components\"" -Force} }
@('AutoDefender','Honeypot','WebTrap','QuickResponse')|%{schtasks /delete /tn ""DefenseSuite-$_"" /f 2>$null}
$env:WL_IPS = ($ips -join ',')
$tasks=@{'AutoDefender'='auto_defender.ps1 /sc minute /mo 3';'Honeypot'='honeypot.ps1 /sc onstart';'WebTrap'='web_trap_watcher.ps1 /sc onstart';'QuickResponse'='quick_response.ps1 /sc onstart'}
foreach($t in $tasks.Keys){schtasks /create /tn ""DefenseSuite-$t"" /tr ""powershell.exe -NoProfile -EP Bypass -W Hidden -File $d\components\$($tasks[$t].Split(' ')[0])"" $($tasks[$t].Substring($tasks[$t].IndexOf('/'))) /ru SYSTEM /rl HIGHEST /f 2>$null}
foreach($t in $tasks.Keys){Start-Process powershell -Args ""-NoProfile -EP Bypass -W Hidden -File $d\components\$($tasks[$t].Split(' ')[0])"" -W Hidden}
Start-Sleep 15
""Done. Honeypot ports active. Check: DefenseSuite-Setup.exe /status""
if(!$Silent){Read-Host}";
    }
}
