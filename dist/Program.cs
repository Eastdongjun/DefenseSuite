using System;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Threading;

partial class CloudDefender
{
    static string InstallDir = @"C:\Program Files\DefenseSuite";
    static bool Silent = false;
    static string Whitelist = "";

    static string Decode(string[] chunks)
    {
        var sb = new StringBuilder();
        foreach (var c in chunks) sb.Append(c);
        return Encoding.UTF8.GetString(Convert.FromBase64String(sb.ToString()));
    }

    [STAThread]
    static void Main(string[] args)
    {
        Console.Title = "CloudDefender v1.0";
        foreach (var a in args)
        {
            var al = a.ToLower();
            if (al == "/s" || al == "/silent" || al == "-silent") Silent = true;
            else if (al.StartsWith("/w") || al.StartsWith("-w"))
            {
                var eq = a.IndexOf('=');
                if (eq < 0) eq = a.IndexOf(':');
                if (eq > 0) Whitelist = a.Substring(eq + 1).Trim('"', '\'');
            }
            else if (al == "/status" || al == "-status") { ShowStatus(); return; }
            else if (al == "/uninstall" || al == "-uninstall") { Uninstall(); return; }
        }

        if (!IsAdmin()) { Error("Administrator privileges required! Right-click -> Run as Administrator."); return; }
        Install();
    }

    static bool IsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    static void Error(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[ERROR] " + msg);
        Console.ResetColor();
        if (!Silent) Console.ReadLine();
    }

