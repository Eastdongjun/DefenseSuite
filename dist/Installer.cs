using System;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace DefenseSuite
{
    class Installer
    {
        static string InstallDir = @"C:\Program Files\DefenseSuite";
        static bool Silent = false;
        static bool ShowStatus = false;
        static bool DoUninstall = false;
        static string WhitelistIPs = "";

        [STAThread]
        static int Main(string[] args)
        {
            Console.Title = "DefenseSuite v1.0 Setup";

            // Parse args
            foreach (var arg in args)
            {
                var a = arg.ToLower();
                if (a == "/status" || a == "-status") ShowStatus = true;
                else if (a == "/uninstall" || a == "-uninstall") DoUninstall = true;
                else if (a == "/silent" || a == "-silent" || a == "/s") Silent = true;
                else if (a.StartsWith("/whitelist") || a.StartsWith("-whitelist"))
                {
                    var parts = arg.Split(new[] { '=', ':' }, 2);
                    if (parts.Length > 1) WhitelistIPs = parts[1].Trim('"', '\'');
                }
            }

            // Check admin
            if (!IsAdministrator())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Must run as Administrator!");
                Console.ResetColor();
                if (!Silent) { Console.Write("Press Enter to exit..."); Console.ReadLine(); }
                return 1;
            }

            if (DoUninstall) { Uninstall(); return 0; }
            if (ShowStatus) { RunPowerShell("-Status"); return 0; }

            Install();
            return 0;
        }

        static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        static void Banner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
   ____        __ _                        ____        _ __       _
  |  _ \  ___ / _(_)_ __   ___  _____   __/ ___| _   _(_) |_ ___ | |_
  | | | |/ _ \ |_| | '_ \ / _ \/ __\ \ / /\___ \| | | | | __/ _ \| __|
  | |_| |  __/  _| | | | |  __/\__ \\ V /  ___) | |_| | | || (_) | |_
  |____/ \___|_| |_|_| |_|\___||___/ \_/  |____/ \__,_|_|\__\___/ \__|
                       Windows Server Protection v1.0");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void Install()
        {
            Banner();
            Console.WriteLine("Installing to: " + InstallDir);
            Console.WriteLine();

            // Ask for whitelist in interactive mode
            if (string.IsNullOrEmpty(WhitelistIPs) && !Silent)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Enter whitelist IPs (comma-separated, Enter for none): ");
                Console.ResetColor();
                WhitelistIPs = Console.ReadLine()?.Trim() ?? "";
            }

            // Extract files
            Console.WriteLine("[1/4] Extracting files...");
            Directory.CreateDirectory(InstallDir);
            Directory.CreateDirectory(Path.Combine(InstallDir, "components"));
            Directory.CreateDirectory(Path.Combine(InstallDir, "logs"));

            // Extract the installer script
            string installerPath = Path.Combine(InstallDir, "DefenseSuite-Setup.ps1");
            File.WriteAllText(installerPath, Properties.Resources.InstallerScript, Encoding.UTF8);

            // Extract components
            File.WriteAllText(Path.Combine(InstallDir, "components", "auto_defender.ps1"),
                Properties.Resources.auto_defender, Encoding.UTF8);
            File.WriteAllText(Path.Combine(InstallDir, "components", "honeypot.ps1"),
                Properties.Resources.honeypot, Encoding.UTF8);
            File.WriteAllText(Path.Combine(InstallDir, "components", "web_trap_watcher.ps1"),
                Properties.Resources.web_trap_watcher, Encoding.UTF8);
            File.WriteAllText(Path.Combine(InstallDir, "components", "quick_response.ps1"),
                Properties.Resources.quick_response, Encoding.UTF8);
            File.WriteAllText(Path.Combine(InstallDir, "config.json"),
                Properties.Resources.config, Encoding.UTF8);

            Console.WriteLine("  Done.");

            // Create scheduled tasks
            Console.WriteLine("[2/4] Creating scheduled tasks...");
            RunCmd("schtasks", $"/delete /tn DefenseSuite-AutoDefender /f");
            RunCmd("schtasks", $"/delete /tn DefenseSuite-Honeypot /f");
            RunCmd("schtasks", $"/delete /tn DefenseSuite-WebTrap /f");
            RunCmd("schtasks", $"/delete /tn DefenseSuite-QuickResponse /f");

            string psExe = "powershell.exe";
            string psFlags = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File";

            RunCmd("schtasks", $"/create /tn DefenseSuite-AutoDefender /tr \"{psExe} {psFlags} \\\"{InstallDir}\\components\\auto_defender.ps1\\\"\" /sc minute /mo 3 /ru SYSTEM /rl HIGHEST /f");
            RunCmd("schtasks", $"/create /tn DefenseSuite-Honeypot /tr \"{psExe} {psFlags} \\\"{InstallDir}\\components\\honeypot.ps1\\\"\" /sc onstart /ru SYSTEM /rl HIGHEST /f");
            RunCmd("schtasks", $"/create /tn DefenseSuite-WebTrap /tr \"{psExe} {psFlags} \\\"{InstallDir}\\components\\web_trap_watcher.ps1\\\"\" /sc onstart /ru SYSTEM /rl HIGHEST /f");
            RunCmd("schtasks", $"/create /tn DefenseSuite-QuickResponse /tr \"{psExe} {psFlags} \\\"{InstallDir}\\components\\quick_response.ps1\\\"\" /sc onstart /ru SYSTEM /rl HIGHEST /f");
            Console.WriteLine("  Done.");

            // Launch
            Console.WriteLine("[3/4] Starting defense components...");
            foreach (var script in new[] { "auto_defender.ps1", "honeypot.ps1", "web_trap_watcher.ps1", "quick_response.ps1" })
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{InstallDir}\\components\\{script}\" -WhitelistIPs \"{WhitelistIPs}\"",
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            System.Threading.Thread.Sleep(20000);

            Console.WriteLine("[4/4] Installation complete!");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("========================================");
            Console.WriteLine("  DefenseSuite Installed Successfully");
            Console.WriteLine("========================================");
            Console.WriteLine($"  Install: {InstallDir}");
            Console.WriteLine($"  Logs:    {InstallDir}\\logs");
            Console.WriteLine();
            Console.WriteLine("  Check status: DefenseSuite-Setup.exe /status");
            Console.WriteLine("========================================");
            Console.ResetColor();

            if (!Silent) { Console.Write("Press Enter to exit..."); Console.ReadLine(); }
        }

        static void Uninstall()
        {
            Banner();
            Console.WriteLine("[1/4] Stopping tasks...");
            foreach (var task in new[] { "DefenseSuite-AutoDefender", "DefenseSuite-Honeypot", "DefenseSuite-WebTrap", "DefenseSuite-QuickResponse" })
            {
                RunCmd("schtasks", $"/end /tn {task}");
                RunCmd("schtasks", $"/delete /tn {task} /f");
                Console.WriteLine($"  Removed: {task}");
            }

            Console.WriteLine("[2/4] Removing firewall rules...");
            var rules = RunCmdAndGetOutput("netsh", "advfirewall firewall show rule name=all");
            foreach (var line in rules.Split('\n'))
            {
                if (line.Contains("DEFENDER_BLOCK_") || line.Contains("HONEYPOT_") || line.Contains("WEBTRAP_"))
                {
                    var ruleName = line.Replace("Rule Name:", "").Trim();
                    RunCmd("netsh", $"advfirewall firewall delete rule name=\"{ruleName}\"");
                }
            }
            Console.WriteLine("  Done.");

            Console.WriteLine("[3/4] Stopping processes...");
            RunCmd("taskkill", "/f /im powershell.exe /fi \"MEMUSAGE gt 10000\"");

            Console.WriteLine("[4/4] Removing files...");
            try { Directory.Delete(InstallDir, true); } catch { }
            try { Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DefenseSuite"), true); } catch { }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("DefenseSuite uninstalled.");
            Console.ResetColor();

            if (!Silent) { Console.Write("Press Enter to exit..."); Console.ReadLine(); }
        }

        static void RunPowerShell(string args)
        {
            string script = Path.Combine(InstallDir, "DefenseSuite-Setup.ps1");
            if (!File.Exists(script))
            {
                // Extract first
                Directory.CreateDirectory(InstallDir);
                File.WriteAllText(script, Properties.Resources.InstallerScript, Encoding.UTF8);
            }
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var p = Process.Start(psi);
            p.WaitForExit(30000);
            Console.Write(p.StandardOutput.ReadToEnd());
            if (!Silent) { Console.Write("Press Enter to exit..."); Console.ReadLine(); }
        }

        static void RunCmd(string cmd, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(cmd, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var p = Process.Start(psi);
                p.WaitForExit(15000);
            }
            catch { }
        }

        static string RunCmdAndGetOutput(string cmd, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(cmd, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                var p = Process.Start(psi);
                return p.StandardOutput.ReadToEnd();
            }
            catch { return ""; }
        }
    }
}
