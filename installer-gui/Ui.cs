using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OsuLazerFix
{
    /// <summary>Small building blocks shared by all screens.</summary>
    internal static class Ui
    {
        public static TextBlock Text(string text, double size, Brush fg, FontWeight weight, FontFamily font = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = fg,
                FontWeight = weight,
                FontFamily = font ?? Theme.Sans,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        public static TextBlock Wrapped(string text, double size, Brush fg)
        {
            TextBlock t = Text(text, size, fg, FontWeights.Normal);
            t.TextWrapping = TextWrapping.Wrap;
            t.TextTrimming = TextTrimming.None;
            return t;
        }

        /// <summary>Makes an element behave like a button: hand cursor, a small press-in, click on release inside.</summary>
        public static void Clickable(FrameworkElement el, Action click, double pressScale = 0.965)
        {
            el.Cursor = Cursors.Hand;
            ScaleTransform st = null;
            if (pressScale < 1)
            {
                st = new ScaleTransform(1, 1);
                el.RenderTransformOrigin = new Point(0.5, 0.5);
                el.RenderTransform = st;
            }

            bool down = false;
            Action release = delegate
            {
                if (st == null) return;
                Anim.To(st, ScaleTransform.ScaleXProperty, 1, 260, Anim.Pop);
                Anim.To(st, ScaleTransform.ScaleYProperty, 1, 260, Anim.Pop);
            };

            el.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (!el.IsEnabled) return;
                down = true;
                el.CaptureMouse();
                if (st != null)
                {
                    Anim.To(st, ScaleTransform.ScaleXProperty, pressScale, 90);
                    Anim.To(st, ScaleTransform.ScaleYProperty, pressScale, 90);
                }
            };
            el.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                if (!down) return;
                e.Handled = true;
                down = false;
                Point p = e.GetPosition(el);
                bool inside = p.X >= 0 && p.Y >= 0 && p.X <= el.ActualWidth && p.Y <= el.ActualHeight;
                el.ReleaseMouseCapture();
                release();
                if (inside && el.IsEnabled && click != null) click();
            };
            el.LostMouseCapture += delegate
            {
                if (!down) return;
                down = false;
                release();
            };
        }

        /// <summary>A square icon button for the top bar. Danger = turns red on hover (close). Spin = the icon turns on hover (gear).</summary>
        public static Border IconButton(Geometry icon, Action click, bool danger = false, bool spin = false)
        {
            var bg = Theme.Live(Theme.Transparent);
            var fg = Theme.Live(Theme.SubC);
            Path path = Icons.Make(icon, 17, fg, 1.75);
            RotateTransform rot = null;
            if (spin)
            {
                rot = new RotateTransform(0);
                path.RenderTransformOrigin = new Point(0.5, 0.5);
                path.RenderTransform = rot;
            }

            var b = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(10),
                Background = bg,
                VerticalAlignment = VerticalAlignment.Center,
                Child = path
            };
            b.MouseEnter += delegate
            {
                Anim.Color(bg, danger ? Color.FromArgb(235, 232, 72, 92) : Color.FromArgb(22, 255, 255, 255), 140);
                Anim.Color(fg, danger ? Colors.White : Theme.TxtC, 140);
                if (rot != null) Anim.To(rot, RotateTransform.AngleProperty, 90, 520, Anim.Soft);
            };
            b.MouseLeave += delegate
            {
                Anim.Color(bg, Theme.Transparent, 220);
                Anim.Color(fg, Theme.SubC, 220);
                if (rot != null) Anim.To(rot, RotateTransform.AngleProperty, 0, 520, Anim.Soft);
            };
            Clickable(b, click, 0.9);
            return b;
        }

        /// <summary>A small "BETA" label.</summary>
        public static Border Badge(out TextBlock text)
        {
            text = Text("", 9.5, Theme.PinkSoft, FontWeights.Bold);
            return new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 1, 6, 1.5),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = Theme.Gradient(Theme.A(52, Theme.PinkC), Theme.A(52, Theme.VioletC), new Point(0, 0), new Point(1, 0)),
                BorderBrush = Theme.B(Theme.A(84, Theme.PinkC)),
                BorderThickness = new Thickness(1),
                Child = text
            };
        }

        /// <summary>The square tile with an icon at the start of a settings row.</summary>
        public static Border Tile(Geometry icon)
        {
            return new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(11),
                VerticalAlignment = VerticalAlignment.Center,
                Background = Theme.Gradient(Theme.A(44, Theme.PinkC), Theme.A(44, Theme.VioletC), new Point(0, 0), new Point(1, 1)),
                BorderBrush = Theme.B(Theme.A(40, Theme.PinkSoftC)),
                BorderThickness = new Thickness(1),
                Child = Icons.Make(icon, 18, Theme.PinkSoft, 1.7)
            };
        }

        private static BitmapFrame[] iconFrames;

        /// <summary>The logo: the program icon itself (a frame of the embedded app.ico). The tile inside is size x size.</summary>
        public static FrameworkElement Logo(double size)
        {
            // the tile fills 92 % of the icon picture, the rest of it is transparent
            double box = Math.Round(size / 0.92);
            var image = new Image
            {
                Width = box,
                Height = box,
                Margin = new Thickness((size - box) / 2),
                Stretch = Stretch.Fill,
                Source = IconFrame(box * 2)
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return image;
        }

        /// <summary>The smallest frame of app.ico that is at least this many pixels wide (or else the biggest one).</summary>
        private static BitmapFrame IconFrame(double pixels)
        {
            try
            {
                if (iconFrames == null)
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                    {
                        if (stream == null) return null;
                        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        var frames = new BitmapFrame[decoder.Frames.Count];
                        decoder.Frames.CopyTo(frames, 0);
                        iconFrames = frames;
                    }
                }
                BitmapFrame best = null;
                foreach (BitmapFrame frame in iconFrames)
                {
                    if (best == null
                        || (frame.PixelWidth >= pixels && (best.PixelWidth < pixels || frame.PixelWidth < best.PixelWidth))
                        || (best.PixelWidth < pixels && frame.PixelWidth > best.PixelWidth))
                        best = frame;
                }
                return best;
            }
            catch (Exception)
            {
                return null;   // the window works without the logo
            }
        }

        public static bool IsInside(DependencyObject d, DependencyObject ancestor)
        {
            while (d != null)
            {
                if (d == ancestor) return true;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }
    }

    // ===================================================================================== switch

    /// <summary>An on/off switch with a sliding knob. Toggle is called on click; the owner changes the setting and calls Set.</summary>
    internal sealed class Switch : Grid
    {
        private readonly Border fill;
        private readonly Border knob;
        private readonly TranslateTransform knobMove = new TranslateTransform();
        private readonly SolidColorBrush trackBrush = Theme.Live(Color.FromArgb(30, 255, 255, 255));
        private readonly SolidColorBrush knobBrush = Theme.Live(Theme.C(205, 199, 216));

        public Action Toggle;
        public bool IsOn { get; private set; }

        public Switch()
        {
            Width = 46;
            Height = 26;
            VerticalAlignment = VerticalAlignment.Center;
            Background = Brushes.Transparent;

            Children.Add(new Border
            {
                CornerRadius = new CornerRadius(13),
                Background = trackBrush,
                BorderBrush = Theme.B(Color.FromArgb(36, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            });
            fill = new Border { CornerRadius = new CornerRadius(13), Background = Theme.Accent, Opacity = 0 };
            Children.Add(fill);
            knob = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = knobBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(3, 0, 0, 0),
                RenderTransform = knobMove
            };
            Children.Add(knob);

            MouseEnter += delegate { Anim.Color(trackBrush, Color.FromArgb(48, 255, 255, 255), 140); };
            MouseLeave += delegate { Anim.Color(trackBrush, Color.FromArgb(30, 255, 255, 255), 220); };
            Ui.Clickable(this, delegate { if (Toggle != null) Toggle(); }, 0.92);
        }

        public void Set(bool on, bool animate)
        {
            IsOn = on;
            double ms = animate ? 300 : 0;
            Anim.To(fill, OpacityProperty, on ? 1 : 0, ms * 0.7);
            Anim.To(knobMove, TranslateTransform.XProperty, on ? 20 : 0, ms, Anim.Pop);
            Anim.Color(knobBrush, on ? Colors.White : Theme.C(205, 199, 216), ms);
        }

        /// <summary>While the setting is being saved.</summary>
        public bool Busy
        {
            set
            {
                Opacity = value ? 0.55 : 1;
                IsHitTestVisible = !value;
            }
        }
    }

    // ===================================================================================== buttons

    internal enum Look { Primary, Update, Secondary, Danger, Done, Disabled }

    /// <summary>
    /// A rounded button with an optional icon. Primary buttons use the accent gradient, glow and get a light sweep on hover;
    /// the Update look sweeps on its own every few seconds to draw attention.
    /// </summary>
    internal sealed class FancyButton : Grid
    {
        private readonly Border glow, body, hover;
        private readonly Rectangle shine;
        private readonly TranslateTransform shineMove = new TranslateTransform(-200, 0);
        private readonly StackPanel content;
        private readonly TextBlock label;
        private readonly Border iconHost;
        private readonly RotateTransform iconTurn = new RotateTransform(0);
        private readonly double radius, iconSize;
        private readonly object shimmer, pulse;
        private Look look = (Look)(-1);
        private Geometry icon;
        private bool spinning;

        public Action Click;

        public FancyButton(double height, double radius, double fontSize, double iconSize = 17)
        {
            this.radius = radius;
            this.iconSize = iconSize;
            Height = height;

            glow = new Border { Margin = new Thickness(-18, -14, -18, -20), Opacity = 0, IsHitTestVisible = false };
            Children.Add(glow);

            body = new Border { CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1) };
            var inner = new Grid();
            hover = new Border { CornerRadius = new CornerRadius(radius), Opacity = 0, IsHitTestVisible = false };
            inner.Children.Add(hover);

            var shineBrush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            shineBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
            shineBrush.GradientStops.Add(new GradientStop(Color.FromArgb(90, 255, 255, 255), 0.5));
            shineBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            shineBrush.Freeze();
            var tg = new TransformGroup();
            tg.Children.Add(new SkewTransform(-22, 0));
            tg.Children.Add(shineMove);
            shine = new Rectangle { Width = 70, Fill = shineBrush, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = tg, IsHitTestVisible = false };
            inner.Children.Add(shine);

            content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) };
            iconHost = new Border { Width = iconSize, Height = iconSize, Margin = new Thickness(0, 0, 9, 0), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = iconTurn, Visibility = Visibility.Collapsed };
            label = Ui.Text("", fontSize, Theme.Txt, FontWeights.SemiBold);
            content.Children.Add(iconHost);
            content.Children.Add(label);
            inner.Children.Add(content);
            body.Child = inner;
            Children.Add(body);

            SizeChanged += delegate
            {
                body.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), radius, radius);
            };

            MouseEnter += delegate
            {
                if (!IsEnabled) return;
                Anim.To(hover, OpacityProperty, 1, 150);
                if (look == Look.Update) return;   // its glow breathes on its own
                Anim.To(glow, OpacityProperty, GlowHover(), 220);
                if (look == Look.Primary || look == Look.Danger) Sweep();
            };
            MouseLeave += delegate
            {
                Anim.To(hover, OpacityProperty, 0, 260);
                if (look != Look.Update) Anim.To(glow, OpacityProperty, GlowRest(), 320);
            };
            IsEnabledChanged += delegate { Cursor = IsEnabled ? Cursors.Hand : Cursors.Arrow; };

            // the Update look: a light sweep every few seconds and a breathing glow
            shimmer = Anim.Forever(shineMove, TranslateTransform.XProperty, () => Anim.Repeat(-120, 520, 1100, 2300, Anim.InOut), -200, false);
            pulse = Anim.Forever(glow, OpacityProperty, () => Anim.Wave(0.35, 0.85, 1400), 0.6, false);

            Ui.Clickable(this, delegate { if (Click != null) Click(); }, 0.97);
        }

        public string Text
        {
            get { return label.Text; }
            set
            {
                if (label.Text == value) return;
                label.Text = value;
                if (IsLoaded) Anim.FromTo(content, OpacityProperty, 0.25, 1, 260);
            }
        }

        public Look Look { get { return look; } }

        private double GlowRest() { return look == Look.Update ? 0.6 : (look == Look.Primary ? 0.32 : 0); }
        private double GlowHover() { return look == Look.Primary || look == Look.Update ? 0.95 : (look == Look.Danger ? 0.7 : 0); }

        private void Sweep()
        {
            if (!Anim.On || look == Look.Update) return;
            Anim.FromTo(shineMove, TranslateTransform.XProperty, -120, ActualWidth + 60, 650, Anim.InOut);
        }

        public void SetLook(Look l, Geometry newIcon)
        {
            if (newIcon != icon)
            {
                icon = newIcon;
                iconHost.Child = null;
                iconHost.Visibility = newIcon == null ? Visibility.Collapsed : Visibility.Visible;
            }

            bool changed = l != look;
            look = l;

            Brush bg, border, hov, text, glowBrush = null;
            switch (l)
            {
                case Look.Primary:
                case Look.Update:
                    bg = Theme.Accent;
                    border = Theme.B(Color.FromArgb(50, 255, 255, 255));
                    hov = Theme.B(Color.FromArgb(34, 255, 255, 255));
                    text = Theme.White;
                    glowBrush = Theme.Glow(Theme.PinkC, 120);
                    break;
                case Look.Danger:
                    bg = Theme.B(Theme.A(20, Theme.RedC));
                    border = Theme.B(Theme.A(130, Theme.RedC));
                    hov = Theme.B(Theme.A(46, Theme.RedC));
                    text = Theme.Red;
                    glowBrush = Theme.Glow(Theme.RedC, 70);
                    break;
                case Look.Done:
                    bg = Theme.B(Theme.A(30, Theme.MintC));
                    border = Theme.B(Theme.A(110, Theme.MintC));
                    hov = Theme.Clear;
                    text = Theme.Mint;
                    break;
                case Look.Disabled:
                    bg = Theme.B(Color.FromArgb(90, 40, 34, 52));
                    border = Theme.Stroke;
                    hov = Theme.Clear;
                    text = Theme.Dim;
                    break;
                default:   // Secondary
                    bg = Theme.Surface2;
                    border = Theme.Stroke;
                    hov = Theme.B(Color.FromArgb(22, 255, 255, 255));
                    text = Theme.Txt;
                    break;
            }

            body.Background = bg;
            body.BorderBrush = border;
            hover.Background = hov;
            label.Foreground = text;
            glow.Background = glowBrush;
            if (icon != null && (iconHost.Child == null || changed)) iconHost.Child = Icons.Make(icon, iconSize, text, 2.0);

            IsEnabled = l != Look.Done && l != Look.Disabled;
            Anim.Want(shimmer, l == Look.Update);
            Anim.Want(pulse, l == Look.Update);
            if (l != Look.Update)
            {
                bool over = IsMouseOver && IsEnabled;
                Anim.To(glow, OpacityProperty, over ? GlowHover() : GlowRest(), changed ? 300 : 0);
                Anim.To(hover, OpacityProperty, over ? 1 : 0, 0);
                if (changed) shineMove.BeginAnimation(TranslateTransform.XProperty, null);
                if (changed) shineMove.X = -200;
            }
        }

        /// <summary>Turns the icon around while something is in progress (Check for updates).</summary>
        public void Spin(bool on)
        {
            if (on == spinning) return;
            spinning = on;
            if (on && Anim.On)
                iconTurn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { RepeatBehavior = RepeatBehavior.Forever });
            else
            {
                iconTurn.BeginAnimation(RotateTransform.AngleProperty, null);
                iconTurn.Angle = 0;
            }
        }
    }

    // ===================================================================================== scrolling

    /// <summary>A scrolling area with smooth wheel scrolling and a thin bar that shows up while you scroll or hover.</summary>
    internal sealed class Scroller : Grid
    {
        public readonly ScrollViewer View;
        private readonly Border track, thumb;
        private readonly SolidColorBrush thumbBrush = Theme.Live(Color.FromArgb(70, 255, 255, 255));
        private readonly Stopwatch clock = new Stopwatch();
        private readonly DispatcherTimer hideTimer;
        private double target, lastTick;
        private bool animating;

        public Scroller(UIElement content, double barOffset)
        {
            View = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Content = content
            };
            Children.Add(View);

            track = new Border
            {
                Width = 10,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, -barOffset, 4),
                Opacity = 0,
                Visibility = Visibility.Collapsed
            };
            thumb = new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = thumbBrush, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand };
            track.Child = thumb;
            Children.Add(track);

            hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            hideTimer.Tick += delegate
            {
                hideTimer.Stop();
                if (!IsMouseOver && !thumb.IsMouseCaptured) Anim.To(track, OpacityProperty, 0, 400);
            };

            PreviewMouseWheel += OnWheel;
            View.ScrollChanged += delegate { UpdateThumb(); };
            SizeChanged += delegate { UpdateThumb(); };
            MouseEnter += delegate { if (track.Visibility == Visibility.Visible) Anim.To(track, OpacityProperty, 1, 160); };
            MouseLeave += delegate { if (!thumb.IsMouseCaptured) Anim.To(track, OpacityProperty, 0, 400); };

            thumb.MouseEnter += delegate { Anim.Color(thumbBrush, Color.FromArgb(150, 255, 255, 255), 120); thumb.Width = 6; };
            thumb.MouseLeave += delegate { if (!thumb.IsMouseCaptured) { Anim.Color(thumbBrush, Color.FromArgb(70, 255, 255, 255), 250); thumb.Width = 4; } };

            double dragStartY = 0, dragStartOffset = 0;
            thumb.MouseLeftButtonDown += (s, e) =>
            {
                StopAnimation();
                thumb.CaptureMouse();
                dragStartY = e.GetPosition(track).Y;
                dragStartOffset = View.VerticalOffset;
                e.Handled = true;
            };
            thumb.MouseMove += (s, e) =>
            {
                if (!thumb.IsMouseCaptured) return;
                double room = track.ActualHeight - thumb.ActualHeight;
                if (room <= 0) return;
                double dy = e.GetPosition(track).Y - dragStartY;
                View.ScrollToVerticalOffset(dragStartOffset + dy * View.ScrollableHeight / room);
            };
            thumb.MouseLeftButtonUp += (s, e) =>
            {
                thumb.ReleaseMouseCapture();
                if (!thumb.IsMouseOver) { Anim.Color(thumbBrush, Color.FromArgb(70, 255, 255, 255), 250); thumb.Width = 4; }
            };
            track.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == thumb) return;
                double y = e.GetPosition(track).Y;
                ScrollTo(View.VerticalOffset + (y < thumb.Margin.Top ? -1 : 1) * View.ViewportHeight * 0.9);
                e.Handled = true;
            };
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if (View.ScrollableHeight <= 0) return;
            e.Handled = true;
            double from = animating ? target : View.VerticalOffset;
            ScrollTo(from - e.Delta * 0.75);
        }

        public void ScrollTo(double offset)
        {
            target = Math.Max(0, Math.Min(View.ScrollableHeight, offset));
            if (!Anim.On)
            {
                StopAnimation();
                View.ScrollToVerticalOffset(target);
                return;
            }
            if (animating) return;
            animating = true;
            clock.Restart();
            lastTick = 0;
            CompositionTarget.Rendering += Step;
        }

        public void ScrollToTop()
        {
            StopAnimation();
            View.ScrollToVerticalOffset(0);
        }

        /// <summary>Scrolls just enough to show the element (used after a version tab opens).</summary>
        public void Reveal(FrameworkElement el)
        {
            if (!el.IsVisible || !(View.Content is Visual)) return;
            try
            {
                Point p = el.TranslatePoint(new Point(0, 0), (UIElement)View.Content);
                double top = p.Y, bottom = p.Y + el.ActualHeight;
                double off = animating ? target : View.VerticalOffset;
                double view = View.ViewportHeight;
                if (bottom > off + view) off = bottom - view + 2;
                if (top < off) off = top;
                if (Math.Abs(off - View.VerticalOffset) > 0.5) ScrollTo(off);
            }
            catch (InvalidOperationException)
            {
                // not in the same tree any more
            }
        }

        private void Step(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.05, Math.Max(0.001, now - lastTick));
            lastTick = now;
            double cur = View.VerticalOffset;
            double k = 1 - Math.Pow(0.76, dt * 60);
            double next = cur + (target - cur) * k;
            if (Math.Abs(target - next) < 0.4)
            {
                next = target;
                StopAnimation();
            }
            View.ScrollToVerticalOffset(next);
        }

        private void StopAnimation()
        {
            if (!animating) return;
            animating = false;
            CompositionTarget.Rendering -= Step;
        }

        private void UpdateThumb()
        {
            double extent = View.ExtentHeight, view = View.ViewportHeight;
            if (view <= 0 || extent <= view + 0.5)
            {
                track.Visibility = Visibility.Collapsed;
                return;
            }
            bool wasHidden = track.Visibility != Visibility.Visible;
            track.Visibility = Visibility.Visible;
            double trackHeight = track.ActualHeight > 0 ? track.ActualHeight : view - 8;
            double thumbHeight = Math.Max(34, trackHeight * view / extent);
            double room = Math.Max(0, trackHeight - thumbHeight);
            thumb.Height = thumbHeight;
            thumb.Margin = new Thickness(0, room * View.VerticalOffset / (extent - view), 0, 0);

            if (!wasHidden || IsMouseOver)
            {
                Anim.To(track, OpacityProperty, 1, 140);
                hideTimer.Stop();
                hideTimer.Start();
            }
        }
    }
}
