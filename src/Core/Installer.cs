using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CloudDefender.Core
{
    public class Installer
    {
        public readonly string InstallDir;
        public readonly List<ModuleBase> Modules = new List<ModuleBase>();

        public Installer(string installDir)
        {
            InstallDir = installDir;
        }

        public void RegisterModule(ModuleBase module)
        {
            module.Initialize(InstallDir);
            Modules.Add(module);
        }

        public void InstallAll(string whitelistIPs = "")
        {
            Directory.CreateDirectory(InstallDir);
            Directory.CreateDirectory(Path.Combine(InstallDir, "components"));
            Directory.CreateDirectory(Path.Combine(InstallDir, "logs"));

            Console.WriteLine("[1/3] Extracting modules...");
            Console.WriteLine("  Install dir: " + InstallDir);

            // Extract config
            if (!string.IsNullOrEmpty(whitelistIPs))
            {
                var ips = whitelistIPs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var ipList = string.Join("\",\n        \"", ips);
                var configJson = Resources.CONFIG.Replace(
                    "\"whitelist_ips\": []",
                    "\"whitelist_ips\": [\n        \"" + ipList + "\"\n    ]"
                ).Replace("\"install_dir\": \"C:\\\\Program Files\\\\DefenseSuite\"",
                    "\"install_dir\": \"" + InstallDir.Replace("\\", "\\\\") + "\"");
                File.WriteAllText(Path.Combine(InstallDir, "config.json"), configJson, System.Text.Encoding.UTF8);
            }
            else
            {
                File.WriteAllText(Path.Combine(InstallDir, "config.json"), Resources.CONFIG, System.Text.Encoding.UTF8);
            }

            // Extract all modules
            foreach (var m in Modules)
            {
                m.Extract();
                Console.WriteLine("  [" + m.Name + "] " + m.Description);
            }

            // Create scheduled tasks
            Console.WriteLine("[2/3] Creating scheduled tasks...");
            foreach (var m in Modules)
            {
                m.CreateTask();
                Console.WriteLine("  [" + m.Name + "] Task created");
            }

            // Start modules
            Console.WriteLine("[3/3] Starting modules...");
            foreach (var m in Modules)
            {
                m.Start();
                Console.WriteLine("  [" + m.Name + "] Started");
            }
            System.Threading.Thread.Sleep(15000);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("========================================");
            Console.WriteLine("  CloudDefender is now protecting this server!");
            Console.WriteLine("  Dashboard: http://localhost:8888");
            Console.WriteLine("  Status:   CloudDefender.exe /status");
            Console.WriteLine("========================================");
            Console.ResetColor();
        }

        public void UninstallAll()
        {
            Console.WriteLine("[1/3] Removing scheduled tasks...");
            foreach (var m in Modules)
            {
                m.Remove();
                Console.WriteLine("  [" + m.Name + "] Removed");
            }

            Console.WriteLine("[2/3] Removing firewall rules...");
            var rules = Shell("netsh", "advfirewall firewall show rule name=all");
            foreach (var line in rules.Split('\n'))
            {
                if (!line.Contains("Rule Name:")) continue;
                var name = line.Substring(line.IndexOf("Rule Name:") + 10).Trim();
                if (name.StartsWith("DEFENDER_BLOCK_") || name.StartsWith("HONEYPOT_") || name.StartsWith("WEBTRAP_"))
                    Shell("netsh", "advfirewall firewall delete rule name=\"" + name + "\"");
            }
            Console.WriteLine("  Done.");

            Console.WriteLine("[3/3] Removing files...");
            try { Directory.Delete(InstallDir, true); } catch { }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Uninstalled.");
            Console.ResetColor();
        }

        public void ShowStatus()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("CloudDefender Status");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var m in Modules)
            {
                var status = m.GetStatus();
                var icon = (status == "Ready" || status == "Running") ? "[OK]" : "[!!]";
                Console.WriteLine("  " + icon + " " + m.Name + " : " + status + " (Last: " + m.GetLastRun() + ")");
            }

            int[] ports = { 3389, 22, 23, 21, 5900, 9200, 11211, 27017, 8088, 5432, 5555, 8443 };
            int active = 0;
            var netstat = Shell("netstat", "-ano -p tcp");
            Console.WriteLine("\nHoneypot Ports:");
            foreach (var p in ports)
            {
                if (netstat.Contains("0.0.0.0:" + p + " ") && netstat.Contains("LISTENING"))
                { Console.WriteLine("  [ON]  Port " + p); active++; }
                else { Console.WriteLine("  [--]  Port " + p); }
            }
            Console.WriteLine("  Active: " + active + "/" + ports.Length);

            var rules = Shell("netsh", "advfirewall firewall show rule name=all");
            int d = CountStr(rules, "DEFENDER_BLOCK_");
            int h = CountStr(rules, "HONEYPOT_");
            int w = CountStr(rules, "WEBTRAP_");
            Console.WriteLine("\nFirewall Rules: DEFENDER=" + d + " HONEYPOT=" + h + " WEBTRAP=" + w + " TOTAL=" + (d + h + w));
            Console.WriteLine("\nDashboard: http://localhost:8888");
        }

        private static string Shell(string cmd, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(cmd, args)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
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
    }
}
