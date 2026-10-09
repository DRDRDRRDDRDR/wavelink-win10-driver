using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using WaveLinkWin10Setup.Ui;

namespace WaveLinkWin10Setup
{
    /// <summary>
    /// The installer window.
    ///
    /// Layout follows Telegram Desktop's conventions: a flat custom caption, the content split
    /// into titled cards with generous padding, hairline separators instead of frames, one
    /// accent colour reserved for the primary action, and a press ripple on every button.
    /// </summary>
    public partial class MainForm : TgForm
    {
        // -- source card --
        TgTextBox txtMsix;
        TgButton btnBrowse;
        TgCheckBox chkSkipApp;
        TgCheckBox chkSkipDriver;
        TgComboBoxAdapter cboMinBuild;
        Label lblMsix, lblMinBuild;

        // -- action card --
        TgButton btnRunAll, btnInstallApp, btnInstallDriver, btnVerify, btnCheck, btnUpdate;

        // -- status --
        TgProgress progBar;
        Label lblStatus;
        TgLogView logView;
        TgButton btnSaveLog;
        TgToast toast;

        readonly TgCombo _cboBuild = new TgCombo();
        static readonly int[] Builds = { 19041, 18362, 17763 };

        string pendingRun;
        int _minBuild = 19041;
        Panel _content;
        TgCard _cardLog;

        public MainForm(string[] args)
        {
            CapTitleText = Lang.T("title");
            CapSubtitleText = Lang.T("subtitle");
            ClientSize = new Size(900, 720);
            MinimumSize = new Size(840, 660);

            InitializeComponent();
            ApplyTheme();
            ParseArgs(args);
        }

        /// <summary>Small shim so the combo can be treated like the old NumericUpDown.</summary>
        class TgComboBoxAdapter
        {
            readonly TgCombo _c;
            readonly Action<int> _set;
            public TgComboBoxAdapter(TgCombo c, Func<int> get, Action<int> set) { _c = c; _set = set; }
            public void SetBuild(int build)
            {
                for (int i = 0; i < Builds.Length; i++)
                    if (Builds[i] == build) { _c.SelectedIndex = i; _set(build); return; }
            }
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
                    if (int.TryParse(args[++i], out var v)) { _minBuild = v; cboMinBuild.SetBuild(v); }
                }
                else if (a == "--skipapp") chkSkipApp.Checked = true;
                else if (a == "--skipdriver") chkSkipDriver.Checked = true;
            }

