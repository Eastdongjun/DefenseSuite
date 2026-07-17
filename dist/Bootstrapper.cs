using System;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;

class DefenseSuiteSetup
{
    static void Main(string[] args)
    {
        Console.Title = "DefenseSuite v1.0";

        // Admin check
        var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            Console.WriteLine("[ERROR] Must run as Administrator!");
            Console.WriteLine("Right-click -> Run as Administrator");
            Console.ReadLine();
            return;
        }

        // Find the companion PowerShell script
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] locations = {
            Path.Combine(exeDir, "DefenseSuite-Setup.ps1"),
            Path.Combine(exeDir, @"..\DefenseSuite-Setup.ps1"),
            @"C:\Program Files\DefenseSuite\DefenseSuite-Setup.ps1"
        };

        string psScript = null;
        foreach (var loc in locations)
        {
            if (File.Exists(loc)) { psScript = loc; break; }
        }

        if (psScript == null)
        {
            Console.WriteLine("[ERROR] Cannot find DefenseSuite-Setup.ps1");
            Console.WriteLine("Make sure the .ps1 file is in the same folder as this EXE.");
            Console.ReadLine();
            return;
        }

        // Build args
        string psArgs = "-NoProfile -ExecutionPolicy Bypass -File \"" + psScript + "\"";
        foreach (var arg in args)
        {
            if (arg.Contains(" "))
                psArgs += " \"" + arg + "\"";
            else
                psArgs += " " + arg;
        }

        // Show banner
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("DefenseSuite v1.0 — Windows Server Protection");
        Console.ResetColor();
        Console.WriteLine();

        // Run PowerShell
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = psArgs,
            UseShellExecute = false
        };
        var p = Process.Start(psi);
        p.WaitForExit();
    }
}
