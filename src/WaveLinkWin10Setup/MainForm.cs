using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WaveLinkWin10Setup
{
    public partial class MainForm : Form
    {
        TextBox txtMsix;
        Button btnBrowse;
        CheckBox chkSkipApp;
        CheckBox chkSkipDriver;
        NumericUpDown numMinBuild;
        Label lblMsix;
        Label lblMinBuild;
        Button btnRunAll;
        Button btnInstallApp;
        Button btnInstallDriver;
        Button btnVerify;
        Button btnCheck;
        TextBox txtLog;
        Label lblLang;
        ComboBox cboLang;
        string pendingRun;

        public MainForm(string[] args)
        {
            InitializeComponent();
            ParseArgs(args);
        }

        void ParseArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a == "--run" && i + 1 < args.Length) pendingRun = args[++i];
                else if (a == "--msix" && i + 1 < args.Length) txtMsix.Text = args[++i];
                else if (a == "--minbuild" && i + 1 < args.Length)
                {
                    if (decimal.TryParse(args[++i], out var v)) numMinBuild.Value = v;
                }
                else if (a == "--skipapp") chkSkipApp.Checked = true;
                else if (a == "--skipdriver") chkSkipDriver.Checked = true;
            }

            // Auto-execute when launched elevated by RunElevated.
            if (pendingRun != null)
            {
                this.Load += (s, e) => DoRun(pendingRun);
            }
        }

        void InitializeComponent()
        {
            Text = Lang.T("title");
            ClientSize = new System.Drawing.Size(760, 580);
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            lblMsix = new Label { Left = 12, Top = 14, Width = 440, Height = 18, Text = Lang.T("lblMsix") };
            txtMsix = new TextBox { Left = 12, Top = 36, Width = 520, Height = 23 };
            btnBrowse = new Button { Left = 544, Top = 34, Width = 96, Height = 26, Text = Lang.T("btnBrowse") };
            btnBrowse.Click += (s, e) => BrowseMsix();

            lblLang = new Label { Left = 470, Top = 12, Width = 60, Height = 18, Text = Lang.T("langLabel") };
            cboLang = new ComboBox { Left = 536, Top = 10, Width = 120, Height = 23, DropDownStyle = ComboBoxStyle.DropDownList };
            cboLang.Items.Add("中文");
            cboLang.Items.Add("English");
            cboLang.SelectedIndex = Lang.Mode == "zh" ? 0 : 1;
            cboLang.SelectedIndexChanged += (s, e) =>
            {
                Lang.SetMode(cboLang.SelectedIndex == 0 ? "zh" : "en");
                ApplyLang();
            };

            chkSkipApp = new CheckBox { Left = 12, Top = 70, Width = 200, Height = 22, Text = Lang.T("chkSkipApp") };
            chkSkipDriver = new CheckBox { Left = 220, Top = 70, Width = 200, Height = 22, Text = Lang.T("chkSkipDriver") };

            lblMinBuild = new Label { Left = 470, Top = 70, Width = 120, Height = 22, Text = Lang.T("lblMinBuild") };
            numMinBuild = new NumericUpDown { Left = 596, Top = 68, Width = 120, Height = 23, Minimum = 17134, Maximum = 22000, Value = 19041, Increment = 1 };

            btnRunAll = new Button { Left = 12, Top = 104, Width = 140, Height = 30, Text = Lang.T("btnRunAll") };
            btnInstallApp = new Button { Left = 162, Top = 104, Width = 130, Height = 30, Text = Lang.T("btnInstallApp") };
            btnInstallDriver = new Button { Left = 302, Top = 104, Width = 130, Height = 30, Text = Lang.T("btnInstallDriver") };
            btnVerify = new Button { Left = 442, Top = 104, Width = 120, Height = 30, Text = Lang.T("btnVerify") };
            btnCheck = new Button { Left = 572, Top = 104, Width = 160, Height = 30, Text = Lang.T("btnCheck") };

            btnRunAll.Click += (s, e) => RunElevated("all");
            btnInstallApp.Click += (s, e) => RunElevated("app");
            btnInstallDriver.Click += (s, e) => RunElevated("driver");
            btnVerify.Click += (s, e) => DoRun("verify");
            btnCheck.Click += (s, e) => { Log(""); Installer.EnvCheckGui(Log); };

            txtLog = new TextBox
            {
                Left = 12,
                Top = 148,
                Width = 736,
                Height = 420,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Consolas", 9.5F)
            };

            Controls.AddRange(new Control[] { lblMsix, txtMsix, btnBrowse, lblLang, cboLang, chkSkipApp, chkSkipDriver,
                lblMinBuild, numMinBuild, btnRunAll, btnInstallApp, btnInstallDriver, btnVerify, btnCheck, txtLog });

            Log(Lang.T("tipLog"));
            Log(Lang.T("stepLog"));
        }

        /// <summary>Refresh all control captions to the current language.</summary>
        void ApplyLang()
        {
            Text = Lang.T("title");
            lblMsix.Text = Lang.T("lblMsix");
            btnBrowse.Text = Lang.T("btnBrowse");
            chkSkipApp.Text = Lang.T("chkSkipApp");
            chkSkipDriver.Text = Lang.T("chkSkipDriver");
            lblMinBuild.Text = Lang.T("lblMinBuild");
            btnRunAll.Text = Lang.T("btnRunAll");
            btnInstallApp.Text = Lang.T("btnInstallApp");
            btnInstallDriver.Text = Lang.T("btnInstallDriver");
            btnVerify.Text = Lang.T("btnVerify");
            btnCheck.Text = Lang.T("btnCheck");
            lblLang.Text = Lang.T("langLabel");
            cboLang.SelectedIndex = Lang.Mode == "zh" ? 0 : 1;
        }

        void BrowseMsix()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = Lang.T("dlgFilter"),
                Title = Lang.T("dlgTitle")
            };
            if (dlg.ShowDialog() == DialogResult.OK) txtMsix.Text = dlg.FileName;
        }

        void Log(string s)
        {
            if (txtLog.InvokeRequired) { txtLog.Invoke(new Action<string>(Log), s); return; }
            txtLog.AppendText(s + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        /// <summary>
        /// Install actions need admin. If not elevated, relaunch this exe elevated with the same
        /// options and exit the current (non-admin) instance.
        /// </summary>
        void RunElevated(string mode)
        {
            if (Installer.IsAdmin()) { DoRun(mode); return; }

            var a = "--run " + mode;
            if (!string.IsNullOrWhiteSpace(txtMsix.Text)) a += " --msix \"" + txtMsix.Text + "\"";
            a += " --minbuild " + numMinBuild.Value;
            if (chkSkipApp.Checked) a += " --skipapp";
            if (chkSkipDriver.Checked) a += " --skipdriver";

            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, a)
                {
                    Verb = "runas",
                    UseShellExecute = true
                });
                Application.Exit();
            }
            catch (Exception ex)
            {
                Log(Lang.T("elevateFail") + ex.Message);
            }
        }

        void DoRun(string mode)
        {
            try
            {
                Log("");
                if (mode == "verify")
                {
                    Installer.VerifyOnly(Log);
                }
                else
                {
                    Installer.Run(mode, txtMsix.Text, (int)numMinBuild.Value,
                        chkSkipApp.Checked, chkSkipDriver.Checked, Log);
                }
            }
            catch (Exception ex)
            {
                Log(Lang.T("errPrefix") + ex.Message);
            }
        }
    }
}
