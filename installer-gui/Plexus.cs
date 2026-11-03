using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace OsuLazerFix
{
    /// <summary>
    /// The animated background: a web of points and lines. It is dense at the top and thins out further down,
    /// drifts slowly, follows the mouse a little (parallax), moves away from the cursor and links to it with thin lines.
    /// Soft pink and violet glows sit behind it at the top. With animations off one still frame is drawn.
    /// </summary>
    internal sealed class Plexus : FrameworkElement
    {
        private sealed class Node
        {
            public double Bx, By, Ax, Ay, Wx, Wy, Fx, Fy, Z, R;
            public double X, Y, PushX, PushY;
        }

        private const double Link = 88, LinkSq = Link * Link;
        private const double PushRadius = 150, PushForce = 30;
        private const double CursorLink = 150, CursorLinkSq = CursorLink * CursorLink;
        private const int Steps = 14;   // alpha steps of the cached pens

        private static readonly Color[] Hues = { Theme.C(255, 118, 180), Theme.C(232, 116, 206), Theme.C(196, 118, 236), Theme.C(158, 120, 255) };
        private static readonly Pen[,] LinePens = new Pen[Hues.Length, Steps + 1];
        private static readonly Pen[] CursorPens = new Pen[Steps + 1];
        private static readonly Brush[,] DotBrushes = new Brush[Hues.Length, Steps + 1];
        private static readonly Brush GlowPink = Theme.Glow(Theme.PinkC, 74);
        private static readonly Brush GlowViolet = Theme.Glow(Theme.VioletC, 70);
        private static readonly Brush GlowCursor = Theme.Glow(Theme.C(255, 120, 190), 40);

        private readonly DrawingGroup drawing = new DrawingGroup();
        private readonly List<Node> nodes = new List<Node>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double w, h, lastFrame, lastDraw, time;
        private bool running;

        private Point mouse;
        private bool mouseInside;
        private double strength, smx, smy, parX, parY;

        /// <summary>The window is active: full frame rate. Otherwise a lower one is enough.</summary>
        public bool Active = true;

        static Plexus()
        {
            for (int hue = 0; hue < Hues.Length; hue++)
            {
                Color line = Mix(Hues[hue], Colors.White, 0.22);
                Color dot = Mix(Hues[hue], Colors.White, 0.45);
                for (int s = 0; s <= Steps; s++)
                {
                    var p = new Pen(Theme.B(Theme.A((byte)(s * 150 / Steps), line)), 1.0);
                    p.Freeze();
                    LinePens[hue, s] = p;
                    DotBrushes[hue, s] = Theme.B(Theme.A((byte)(s * 235 / Steps), dot));
                }
            }
            for (int s = 0; s <= Steps; s++)
            {
                var p = new Pen(Theme.B(Theme.A((byte)(s * 165 / Steps), Theme.C(255, 160, 205))), 1.0);
                p.Freeze();
                CursorPens[s] = p;
            }
        }

        public Plexus()
        {
            IsHitTestVisible = false;
            SizeChanged += delegate { Build(); Draw(); };
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawDrawing(drawing);
        }

        private static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Places the points: many near the top, fewer further down, never too close to each other.</summary>
        private void Build()
        {
            w = ActualWidth;
            h = ActualHeight;
            nodes.Clear();
            if (w <= 0 || h <= 0) return;

            var rnd = new Random(20261005);
            int count = (int)(w * h / 3300);
            for (int i = 0; i < count; i++)
            {
                double x = 0, y = 0;
                for (int attempt = 0; attempt < 14; attempt++)
                {
                    x = -24 + (w + 48) * rnd.NextDouble();
                    y = -24 + (h + 48) * Math.Pow(rnd.NextDouble(), 1.9);
                    double min = 20 + 34 * Math.Max(0, y / h);
                    bool ok = true;
                    foreach (Node o in nodes)
                    {
                        double dx = o.Bx - x, dy = o.By - y;
                        if (dx * dx + dy * dy < min * min) { ok = false; break; }
                    }
                    if (ok) break;
                }

                double z = 0.55 + rnd.NextDouble();
                nodes.Add(new Node
                {
                    Bx = x,
                    By = y,
                    Ax = 7 + 15 * rnd.NextDouble(),
                    Ay = 5 + 11 * rnd.NextDouble(),
                    Wx = 0.10 + 0.32 * rnd.NextDouble(),
                    Wy = 0.08 + 0.28 * rnd.NextDouble(),
                    Fx = rnd.NextDouble() * Math.PI * 2,
                    Fy = rnd.NextDouble() * Math.PI * 2,
                    Z = z,
                    R = 0.9 + z * 0.85
                });
            }
        }

        public void SetMouse(Point p, bool inside)
        {
            mouse = p;
            if (inside && !mouseInside && strength < 0.02)
            {
                smx = p.X;
                smy = p.Y;
            }
            mouseInside = inside;
        }

        /// <summary>The cursor has left: the light fades out where it was last seen.</summary>
        public void MouseOut()
        {
            mouseInside = false;
        }

        /// <summary>Starts or stops the animation (Anim.Lively) and draws the still frame when it is off.</summary>
        public void Refresh()
        {
            bool want = Anim.Lively;
            if (want && !running)
            {
                running = true;
                lastFrame = clock.Elapsed.TotalSeconds;
                CompositionTarget.Rendering += OnFrame;
            }
            else if (!want && running)
            {
                running = false;
                CompositionTarget.Rendering -= OnFrame;
            }

            if (!want)
            {
                if (!Anim.On)
                {
                    time = 0;
                    strength = 0;
                    parX = parY = 0;
                    foreach (Node n in nodes) n.PushX = n.PushY = 0;
                }
                Draw();
            }
        }

        private void OnFrame(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            if (now - lastDraw < (Active ? 1.0 / 62 : 1.0 / 24)) return;
            lastDraw = now;

            double dt = Math.Min(0.1, Math.Max(0.001, now - lastFrame));
            lastFrame = now;
            time += dt;

            double f = dt * 60;
            strength += ((mouseInside ? 1 : 0) - strength) * (1 - Math.Pow(0.93, f));
            double km = 1 - Math.Pow(0.80, f);
            smx += (mouse.X - smx) * km;
            smy += (mouse.Y - smy) * km;
            double wantParX = w > 0 ? -(smx - w / 2) / w * 18 * strength : 0;
            double wantParY = h > 0 ? -(smy - h / 2) / h * 12 * strength : 0;
            double kp = 1 - Math.Pow(0.94, f);
            parX += (wantParX - parX) * kp;
            parY += (wantParY - parY) * kp;

            double kpush = 1 - Math.Pow(0.87, f);
            foreach (Node n in nodes)
            {
                double x0 = n.Bx + n.Ax * Math.Sin(time * n.Wx + n.Fx) + parX * n.Z;
                double y0 = n.By + n.Ay * Math.Sin(time * n.Wy + n.Fy) + parY * n.Z;
                double tx = 0, ty = 0;
                if (strength > 0.01)
                {
                    double dx = x0 - smx, dy = y0 - smy;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < PushRadius && d > 0.01)
                    {
                        double k = 1 - d / PushRadius;
                        double push = k * k * PushForce * strength;
                        tx = dx / d * push;
                        ty = dy / d * push;
                    }
                }
                n.PushX += (tx - n.PushX) * kpush;
                n.PushY += (ty - n.PushY) * kpush;
            }

            Draw();
        }

        private void Draw()
        {
            if (w <= 0 || h <= 0) return;

            foreach (Node n in nodes)
            {
                n.X = n.Bx + n.Ax * Math.Sin(time * n.Wx + n.Fx) + parX * n.Z + n.PushX;
                n.Y = n.By + n.Ay * Math.Sin(time * n.Wy + n.Fy) + parY * n.Z + n.PushY;
            }

            using (DrawingContext dc = drawing.Open())
            {
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));

                // soft glows at the top
                dc.DrawEllipse(GlowPink, null, new Point(w * 0.16 + 34 * Math.Sin(time * 0.11), -30 + 22 * Math.Sin(time * 0.17 + 1)), 290, 230);
                dc.DrawEllipse(GlowViolet, null, new Point(w * 0.88 + 30 * Math.Sin(time * 0.13 + 2), 20 + 26 * Math.Sin(time * 0.09 + 3)), 260, 220);

                if (strength > 0.01)
                {
                    dc.PushOpacity(strength);
                    dc.DrawEllipse(GlowCursor, null, new Point(smx, smy), 175, 175);
                    dc.Pop();
                }

                // lines between near points; they fade with distance and towards the bottom
                int count = nodes.Count;
                for (int i = 0; i < count; i++)
                {
                    Node a = nodes[i];
                    for (int j = i + 1; j < count; j++)
                    {
                        Node b = nodes[j];
                        double dx = a.X - b.X, dy = a.Y - b.Y;
                        double d2 = dx * dx + dy * dy;
                        if (d2 >= LinkSq) continue;
                        double t = 1 - Math.Sqrt(d2) / Link;
                        double alpha = t * t * Fade((a.Y + b.Y) * 0.5);
                        int step = (int)(alpha * Steps + 0.5);
                        if (step <= 0) continue;
                        dc.DrawLine(LinePens[Hue((a.X + b.X) * 0.5), Math.Min(Steps, step)], new Point(a.X, a.Y), new Point(b.X, b.Y));
                    }
                }

                // lines to the cursor
                if (strength > 0.02)
                {
                    var c = new Point(smx, smy);
                    foreach (Node n in nodes)
                    {
                        double dx = n.X - smx, dy = n.Y - smy;
                        double d2 = dx * dx + dy * dy;
                        if (d2 >= CursorLinkSq) continue;
                        double t = 1 - Math.Sqrt(d2) / CursorLink;
                        int step = (int)(t * strength * Steps + 0.5);
                        if (step <= 0) continue;
                        dc.DrawLine(CursorPens[Math.Min(Steps, step)], c, new Point(n.X, n.Y));
                    }
                }

                foreach (Node n in nodes)
                {
                    int step = (int)((0.35 + 0.65 * Fade(n.Y)) * (0.55 + 0.45 * (n.Z - 0.55)) * Steps + 0.5);
                    if (step <= 0) continue;
                    dc.DrawEllipse(DotBrushes[Hue(n.X), Math.Min(Steps, step)], null, new Point(n.X, n.Y), n.R, n.R);
                }

                dc.Pop();
            }
        }

        /// <summary>1 at the top, about 0.2 at the bottom.</summary>
        private double Fade(double y)
        {
            double t = Math.Max(0, Math.Min(1, y / h));
            return 1 - 0.8 * Math.Pow(t, 0.85);
        }

        private int Hue(double x)
        {
            int i = (int)(x / w * Hues.Length);
            return i < 0 ? 0 : (i >= Hues.Length ? Hues.Length - 1 : i);
        }
    }
}
