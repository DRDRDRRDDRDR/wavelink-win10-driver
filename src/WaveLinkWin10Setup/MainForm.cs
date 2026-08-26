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
        Label lblMinBuild;
        Button btnRunAll;
        Button btnInstallApp;
        Button btnInstallDriver;
        Button btnVerify;
        Button btnCheck;
        TextBox txtLog;
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
            Text = "Wave Link 3.x · Windows 10 安装器";
            ClientSize = new System.Drawing.Size(760, 580);
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var lblMsix = new Label { Left = 12, Top = 14, Width = 420, Height = 18, Text = "官方 Wave Link MSIX 路径（留空则自动用 input/ 下第一个）" };
            txtMsix = new TextBox { Left = 12, Top = 36, Width = 520, Height = 23 };
            btnBrowse = new Button { Left = 544, Top = 34, Width = 96, Height = 26, Text = "浏览..." };
            btnBrowse.Click += (s, e) => BrowseMsix();

            chkSkipApp = new CheckBox { Left = 12, Top = 70, Width = 200, Height = 22, Text = "跳过应用安装（已装好）" };
            chkSkipDriver = new CheckBox { Left = 220, Top = 70, Width = 200, Height = 22, Text = "跳过驱动安装（已装好）" };

            lblMinBuild = new Label { Left = 470, Top = 70, Width = 120, Height = 22, Text = "最低 Win10 版本(build)" };
            numMinBuild = new NumericUpDown { Left = 596, Top = 68, Width = 120, Height = 23, Minimum = 17134, Maximum = 22000, Value = 19041, Increment = 1 };

            btnRunAll = new Button { Left = 12, Top = 104, Width = 140, Height = 30, Text = "一键运行全部" };
            btnInstallApp = new Button { Left = 162, Top = 104, Width = 130, Height = 30, Text = "仅装应用" };
            btnInstallDriver = new Button { Left = 302, Top = 104, Width = 130, Height = 30, Text = "仅装驱动" };
            btnVerify = new Button { Left = 442, Top = 104, Width = 120, Height = 30, Text = "验证" };
            btnCheck = new Button { Left = 572, Top = 104, Width = 160, Height = 30, Text = "环境检查(干跑)" };

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

            Controls.AddRange(new Control[] { lblMsix, txtMsix, btnBrowse, chkSkipApp, chkSkipDriver,
                lblMinBuild, numMinBuild, btnRunAll, btnInstallApp, btnInstallDriver, btnVerify, btnCheck, txtLog });

            Log("提示：安装类操作需管理员权限，点击后会自动请求提权（UAC）。");
            Log("步骤：① 浏览/放入官方 MSIX 到 input/ → ② 点「一键运行全部」→ ③ 验证服务 Running。");
        }

        void BrowseMsix()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "MSIX 包 (*.msix)|*.msix|所有文件 (*.*)|*.*",
                Title = "选择官方 Wave Link MSIX"
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
                Log("提权失败（已取消或出错）: " + ex.Message);
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
                Log("错误: " + ex.Message);
            }
        }
    }
}
