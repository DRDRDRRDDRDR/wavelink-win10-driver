using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WaveLinkWin10Setup.Ui
{
    /// <summary>
    /// Borderless window with a custom title bar, the way Telegram Desktop draws its own
    /// chrome on Windows: a flat caption strip, the app name on the left, and borderless
    /// minimize / close buttons on the right that light up on hover.
    ///
    /// Resizing is handled through WM_NCHITTEST so the window keeps native resize behaviour
    /// without a visible frame.
    /// </summary>
    public class TgForm : Form
    {
        const int ResizeBorder = 6;
        const int CaptionH = 52;

        protected readonly TgSurfacePanel Caption;
        protected readonly Label CapTitle;
        protected readonly Label CapSubtitle;
        TgTitleButton _btnMin, _btnClose;
        protected TgButton BtnTheme;

        const int WM_NCHITTEST = 0x0084;

        public TgForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = Theme.FontBody;
            BackColor = Theme.Light.WindowBg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            Caption = new TgSurfacePanel { Dock = DockStyle.Top, Height = CaptionH, BackColor = Tg.Current.CardBg };
            Caption.MouseDown += CaptionDrag;

            CapTitle = new Label
            {
                AutoSize = false,
                Left = 18,
                Top = 11,
                Height = 19,
                Width = 420,
                Font = Theme.FontTitle,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
            };
            CapSubtitle = new Label
            {
                AutoSize = false,
                Left = 19,
                Top = 30,
                Height = 16,
                Width = 460,
                Font = Theme.FontCaption,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
            };

            _btnClose = new TgTitleButton { Kind = TgTitleButtonKind.Close, Width = 46, Dock = DockStyle.Right };
            _btnMin = new TgTitleButton { Kind = TgTitleButtonKind.Minimize, Width = 46, Dock = DockStyle.Right };
            _btnClose.Click += (s, e) => Close();
            _btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            BtnTheme = new TgButton { Style = TgButtonStyle.Ghost, Width = 40, Height = 30 };
            BtnTheme.Click += (s, e) => ToggleTheme();

            Caption.Controls.Add(CapTitle);
            Caption.Controls.Add(CapSubtitle);
            Caption.Controls.Add(BtnTheme);
            Caption.Controls.Add(_btnMin);
            Caption.Controls.Add(_btnClose);
            Controls.Add(Caption);

            Tg.Changed += ApplyTheme;
        }

        string _capTitle = "";
        string _capSubtitle = "";

        public string CapTitleText
        {
            get => _capTitle;
            set { _capTitle = value; Text = value; CapTitle.Text = value; }
        }

        public string CapSubtitleText
        {
            get => _capSubtitle;
            set { _capSubtitle = value; CapSubtitle.Text = value; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                // Dark caption for the dark theme, so the system does not flash a white strip.
                int dark = Tg.Current.Dark ? 1 : 0;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            }
            catch { /* older Windows builds: not fatal */ }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Hairline under the caption, the way Telegram separates its header from content.
            using var pen = new Pen(Tg.Current.Separator);
            e.Graphics.DrawLine(pen, 0, Caption.Bottom - 1, ClientSize.Width, Caption.Bottom - 1);
        }

        void ToggleTheme()
        {
            Tg.Apply(Tg.Current.Dark ? Theme.Light : Theme.Night);
        }

        protected virtual void ApplyTheme()
        {
            BackColor = Tg.Current.WindowBg;
            ForeColor = Tg.Current.WindowFg;
            Caption.BackColor = Tg.Current.CardBg;
            CapTitle.ForeColor = Tg.Current.WindowBoldFg;
            CapSubtitle.ForeColor = Tg.Current.WindowSubTextFg;
            BtnTheme.Text = Tg.Current.Dark ? Lang.T("themeLight") : Lang.T("themeDark");
            LayoutCaptionButtons();
            Tg.Repaint(this);
            Invalidate(true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutCaptionButtons();
        }

        void LayoutCaptionButtons()
        {
            if (BtnTheme == null) return;
            BtnTheme.Left = Caption.Width - 100 - BtnTheme.Width;
            BtnTheme.Top = (CaptionH - BtnTheme.Height) / 2;
            BtnTheme.BackColor = Tg.Current.CardBg;
        }

        void CaptionDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1 /*WM_NCLBUTTONDOWN*/, (IntPtr)0x2 /*HTCAPTION*/, IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result != 1 /*HTCLIENT*/) return;
                var p = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
                bool left = p.X <= ResizeBorder, right = p.X >= ClientSize.Width - ResizeBorder;
                bool top = p.Y <= ResizeBorder, bottom = p.Y >= ClientSize.Height - ResizeBorder;
                int hit = 1;
                if (top && left) hit = 13; else if (top && right) hit = 14;
                else if (bottom && left) hit = 16; else if (bottom && right) hit = 17;
                else if (left) hit = 10; else if (right) hit = 11;
                else if (top) hit = 12; else if (bottom) hit = 15;
                if (hit != 1) { m.Result = (IntPtr)hit; return; }
            }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }

    public enum TgTitleButtonKind { Minimize, Close }

    /// <summary>One of the two borderless caption buttons. Close turns red on hover.</summary>
    public class TgTitleButton : TgControl
    {
        public TgTitleButtonKind Kind { get; set; }

        public TgTitleButton()
        {
            Height = 52;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Hover > 0)
            {
                Color hoverBg = Kind == TgTitleButtonKind.Close ? Color.FromArgb(0xE8, 0x11, 0x23) : t.WindowBgOver;
                using var b = new SolidBrush(Color.FromArgb((int)(220 * Hover), hoverBg));
                g.FillRectangle(b, ClientRectangle);
            }

            Color fg = Kind == TgTitleButtonKind.Close && Hover > 0.5 ? Color.White : t.WindowSubTextFg;
            int cx = Width / 2, cy = Height / 2;
            using var pen = new Pen(fg, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (Kind == TgTitleButtonKind.Close)
            {
                g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
            }
            else
            {
                g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
            }
        }
    }
}