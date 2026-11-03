using System.Windows;
using System.Windows.Media;

namespace OsuLazerFix
{
    /// <summary>Colours, brushes and fonts of the program. Dark violet glass with an osu! pink to violet accent.</summary>
    internal static class Theme
    {
        public static Color C(byte r, byte g, byte b) { return Color.FromRgb(r, g, b); }
        public static Color A(byte a, Color c) { return Color.FromArgb(a, c.R, c.G, c.B); }

        public static SolidColorBrush B(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>A brush that can be animated (not frozen).</summary>
        public static SolidColorBrush Live(Color c) { return new SolidColorBrush(c); }

        // ---- base colours ----
        public static readonly Color BgTopC = C(26, 17, 37);
        public static readonly Color BgMidC = C(15, 12, 22);
        public static readonly Color BgBottomC = C(10, 9, 14);
        public static readonly Color TxtC = C(246, 243, 250);
        public static readonly Color SubC = C(163, 155, 178);
        public static readonly Color DimC = C(104, 97, 118);
        public static readonly Color PinkC = C(255, 102, 170);
        public static readonly Color PinkSoftC = C(255, 168, 208);
        public static readonly Color VioletC = C(150, 110, 255);
        public static readonly Color MintC = C(110, 231, 168);
        public static readonly Color AmberC = C(255, 196, 107);
        public static readonly Color RedC = C(255, 107, 122);
        public static readonly Color InkC = C(20, 10, 22);   // text on bright buttons

        // glass surfaces (drawn over the animated background)
        public static readonly Color SurfaceC = Color.FromArgb(186, 25, 20, 35);
        public static readonly Color SurfaceHoverC = Color.FromArgb(206, 36, 29, 49);
        public static readonly Color Surface2C = Color.FromArgb(150, 44, 37, 58);
        public static readonly Color Surface2HoverC = Color.FromArgb(190, 58, 49, 76);
        public static readonly Color FieldC = Color.FromArgb(170, 13, 11, 19);
        public static readonly Color StrokeC = Color.FromArgb(24, 255, 255, 255);
        public static readonly Color StrokeHiC = Color.FromArgb(52, 255, 255, 255);
        public static readonly Color Transparent = Color.FromArgb(0, 255, 255, 255);

        public static readonly SolidColorBrush Txt = B(TxtC);
        public static readonly SolidColorBrush Sub = B(SubC);
        public static readonly SolidColorBrush Dim = B(DimC);
        public static readonly SolidColorBrush Pink = B(PinkC);
        public static readonly SolidColorBrush PinkSoft = B(PinkSoftC);
        public static readonly SolidColorBrush Violet = B(VioletC);
        public static readonly SolidColorBrush Mint = B(MintC);
        public static readonly SolidColorBrush Amber = B(AmberC);
        public static readonly SolidColorBrush Red = B(RedC);
        public static readonly SolidColorBrush Ink = B(InkC);
        public static readonly SolidColorBrush White = B(Colors.White);
        public static readonly SolidColorBrush Surface = B(SurfaceC);
        public static readonly SolidColorBrush Surface2 = B(Surface2C);
        public static readonly SolidColorBrush Field = B(FieldC);
        public static readonly SolidColorBrush Stroke = B(StrokeC);
        public static readonly SolidColorBrush StrokeHi = B(StrokeHiC);
        public static readonly SolidColorBrush Divider = B(Color.FromArgb(16, 255, 255, 255));
        public static readonly SolidColorBrush Clear = B(Transparent);

        /// <summary>The accent: osu! pink to violet.</summary>
        public static readonly LinearGradientBrush Accent = Gradient(PinkC, VioletC, new Point(0, 0), new Point(1, 1));
        public static readonly LinearGradientBrush AccentText = Gradient(C(255, 140, 196), C(176, 146, 255), new Point(0, 0), new Point(1, 0));

        public static readonly LinearGradientBrush WindowBg = Gradient3(BgTopC, BgMidC, BgBottomC);

        public static LinearGradientBrush Gradient(Color a, Color b, Point start, Point end)
        {
            var g = new LinearGradientBrush(a, b, start, end);
            g.Freeze();
            return g;
        }

        private static LinearGradientBrush Gradient3(Color a, Color b, Color c)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g.GradientStops.Add(new GradientStop(a, 0));
            g.GradientStops.Add(new GradientStop(b, 0.45));
            g.GradientStops.Add(new GradientStop(c, 1));
            g.Freeze();
            return g;
        }

        /// <summary>A soft round glow: the colour in the middle fading to nothing at the edge.</summary>
        public static RadialGradientBrush Glow(Color c, byte alpha)
        {
            var g = new RadialGradientBrush();
            g.GradientStops.Add(new GradientStop(A(alpha, c), 0));
            g.GradientStops.Add(new GradientStop(A((byte)(alpha * 0.45), c), 0.45));
            g.GradientStops.Add(new GradientStop(A(0, c), 1));
            g.Freeze();
            return g;
        }

        public static readonly FontFamily Sans = new FontFamily("Segoe UI");
        public static readonly FontFamily Mono = new FontFamily("Consolas");
    }
}
