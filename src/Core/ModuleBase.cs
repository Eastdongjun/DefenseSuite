using System;
using System.Diagnostics;
using System.IO;

namespace CloudDefender.Core
{
    /// <summary>
    /// Base class for all defense modules.
    /// Each module manages one scheduled task and one PowerShell script.
    /// </summary>
    public abstract class ModuleBase
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string ScriptFileName { get; }
        public abstract string ScheduleArgs { get; }  // e.g. "/sc minute /mo 3" or "/sc onstart"
        public abstract string[] EmbeddedChunks { get; }

        protected string InstallDir;
        protected string TaskName { get { return "DefenseSuite-" + Name; } }
        protected string ScriptPath { get { return Path.Combine(InstallDir, "components", ScriptFileName); } }

        public void Initialize(string installDir)
        {
            InstallDir = installDir;
        }

        /// <summary>Extract embedded PowerShell script to disk</summary>
        public void Extract()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScriptPath));
            string b64 = string.Join("", EmbeddedChunks);
            File.WriteAllText(ScriptPath,
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64)),
                System.Text.Encoding.UTF8);
        }

        /// <summary>Create Windows scheduled task</summary>
        public void CreateTask()
        {
            Shell("schtasks", "/delete /tn " + TaskName + " /f");
            string cmd = "powershell.exe -NoProfile -EP Bypass -W Hidden -File \"" + ScriptPath + "\"";
            Shell("schtasks", "/create /tn " + TaskName + " /tr \"" + cmd + "\" " + ScheduleArgs + " /ru SYSTEM /rl HIGHEST /f");
        }

        /// <summary>Start the module immediately</summary>
        public void Start()
        {
            var si = new ProcessStartInfo("powershell.exe",
                "-NoProfile -EP Bypass -W Hidden -File \"" + ScriptPath + "\"")
            { WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(si);
        }

        /// <summary>Stop and remove scheduled task</summary>
        public void Remove()
        {
            Shell("schtasks", "/end /tn " + TaskName);
            Shell("schtasks", "/delete /tn " + TaskName + " /f");
        }

        /// <summary>Get task status</summary>
        public string GetStatus()
        {
            var output = Shell("schtasks", "/query /tn " + TaskName + " /fo list");
            foreach (var line in output.Split('\n'))
                if (line.Trim().StartsWith("Status:"))
                    return line.Split(':')[1].Trim();
            return "NOT INSTALLED";
        }

        /// <summary>Get last run time</summary>
        public string GetLastRun()
        {
            var output = Shell("schtasks", "/query /tn " + TaskName + " /fo list");
            foreach (var line in output.Split('\n'))
                if (line.Contains("Last Run Time:"))
                    return line.Split(new[] { ':' }, 2)[1].Trim();
            return "-";
        }

        protected static string Shell(string cmd, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(cmd, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                var p = Process.Start(psi);
                var result = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(10000);
                return result;
            }
            catch { return ""; }
        }
    }
}
