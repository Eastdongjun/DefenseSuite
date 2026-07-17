using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Linq;

namespace CloudDefender.Core
{
    /// <summary>
    /// Embedded web dashboard for monitoring and managing defense.
    /// Serves HTML dashboard + JSON API on configurable port.
    /// </summary>
    public class DashboardServer
    {
        private HttpListener _listener;
        private Thread _thread;
        private int _port;
        private Installer _installer;
        public bool Running { get; private set; }

        public DashboardServer(int port, Installer installer)
        {
            _port = port;
            _installer = installer;
        }

        public void Start()
        {
            _thread = new Thread(Listen) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            Running = false;
            try { if (_listener != null) { try { _listener.Stop(); } catch { } } } catch { }
        }

        private void Listen()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://+:" + _port + "/");
                _listener.Start();
                Running = true;
            }
            catch
            {
                // Try localhost only
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add("http://localhost:" + _port + "/");
                    _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
                    _listener.Start();
                    Running = true;
                }
                catch { return; }
            }

            while (Running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    HandleRequest(ctx);
                }
                catch { if (!Running) break; }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath.ToLower();
            byte[] response;

            if (path == "/" || path == "/index.html")
                response = GetHtmlResponse(GetDashboardHtml());
            else if (path == "/api/status")
                response = GetJsonResponse(GetStatusJson());
            else if (path == "/api/attacks")
                response = GetJsonResponse(GetAttackLogJson());
            else if (path == "/api/rules")
                response = GetJsonResponse(GetRulesJson());
            else
                response = GetHtmlResponse(GetDashboardHtml());

            ctx.Response.ContentType = path.StartsWith("/api/") ? "application/json; charset=utf-8" : "text/html; charset=utf-8";
            ctx.Response.OutputStream.Write(response, 0, response.Length);
            ctx.Response.Close();
        }

        private byte[] GetHtmlResponse(string html)
        {
            return Encoding.UTF8.GetBytes(html);
        }

        private byte[] GetJsonResponse(string json)
        {
            return Encoding.UTF8.GetBytes(json);
        }

        // ========== Dashboard HTML ==========
        private string GetDashboardHtml()
        {
            return @"<!DOCTYPE html>
<html lang='zh'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>CloudDefender 鈥?Server Protection</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI',Arial,sans-serif;background:#0a0e17;color:#c9d1d9;min-height:100vh}
.header{background:#161b22;border-bottom:1px solid #30363d;padding:16px 24px;display:flex;align-items:center;justify-content:space-between}
.header h1{font-size:20px;color:#58a6ff}.header .status{font-size:12px;padding:4px 12px;border-radius:12px;background:#238636;color:#fff}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:16px;padding:24px}
.card{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:20px}
.card h3{font-size:14px;color:#8b949e;margin-bottom:12px;text-transform:uppercase;letter-spacing:0.5px}
.card .value{font-size:32px;font-weight:bold;color:#58a6ff}
.card.green .value{color:#3fb950}.card.red .value{color:#f85149}.card.yellow .value{color:#d2991d}
.module-list{list-style:none}.module-list li{display:flex;justify-content:space-between;padding:8px 0;border-bottom:1px solid #21262d;font-size:13px}
.module-list li:last-child{border:none}.module-list .ok{color:#3fb950}.module-list .err{color:#f85149}
.ports{display:flex;flex-wrap:wrap;gap:6px}.ports span{padding:3px 8px;border-radius:4px;font-size:11px;font-family:monospace;background:#0d419d;color:#58a6ff}.ports span.off{background:#21262d;color:#484f58}
.table-wrap{overflow-x:auto}.table{width:100%;font-size:12px;border-collapse:collapse}.table th{background:#21262d;padding:8px;text-align:left;font-weight:normal;color:#8b949e}.table td{padding:8px;border-bottom:1px solid #21262d}.table tr:hover{background:#1c2128}
.footer{text-align:center;padding:20px;color:#484f58;font-size:11px}
.refresh{color:#58a6ff;cursor:pointer;font-size:12px}
</style>
</head>
<body>
<div class='header'>
<h1>CloudDefender</h1>
<div><span class='status' id='uptime'>Active</span></div>
</div>
<div class='grid'>
<div class='card'>
<h3>Modules</h3>
<ul class='module-list' id='modules'><li>Loading...</li></ul>
</div>
<div class='card'>
<h3>Honeypot Ports</h3>
<div class='value' id='portCount'>-</div>
<div class='ports' id='portList'></div>
</div>
<div class='card'>
<h3>Firewall Rules</h3>
<div class='value green' id='ruleTotal'>-</div>
<div style='font-size:12px;margin-top:8px'>
Defender: <span id='ruleDefender'>-</span> |
Honeypot: <span id='ruleHoneypot'>-</span> |
WebTrap: <span id='ruleWebtrap'>-</span>
</div>
</div>
<div class='card'>
<h3>Recent Attacks (24h)</h3>
<div class='value red' id='attackCount'>-</div>
</div>
</div>
<div class='grid'>
<div class='card' style='grid-column:1/-1'>
<h3>Attack Log</h3>
<div class='table-wrap'><table class='table'>
<thead><tr><th>Time</th><th>Type</th><th>IP</th><th>Detail</th></tr></thead>
<tbody id='attackTable'><tr><td colspan='4'>Loading...</td></tr></tbody>
</table></div>
</div>
</div>
<div class='footer'>CloudDefender v1.0 路 <span class='refresh' onclick='loadAll()'>Refresh</span> 路 Auto-refresh 30s</div>
<script>
async function loadAll(){
try{let r=await fetch('/api/status');let d=await r.json();
// Modules
let mhtml='';
for(let m of d.modules){mhtml+='<li>'+m.name+' <span class=''+(m.status=='Running'||m.status=='Ready'?'ok':'err')+''>'+m.status+'</span></li>';}
document.getElementById('modules').innerHTML=mhtml;
// Ports
document.getElementById('portCount').textContent=d.ports.active+'/'+d.ports.total;
let phtml='';
for(let p of d.ports.list){phtml+='<span class='''+(p.active?'':'off')+''>'+p.number+'</span>';}
document.getElementById('portList').innerHTML=phtml;
// Rules
document.getElementById('ruleTotal').textContent=d.rules.total;
document.getElementById('ruleDefender').textContent=d.rules.defender;
document.getElementById('ruleHoneypot').textContent=d.rules.honeypot;
document.getElementById('ruleWebtrap').textContent=d.rules.webtrap;
// Attack count
document.getElementById('attackCount').textContent=d.attacks.total;
}catch(e){console.error(e);}
try{let r=await fetch('/api/attacks');let d=await r.json();
let ahtml='';
if(d.length==0)ahtml='<tr><td colspan=4>No attacks detected</td></tr>';
else for(let a of d.slice(0,20)){ahtml+='<tr><td>'+a.time+'</td><td>'+a.type+'</td><td>'+a.ip+'</td><td>'+a.detail+'</td></tr>';}
document.getElementById('attackTable').innerHTML=ahtml;
}catch(e){}
}
loadAll();setInterval(loadAll,30000);
</script>
</body></html>";
        }

        // ========== JSON APIs ==========
        private string GetStatusJson()
        {
            var modules = new List<string>();
            foreach (var m in _installer.Modules)
            {
                modules.Add("{\"name\":\"" + m.Name + "\",\"status\":\"" + m.GetStatus() + "\"}");
            }

            int[] trapPorts = { 3389, 22, 23, 21, 5900, 9200, 11211, 27017, 8088, 5432, 5555, 8443 };
            var ports = new List<string>();
            int active = 0;
            var netstat = Shell("netstat", "-ano -p tcp");
            foreach (var p in trapPorts)
            {
                bool isActive = netstat.Contains("0.0.0.0:" + p + " ") && netstat.Contains("LISTENING");
                if (isActive) active++;
                ports.Add("{\"number\":" + p + ",\"active\":" + (isActive ? "true" : "false") + "}");
            }

            var rules = Shell("netsh", "advfirewall firewall show rule name=all");
            int defender = CountStr(rules, "DEFENDER_BLOCK_");
            int honeypot = CountStr(rules, "HONEYPOT_");
            int webtrap = CountStr(rules, "WEBTRAP_");

            // Count recent attacks
            int attackCount = 0;
            var paths = new[] {
                Path.Combine(_installer.InstallDir, "logs"),
                @"D:\Agent\Protect",
                @"C:\ProgramData\DefenseSuite\logs"
            };
            foreach (var dir in paths)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.log", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTime > DateTime.Now.AddDays(-1))
                        {
                            var content = File.ReadAllText(f);
                            attackCount += CountStr(content, "TRAP") + CountStr(content, "BAN");
                        }
                    }
                    catch { }
                }
            }

            return "{\"modules\":[" + string.Join(",", modules) + "]," +
                "\"ports\":{\"active\":" + active + ",\"total\":" + trapPorts.Length + ",\"list\":[" + string.Join(",", ports) + "]}," +
                "\"rules\":{\"total\":" + (defender + honeypot + webtrap) + ",\"defender\":" + defender + ",\"honeypot\":" + honeypot + ",\"webtrap\":" + webtrap + "}," +
                "\"attacks\":{\"total\":" + attackCount + "}}";
        }

        private string GetAttackLogJson()
        {
            var entries = new List<string>();
            var logDirs = new[] {
                Path.Combine(_installer.InstallDir, "logs"),
                @"D:\Agent\Protect",
                @"C:\ProgramData\DefenseSuite\logs"
            };

            foreach (var dir in logDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.log", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var lines = File.ReadAllLines(f);
                        for (int i = lines.Length - 1; i >= 0 && entries.Count < 50; i--)
                        {
                            var line = lines[i];
                            if (line.Contains("TRAP") || line.Contains("BAN") || line.Contains("ALERT"))
                            {
                                string type = line.Contains("TRAP") ? "Trap" : line.Contains("BAN") ? "Ban" : "Alert";
                                string time = line.Length > 19 ? line.Substring(0, 19) : "";
                                string ip = "";
                                string detail = line;
                                var ipMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.\d+\.\d+\.\d+)");
                                if (ipMatch.Success) ip = ipMatch.Value;

                                entries.Add("{\"time\":\"" + EscapeJson(time) + "\",\"type\":\"" + type + "\",\"ip\":\"" + ip + "\",\"detail\":\"" + EscapeJson(detail.Substring(0, Math.Min(detail.Length, 120))) + "\"}");
                            }
                        }
                    }
                    catch { }
                }
            }

            return "[" + string.Join(",", entries) + "]";
        }

        private string GetRulesJson()
        {
            var rules = Shell("netsh", "advfirewall firewall show rule name=all");
            var list = new List<string>();
            foreach (var line in rules.Split('\n'))
            {
                if (line.Contains("Rule Name:") &&
                    (line.Contains("DEFENDER_BLOCK_") || line.Contains("HONEYPOT_") || line.Contains("WEBTRAP_")))
                {
                    var name = line.Substring(line.IndexOf("Rule Name:") + 10).Trim();
                    list.Add("{\"name\":\"" + EscapeJson(name) + "\"}");
                }
            }
            return "{\"count\":" + list.Count + ",\"rules\":[" + string.Join(",", list.Take(100)) + "]}";
        }

        private static string Shell(string cmd, string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(cmd, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                var p = System.Diagnostics.Process.Start(psi);
                var r = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                return r;
            }
            catch { return ""; }
        }

        private static int CountStr(string text, string pattern)
        {
            int count = 0, i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1) { count++; i += pattern.Length; }
            return count;
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}
