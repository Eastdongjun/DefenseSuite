using System;
using System.IO;
using System.Windows.Forms;

namespace CloudDefender.App
{
    static class EntryPoint
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string installDir = @"C:\Program Files\CloudDefender";
            string marker = Path.Combine(installDir, ".installed");

            if (File.Exists(marker))
            {
                // Already installed — open Control Panel
                Application.Run(new ControlPanel());
            }
            else
            {
                // Not installed — show Setup Wizard
                Application.Run(new SetupWizard());
            }
        }
    }
}