            // Auto-execute when relaunched elevated with --run.
            if (pendingRun != null) Load += (s, e) => DoRun(pendingRun);
        }

        void InitializeComponent()
        {
            var content = new Panel { Dock = DockStyle.Fill, BackColor = Tg.Current.WindowBg };
            _content = content;
            Controls.Add(content);
            content.BringToFront();

            const int X = 20;
            int W = ClientSize.Width - X * 2;

            // ---------------------------------------------------------------- source card
            var cardSource = new TgCard { Title = Lang.T("sectionSource"), Left = X, Top = 16, Width = W, Height = 202 };
            content.Controls.Add(cardSource);

            lblMsix = MakeLabel(Lang.T("lblMsix"), Theme.Pad, 40, cardSource, 520);
            txtMsix = new TgTextBox { Left = Theme.Pad, Top = 62, Width = cardSource.Width - Theme.Pad * 2 - 108 };
            btnBrowse = new TgButton { Text = Lang.T("btnBrowse"), Style = TgButtonStyle.Light, Width = 96 };
            btnBrowse.Left = txtMsix.Right + 12;
            btnBrowse.Top = 62;
            btnBrowse.Click += (s, e) => BrowseMsix();

            // Two full-width checkboxes on their own row, so neither caption is clipped.
            chkSkipApp = new TgCheckBox { Text = Lang.T("chkSkipApp"), Left = Theme.Pad, Top = 112, Width = 292 };
            chkSkipDriver = new TgCheckBox { Text = Lang.T("chkSkipDriver"), Left = Theme.Pad + 306, Top = 112, Width = 292 };

            lblMinBuild = MakeLabel(Lang.T("lblMinBuild"), Theme.Pad, 160, cardSource, 150);
            _cboBuild.SetItems("19041 · Windows 10 2004", "18362 · Windows 10 1903", "17763 · Windows 10 1809");
            _cboBuild.SelectedIndex = 0;
            _cboBuild.Left = Theme.Pad + 150;
            _cboBuild.Top = 152;
            _cboBuild.Width = 236;
            _cboBuild.SelectedIndexChanged += (s, e) =>
            {
                _minBuild = Builds[Math.Max(0, Math.Min(Builds.Length - 1, _cboBuild.SelectedIndex))];
            };

            cardSource.Controls.AddRange(new Control[] { lblMsix, txtMsix, btnBrowse, chkSkipApp, chkSkipDriver, lblMinBuild, _cboBuild });
            cboMinBuild = new TgComboBoxAdapter(_cboBuild, () => _minBuild, v => _minBuild = v);

            // ---------------------------------------------------------------- actions card
            // Two rows: the per-step actions on top, the online-update action below, so the
            // six buttons never overlap at the minimum window width.
            var cardActions = new TgCard { Title = Lang.T("sectionActions"), Left = X, Top = cardSource.Bottom + 12, Width = W, Height = 132 };
            content.Controls.Add(cardActions);

            btnRunAll = new TgButton { Text = Lang.T("btnRunAll"), Style = TgButtonStyle.Accent, Width = 176, Left = Theme.Pad, Top = 40 };
            btnInstallApp = new TgButton { Text = Lang.T("btnInstallApp"), Style = TgButtonStyle.Light, Width = 128, Left = btnRunAll.Right + 10, Top = 40 };
            btnInstallDriver = new TgButton { Text = Lang.T("btnInstallDriver"), Style = TgButtonStyle.Light, Width = 128, Left = btnInstallApp.Right + 10, Top = 40 };
            btnVerify = new TgButton { Text = Lang.T("btnVerify"), Style = TgButtonStyle.Light, Width = 108, Left = btnInstallDriver.Right + 10, Top = 40 };
            btnCheck = new TgButton { Text = Lang.T("btnCheck"), Style = TgButtonStyle.Light, Width = 158, Left = btnVerify.Right + 10, Top = 40 };
            btnUpdate = new TgButton { Text = Lang.T("btnUpdate"), Style = TgButtonStyle.Light, Width = 252, Left = Theme.Pad, Top = 84 };

            btnRunAll.Click += (s, e) => RunElevated("all");
            btnInstallApp.Click += (s, e) => RunElevated("app");
            btnInstallDriver.Click += (s, e) => RunElevated("driver");
            btnVerify.Click += (s, e) => DoRun("verify");
            btnCheck.Click += (s, e) => { Log(""); Installer.EnvCheckGui(Log); };
            btnUpdate.Click += (s, e) => StartUpdate();

            cardActions.Controls.AddRange(new Control[] { btnRunAll, btnInstallApp, btnInstallDriver, btnVerify, btnCheck, btnUpdate });

            // ---------------------------------------------------------------- progress row
            progBar = new TgProgress { Left = X + 2, Top = cardActions.Bottom + 16, Width = W - 200 };
            lblStatus = new Label
            {
                Left = progBar.Right + 12,
                Top = progBar.Top - 8,
                Width = 188,
                Height = 22,
                Font = Theme.FontCaption,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
            };
            content.Controls.Add(progBar);
            content.Controls.Add(lblStatus);

            // ---------------------------------------------------------------- log card
            var cardLog = new TgCard { Left = X, Top = progBar.Bottom + 12, Width = W, Height = ClientSize.Height - progBar.Bottom - 28 };
            _cardLog = cardLog;
            cardLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            content.Controls.Add(cardLog);

            logView = new TgLogView { Left = Theme.Pad, Top = 36, Width = cardLog.Width - Theme.Pad * 2, Height = cardLog.Height - Theme.Pad - 44 };
            logView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            cardLog.Controls.Add(logView);

            btnSaveLog = new TgButton { Text = Lang.T("btnSaveLog"), Style = TgButtonStyle.Ghost, Width = 130, Height = 28 };
            btnSaveLog.Left = cardLog.Width - Theme.Pad - btnSaveLog.Width;
            btnSaveLog.Top = 6;
            btnSaveLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveLog.Click += (s, e) => SaveLog();
            cardLog.Controls.Add(btnSaveLog);

            toast = new TgToast();
            content.Controls.Add(toast);

            Log(Lang.T("tipLog"));
            Log(Lang.T("stepLog"));
        }

        /// <summary>
        /// Lay the bottom-anchored pieces out from the content panel's real size.
        ///
        /// This cannot happen in the constructor: the caption is Dock.Top and the content panel
        /// Dock.Fill, so the panel has not been sized yet when the controls are created and any
        /// ClientSize-derived height is wrong. OnResize runs after every real layout.
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        /// <summary>Show() does not change the size, so no Resize fires; lay out once on load too.</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Relayout();
            PerformLayout();
        }

        void Relayout()
        {
            if (_content == null || _cardLog == null) return;

            int w = _content.ClientSize.Width;
            int h = _content.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int cardW = Math.Max(600, w - 40);
            _cardLog.Width = cardW;
            progBar.Width = Math.Max(200, cardW - 200);
            lblStatus.Left = progBar.Right + 12;

            _cardLog.Height = Math.Max(140, h - _cardLog.Top - 20);
            logView.Width = Math.Max(120, _cardLog.Width - Theme.Pad * 2);
            logView.Height = Math.Max(80, _cardLog.Height - Theme.Pad - 44);
        }

        Label MakeLabel(string text, int left, int top, Control parent, int width)
        {
            var l = new Label
            {
                Text = text,
                Left = left,
                Top = top,
                Width = width,
                Height = 18,
                Font = Theme.FontCaption,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };
            parent.Controls.Add(l);
            return l;
        }

        protected override void ApplyTheme()
        {
            base.ApplyTheme();
            Tg.Repaint(this);
            Invalidate(true);
        }

        /// <summary>Refresh all control captions to the current language.</summary>
        void ApplyLang()
        {
            CapTitleText = Lang.T("title");
            CapSubtitleText = Lang.T("subtitle");
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
            btnUpdate.Text = Lang.T("btnUpdate");
            btnSaveLog.Text = Lang.T("btnSaveLog");
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

        void SaveLog()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Log file (*.log)|*.log|Text file (*.txt)|*.txt",
                FileName = "wavelink-setup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try { logView.SaveTo(dlg.FileName); Toast(Lang.T("logSaved") + " " + dlg.FileName); }
            catch (Exception ex) { Toast(Lang.T("errPrefix") + ex.Message); }
        }

        void Toast(string text)
        {
            toast.Left = (ClientSize.Width - toast.Width) / 2;
            toast.Top = ClientSize.Height - 68;
            toast.Show(text);
        }

        void Log(string s)
        {
            if (logView.InvokeRequired) { logView.Invoke(new Action<string>(Log), s); return; }
            logView.Append(s);
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
            a += " --minbuild " + _minBuild;
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
                Toast(Lang.T("elevateFail"));
            }
        }

        /// <summary>
        /// Runs the install/verify flow on a background thread so the UI stays responsive.
        /// Progress is reported to the progress bar through <see cref="SetProgress"/>.
        /// </summary>
        void DoRun(string mode)
        {
            SetControlsEnabled(false);
            SetBusy(true);
            Action<int> progress = SetProgress;
            Task.Run(() =>
            {
                try
                {
                    Log("");
                    if (mode == "verify")
                        Installer.VerifyOnly(Log, progress);
                    else
                        Installer.Run(mode, txtMsix.Text, _minBuild,
                            chkSkipApp.Checked, chkSkipDriver.Checked, Log, progress);
                    SetProgress(100);
                    SetBusy(false);
                    Toast(Lang.T("runFinished"));
                }
                catch (Exception ex)
                {
                    Log(Lang.T("errPrefix") + ex.Message);
                    SetProgress(0);
                    SetBusy(false);
                    Toast(Lang.T("errPrefix") + ex.Message);
                }
                finally
                {
                    try { Invoke(new Action(() => SetControlsEnabled(true))); } catch { }
                }
            });
        }

        /// <summary>Enable/disable all interactive controls during a run.</summary>
        void SetControlsEnabled(bool enabled)
        {
            if (InvokeRequired) { Invoke(new Action<bool>(SetControlsEnabled), enabled); return; }
            btnRunAll.Enabled = enabled;
            btnInstallApp.Enabled = enabled;
            btnInstallDriver.Enabled = enabled;
            btnVerify.Enabled = enabled;
            btnCheck.Enabled = enabled;
            btnUpdate.Enabled = enabled;
            btnBrowse.Enabled = enabled;
            _cboBuild.Enabled = enabled;
            chkSkipApp.Enabled = enabled;
            chkSkipDriver.Enabled = enabled;
            txtMsix.Enabled = enabled;
        }

        /// <summary>Indeterminate sweep while a step of unknown duration is in flight.</summary>
        void SetBusy(bool busy)
        {
            if (progBar.InvokeRequired) { progBar.Invoke(new Action<bool>(SetBusy), busy); return; }
            progBar.Marquee = busy;
            if (busy) lblStatus.Text = Lang.T("lblProgress") + " ...";
        }

        /// <summary>Set the progress bar to a concrete percentage (0-100).</summary>
        void SetProgress(int pct)
        {
            if (progBar.InvokeRequired) { progBar.Invoke(new Action<int>(SetProgress), pct); return; }
            progBar.Marquee = false;
            progBar.Value = Math.Max(0, Math.Min(100, pct));
            lblStatus.Text = Lang.T("lblProgress") + " " + pct + "%";
        }

        /// <summary>
        /// "Check &amp; Update" button: detect the latest Wave Link from Elgato, download it in the
        /// background, then launch an elevated install of the downloaded MSIX.
        /// </summary>
        void StartUpdate()
        {
            btnUpdate.Enabled = false;
            Log("");
            Task.Run(async () =>
            {
                string local = "";
                try { local = await Updater.UpdateAsync(Log); }
                catch (Exception ex) { Log(Lang.T("errPrefix") + ex.Message); Toast(Lang.T("errPrefix") + ex.Message); }

                try
                {
                    if (!string.IsNullOrWhiteSpace(local))
                    {
                        Invoke(new Action(() =>
                        {
                            txtMsix.Text = local;
                            Log(Lang.T("updStart"));
                            RunElevated("all");
                        }));
                    }
                    else
                    {
                        Log(Lang.T("updNone"));
                    }
                }
                catch (Exception ex) { Log(Lang.T("errPrefix") + ex.Message); }
                finally
                {
                    try { Invoke(new Action(() => btnUpdate.Enabled = true)); } catch { }
                }
            });
        }
    }
}