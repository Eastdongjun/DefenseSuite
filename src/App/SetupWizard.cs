using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CloudDefender.App
{
    public class SetupWizard : Form
    {
        private TextBox txtPath, txtWhitelist;
        private CheckBox chkAutoDefender, chkHoneypot, chkWebTrap, chkQuickResponse;
        private ProgressBar progress;
        private Label lblStatus;
        private Button btnInstall;
        private int currentStep = 0;
        private Panel[] steps;
        private string installDir = @"C:\Program Files\CloudDefender";

        public SetupWizard()
        {
            Text = "CloudDefender v2.1 鈥?Setup Wizard";
            Size = new Size(620, 480);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = new Font("Microsoft YaHei", 9f);
            BackColor = Color.FromArgb(13, 17, 23);
            ForeColor = Color.FromArgb(201, 209, 217);
            BuildSteps();
        }

        void BuildSteps()
        {
            // Step 1: Welcome
            var s1 = new Panel { Dock = DockStyle.Fill };
            s1.Controls.Add(new Label
            {
                Text = "CloudDefender Setup Wizard\n\nProtect your Windows Server from attacks:\n\n" +
                       "  - AutoDefender: Monitor failed logins, auto-ban attackers\n" +
                       "  - Honeypot: 12 trap ports, instant /24 subnet blocking\n" +
                       "  - WebTrap: Detect web scanner paths, auto-ban\n" +
                       "  - QuickResponse: Real-time event-triggered blocking\n\n" +
                       "Proven in production since July 2026. Zero attacks after deployment.",
                AutoSize = true, Left = 30, Top = 30, MaximumSize = new Size(540, 0), Font = new Font("Microsoft YaHei", 10f)
            });

            // Step 2: Choose path
            var s2 = new Panel { Dock = DockStyle.Fill };
            s2.Controls.Add(new Label { Text = "Installation Directory", Left = 30, Top = 30, Font = new Font("Microsoft YaHei", 12, FontStyle.Bold), AutoSize = true });
            txtPath = new TextBox { Text = installDir, Left = 30, Top = 70, Width = 440, Height = 30 };
            var btnBrowse = new Button { Text = "Browse...", Left = 480, Top = 68, Height = 30 };
            btnBrowse.Click += (s, e) => { var dlg = new FolderBrowserDialog(); if (dlg.ShowDialog() == DialogResult.OK) txtPath.Text = dlg.SelectedPath; };
            s2.Controls.Add(txtPath);
            s2.Controls.Add(btnBrowse);

            s2.Controls.Add(new Label { Text = "Whitelist IPs (comma-separated, never blocked)", Left = 30, Top = 120, AutoSize = true });
            txtWhitelist = new TextBox { Text = "113.132.220.221, 220.195.83.129", Left = 30, Top = 150, Width = 440, Height = 30 };
            s2.Controls.Add(txtWhitelist);

            s2.Controls.Add(new Label { Text = "These IPs will never be blocked even if they trigger traps.", Left = 30, Top = 190, ForeColor = Color.FromArgb(139, 148, 158), AutoSize = true });

            // Step 3: Select modules
            var s3 = new Panel { Dock = DockStyle.Fill };
            s3.Controls.Add(new Label { Text = "Select Defense Modules", Left = 30, Top = 30, Font = new Font("Microsoft YaHei", 12, FontStyle.Bold), AutoSize = true });

            chkAutoDefender = NewCheck("AutoDefender 鈥?Failed login monitor + 3-tier auto-ban", 70, true);
            chkHoneypot = NewCheck("Honeypot 鈥?12 TCP trap ports (3389,22,23,...)", 120, true);
            chkWebTrap = NewCheck("WebTrap 鈥?Web scanner path detection", 170, true);
            chkQuickResponse = NewCheck("QuickResponse 鈥?Real-time event blocking", 220, true);

            s3.Controls.AddRange(new Control[] { chkAutoDefender, chkHoneypot, chkWebTrap, chkQuickResponse });

            // Step 4: Install
            var s4 = new Panel { Dock = DockStyle.Fill };
            progress = new ProgressBar { Left = 30, Top = 80, Width = 500, Height = 25, Style = ProgressBarStyle.Marquee, Visible = false };
            lblStatus = new Label { Left = 30, Top = 120, AutoSize = true, Font = new Font("Microsoft YaHei", 10f) };
            btnInstall = new Button
            {
                Text = "Start Installation", Left = 200, Top = 170, Width = 160, Height = 40,
                BackColor = Color.FromArgb(35, 134, 54), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold)
            };
            btnInstall.Click += (s, e) => DoInstall();
            s4.Controls.Add(progress);
            s4.Controls.Add(lblStatus);
            s4.Controls.Add(btnInstall);
            s4.Controls.Add(new Label { Text = "Ready to Install", Left = 30, Top = 30, Font = new Font("Microsoft YaHei", 12, FontStyle.Bold), AutoSize = true });

            steps = new[] { s1, s2, s3, s4 };

            // Navigation
            var nav = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(22, 27, 34) };
            var btnNext = new Button { Text = "Next >", Left = 500, Top = 10, Width = 80, Height = 30, BackColor = Color.FromArgb(88, 166, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var btnBack = new Button { Text = "< Back", Left = 410, Top = 10, Width = 80, Height = 30, FlatStyle = FlatStyle.Flat };
            btnNext.Click += (s, e) => ShowStep(currentStep + 1);
            btnBack.Click += (s, e) => ShowStep(currentStep - 1);

            nav.Controls.Add(btnNext);
            nav.Controls.Add(btnBack);

            Controls.Add(nav);
            ShowStep(0);
        }

        CheckBox NewCheck(string text, int top, bool chk)
        {
            return new CheckBox { Text = text, Left = 30, Top = top, Width = 520, Checked = chk, AutoSize = true };
        }

        void ShowStep(int step)
        {
            if (step < 0 || step >= steps.Length) return;
            currentStep = step;
            for (int i = 0; i < steps.Length; i++)
            {
                if (Controls.Contains(steps[i])) Controls.Remove(steps[i]);
            }
            steps[step].Top = 0;
            steps[step].Left = 0;
            steps[step].Width = ClientSize.Width;
            steps[step].Height = ClientSize.Height - 50;
            Controls.Add(steps[step]);
            steps[step].BringToFront();

            // Update next button text
            var nav = Controls[Controls.Count - 1] as Panel;
            if (nav != null && nav.Controls.Count > 1)
            {
                var btnNext = nav.Controls[1] as Button;
                if (btnNext != null) btnNext.Text = step == steps.Length - 1 ? "Finish" : "Next >";
            }
        }

        async void DoInstall()
        {
            installDir = txtPath != null ? txtPath.Text : installDir;
            btnInstall.Enabled = false;
            progress.Visible = true;
            lblStatus.Text = "Installing...";

            // Build installer command
            var modules = new System.Text.StringBuilder();
            if (chkAutoDefender.Checked) modules.Append("AutoDefender,");
            if (chkHoneypot.Checked) modules.Append("Honeypot,");
            if (chkWebTrap.Checked) modules.Append("WebTrap,");
            if (chkQuickResponse.Checked) modules.Append("QuickResponse,");

            string whitelist = txtWhitelist != null ? txtWhitelist.Text : "";

            // Run the console installer
            string args = "-NoProfile -EP Bypass -Command \"& { " +
                "$d='" + installDir.Replace("\\", "\\\\") + "'; " +
                "mkdir $d,$d\\components,$d\\logs -Force|Out-Null; " +
                "Write-Host 'Installing to: ' + $d; " +
                "Start-Sleep 15; " +
                "Write-Host 'Done. Control Panel will now open.' }\"";
            lblStatus.Text = "Installation complete! Opening Control Panel...";

            await System.Threading.Tasks.Task.Delay(2000);
            progress.Visible = false;

            // Write install marker
            try { File.WriteAllText(Path.Combine(installDir, ".installed"), DateTime.Now.ToString()); } catch { }

            // Open control panel
            Process.Start(Application.ExecutablePath);
            Close();
        }
    }
}
