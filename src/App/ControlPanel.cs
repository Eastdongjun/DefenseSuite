using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CloudDefender.App
{
    public class ControlPanel : Form
    {
        private TabControl tabs;
        private ListView lvModules, lvLogs, lvRules;
        private ToolStripStatusLabel lblPorts, lblRules;
        private Button btnInstall, btnUninstall, btnRefresh, btnStartAll, btnStopAll;
        private Timer timer;
        private string installDir = @"C:\Program Files\CloudDefender";

        public ControlPanel()
        {
            Text = "CloudDefender v2.1 鈥?Server Protection Control Panel";
            Size = new Size(900, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Shield;
            Font = new Font("Microsoft YaHei", 9f);
            Load += (s, e) => RefreshAll();
            FormClosing += (s, e) => { if (timer != null) timer.Stop(); };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildUI();
            timer = new Timer { Interval = 10000 };
            timer.Tick += (s, ev) => RefreshAll();
            timer.Start();
        }

        void BuildUI()
        {
            // Top toolbar
            var top = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(22, 27, 34) };
            var title = new Label { Text = "CloudDefender   ", ForeColor = Color.FromArgb(88, 166, 255), Font = new Font("Microsoft YaHei", 14, FontStyle.Bold), Left = 16, Top = 10, AutoSize = true };
            btnInstall = NewButton("Install", 200, Color.FromArgb(35, 134, 54));
            btnUninstall = NewButton("Uninstall", 280, Color.FromArgb(218, 54, 51));
            btnStartAll = NewButton("Start All", 380, Color.FromArgb(35, 134, 54));
            btnStopAll = NewButton("Stop All", 460, Color.FromArgb(218, 54, 51));
            btnRefresh = NewButton("Refresh", 550, Color.FromArgb(88, 166, 255));

            btnInstall.Click += (s, e) => { RunInstaller(); RefreshAll(); };
            btnUninstall.Click += (s, e) => { if (MessageBox.Show("Remove all defense components?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes) RunUninstaller(); RefreshAll(); };
            btnStartAll.Click += (s, e) => { Cmd("schtasks", "/run /tn DefenseSuite-AutoDefender"); Cmd("schtasks", "/run /tn DefenseSuite-Honeypot"); Cmd("schtasks", "/run /tn DefenseSuite-WebTrap"); Cmd("schtasks", "/run /tn DefenseSuite-QuickResponse"); RefreshAll(); };
            btnStopAll.Click += (s, e) => { Cmd("schtasks", "/end /tn DefenseSuite-AutoDefender"); Cmd("schtasks", "/end /tn DefenseSuite-Honeypot"); Cmd("schtasks", "/end /tn DefenseSuite-WebTrap"); Cmd("schtasks", "/end /tn DefenseSuite-QuickResponse"); RefreshAll(); };
            btnRefresh.Click += (s, e) => RefreshAll();

            top.Controls.AddRange(new Control[] { title, btnInstall, btnUninstall, btnStartAll, btnStopAll, btnRefresh });

            // Status bar
            var statusBar = new StatusStrip { BackColor = Color.FromArgb(13, 17, 23) };
            lblPorts = new ToolStripStatusLabel { ForeColor = Color.FromArgb(88, 166, 255) };
            lblRules = new ToolStripStatusLabel { ForeColor = Color.FromArgb(63, 185, 80) };
            statusBar.Items.Add(lblPorts);
            statusBar.Items.Add(lblRules);

            // Tabs
            tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(CreateModulesTab());
            tabs.TabPages.Add(CreateLogsTab());
            tabs.TabPages.Add(CreateRulesTab());
            tabs.TabPages.Add(CreateSettingsTab());

            Controls.Add(tabs);
            Controls.Add(top);
            Controls.Add(statusBar);
        }

        TabPage CreateModulesTab()
        {
            var page = new TabPage("Module Status");
            lvModules = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            lvModules.Columns.Add("Module", 130);
            lvModules.Columns.Add("Status", 80);
            lvModules.Columns.Add("Schedule", 120);
            lvModules.Columns.Add("Last Run", 170);
            lvModules.Columns.Add("Description", 350);
            page.Controls.Add(lvModules);
            return page;
        }

        TabPage CreateLogsTab()
        {
            var page = new TabPage("Attack Log");
            lvLogs = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            lvLogs.Columns.Add("Time", 160);
            lvLogs.Columns.Add("Type", 70);
            lvLogs.Columns.Add("IP", 130);
            lvLogs.Columns.Add("Detail", 480);
            page.Controls.Add(lvLogs);
            return page;
        }

        TabPage CreateRulesTab()
        {
            var page = new TabPage("Firewall Rules");
            lvRules = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            lvRules.Columns.Add("Rule Name", 350);
            lvRules.Columns.Add("Type", 100);
            lvRules.Columns.Add("Created", 150);
            page.Controls.Add(lvRules);
            return page;
        }

        TabPage CreateSettingsTab()
        {
            var page = new TabPage("Settings");
            var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

            var lblDir = new Label { Text = "Install Directory:", Top = 20, Left = 20, AutoSize = true };
            var txtDir = new TextBox { Text = installDir, Top = 20, Left = 180, Width = 400 };
            var btnBrowse = new Button { Text = "Browse...", Top = 18, Left = 590 };
            btnBrowse.Click += (s, e) => { var dlg = new FolderBrowserDialog(); if (dlg.ShowDialog() == DialogResult.OK) txtDir.Text = dlg.SelectedPath; };

            var lblWL = new Label { Text = "Whitelist IPs:", Top = 60, Left = 20, AutoSize = true };
            var txtWL = new TextBox { Top = 60, Left = 180, Width = 400, Text = "113.132.220.221, 220.195.83.129" };

            var lblPort = new Label { Text = "Dashboard Port:", Top = 100, Left = 20, AutoSize = true };
            var txtPort = new TextBox { Top = 100, Left = 180, Width = 80, Text = "8888" };

            var btnSave = new Button { Text = "Save & Reinstall", Top = 150, Left = 180, BackColor = Color.FromArgb(35, 134, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSave.Click += (s, e) =>
            {
                installDir = txtDir.Text;
                RunInstaller();
            };

            var btnWeb = new Button { Text = "Open Web Dashboard", Top = 150, Left = 320, BackColor = Color.FromArgb(88, 166, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnWeb.Click += (s, e) => Process.Start("http://localhost:" + txtPort.Text);

            p.Controls.AddRange(new Control[] { lblDir, txtDir, btnBrowse, lblWL, txtWL, lblPort, txtPort, btnSave, btnWeb });
            page.Controls.Add(p);
            return page;
        }

        void RefreshAll()
        {
            RefreshModules();
            RefreshLogs();
            RefreshRules();
            RefreshStatusBar();
        }

        void RefreshModules()
        {
            lvModules.Items.Clear();
            var modules = new[] {
                new { Name = "AutoDefender", Schedule = "Every 3 min", Desc = "Failed login monitor + 3-tier auto-ban" },
                new { Name = "Honeypot", Schedule = "On startup", Desc = "12 TCP trap ports, instant /24 subnet ban" },
                new { Name = "WebTrap", Schedule = "On startup", Desc = "Web scanner path detection + auto-ban" },
                new { Name = "QuickResponse", Schedule = "On startup", Desc = "Real-time 4625 event blocking" }
            };

            foreach (var m in modules)
            {
                var output = Cmd("schtasks", "/query /tn DefenseSuite-" + m.Name + " /fo list");
                string status = "NOT INSTALLED";
                string lastRun = "-";
                foreach (var line in output.Split('\n'))
                {
                    if (line.Trim().StartsWith("Status:")) status = line.Split(':')[1].Trim();
                    if (line.Contains("Last Run Time:")) lastRun = line.Split(new[] { ':' }, 2)[1].Trim();
                }

                var item = new ListViewItem(m.Name);
                item.SubItems.Add(status);
                item.SubItems.Add(m.Schedule);
                item.SubItems.Add(lastRun);
                item.SubItems.Add(m.Desc);
                if (status == "Running" || status == "Ready") item.ForeColor = Color.FromArgb(63, 185, 80);
                else if (status == "NOT INSTALLED") item.ForeColor = Color.FromArgb(139, 148, 158);
                else item.ForeColor = Color.FromArgb(248, 81, 73);
                lvModules.Items.Add(item);
            }
        }

        void RefreshLogs()
        {
            lvLogs.Items.Clear();
            var logDirs = new[] { Path.Combine(installDir, "logs"), @"D:\Agent\Protect", @"C:\ProgramData\DefenseSuite\logs" };
            int count = 0;
            foreach (var dir in logDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.log"))
                {
                    try
                    {
                        var lines = File.ReadAllLines(f);
                        for (int i = lines.Length - 1; i >= 0 && count < 50; i--)
                        {
                            var line = lines[i];
                            if (!line.Contains("TRAP") && !line.Contains("BAN") && !line.Contains("ALERT")) continue;

                            string type = line.Contains("TRAP") ? "Trap" : line.Contains("BAN") ? "Ban" : "Alert";
                            string time = line.Length > 19 ? line.Substring(0, 19) : "";
                            string ip = "";
                            var ipMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.\d+\.\d+\.\d+)");
                            if (ipMatch.Success) ip = ipMatch.Value;

                            var item = new ListViewItem(time);
                            item.SubItems.Add(type);
                            item.SubItems.Add(ip);
                            item.SubItems.Add(line.Length > 120 ? line.Substring(0, 120) : line);
                            if (type == "Trap") item.ForeColor = Color.FromArgb(248, 81, 73);
                            else item.ForeColor = Color.FromArgb(210, 153, 29);
                            lvLogs.Items.Add(item);
                            count++;
                        }
                    }
                    catch { }
                }
            }
        }

        void RefreshRules()
        {
            lvRules.Items.Clear();
            var output = Cmd("netsh", "advfirewall firewall show rule name=all");
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("Rule Name:") || (!line.Contains("DEFENDER_BLOCK_") && !line.Contains("HONEYPOT_") && !line.Contains("WEBTRAP_"))) continue;
                var name = line.Substring(line.IndexOf("Rule Name:") + 10).Trim();
                string type = name.StartsWith("DEFENDER") ? "Defender" : name.StartsWith("HONEYPOT") ? "Honeypot" : "WebTrap";
                var item = new ListViewItem(name);
                item.SubItems.Add(type);
                item.SubItems.Add("");
                lvRules.Items.Add(item);
            }
            if (lvRules.Items.Count == 0)
            {
                lvRules.Items.Add(new ListViewItem("No defense rules installed"));
            }
        }

        void RefreshStatusBar()
        {
            int[] ports = { 3389, 22, 23, 21, 5900, 9200, 11211, 27017, 8088, 5432, 5555, 8443 };
            int active = 0;
            var netstat = Cmd("netstat", "-ano -p tcp");
            foreach (var p in ports)
                if (netstat.Contains("0.0.0.0:" + p + " ") && netstat.Contains("LISTENING"))
                    active++;

            var rules = Cmd("netsh", "advfirewall firewall show rule name=all");
            int d = CountStr(rules, "DEFENDER_BLOCK_");
            int h = CountStr(rules, "HONEYPOT_");
            int w = CountStr(rules, "WEBTRAP_");

            lblPorts.Text = "Honeypots: " + active + "/" + ports.Length + "   ";
            lblRules.Text = "Rules: Defender=" + d + " Honeypot=" + h + " WebTrap=" + w + " Total=" + (d + h + w);
        }

        Button NewButton(string text, int x, Color color)
        {
            return new Button
            {
                Text = text, Left = x, Top = 10, Height = 30, Width = 80,
                BackColor = color, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 8f)
            };
        }

        void RunInstaller()
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -EP Bypass -File \"" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "install.ps1") + "\"")
            { Verb = "runas", UseShellExecute = true };
            try { Process.Start(psi); } catch { }
        }

        void RunUninstaller()
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -EP Bypass -Command \"& { schtasks /delete /tn DefenseSuite-AutoDefender /f; schtasks /delete /tn DefenseSuite-Honeypot /f; schtasks /delete /tn DefenseSuite-WebTrap /f; schtasks /delete /tn DefenseSuite-QuickResponse /f; $r=netsh advfirewall firewall show rule name=all; $r|Select-String 'DEFENDER_BLOCK_|HONEYPOT_|WEBTRAP_'|%{$n=($_.Line -replace 'Rule Name:','').Trim();netsh advfirewall firewall delete rule name=$n}; Remove-Item -Recurse -Force 'C:\\Program Files\\CloudDefender' -EA 0; Write-Host 'Uninstalled' }\"")
            { Verb = "runas", UseShellExecute = true };
            try { Process.Start(psi); } catch { }
        }

        string Cmd(string cmd, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(cmd, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
                var r = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                return r;
            }
            catch { return ""; }
        }

        static int CountStr(string text, string pattern)
        {
            int count = 0, i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1) { count++; i += pattern.Length; }
            return count;
        }

    }
}
