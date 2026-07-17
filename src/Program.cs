using System;
using System.Security.Principal;
using CloudDefender.Core;
using CloudDefender.Modules;

class Program
{
    static string InstallDir = @"C:\Program Files\CloudDefender";
    static bool Silent = false;
    static bool ShowStatus = false;
    static bool DoUninstall = false;
    static bool StartDashboard = false;
    static string Whitelist = "";
    static int DashboardPort = 8888;

    [STAThread]
    static int Main(string[] args)
    {
        Console.Title = "CloudDefender v1.0";

        // Parse args
        foreach (var a in args)
        {
            var al = a.ToLower();
            if (al == "/s" || al == "/silent" || al == "-silent") Silent = true;
            else if (al == "/status" || al == "-status") ShowStatus = true;
            else if (al == "/uninstall" || al == "-uninstall") DoUninstall = true;
            else if (al == "/dashboard" || al == "-dashboard" || al == "/web") StartDashboard = true;
            else if (al.StartsWith("/w") || al.StartsWith("-w"))
            {
                var eq = a.IndexOf('='); if (eq < 0) eq = a.IndexOf(':');
                if (eq > 0) Whitelist = a.Substring(eq + 1).Trim('"', '\'');
            }
            else if (al.StartsWith("/port:") || al.StartsWith("-port:"))
            {
                int.TryParse(a.Split(':', '=')[1], out DashboardPort);
            }
        }

        if (!IsAdmin()) { Error("Administrator privileges required! Right-click -> Run as Administrator."); return 1; }

        var installer = new Installer(InstallDir);
        installer.RegisterModule(new AutoDefenderModule());
        installer.RegisterModule(new HoneypotModule());
        installer.RegisterModule(new WebTrapModule());
        installer.RegisterModule(new QuickResponseModule());

        if (DoUninstall) { installer.UninstallAll(); return 0; }
        if (ShowStatus) { installer.ShowStatus(); return 0; }

        // Install
        Banner();
        Console.WriteLine("Installation directory: " + InstallDir);
        Console.WriteLine("Dashboard port: " + DashboardPort);
        Console.WriteLine();

        if (string.IsNullOrEmpty(Whitelist) && !Silent)
        {
            Console.Write("Whitelist IPs (comma-separated, Enter for none): ");
            Whitelist = (Console.ReadLine() ?? "").Trim();
        }

        installer.InstallAll(Whitelist);

        // Start dashboard
        var dashboard = new DashboardServer(DashboardPort, installer);
        dashboard.Start();
        Console.WriteLine();
        Console.WriteLine("  Dashboard: http://localhost:" + DashboardPort);
        Console.WriteLine("  (Leave this window open to keep dashboard running)");
        Console.WriteLine();

        // Open browser
        try { System.Diagnostics.Process.Start("http://localhost:" + DashboardPort); } catch { }

        Console.WriteLine("Press Ctrl+C to stop the dashboard. The defense services will keep running.");
        Console.CancelKeyPress += (s, e) => { dashboard.Stop(); };

        // Block until user exits
        while (true) { System.Threading.Thread.Sleep(1000); }
    }

    static bool IsAdmin()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
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
        Console.WriteLine(@"
   ____ _                 _  ____        __              __
  / ___| | ___  _   _  __| ||  _ \  ___ / _| ___ _ __   / _| ___  _ __ ___
 | |   | |/ _ \| | | |/ _  || | | |/ _ \ |_ / _ \  _ \ | |_ / _ \|  __/ _ \
 | |___| | (_) | |_| | (_| || |_| |  __/  _|  __/ | | ||  _| (_) | | |  __/
  \____|_|\___/ \__,_|\__,_||____/ \___|_|  \___|_| |_||_|  \___/|_|  \___|
                      Cloud Server Protection v1.0");
        Console.ResetColor();
        Console.WriteLine();
    }
}
