using System;
using System.Drawing;
using System.Windows.Forms;

namespace WaveLinkWin10Setup.Ui
{
    /// <summary>
    /// Colour scheme and metrics for the installer UI.
    ///
    /// The palette is not invented: the hex values below are taken from Telegram Desktop's
    /// bundled theme files (Telegram/Resources/day-custom-base.tdesktop-theme and
    /// night-custom-base.tdesktop-theme). The token names are Telegram's own
    /// (windowBg, windowBgOver, windowBgActive, activeButtonBgRipple, ...) so the mapping
    /// back to the source is direct.
    ///
    /// Metrics follow the same spirit: flat surfaces, one accent colour, hairline separators
    /// instead of frames, and a consistent 4px spacing grid.
    /// </summary>
    public sealed class Theme
    {
        public bool Dark { get; private init; }

        // --- surfaces (Telegram: windowBg / windowBgOver / sideBarBg) ---
        public Color WindowBg { get; private init; }
        public Color WindowBgOver { get; private init; }
        public Color WindowBgRipple { get; private init; }
        public Color CardBg { get; private init; }
        public Color CardBorder { get; private init; }
        public Color SideBarBg { get; private init; }

        // --- text (Telegram: windowFg / windowBoldFg / windowSubTextFg) ---
        public Color WindowFg { get; private init; }
        public Color WindowBoldFg { get; private init; }
        public Color WindowSubTextFg { get; private init; }

        // --- accent (Telegram: windowBgActive, activeButtonBg/Over/Ripple) ---
        public Color Accent { get; private init; }
        public Color AccentOver { get; private init; }
        public Color AccentRipple { get; private init; }
        public Color AccentFg { get; private init; }
        public Color ActiveTextFg { get; private init; }

        // --- fields and status (Telegram: inputBorderFg / activeLineFg / boxTextFgGood|Error) ---
        public Color InputBg { get; private init; }
        public Color InputBorder { get; private init; }
        public Color ActiveLine { get; private init; }
        public Color ErrorLine { get; private init; }
        public Color GoodFg { get; private init; }
        public Color ErrorFg { get; private init; }
        public Color WarnFg { get; private init; }

        // --- misc ---
        public Color ScrollThumb { get; private init; }
        public Color ScrollThumbOver { get; private init; }
        public Color Separator { get; private init; }
        public Color ToastBg { get; private init; }
        public Color ToastFg { get; private init; }

        // --- metrics (values taken from lib_ui/ui/basic.style, widgets.style, layers.style) ---
        public const int Grid = 2;
        public const int Pad = 20;          // boxPadding is 24 on the horizontal axis; 20 reads better here
        public const int Gap = 12;
        public const int GapTight = 8;
        public const int ControlH = 34;     // standard button height in widgets.style
        public const int CheckBoxSize = 22; // checkbox check diameter, 2px stroke
        public const int ProgressH = 6;     // TabsSlider bar thickness
        public const int CardRadius = 8;    // box / card radius
        public const int ControlRadius = 4; // buttonRadius
        public const int ScrollRadius = 2;  // scrollbar thumb radius

        // --- typography (Telegram ships Open Sans and falls back to Segoe UI on Windows) ---
        public const string UiFont = "Segoe UI";
        public const string MonoFont = "Consolas";

        public static Font FontTitle => new Font(UiFont, 12f, FontStyle.Bold);      // 16px semibold
        public static Font FontSection => new Font(UiFont, 10f, FontStyle.Bold);    // 14px semibold
        public static Font FontBody => new Font(UiFont, 9.75f, FontStyle.Regular);  // 13px
        public static Font FontBodyBold => new Font(UiFont, 9.75f, FontStyle.Bold);
        public static Font FontCaption => new Font(UiFont, 9f, FontStyle.Regular);  // 12px
        public static Font FontMono => new Font(MonoFont, 9f, FontStyle.Regular);   // Consolas 12px

        // --- motion (ripple_animation.cpp / animation_value.cpp) ---
        public const int HoverMs = 150;     // hover fade
        public const int StateMs = 120;     // universalDuration: check / toggle changes
        public const int RippleMs = 650;    // ripple show, easeOutQuint
        public const int TimerViewMs = 16;

        /// <summary>easeOutQuint, the easing Telegram uses for its ripple.</summary>
        public static double EaseOutQuint(double t)
        {
            double u = t - 1;
            return u * u * u * u * u + 1;
        }