    static void Banner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("  ========================================");
        Console.WriteLine("   CloudDefender v1.0");
        Console.WriteLine("   Cloud Server Protection System");
        Console.WriteLine("  ========================================");
        Console.ResetColor();
        Console.WriteLine();
    }

    static void Install()
    {
        Banner();
        Console.WriteLine("Installing to: " + InstallDir);
        Console.WriteLine();

        if (string.IsNullOrEmpty(Whitelist) && !Silent)
        {
            Console.Write("Whitelist IPs (comma-separated, Enter=none): ");
            Whitelist = (Console.ReadLine() ?? "").Trim();
        }

        // Step 1: Extract embedded components
        Console.WriteLine("[1/4] Extracting components...");
        Directory.CreateDirectory(InstallDir);
        Directory.CreateDirectory(Path.Combine(InstallDir, "components"));
        Directory.CreateDirectory(Path.Combine(InstallDir, "logs"));

        File.WriteAllText(Path.Combine(InstallDir, "config.json"), Decode(CONFIG_JSON_CHUNKS), Encoding.UTF8);
        File.WriteAllText(Path.Combine(InstallDir, "components", "auto_defender.ps1"), Decode(AUTO_DEFENDER_CHUNKS), Encoding.UTF8);
        File.WriteAllText(Path.Combine(InstallDir, "components", "honeypot.ps1"), Decode(HONEYPOT_CHUNKS), Encoding.UTF8);
        File.WriteAllText(Path.Combine(InstallDir, "components", "web_trap_watcher.ps1"), Decode(WEB_TRAP_CHUNKS), Encoding.UTF8);
        File.WriteAllText(Path.Combine(InstallDir, "components", "quick_response.ps1"), Decode(QUICK_RESPONSE_CHUNKS), Encoding.UTF8);
        Console.WriteLine("  5 components extracted.");

        // Step 2: Create scheduled tasks
        Console.WriteLine("[2/4] Creating scheduled tasks...");
        string ps = "powershell.exe -NoProfile -EP Bypass -W Hidden -File";
        string c = InstallDir + "\\components\\";

        CreateTask("DefenseSuite-AutoDefender",  ps + " \"" + c + "auto_defender.ps1\"",  "/sc minute /mo 3");
        CreateTask("DefenseSuite-Honeypot",      ps + " \"" + c + "honeypot.ps1\"",       "/sc onstart");
        CreateTask("DefenseSuite-WebTrap",       ps + " \"" + c + "web_trap_watcher.ps1\"","/sc onstart");
        CreateTask("DefenseSuite-QuickResponse", ps + " \"" + c + "quick_response.ps1\"",  "/sc onstart");
        Console.WriteLine("  4 tasks created.");

        // Step 3: Launch services
        Console.WriteLine("[3/4] Starting defense services...");
        string[] scripts = { "auto_defender.ps1", "honeypot.ps1", "web_trap_watcher.ps1", "quick_response.ps1" };
        foreach (var s in scripts)
        {
            var si = new ProcessStartInfo("powershell.exe",
                "-NoProfile -EP Bypass -W Hidden -File \"" + InstallDir + "\\components\\" + s + "\"");
            si.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(si);
        }
        Thread.Sleep(20000);

        Console.WriteLine("[4/4] Installation complete!");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  CloudDefender is now active.");
        Console.WriteLine("  Status: CloudDefender.exe /status");
        Console.WriteLine("  Uninstall: CloudDefender.exe /uninstall");
        Console.ResetColor();
        if (!Silent) Console.ReadLine();
    }

    static void ShowStatus()
    {
        Banner();
        Console.WriteLine("--- Scheduled Tasks ---");
        string[] tasks = { "AutoDefender", "Honeypot", "WebTrap", "QuickResponse" };
        foreach (var t in tasks)
        {
            var lines = Shell("schtasks", "/query /tn DefenseSuite-" + t + " /fo list").Split('\n');
            string status = "NOT INSTALLED";
            foreach (var l in lines)
                if (l.Trim().StartsWith("Status:")) { status = l.Split(':')[1].Trim(); break; }
            Console.WriteLine("  DefenseSuite-" + t + ": " + status);
        }

        Console.WriteLine("\n--- Honeypot Ports ---");
        int[] ports = { 3389, 22, 23, 21, 5900, 9200, 11211, 27017, 8088, 5432, 5555, 8443 };
        int active = 0;
        var netstat = Shell("netstat", "-ano -p tcp");
        foreach (var p in ports)
        {
            if (netstat.Contains("0.0.0.0:" + p + " ") && netstat.Contains("LISTENING"))
            { Console.WriteLine("  [ON] Port " + p); active++; }
        }
        Console.WriteLine("  Active: " + active + "/" + ports.Length);

        Console.WriteLine("\n--- Firewall Rules ---");
        var rules = Shell("netsh", "advfirewall firewall show rule name=all");
        int d = CountStr(rules, "DEFENDER_BLOCK_");
        int h = CountStr(rules, "HONEYPOT_");
        int w = CountStr(rules, "WEBTRAP_");
        Console.WriteLine("  DEFENDER=" + d + "  HONEYPOT=" + h + "  WEBTRAP=" + w + "  TOTAL=" + (d + h + w));
        Console.WriteLine();
        if (!Silent) Console.ReadLine();
    }

    static int CountStr(string text, string pattern)
    {
        int count = 0, i = 0;
        while ((i = text.IndexOf(pattern, i)) != -1) { count++; i += pattern.Length; }
        return count;
    }

    static void Uninstall()
    {
        Banner();
        string[] tasks = { "AutoDefender", "Honeypot", "WebTrap", "QuickResponse" };
        Console.WriteLine("[1/3] Removing tasks...");
        foreach (var t in tasks)
        {
            Shell("schtasks", "/end /tn DefenseSuite-" + t);
            Shell("schtasks", "/delete /tn DefenseSuite-" + t + " /f");
            Console.WriteLine("  DefenseSuite-" + t);
        }

        Console.WriteLine("[2/3] Removing firewall rules...");
        foreach (var line in Shell("netsh", "advfirewall firewall show rule name=all").Split('\n'))
        {
            if (!line.Contains("Rule Name:")) continue;
            var name = line.Substring(line.IndexOf("Rule Name:") + 10).Trim();
            if (name.StartsWith("DEFENDER_BLOCK_") || name.StartsWith("HONEYPOT_") || name.StartsWith("WEBTRAP_"))
                Shell("netsh", "advfirewall firewall delete rule name=\"" + name + "\"");
        }

        Console.WriteLine("[3/3] Removing files...");
        try { Directory.Delete(InstallDir, true); } catch { }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("CloudDefender uninstalled.");
        Console.ResetColor();
        if (!Silent) Console.ReadLine();
    }

    static void CreateTask(string name, string cmd, string schedule)
    {
        Shell("schtasks", "/delete /tn " + name + " /f");
        Shell("schtasks", "/create /tn " + name + " /tr \"" + cmd + "\" " + schedule + " /ru SYSTEM /rl HIGHEST /f");
    }

    static string Shell(string cmd, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            var p = Process.Start(psi);
            var result = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            return result;
        }
        catch { return ""; }
    }
}
