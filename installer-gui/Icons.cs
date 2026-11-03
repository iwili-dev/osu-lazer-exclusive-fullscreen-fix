using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OsuLazerFix
{
    /// <summary>
    /// Line icons drawn as vector paths on a 24 x 24 grid (no icon font needed, so they look the same everywhere).
    /// </summary>
    internal static class Icons
    {
        public static readonly Geometry Close = G("M6.5,6.5 L17.5,17.5 M17.5,6.5 L6.5,17.5");
        public static readonly Geometry Minimize = G("M6,12 L18,12");
        public static readonly Geometry Back = G("M14.5,5.5 L8,12 L14.5,18.5");
        public static readonly Geometry Chevron = G("M6.5,9.5 L12,15 L17.5,9.5");
        public static readonly Geometry Check = G("M5.5,12.5 L10,17 L18.5,7.5");
        public static readonly Geometry Search = G("M18,11 A7,7 0 1 1 4,11 A7,7 0 1 1 18,11 Z M16.2,16.2 L20.5,20.5");
        public static readonly Geometry Info = G("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 Z M12,11 L12,16.5 M12,7.75 L12,7.8");
        public static readonly Geometry Globe = G("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 Z M3,12 L21,12 M12,3 C8.4,6.6 8.4,17.4 12,21 M12,3 C15.6,6.6 15.6,17.4 12,21");
        public static readonly Geometry Refresh = G("M19.52,14.74 A8,8 0 1 1 17.66,6.34 L21.2,9.9 M21.2,4.9 L21.2,9.9 L16.2,9.9");
        public static readonly Geometry Folder = G("M3,7.5 C3,6.4 3.9,5.5 5,5.5 L9.6,5.5 L11.6,7.8 L19,7.8 C20.1,7.8 21,8.7 21,9.8 L21,17 C21,18.1 20.1,19 19,19 L5,19 C3.9,19 3,18.1 3,17 Z");
        public static readonly Geometry Monitors = G("M1.8,6 L11.2,6 L11.2,14 L1.8,14 Z M12.8,6 L22.2,6 L22.2,14 L12.8,14 Z M6.5,14 L6.5,17.5 M4.3,17.5 L8.7,17.5 M17.5,14 L17.5,17.5 M15.3,17.5 L19.7,17.5");
        public static readonly Geometry Bolt = G("M13.5,2.5 L5.5,13.5 L11.5,13.5 L10.5,21.5 L18.5,10.5 L12.5,10.5 Z");
        public static readonly Geometry Sparkles = G("M10,3.5 C10.5,8 12,9.5 16.5,10 C12,10.5 10.5,12 10,16.5 C9.5,12 8,10.5 3.5,10 C8,9.5 9.5,8 10,3.5 Z M18,14 C18.25,16 18.9,16.75 21,17 C18.9,17.25 18.25,18 18,20 C17.75,18 17.1,17.25 15,17 C17.1,16.75 17.75,16 18,14 Z");
        public static readonly Geometry Trash = G("M4.5,7 L19.5,7 M9.5,7 L9.5,4.8 C9.5,4.4 9.9,4 10.3,4 L13.7,4 C14.1,4 14.5,4.4 14.5,4.8 L14.5,7 M6.5,7 L7.3,19 C7.4,19.9 8.1,20.5 9,20.5 L15,20.5 C15.9,20.5 16.6,19.9 16.7,19 L17.5,7 M10.25,11 L10.25,16.5 M13.75,11 L13.75,16.5");
        public static readonly Geometry Download = G("M12,4 L12,15 M7.5,10.5 L12,15 L16.5,10.5 M5,19.5 L19,19.5");
        public static readonly Geometry Code = G("M8.5,7 L3.5,12 L8.5,17 M15.5,7 L20.5,12 L15.5,17 M13.5,4.5 L10.5,19.5");
        public static readonly Geometry ArrowUp = G("M12,19 L12,5.5 M6.5,11 L12,5.5 L17.5,11");
        public static readonly Geometry Alert = G("M12,3.5 L21.5,20 L2.5,20 Z M12,9.5 L12,14 M12,17 L12,17.05");
        public static readonly Geometry XCircle = G("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 Z M9,9 L15,15 M15,9 L9,15");
        public static readonly Geometry CheckCircle = G("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 Z M8,12.5 L11,15.5 L16.5,9.5");
        public static readonly Geometry Archive = G("M3.5,4.5 L20.5,4.5 L20.5,8.5 L3.5,8.5 Z M5,8.5 L5,19.5 L19,19.5 L19,8.5 M10,12 L14,12");
        public static readonly Geometry UpCircle = G("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 Z M12,16.5 L12,8 M8.5,11.5 L12,8 L15.5,11.5");
        public static readonly Geometry Gear = MakeGear();

        private static Geometry G(string data)
        {
            Geometry g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }

        /// <summary>A gear with 8 teeth and a hole, as one outline.</summary>
        private static Geometry MakeGear()
        {
            const double rOut = 10, rIn = 7.4, rHole = 3.1;
            var sb = new StringBuilder();
            Func<double, double, string> p = (r, deg) =>
            {
                double a = deg * Math.PI / 180;
                return (12 + r * Math.Cos(a)).ToString("0.###", CultureInfo.InvariantCulture) + "," + (12 + r * Math.Sin(a)).ToString("0.###", CultureInfo.InvariantCulture);
            };
            string ro = rOut.ToString(CultureInfo.InvariantCulture), ri = rIn.ToString(CultureInfo.InvariantCulture);
            for (int k = 0; k < 8; k++)
            {
                double a = k * 45 - 90;
                sb.Append(k == 0 ? "M" : "L").Append(p(rIn, a - 14));
                sb.Append(" L").Append(p(rOut, a - 8));
                sb.Append(" A").Append(ro).Append(',').Append(ro).Append(" 0 0 1 ").Append(p(rOut, a + 8));
                sb.Append(" L").Append(p(rIn, a + 14));
                sb.Append(" A").Append(ri).Append(',').Append(ri).Append(" 0 0 1 ").Append(p(rIn, a + 31)).Append(' ');
            }
            string h = rHole.ToString(CultureInfo.InvariantCulture);
            sb.Append("Z M").Append((12 + rHole).ToString(CultureInfo.InvariantCulture)).Append(",12 A").Append(h).Append(',').Append(h)
              .Append(" 0 1 1 ").Append((12 - rHole).ToString(CultureInfo.InvariantCulture)).Append(",12 A").Append(h).Append(',').Append(h)
              .Append(" 0 1 1 ").Append((12 + rHole).ToString(CultureInfo.InvariantCulture)).Append(",12 Z");
            return G(sb.ToString());
        }

        /// <summary>An icon of the given size. Pass a non-frozen brush to animate its colour. thickness is on the 24 x 24 grid.</summary>
        public static Path Make(Geometry icon, double size, Brush stroke, double thickness = 1.8)
        {
            Geometry g = icon.Clone();
            g.Transform = new ScaleTransform(size / 24.0, size / 24.0);
            g.Freeze();
            double px = thickness * size / 24.0;
            return new Path
            {
                Data = g,
                Width = size,
                Height = size,
                Stretch = Stretch.None,
                Stroke = stroke,
                StrokeThickness = px < 1.15 ? 1.15 : px,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
        }
    }
}