        public static readonly Theme Light = new Theme
        {
            Dark = false,
            WindowBg = Hex("FFFFFF"),
            WindowBgOver = Hex("F1F1F1"),
            WindowBgRipple = Hex("E5E5E5"),
            CardBg = Hex("FFFFFF"),
            CardBorder = Hex("E0E0E0"),
            SideBarBg = Hex("F1F1F1"),
            WindowFg = Hex("000000"),
            WindowBoldFg = Hex("222222"),
            WindowSubTextFg = Hex("999999"),
            Accent = Hex("40A7E3"),
            AccentOver = Hex("39A5DB"),
            AccentRipple = Hex("2095D0"),
            AccentFg = Hex("FFFFFF"),
            ActiveTextFg = Hex("168ACD"),
            InputBg = Hex("FFFFFF"),
            InputBorder = Hex("E0E0E0"),
            ActiveLine = Hex("37A1DE"),
            ErrorLine = Hex("E48383"),
            GoodFg = Hex("4AB44A"),
            ErrorFg = Hex("D84D4D"),
            WarnFg = Hex("E8A659"),
            ScrollThumb = Color.FromArgb(0x53, 0x00, 0x00, 0x00),
            ScrollThumbOver = Color.FromArgb(0x7A, 0x00, 0x00, 0x00),
            Separator = Hex("E8E8E8"),
            ToastBg = Hex("2C3033"),
            ToastFg = Hex("FFFFFF"),
        };

        public static readonly Theme Night = new Theme
        {
            Dark = true,
            WindowBg = Hex("17212B"),
            WindowBgOver = Hex("232E3C"),
            WindowBgRipple = Hex("24303D"),
            CardBg = Hex("232E3C"),
            CardBorder = Hex("2B3846"),
            SideBarBg = Hex("0E1621"),
            WindowFg = Hex("F5F5F5"),
            WindowBoldFg = Hex("E9E8E8"),
            WindowSubTextFg = Hex("708499"),
            Accent = Hex("5288C1"),
            AccentOver = Hex("3476AB"),
            AccentRipple = Hex("3B7CB1"),
            AccentFg = Hex("FFFFFF"),
            ActiveTextFg = Hex("6AB3F3"),
            InputBg = Hex("242F3D"),
            InputBorder = Hex("2F3D4C"),
            ActiveLine = Hex("6396CB"),
            ErrorLine = Hex("EF5959"),
            GoodFg = Hex("6FCF6F"),
            ErrorFg = Hex("EF5959"),
            WarnFg = Hex("E8A659"),
            ScrollThumb = Color.FromArgb(0x7A, 0x7F, 0x84, 0x89),
            ScrollThumbOver = Color.FromArgb(0xBC, 0x64, 0x68, 0x6C),
            Separator = Hex("2B3846"),
            ToastBg = Hex("000000"),
            ToastFg = Hex("FFFFFF"),
        };

        static Color Hex(string rgb) => Color.FromArgb(
            Convert.ToInt32(rgb.Substring(0, 2), 16),
            Convert.ToInt32(rgb.Substring(2, 2), 16),
            Convert.ToInt32(rgb.Substring(4, 2), 16));

        // ---- shared painting helpers ----

        public static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0) { path.AddRectangle(r); return path; }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color c)
        {
            using var p = Rounded(r, radius);
            using var b = new SolidBrush(c);
            g.FillPath(b, p);
        }

        public static void DrawRounded(Graphics g, Rectangle r, int radius, Color c, float width = 1f)
        {
            using var p = Rounded(r, radius);
            using var pen = new Pen(c, width);
            g.DrawPath(pen, p);
        }

        public static Color Blend(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>
    /// A container that draws its own surface (a card, or the caption strip). Controls inside
    /// one adopt its surface colour as their opaque background; without this a control left at
    /// <see cref="Color.Transparent"/> renders black wherever the parent does custom painting.
    /// </summary>
    public interface ISurface
    {
        Color SurfaceColor { get; }
    }

    /// <summary>
    /// Process-wide current theme. Controls read <see cref="Current"/> while painting, so a
    /// theme switch only needs a redraw of the form tree.
    /// </summary>
    public static class Tg
    {
        public static Theme Current { get; private set; } = Theme.Light;

        public static event Action? Changed;

        public static void Apply(Theme theme)
        {
            Current = theme;
            Changed?.Invoke();
        }

        /// <summary>Repaint a whole form tree after a theme change, and re-adopt surface colours.</summary>
        public static void Repaint(Control root)
        {
            var surface = Current.WindowBg;
            for (var p = root.Parent; p != null; p = p.Parent)
            {
                if (p is ISurface s) { surface = s.SurfaceColor; break; }
            }

            // A surface draws its own rounded shape, so what shows behind its corners is the
            // parent's colour; everything else is filled flat with the surface it sits on.
            root.BackColor = root is ISurface ? Current.WindowBg : surface;
            root.ForeColor = Current.WindowFg;
            root.Invalidate(true);
            foreach (Control c in root.Controls) Repaint(c);
        }
    }

    /// <summary>Plain panel that acts as a surface (used for the caption strip).</summary>
    public class TgSurfacePanel : Panel, ISurface
    {
        public Color SurfaceColor => Tg.Current.CardBg;

        public TgSurfacePanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var b = new SolidBrush(SurfaceColor);
            e.Graphics.FillRectangle(b, ClientRectangle);
        }
    }
}