using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace WaveLinkWin10Setup.Ui
{
    // =====================================================================================
    //  Shared base: dark/light aware, double buffered, hover-animated control.
    // =====================================================================================
    public abstract class TgControl : Control
    {
        protected double Hover;          // 0..1 fade towards "over" state
        protected bool Over, Pressed;
        readonly Timer _fade;

        protected TgControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            _fade = new Timer { Interval = Theme.TimerViewMs };
            _fade.Tick += (s, e) =>
            {
                double target = Over ? 1 : 0;
                double step = Theme.TimerViewMs / (double)Theme.HoverMs;
                if (Math.Abs(Hover - target) <= step) { Hover = target; _fade.Stop(); }
                else Hover += (target > Hover ? step : -step);
                Invalidate();
            };
            Disposed += (s, e) => _fade.Dispose();
        }

        protected override void OnMouseEnter(EventArgs e) { Over = true; _fade.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Over = false; Pressed = false; _fade.Start(); base.OnMouseLeave(e); }

        protected Theme T => Tg.Current;
    }

    // =====================================================================================
    //  Card: a rounded surface that groups related controls, optionally with a section title.
    // =====================================================================================
    public class TgCard : Panel, ISurface
    {
        public string? Title { get; set; }

        public Color SurfaceColor => Tg.Current.CardBg;

        public TgCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var t = Tg.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(e.Graphics, r, Theme.CardRadius, t.CardBg);
            Theme.DrawRounded(e.Graphics, r, Theme.CardRadius, t.CardBorder);
            if (!string.IsNullOrEmpty(Title))
            {
                TextRenderer.DrawText(e.Graphics, Title, Theme.FontSection,
                    new Point(Theme.Pad, Theme.Pad - 2), t.WindowSubTextFg,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }
    }

    // =====================================================================================
    //  Button: flat accent (filled) or light (tinted text), with hover fade and press ripple.
    // =====================================================================================
    public enum TgButtonStyle { Accent, Light, Ghost }

    public class TgButton : TgControl
    {
        readonly Timer _ripple = new Timer { Interval = 16 };
        double _rippleT = 1;
        Point _rippleAt;
        double _progress;               // 0..1 overlay used while a task is running

        public TgButtonStyle Style { get; set; } = TgButtonStyle.Light;
        public double Progress
        {
            get => _progress;
            set { _progress = Math.Max(0, Math.Min(1, value)); Invalidate(); }
        }

        public TgButton()
        {
            Size = new Size(120, Theme.ControlH);
            Font = Theme.FontBody;
            Cursor = Cursors.Hand;
            _ripple.Tick += (s, e) =>
            {
                _rippleT += 16.0 / Theme.RippleMs;
                if (_rippleT >= 1) { _rippleT = 1; _ripple.Stop(); }
                Invalidate();
            };
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Pressed = true; _rippleAt = e.Location; _rippleT = 0; _ripple.Start();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.Rounded(r, Theme.ControlRadius);

            Color fill, fg;
            switch (Style)
            {
                case TgButtonStyle.Accent:
                    fill = Theme.Blend(t.Accent, t.AccentOver, Hover);
                    fg = t.AccentFg;
                    break;
                case TgButtonStyle.Ghost:
                    fill = Color.Transparent;
                    fg = Theme.Blend(t.WindowSubTextFg, t.ActiveTextFg, Hover);
                    break;
                default: // Light — Telegram's "light button": subtle fill, accent text
                    fill = Theme.Blend(t.CardBg, t.WindowBgOver, Math.Max(0.55, Hover));
                    fg = Theme.Blend(t.ActiveTextFg, t.ActiveTextFg, 1);
                    break;
            }

            if (!Enabled)
            {
                // Disabled controls lose contrast rather than gaining a new colour.
                fill = Style == TgButtonStyle.Accent ? Theme.Blend(t.Accent, t.CardBg, 0.62) : t.CardBg;
                fg = Theme.Blend(t.WindowSubTextFg, t.CardBg, 0.5);
            }

            if (fill.A > 0)
            {
                using var b = new SolidBrush(fill);
                g.FillPath(b, path);
            }

            // Press ripple, drawn inside the button outline.
            if (_rippleT < 1 && Enabled)
            {
                var old = g.Clip;
                g.SetClip(path);
                int maxR = (int)(Math.Sqrt(Width * Width + Height * Height) * 1.1);
                int rad = (int)(maxR * Theme.EaseOutQuint(_rippleT));
                int alpha = (int)(70 * (1 - _rippleT));
                using var rb = new SolidBrush(Color.FromArgb(alpha, Style == TgButtonStyle.Accent ? t.AccentRipple : t.WindowBgRipple));
                g.FillEllipse(rb, _rippleAt.X - rad, _rippleAt.Y - rad, rad * 2, rad * 2);
                g.Clip = old;
            }

            // Progress sweep while a task is bound to this button.
            if (_progress > 0)
            {
                using var pc = new SolidBrush(Color.FromArgb(46, t.AccentRipple));
                var pr = new Rectangle(r.X, r.Bottom - 3, (int)(r.Width * _progress), 3);
                g.FillRectangle(pc, pr);
            }

            if (Style == TgButtonStyle.Light && Enabled)
            {
                using var pb = new Pen(t.CardBorder);
                g.DrawPath(pb, path);
            }

            TextRenderer.DrawText(g, Text, Font, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    // =====================================================================================
    //  Check box: 18px rounded square, accent fill when checked, smooth tick.
    // =====================================================================================
    public class TgCheckBox : TgControl
    {
        bool _checked;
        public bool Checked
        {
            get => _checked;
            set { _checked = value; Invalidate(); OnCheckedChanged?.Invoke(this, EventArgs.Empty); }
        }
        public event EventHandler? OnCheckedChanged;

        public TgCheckBox()
        {
            Height = 24;
            Font = Theme.FontBody;
            Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e) { if (Enabled) Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int s = Theme.CheckBoxSize;
            var box = new Rectangle(0, (Height - s) / 2, s, s);

            if (_checked)
            {
                Theme.FillRounded(g, box, 5, Enabled ? Theme.Blend(t.Accent, t.AccentOver, Hover) : Theme.Blend(t.Accent, t.CardBg, 0.6));
                using var pen = new Pen(t.AccentFg, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var pts = new[]
                {
                    new PointF(box.X + s * 0.26f, box.Y + s * 0.53f),
                    new PointF(box.X + s * 0.44f, box.Y + s * 0.70f),
                    new PointF(box.X + s * 0.76f, box.Y + s * 0.30f),
                };
                g.DrawLines(pen, pts);
            }
            else
            {
                Theme.FillRounded(g, box, 5, Theme.Blend(t.InputBg, t.WindowBgOver, Hover));
                Theme.DrawRounded(g, box, 5, Theme.Blend(t.InputBorder, t.ActiveLine, Hover));
            }

            var textRect = new Rectangle(s + 10, 0, Math.Max(0, Width - s - 10), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? t.WindowFg : t.WindowSubTextFg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }
    }

    // =====================================================================================
    //  Text field: rounded surface with an underline that lights up on focus,
    //  wrapping a borderless TextBox so editing behaviour stays native.
    // =====================================================================================
    public class TgTextBox : TgControl
    {
        public readonly TextBox Inner;
        double _focus;               // 0..1 focus fade
        readonly Timer _focusTimer;

        public override string Text { get => Inner.Text; set => Inner.Text = value; }
        public bool ReadOnlyField { get => Inner.ReadOnly; set => Inner.ReadOnly = value; }

        public TgTextBox()
        {
            // NOTE: Inner must exist before the first resize, because OnResize -> LayoutInner()
            // touches it. Setting Height first would fire OnResize against a null field.
            Padding = new Padding(12, 0, 12, 0);

            Inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody,
                BackColor = Color.White,
                ForeColor = Color.Black,
            };
            Inner.GotFocus += (s, e) => { _focusTimer.Start(); };
            Inner.LostFocus += (s, e) => { _focusTimer.Start(); };
            Inner.TextChanged += (s, e) => OnTextChanged(e);
            Controls.Add(Inner);

            _focusTimer = new Timer { Interval = Theme.TimerViewMs };
            _focusTimer.Tick += (s, e) =>
            {
                double target = Inner.Focused ? 1 : 0;
                _focus += target > _focus ? 0.12 : -0.12;
                _focus = Math.Max(0, Math.Min(1, _focus));
                if (_focus == target) _focusTimer.Stop();
                LayoutInner();
                Invalidate();
            };

            Height = Theme.ControlH;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }

        void LayoutInner()
        {
            if (Inner == null) return;
            var t = Tg.Current;
            Inner.BackColor = t.InputBg;
            Inner.ForeColor = t.WindowFg;
            Inner.Left = Padding.Left;
            Inner.Width = Math.Max(10, Width - Padding.Left - Padding.Right);
            Inner.Top = (Height - Inner.PreferredHeight) / 2 + 1;
        }

        protected override void OnMouseDown(MouseEventArgs e) { Inner.Focus(); base.OnMouseDown(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            LayoutInner();
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.ControlRadius, Enabled ? t.InputBg : Theme.Blend(t.InputBg, t.WindowBgOver, 0.4));
            Theme.DrawRounded(g, r, Theme.ControlRadius,
                Enabled ? Theme.Blend(t.InputBorder, t.ActiveLine, _focus) : t.InputBorder);

            if (_focus > 0.01 && Enabled)
            {
                // Telegram's "active line": a 2px accent underline inside the field.
                var bar = new Rectangle(r.X + Theme.ControlRadius, r.Bottom - 2, r.Width - Theme.ControlRadius * 2, 2);
                Theme.FillRounded(g, bar, 1, Color.FromArgb((int)(255 * _focus), t.ActiveLine));
            }
        }
    }

    // =====================================================================================
    //  Drop-down: rounded field that opens a flat popup list with hover highlight.
    // =====================================================================================
    public class TgCombo : TgControl
    {
        readonly List<string> _items = new List<string>();
        int _index = -1;
        TgDropdown? _open;

        public event EventHandler? SelectedIndexChanged;

        public TgCombo()
        {
            Height = Theme.ControlH;
            Font = Theme.FontBody;
            Cursor = Cursors.Hand;
        }

        public void SetItems(params string[] items)
        {
            _items.Clear();
            _items.AddRange(items);
            if (_index < 0 && _items.Count > 0) _index = 0;
            Invalidate();
        }

        public int SelectedIndex
        {
            get => _index;
            set { if (value == _index) return; _index = value; Invalidate(); }
        }

        public string SelectedItem => _index >= 0 && _index < _items.Count ? _items[_index] : "";

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Enabled) return;
            OpenDrop();
            base.OnMouseDown(e);
        }

        void OpenDrop()
        {
            if (_open != null) { _open.Close(); _open = null; return; }
            var host = FindForm();
            if (host == null) return;
            _open = new TgDropdown(_items, _index, Width);
            _open.Picked += i =>
            {
                _index = i;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            };
            _open.Closed += (s, e) => { _open = null; Invalidate(); };
            var p = host.PointToClient(PointToScreen(new Point(0, Height + 4)));
            _open.Show(host, p);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.ControlRadius, Theme.Blend(t.InputBg, t.WindowBgOver, Hover));
            Theme.DrawRounded(g, r, Theme.ControlRadius, Theme.Blend(t.InputBorder, t.ActiveLine, Hover * 0.6));

            var textRect = new Rectangle(Padding.Left + 4, 0, Width - 34, Height);
            TextRenderer.DrawText(g, SelectedItem, Font, textRect, Enabled ? t.WindowFg : t.WindowSubTextFg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            // Chevron, drawn as two strokes so it stays crisp at any DPI.
            int cx = Width - 16, cy = Height / 2 - 1;
            using var pen = new Pen(Theme.Blend(t.WindowSubTextFg, t.ActiveTextFg, Hover), 1.6f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
        }
    }

    /// <summary>Borderless popup list used by <see cref="TgCombo"/>.</summary>
    public class TgDropdown : ToolStripDropDown
    {
        public event Action<int>? Picked;

        public TgDropdown(IReadOnlyList<string> items, int selected, int width)
        {
            var t = Tg.Current;
            AutoSize = false;
            Size = new Size(width, Math.Min(items.Count, 8) * 30 + 12);
            Padding = new Padding(6);
            BackColor = t.CardBg;
            DropShadowEnabled = true;

            var host = new Panel { Dock = DockStyle.Fill, BackColor = t.CardBg };
            int top = 0;
            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                var row = new Label
                {
                    Text = items[i],
                    Left = 6,
                    Top = top,
                    Width = width - 24,
                    Height = 30,
                    Font = Theme.FontBody,
                    ForeColor = idx == selected ? t.ActiveTextFg : t.WindowFg,
                    BackColor = t.CardBg,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0),
                    Cursor = Cursors.Hand,
                };
                row.MouseEnter += (s, e) => { row.BackColor = t.WindowBgOver; };
                row.MouseLeave += (s, e) => { row.BackColor = t.CardBg; };
                row.Click += (s, e) => { Picked?.Invoke(idx); Close(); };
                host.Controls.Add(row);
                top += 30;
            }
            Controls.Add(host);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var t = Tg.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(e.Graphics, r, Theme.CardRadius, t.CardBg);
            Theme.DrawRounded(e.Graphics, r, Theme.CardRadius, t.CardBorder);
        }
    }

    // =====================================================================================
    //  Progress: 6px rounded track (sliderBgInactive) with an accent fill (sliderBgActive),
    //  or an indeterminate sweep when the duration is unknown.
    // =====================================================================================
    public class TgProgress : TgControl
    {
        int _value;
        bool _marquee;
        double _sweep;
        readonly Timer _anim = new Timer { Interval = 16 };

        public TgProgress()
        {
            Height = Theme.ProgressH;
            _anim.Tick += (s, e) => { _sweep = (_sweep + 0.010) % 1.0; Invalidate(); };
        }

        public int Value
        {
            get => _value;
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public bool Marquee
        {
            get => _marquee;
            set { _marquee = value; if (value) _anim.Start(); else _anim.Stop(); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width, Height);
            var track = t.Dark ? Color.FromArgb(0x60, 0x2F, 0x3D, 0x4C) : Color.FromArgb(0x60, 0xE0, 0xE0, 0xE0);
            Theme.FillRounded(g, r, Height / 2, track);

            if (_marquee)
            {
                int w = Math.Max(60, Width / 4);
                double p = _sweep;
                int x = (int)((Width + w) * p) - w;
                var clip = new Rectangle(Math.Max(0, x), 0, Math.Min(w, Width - Math.Max(0, x)), Height);
                if (clip.Width > 0)
                {
                    var old = g.Clip;
                    using var path = Theme.Rounded(r, Height / 2);
                    g.SetClip(path);
                    Theme.FillRounded(g, clip, Height / 2, Theme.Blend(t.Accent, t.AccentRipple, 0.5));
                    g.Clip = old;
                }
            }
            else
            {
                int w = (int)(Width * (_value / 100.0));
                if (w > 0)
                {
                    var fill = new Rectangle(0, 0, w, Height);
                    var old = g.Clip;
                    using var path = Theme.Rounded(new Rectangle(0, 0, Width, Height), Height / 2);
                    g.SetClip(path);
                    Theme.FillRounded(g, fill, Height / 2, Theme.Blend(t.Accent, t.AccentOver, Hover));
                    g.Clip = old;
                }
            }
        }
    }

    // =====================================================================================
    //  Log view: monospace text on the card surface with a thin auto-hiding scrollbar.
    // =====================================================================================
    public class TgLogView : TgControl
    {
        readonly List<string> _lines = new List<string>();
        readonly VScrollBar _bar;
        int _topLine;
        double _overScroll;
        readonly Timer _barFade = new Timer { Interval = 16 };

        public int MaxLines { get; set; } = 4000;

        public TgLogView()
        {
            Font = Theme.FontMono;
            _bar = new VScrollBar { Width = 10, Dock = DockStyle.Right, SmallChange = 1, Visible = false };
            _bar.Scroll += (s, e) => { _topLine = _bar.Value; Invalidate(); };
            Controls.Add(_bar);
            _barFade.Tick += (s, e) =>
            {
                _overScroll += 0.06;
                if (_overScroll >= 1) _overScroll = 1;
                Invalidate();
            };
            MouseWheel += (s, e) => { ScrollBy(-Math.Sign(e.Delta) * 3); };
            Resize += (s, e) => Recompute();
        }

        public void Append(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var l in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')) _lines.Add(l);
            while (_lines.Count > MaxLines) _lines.RemoveAt(0);
            Recompute();
            ScrollToEnd();
        }

        public void Clear() { _lines.Clear(); _topLine = 0; Recompute(); Invalidate(); }

        /// <summary>Write the current contents to a UTF-8 file (users often want the raw log).</summary>
        public void SaveTo(string path) => System.IO.File.WriteAllLines(path, _lines, System.Text.Encoding.UTF8);

        int LineHeight => Font.Height + 2;

        int VisibleLines
        {
            get { int h = Height - 16; return Math.Max(1, h / LineHeight); }
        }

        void Recompute()
        {
            int vis = VisibleLines;
            int max = Math.Max(0, _lines.Count - vis);
            _topLine = Math.Max(0, Math.Min(_topLine, max));
            _bar.Maximum = Math.Max(0, _lines.Count - 1);
            _bar.LargeChange = vis;
            _bar.Value = Math.Min(_topLine, Math.Max(0, _bar.Maximum - _bar.LargeChange + 1));
            _bar.Visible = _lines.Count > vis;
        }

        void ScrollBy(int lines) { _topLine += lines; Recompute(); Invalidate(); }

        public void ScrollToEnd()
        {
            _topLine = Math.Max(0, _lines.Count - VisibleLines);
            Recompute();
            _overScroll = 0;
            _barFade.Start();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.CardRadius, t.Dark ? t.SideBarBg : t.WindowBgOver);

            var old = g.Clip;
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width, Height), Theme.CardRadius)) g.SetClip(path);

            int y = 8;
            int vis = VisibleLines;
            for (int i = 0; i < vis; i++)
            {
                int li = _topLine + i;
                if (li >= _lines.Count) break;
                string line = _lines[li];
                Color c = ColorFor(line, t);
                TextRenderer.DrawText(g, line, Font, new Point(12, y), c,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);
                y += LineHeight;
            }
            g.Clip = old;

            // Thin scrollbar that appears when the view scrolls and fades away afterwards.
            int rows = _lines.Count;
            if (rows > vis)
            {
                int trackH = Height - 16;
                int thumbH = Math.Max(28, trackH * vis / rows);
                int span = Math.Max(1, rows - vis);
                int thumbY = 8 + (trackH - thumbH) * _topLine / span;
                var thumb = new Rectangle(Width - 7, thumbY, Theme.ScrollRadius + 1, thumbH);
                int alpha = (int)(180 * (1 - _overScroll * 0.75));
                var thumbColor = Color.FromArgb(Math.Max(40, alpha), t.Dark ? Color.FromArgb(0x7F, 0x84, 0x89) : Color.FromArgb(0x00, 0x00, 0x00));
                Theme.FillRounded(g, thumb, Theme.ScrollRadius, thumbColor);
            }
        }

        /// <summary>Colour-code well-known result markers so the log is scannable.</summary>
        static Color ColorFor(string line, Theme t)
        {
            if (line.StartsWith("===")) return t.ActiveTextFg;
            if (line.Contains("ERROR") || line.Contains("错误") || line.Contains("Error:")) return t.ErrorFg;
            if (line.Contains("Warning") || line.Contains("警告") || line.Contains("not registered") || line.Contains("缺失")) return t.WarnFg;
            if (line.Contains("OK") || line.Contains("成功") || line.Contains("passed") || line.Contains("Done") || line.Contains("完成")) return t.GoodFg;
            return t.Dark ? Color.FromArgb(0xC8, t.WindowFg) : Color.FromArgb(0xDD, t.WindowFg);
        }
    }

    // =====================================================================================
    //  Toast: a small non-blocking message pinned to the bottom of its parent.
    // =====================================================================================
    public class TgToast : TgControl
    {
        string _text = "";
        double _life;                     // 0..1 remaining
        readonly Timer _timer = new Timer { Interval = 16 };

        public TgToast()
        {
            Height = 40;
            Font = Theme.FontBody;
            Visible = false;
            _timer.Tick += (s, e) =>
            {
                _life -= _decay;
                if (_life <= 0) { _timer.Stop(); Visible = false; }
                else Invalidate();
            };
        }

        double _decay;                    // life lost per tick

        public void Show(string text, int ms = 3200)
        {
            _text = text;
            _life = 1.0;
            _decay = Theme.TimerViewMs / (double)Math.Max(400, ms);
            Width = Math.Min(520, Math.Max(220, TextRenderer.MeasureText(text, Font).Width + 40));
            Visible = true;
            BringToFront();
            _timer.Stop();
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Tg.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.ControlRadius, t.ToastBg);
            TextRenderer.DrawText(g, _text, Font, r, t.ToastFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }
}