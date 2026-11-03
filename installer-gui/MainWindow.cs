using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OsuLazerFix
{
    /// <summary>One patch: a game version + an edition. Shown inside the tab of its game version.</summary>
    internal sealed class PatchItem
    {
        public string Version;
        public string Edition;
        public bool Enabled;   // false = the patch is for another game version than the installed one
        public bool Download;  // the files come from the internet and are not downloaded yet

        public string Key { get { return Version + "|" + Edition; } }
        public string Title { get { return Loc.T(Edition == Patcher.EditionOffline ? "ed.offline" : "ed.online"); } }
        public string Sub { get { return Edition == Patcher.EditionOffline ? "2000 FPS" : "1000 FPS"; } }
    }

    /// <summary>
    /// The window, built in code (no XAML): dark glass cards over an animated web (Plexus), an osu! pink to violet accent,
    /// mint green for "installed". Every change is animated unless Settings - Animations is off.
    /// </summary>
    internal sealed class MainWindow : Window
    {
        private const string GitHubUrl = "https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix";
        private const string ReleasesUrl = GitHubUrl + "/releases";
        private const double Pad = 22;   // page margin

        private enum Screen { Main, Settings, About }
        private enum Kind { Info, Success, Warning, Error }

        private sealed class CardView
        {
            public PatchItem Item;
            public Border Root, SelRing, RadioFill;
            public SolidColorBrush Bg, Stroke, Ring;
            public ScaleTransform RadioScale;
            public Path RadioCheck;
            public TextBlock Title, Sub;
            public bool Hover, Filled;
        }

        private sealed class GroupView
        {
            public string Version;
            public bool Open;
            public StackPanel Wrap, Body;
            public Border BodyHost;
            public TranslateTransform BodyMove;
            public RotateTransform Chevron;
            public readonly List<CardView> Cards = new List<CardView>();
        }

        // ---- window ----
        private Border frame;
        private Plexus plexus;
        private Grid mainPage, settingsPage, aboutPage;
        private Screen screen = Screen.Main;
        private int pageSeq;

        // ---- main page ----
        private TextBlock txtTitle, lblGame, txtGameVersion, txtGameNote, txtStatus, txtPlaceholder, txtBanner, txtListEmpty;
        private Border statusPill, bannerBorder, searchGlow, searchClear;
        private SolidColorBrush pillBg, pillStroke, pillColor, searchStroke, searchIconBrush;
        private GradientStop heroGlow0, heroGlow1;
        private readonly List<object> ripple = new List<object>();
        private FancyButton bannerBtn, btnInstall, btnRemove;
        private TextBox txtSearch;
        private StackPanel listPanel;
        private Scroller listScroller;
        private readonly List<GroupView> groups = new List<GroupView>();

        // ---- settings page ----
        private TextBlock txtSettingsTitle, lblSecGame, lblSecApp, lblDual, lblDualSub, lblLatency, lblLatencySub, lblFolder, txtPath;
        private TextBlock lblLang, lblLangSub, txtLangValue, lblAuto, lblAutoSub, lblAnim, lblAnimSub, lblDownloads, lblDownloadsSub, lblCheck, lblCheckSub, badgeDual, badgeLatency;
        private Switch swDual, swLatency, swAuto, swAnim;
        private FancyButton btnBrowse, btnDeleteDownloads, btnCheck;
        private Scroller settingsScroller;
        private Popup langPopup;
        private Border langCard;
        private TranslateTransform langCardMove;
        private RotateTransform langChevron;
        private readonly List<Path> langChecks = new List<Path>();
        private int langClosedTick;

        // ---- about page ----
        private TextBlock txtAboutTitle, txtAboutDesc, txtAboutNote;

        // ---- dialog ----
        private Grid overlay;
        private Border dlgBackdrop, dlgCard, dlgIconTile, dlgProgress;
        private StackPanel dlgButtons;
        private ScaleTransform dlgScale, progressScale;
        private TranslateTransform dlgMove;
        private TextBlock dlgTitle, dlgText;
        private FancyButton btnDlgOk, btnDlgCancel;
        private Scroller dlgScroller;
        private Action dlgOnOk;
        private bool overlayOpen;
        private int overlaySeq;

        // ---- toast ----
        private Border toast;
        private TextBlock toastText;
        private TranslateTransform toastMove;
        private DispatcherTimer toastTimer;
        private DispatcherTimer idleTimer;   // keeps the animation running for a moment after the window loses focus
        private int toastSeq;

        // ---- state ----
        private string currentPath = "";
        private GameInfo game;
        private PatchSet patch;
        private InstallState state = InstallState.NoGame;
        private string selectedKey = "";
        private bool installedCurrent = true;   // the installed fix is the newest build of its edition
        private bool checking;                  // the online list is being downloaded
        private bool manualCheck;               // ... because the user pressed Check
        private bool updating;                  // a new version of the program is being downloaded
        private AppRelease appUpdate;           // a newer version of the program, if the online list has one
        private bool appStaged;                 // the new exe is already in place, a restart starts it
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly List<PatchItem> items = new List<PatchItem>();
        private readonly List<string> opLog = new List<string>();

        public MainWindow()
        {
            Settings.Load();
            Anim.On = Settings.Animations;

            // the online list saved last time, so the patches are there even without internet
            Manifest cached = Online.LoadCached();
            if (cached != null) Payload.SetOnline(cached.Patches);

            BuildUi();

            if (!string.IsNullOrEmpty(Settings.Folder) && Patcher.Resolve(Settings.Folder) != null)
                currentPath = Settings.Folder;
            else if (Patcher.Resolve(Patcher.DefaultRoot()) != null)
                currentPath = Patcher.DefaultRoot();

            ApplyLanguage();
            UpdateSwitches(false);
            UpdateStatus();

            Loaded += delegate
            {
                UpdateLively();
                Enter();
#if PREVIEW
                Preview();
#endif
            };
            // in the background (for example while osu! is running) the animation pauses after a few seconds,
            // so the program takes no CPU or GPU time from the game; it wakes up on activation or when the mouse moves over it
            idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            idleTimer.Tick += delegate { idleTimer.Stop(); UpdateLively(); };
            Activated += delegate { idleTimer.Stop(); UpdateLively(); };
            Deactivated += delegate { idleTimer.Stop(); idleTimer.Start(); UpdateLively(); };
            StateChanged += delegate { UpdateLively(); };

            // always refresh the list of patches; the program itself updates on its own only if Auto-update is on
            CheckForUpdates(false);
        }

        // =====================================================================  window

        private void BuildUi()
        {
            Title = "osu!lazer Fix by iwili";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 480;
            Height = 756;
            UseLayoutRounding = true;
            LoadWindowIcon();
            if (Anim.On) Opacity = 0;

            var outer = new Grid();
            // the shadow is its own cached element, so the animated content never has to go through the shadow effect
            outer.Children.Add(new Border
            {
                Margin = new Thickness(20),
                Background = Theme.B(Theme.BgBottomC),
                Effect = new DropShadowEffect { BlurRadius = 34, ShadowDepth = 10, Direction = 270, Opacity = 0.62, Color = Colors.Black },
                CacheMode = new BitmapCache()
            });

            frame = new Border
            {
                Margin = new Thickness(20),
                Background = Theme.WindowBg,
                BorderBrush = Theme.B(Color.FromArgb(34, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };
            outer.Children.Add(frame);
            Content = outer;

            var root = new Grid();
            frame.Child = root;

            plexus = new Plexus();
            root.Children.Add(plexus);

            mainPage = BuildMainPage();
            settingsPage = BuildSettingsPage();
            aboutPage = BuildAboutPage();
            foreach (Grid page in new[] { mainPage, settingsPage, aboutPage })
            {
                page.RenderTransform = new TranslateTransform();
                root.Children.Add(page);
            }
            settingsPage.Visibility = Visibility.Collapsed;
            aboutPage.Visibility = Visibility.Collapsed;

            root.Children.Add(BuildToast());
            overlay = BuildOverlay();
            root.Children.Add(overlay);

            // the web reacts to the mouse
            frame.MouseMove += delegate (object s, MouseEventArgs e)
            {
                plexus.SetMouse(e.GetPosition(plexus), true);
                if (!IsActive && Anim.On)
                {
                    idleTimer.Stop();
                    idleTimer.Start();
                    if (!Anim.Lively) UpdateLively();
                }
            };
            frame.MouseLeave += (s, e) => plexus.SetMouse(e.GetPosition(plexus), false);

            // drag the window by any empty place
            frame.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.Handled) return;
                try { DragMove(); } catch (InvalidOperationException) { }
            };

            // a click outside the search box takes the focus away from it
            PreviewMouseDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (txtSearch.IsKeyboardFocusWithin && !Ui.IsInside(e.OriginalSource as DependencyObject, txtSearch))
                    Keyboard.ClearFocus();
            };
            PreviewKeyDown += OnKey;
        }

        private void LoadWindowIcon()
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                {
                    if (stream != null)
                        Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
            }
            catch (Exception)
            {
                // the icon is optional
            }
        }

        /// <summary>Endless effects and the moving background run only while animations are on and the window is not minimised.</summary>
        private void UpdateLively()
        {
            Anim.SetLively(Anim.On && IsLoaded && WindowState != WindowState.Minimized && (IsActive || (idleTimer != null && idleTimer.IsEnabled)));
            plexus.Active = IsActive;
            plexus.Refresh();
        }

        /// <summary>The window fades in and the main screen rises into place, part by part.</summary>
        private void Enter()
        {
            if (!Anim.On)
            {
                Opacity = 1;
                return;
            }
            Anim.To(this, OpacityProperty, 1, 260);
            int i = 0;
            foreach (UIElement el in mainPage.Children)
            {
                var move = new TranslateTransform(0, 18);
                el.RenderTransform = move;
                double delay = 80 + i * 55;
                Anim.FromTo(el, OpacityProperty, 0, 1, 460, Anim.Out, delay);
                Anim.FromTo(move, TranslateTransform.YProperty, 18, 0, 700, Anim.Soft, delay);
                i++;
            }
        }

        private void CloseWindow()
        {
            if (!Anim.On)
            {
                Close();
                return;
            }
            IsHitTestVisible = false;
            Anim.To(this, OpacityProperty, 0, 150, Anim.In, 0, Close);
        }

        private StackPanel WindowButtons()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var minimize = Ui.IconButton(Icons.Minimize, delegate { WindowState = WindowState.Minimized; });
            minimize.Margin = new Thickness(0, 0, 4, 0);
            panel.Children.Add(minimize);
            panel.Children.Add(Ui.IconButton(Icons.Close, CloseWindow, true));
            return panel;
        }

        private Grid PageHeader(out TextBlock title)
        {
            var h = new Grid { Margin = new Thickness(Pad - 8, 18, Pad - 6, 0), Height = 40 };
            h.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            h.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            h.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var back = Ui.IconButton(Icons.Back, delegate { ShowPage(Screen.Main); });
            h.Children.Add(back);
            title = Ui.Text("", 18, Theme.Txt, FontWeights.SemiBold);
            title.Margin = new Thickness(8, 0, 12, 0);
            Grid.SetColumn(title, 1);
            h.Children.Add(title);
            var buttons = WindowButtons();
            Grid.SetColumn(buttons, 2);
            h.Children.Add(buttons);
            return h;
        }

        private Grid PageOf(Screen s)
        {
            return s == Screen.Settings ? settingsPage : (s == Screen.About ? aboutPage : mainPage);
        }

        /// <summary>Slides the current screen out and the new one in (from the right going deeper, from the left going back).</summary>
        private void ShowPage(Screen to)
        {
            if (to == screen) return;
            if (langPopup != null) langPopup.IsOpen = false;
            Grid from = PageOf(screen), next = PageOf(to);
            bool forward = to != Screen.Main;
            screen = to;
            int seq = ++pageSeq;

            if (to == Screen.Settings)
            {
                UpdateDownloadsRow();
                UpdateSwitches(false);
                settingsScroller.ScrollToTop();
            }

            from.IsHitTestVisible = false;
            Anim.To(from, OpacityProperty, 0, 150, Anim.In);
            Anim.To((TranslateTransform)from.RenderTransform, TranslateTransform.XProperty, forward ? -30 : 30, 170, Anim.In, 0, delegate
            {
                if (seq == pageSeq) from.Visibility = Visibility.Collapsed;
            });

            next.Visibility = Visibility.Visible;
            next.IsHitTestVisible = true;
            Anim.FromTo(next, OpacityProperty, 0, 1, 260, Anim.Out, 60);
            Anim.FromTo((TranslateTransform)next.RenderTransform, TranslateTransform.XProperty, forward ? 40 : -40, 0, 420, Anim.Soft, 60);
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (overlayOpen)
            {
                bool busy = dlgProgress.Visibility == Visibility.Visible;
                if (e.Key == Key.Escape && !busy)
                {
                    DialogCancel();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && !busy && btnDlgOk.Visibility == Visibility.Visible)
                {
                    DialogOk();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (screen != Screen.Main) ShowPage(Screen.Main);
                else if (!string.IsNullOrEmpty(txtSearch.Text)) txtSearch.Text = "";
                e.Handled = true;
            }
        }

        /// <summary>Clips an element to a rounded rectangle (Border alone does not clip its content to the corners).</summary>
        private static void RoundClip(FrameworkElement el, double radius)
        {
            el.SizeChanged += delegate
            {
                el.Clip = new RectangleGeometry(new Rect(0, 0, el.ActualWidth, el.ActualHeight), radius, radius);
            };
        }

        private static Border Pill(string text, Color c)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2, 7, 2.5),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = Theme.B(Theme.A(30, c)),
                BorderBrush = Theme.B(Theme.A(80, c)),
                BorderThickness = new Thickness(1),
                Child = Ui.Text(text, 10.5, Theme.B(c), FontWeights.SemiBold)
            };
        }

        // =====================================================================  main page

        private Grid BuildMainPage()
        {
            var g = new Grid();
            for (int i = 0; i < 6; i++)
                g.RowDefinitions.Add(new RowDefinition { Height = i == 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            // ---- row 0: logo, title, buttons
            var header = new Grid { Margin = new Thickness(Pad, 18, Pad - 6, 0), Height = 40 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var logo = Ui.Logo(36);
            logo.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(logo);

            var titles = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            txtTitle = Ui.Text("", 16, Theme.Txt, FontWeights.SemiBold);
            var by = Ui.Text("by iwili", 11.5, Theme.Sub, FontWeights.Normal);
            by.Margin = new Thickness(0, 0, 0, 0);
            titles.Children.Add(txtTitle);
            titles.Children.Add(by);
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var gear = Ui.IconButton(Icons.Gear, delegate { ShowPage(Screen.Settings); }, false, true);
            gear.Margin = new Thickness(0, 0, 4, 0);
            buttons.Children.Add(gear);
            var about = Ui.IconButton(Icons.Info, delegate { ShowPage(Screen.About); });
            buttons.Children.Add(about);
            buttons.Children.Add(new Border { Width = 1, Height = 18, Background = Theme.StrokeHi, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            buttons.Children.Add(WindowButtons());
            Grid.SetColumn(buttons, 2);
            header.Children.Add(buttons);
            g.Children.Add(header);

            // ---- row 1: the game card
            var hero = new Border
            {
                Margin = new Thickness(Pad, 18, Pad, 0),
                CornerRadius = new CornerRadius(20),
                Background = Theme.Surface,
                BorderBrush = Theme.Stroke,
                BorderThickness = new Thickness(1)
            };
            var heroGrid = new Grid();
            RoundClip(heroGrid, 19);

            // a soft glow in the corner, in the colour of the status
            var glowBrush = new RadialGradientBrush();
            heroGlow0 = new GradientStop(Theme.A(60, Theme.SubC), 0);
            heroGlow1 = new GradientStop(Theme.A(0, Theme.SubC), 1);
            glowBrush.GradientStops.Add(heroGlow0);
            glowBrush.GradientStops.Add(heroGlow1);
            heroGrid.Children.Add(new Ellipse
            {
                Width = 300,
                Height = 220,
                Fill = glowBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -110, -110, 0),
                IsHitTestVisible = false
            });
            // a light edge at the top of the glass
            heroGrid.Children.Add(new Border
            {
                Background = Theme.Gradient(Color.FromArgb(14, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(0, 1)),
                IsHitTestVisible = false
            });

            var heroBody = new StackPanel { Margin = new Thickness(20, 16, 20, 18) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lblGame = Ui.Text("", 11, Theme.Dim, FontWeights.SemiBold);
            top.Children.Add(lblGame);
            statusPill = BuildStatusPill();
            Grid.SetColumn(statusPill, 1);
            top.Children.Add(statusPill);
            heroBody.Children.Add(top);

            txtGameVersion = Ui.Text("-", 32, Theme.Dim, FontWeights.SemiBold);
            txtGameVersion.Margin = new Thickness(-1, 4, 0, 0);
            heroBody.Children.Add(txtGameVersion);
            txtGameNote = Ui.Text("", 11.5, Theme.Dim, FontWeights.Normal);
            txtGameNote.Margin = new Thickness(0, 2, 0, 0);
            heroBody.Children.Add(txtGameNote);
            heroGrid.Children.Add(heroBody);
            hero.Child = heroGrid;
            Grid.SetRow(hero, 1);
            g.Children.Add(hero);

            // ---- row 2: banner "a new version of the program"
            bannerBorder = new Border
            {
                Margin = new Thickness(Pad, 12, Pad, 0),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 9, 9, 9),
                Background = Theme.Gradient(Theme.A(46, Theme.PinkC), Theme.A(46, Theme.VioletC), new Point(0, 0), new Point(1, 0)),
                BorderBrush = Theme.Gradient(Theme.A(130, Theme.PinkC), Theme.A(130, Theme.VioletC), new Point(0, 0), new Point(1, 0)),
                BorderThickness = new Thickness(1),
                Visibility = Visibility.Collapsed
            };
            var bg = new Grid();
            bg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bg.Children.Add(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Theme.Accent, Child = Icons.Make(Icons.ArrowUp, 15, Theme.White, 2.2) });
            txtBanner = Ui.Text("", 13.5, Theme.Txt, FontWeights.SemiBold);
            txtBanner.Margin = new Thickness(12, 0, 10, 0);
            Grid.SetColumn(txtBanner, 1);
            bg.Children.Add(txtBanner);
            bannerBtn = new FancyButton(34, 11, 13, 15) { Click = OnBannerClick };
            Grid.SetColumn(bannerBtn, 2);
            bg.Children.Add(bannerBtn);
            bannerBorder.Child = bg;
            Grid.SetRow(bannerBorder, 2);
            g.Children.Add(bannerBorder);

            // ---- row 3: search
            var search = BuildSearch();
            Grid.SetRow(search, 3);
            g.Children.Add(search);

            // ---- row 4: the version tabs
            listPanel = new StackPanel();
            txtListEmpty = Ui.Wrapped("", 13, Theme.Dim);
            txtListEmpty.TextAlignment = TextAlignment.Center;
            txtListEmpty.Margin = new Thickness(20, 34, 20, 0);
            txtListEmpty.Visibility = Visibility.Collapsed;
            var listContent = new StackPanel();
            listContent.Children.Add(listPanel);
            listContent.Children.Add(txtListEmpty);
            listScroller = new Scroller(listContent, 15) { Margin = new Thickness(Pad, 14, Pad, 0) };
            Grid.SetRow(listScroller, 4);
            g.Children.Add(listScroller);

            // ---- row 5: install / remove
            var actions = new Grid { Margin = new Thickness(Pad, 10, Pad, 22) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) });
            btnInstall = new FancyButton(54, 16, 16, 18) { Click = OnInstall };
            btnRemove = new FancyButton(54, 16, 14.5, 16) { Click = OnRemove, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(btnRemove, 1);
            actions.Children.Add(btnInstall);
            actions.Children.Add(btnRemove);
            Grid.SetRow(actions, 5);
            g.Children.Add(actions);

            return g;
        }

        /// <summary>The coloured status label with a pulsing dot while the fix is installed.</summary>
        private Border BuildStatusPill()
        {
            pillBg = Theme.Live(Theme.A(26, Theme.SubC));
            pillStroke = Theme.Live(Theme.A(70, Theme.SubC));
            pillColor = Theme.Live(Theme.SubC);

            var dot = new Grid { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center };
            var rippleScale = new ScaleTransform(1, 1);
            var rippleDot = new Ellipse { Width = 8, Height = 8, Fill = pillColor, Opacity = 0, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = rippleScale };
            dot.Children.Add(rippleDot);
            dot.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = pillColor });
            ripple.Add(Anim.Forever(rippleScale, ScaleTransform.ScaleXProperty, () => Anim.Repeat(1, 3, 1700, 500, Anim.Out), 1, false));
            ripple.Add(Anim.Forever(rippleScale, ScaleTransform.ScaleYProperty, () => Anim.Repeat(1, 3, 1700, 500, Anim.Out), 1, false));
            ripple.Add(Anim.Forever(rippleDot, OpacityProperty, () => Anim.Repeat(0.75, 0, 1700, 500, Anim.Out), 0, false));

            txtStatus = Ui.Text("", 12, pillColor, FontWeights.SemiBold);
            txtStatus.Margin = new Thickness(7, 0, 0, 0);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(dot);
            sp.Children.Add(txtStatus);
            return new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(9, 4, 11, 4.5),
                Background = pillBg,
                BorderBrush = pillStroke,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, -2, -4, 0),
                Child = sp
            };
        }

        private Grid BuildSearch()
        {
            var host = new Grid { Margin = new Thickness(Pad, 14, Pad, 0) };
            searchGlow = new Border { Margin = new Thickness(-10, -12, -10, -12), Background = Theme.Glow(Theme.PinkC, 46), Opacity = 0, IsHitTestVisible = false };
            host.Children.Add(searchGlow);

            searchStroke = Theme.Live(Theme.StrokeC);
            searchIconBrush = Theme.Live(Theme.DimC);
            var box = new Border { Height = 46, CornerRadius = new CornerRadius(14), Background = Theme.Field, BorderBrush = searchStroke, BorderThickness = new Thickness(1) };
            var sg = new Grid();
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sg.Children.Add(Icons.Make(Icons.Search, 17, searchIconBrush, 1.9));

            txtSearch = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Theme.Txt,
                CaretBrush = Theme.Pink,
                SelectionBrush = Theme.Pink,
                FontSize = 14,
                FontFamily = Theme.Sans,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0),
                FocusVisualStyle = null
            };
            txtSearch.TextChanged += delegate
            {
                bool empty = string.IsNullOrEmpty(txtSearch.Text);
                txtPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                searchClear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
                RebuildList();
                listScroller.ScrollToTop();
            };
            txtSearch.GotKeyboardFocus += delegate
            {
                Anim.Color(searchStroke, Theme.A(150, Theme.PinkC), 200);
                Anim.Color(searchIconBrush, Theme.PinkSoftC, 200);
                Anim.To(searchGlow, OpacityProperty, 1, 260);
            };
            txtSearch.LostKeyboardFocus += delegate
            {
                Anim.Color(searchStroke, Theme.StrokeC, 260);
                Anim.Color(searchIconBrush, Theme.DimC, 260);
                Anim.To(searchGlow, OpacityProperty, 0, 300);
            };
            Grid.SetColumn(txtSearch, 1);
            sg.Children.Add(txtSearch);

            txtPlaceholder = Ui.Text("", 14, Theme.Dim, FontWeights.Normal);
            txtPlaceholder.IsHitTestVisible = false;
            Grid.SetColumn(txtPlaceholder, 1);
            sg.Children.Add(txtPlaceholder);

            var clearBg = Theme.Live(Theme.Transparent);
            searchClear = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(0, 0, 9, 0),
                Background = clearBg,
                Visibility = Visibility.Collapsed,
                Child = Icons.Make(Icons.Close, 13, Theme.Sub, 2.2)
            };
            searchClear.MouseEnter += delegate { Anim.Color(clearBg, Color.FromArgb(24, 255, 255, 255), 120); };
            searchClear.MouseLeave += delegate { Anim.Color(clearBg, Theme.Transparent, 200); };
            Ui.Clickable(searchClear, delegate { txtSearch.Text = ""; }, 0.88);
            Grid.SetColumn(searchClear, 2);
            sg.Children.Add(searchClear);

            box.Child = sg;
            host.Children.Add(box);
            return host;
        }

        // =====================================================================  settings page

        private Grid BuildSettingsPage()
        {
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(PageHeader(out txtSettingsTitle));

            var body = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };

            // ---- the game
            lblSecGame = SectionLabel();
            body.Children.Add(lblSecGame);
            StackPanel gameRows;
            body.Children.Add(SectionCard(out gameRows));

            swDual = new Switch { Toggle = ToggleDualMonitor };
            gameRows.Children.Add(SettingRow(Icons.Monitors, out lblDual, out lblDualSub, swDual, Ui.Badge(out badgeDual)));
            gameRows.Children.Add(Divider());
            swLatency = new Switch { Toggle = ToggleLatency };
            gameRows.Children.Add(SettingRow(Icons.Bolt, out lblLatency, out lblLatencySub, swLatency, Ui.Badge(out badgeLatency)));
            gameRows.Children.Add(Divider());
            btnBrowse = SmallButton(Look.Secondary, Icons.Folder, BrowseFolder);
            gameRows.Children.Add(SettingRow(Icons.Folder, out lblFolder, out txtPath, btnBrowse, null));
            txtPath.FontFamily = Theme.Mono;
            txtPath.FontSize = 11;
            txtPath.TextWrapping = TextWrapping.NoWrap;
            txtPath.TextTrimming = TextTrimming.CharacterEllipsis;

            // ---- the program
            lblSecApp = SectionLabel();
            body.Children.Add(lblSecApp);
            StackPanel appRows;
            body.Children.Add(SectionCard(out appRows));

            appRows.Children.Add(SettingRow(Icons.Globe, out lblLang, out lblLangSub, BuildLanguageChip(), null));
            lblLangSub.Visibility = Visibility.Collapsed;
            appRows.Children.Add(Divider());
            swAuto = new Switch { Toggle = ToggleAutoUpdate };
            appRows.Children.Add(SettingRow(Icons.UpCircle, out lblAuto, out lblAutoSub, swAuto, null));
            appRows.Children.Add(Divider());
            swAnim = new Switch { Toggle = ToggleAnimations };
            appRows.Children.Add(SettingRow(Icons.Sparkles, out lblAnim, out lblAnimSub, swAnim, null));
            appRows.Children.Add(Divider());
            btnDeleteDownloads = SmallButton(Look.Danger, Icons.Trash, OnDeleteDownloads);
            appRows.Children.Add(SettingRow(Icons.Archive, out lblDownloads, out lblDownloadsSub, btnDeleteDownloads, null));
            appRows.Children.Add(Divider());
            btnCheck = SmallButton(Look.Secondary, Icons.Refresh, delegate { CheckForUpdates(true); });
            appRows.Children.Add(SettingRow(Icons.Refresh, out lblCheck, out lblCheckSub, btnCheck, null));

            settingsScroller = new Scroller(body, 15) { Margin = new Thickness(Pad, 8, Pad, 0) };
            Grid.SetRow(settingsScroller, 1);
            g.Children.Add(settingsScroller);
            return g;
        }

        private static TextBlock SectionLabel()
        {
            TextBlock t = Ui.Text("", 11, Theme.Dim, FontWeights.SemiBold);
            t.Margin = new Thickness(6, 14, 0, 8);
            return t;
        }

        private static Border SectionCard(out StackPanel rows)
        {
            rows = new StackPanel();
            var card = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = Theme.Surface,
                BorderBrush = Theme.Stroke,
                BorderThickness = new Thickness(1),
                Child = rows
            };
            RoundClip(rows, 17);
            return card;
        }

        private static Border Divider()
        {
            return new Border { Height = 1, Background = Theme.Divider, Margin = new Thickness(64, 0, 0, 0) };
        }

        private static FancyButton SmallButton(Look look, Geometry icon, Action click)
        {
            var b = new FancyButton(34, 10, 12.5, 14) { Click = click, HorizontalAlignment = HorizontalAlignment.Right };
            b.SetLook(look, icon);
            return b;
        }

        private static Border SettingRow(Geometry icon, out TextBlock title, out TextBlock sub, FrameworkElement control, Border badge)
        {
            var bg = Theme.Live(Theme.Transparent);
            var row = new Border { Background = bg, Padding = new Thickness(14, 13, 14, 13), MinHeight = 64 };
            row.MouseEnter += delegate { Anim.Color(bg, Color.FromArgb(9, 255, 255, 255), 150); };
            row.MouseLeave += delegate { Anim.Color(bg, Theme.Transparent, 260); };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(Ui.Tile(icon));

            var texts = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            title = Ui.Text("", 14.5, Theme.Txt, FontWeights.SemiBold);
            titleRow.Children.Add(title);
            if (badge != null) titleRow.Children.Add(badge);
            texts.Children.Add(titleRow);
            sub = Ui.Wrapped("", 11.5, Theme.Sub);
            sub.Margin = new Thickness(0, 3, 0, 0);
            texts.Children.Add(sub);
            Grid.SetColumn(texts, 1);
            g.Children.Add(texts);

            control.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(control, 2);
            g.Children.Add(control);
            row.Child = g;
            return row;
        }

        private Border BuildLanguageChip()
        {
            var chipBg = Theme.Live(Theme.Surface2C);
            var chip = new Border { Width = 128, Height = 34, CornerRadius = new CornerRadius(10), Background = chipBg, BorderBrush = Theme.Stroke, BorderThickness = new Thickness(1) };
            var cg = new Grid();
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            txtLangValue = Ui.Text("", 13.5, Theme.Txt, FontWeights.Normal);
            txtLangValue.Margin = new Thickness(12, 0, 0, 0);
            cg.Children.Add(txtLangValue);
            langChevron = new RotateTransform(0);
            Path chev = Icons.Make(Icons.Chevron, 14, Theme.Sub, 2.0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = langChevron;
            Grid.SetColumn(chev, 1);
            cg.Children.Add(chev);
            chip.Child = cg;
            chip.MouseEnter += delegate { Anim.Color(chipBg, Theme.Surface2HoverC, 140); };
            chip.MouseLeave += delegate { Anim.Color(chipBg, Theme.Surface2C, 220); };

            var list = new StackPanel();
            string[] names = { "English", "Русский" };
            for (int n = 0; n < names.Length; n++)
            {
                int index = n;
                var itemBg = Theme.Live(Theme.Transparent);
                var item = new Border { Height = 34, CornerRadius = new CornerRadius(8), Background = itemBg };
                var ig = new Grid();
                ig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ig.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                TextBlock name = Ui.Text(names[n], 13.5, Theme.Txt, FontWeights.Normal);
                name.Margin = new Thickness(10, 0, 0, 0);
                ig.Children.Add(name);
                Path check = Icons.Make(Icons.Check, 14, Theme.PinkSoft, 2.2);
                Grid.SetColumn(check, 1);
                ig.Children.Add(check);
                langChecks.Add(check);
                item.Child = ig;
                item.MouseEnter += delegate { Anim.Color(itemBg, Color.FromArgb(20, 255, 255, 255), 120); };
                item.MouseLeave += delegate { Anim.Color(itemBg, Theme.Transparent, 200); };
                Ui.Clickable(item, delegate
                {
                    langPopup.IsOpen = false;
                    SetLanguage(index);
                }, 0.98);
                list.Children.Add(item);
            }

            langCardMove = new TranslateTransform();
            langCard = new Border
            {
                Width = 128,
                Background = Theme.B(Color.FromArgb(250, 31, 26, 42)),
                BorderBrush = Theme.StrokeHi,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(4),
                RenderTransform = langCardMove,
                Child = list
            };
            langPopup = new Popup
            {
                PlacementTarget = chip,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 6,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = langCard
            };
            langPopup.Opened += delegate
            {
                Anim.FromTo(langCard, OpacityProperty, 0, 1, 160);
                Anim.FromTo(langCardMove, TranslateTransform.YProperty, -8, 0, 260, Anim.Soft);
                Anim.To(langChevron, RotateTransform.AngleProperty, 180, 260, Anim.Soft);
            };
            langPopup.Closed += delegate
            {
                langClosedTick = Environment.TickCount;
                Anim.To(langChevron, RotateTransform.AngleProperty, 0, 260, Anim.Soft);
            };
            Ui.Clickable(chip, delegate
            {
                if (Environment.TickCount - langClosedTick < 250) return;   // this click just closed it
                langPopup.IsOpen = true;
            }, 0.97);
            return chip;
        }

        // =====================================================================  about page

        private Grid BuildAboutPage()
        {
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(PageHeader(out txtAboutTitle));

            var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Pad, 0, Pad, 20) };

            // the logo in a slowly turning ring, with a dot going round it
            var art = new Grid { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Center };
            var glow = new Ellipse { Width = 200, Height = 200, Fill = Theme.Glow(Theme.PinkC, 80), IsHitTestVisible = false };
            art.Children.Add(glow);
            Anim.Forever(glow, OpacityProperty, () => Anim.Wave(0.55, 1, 2600), 0.8);

            var ringTurn = new RotateTransform(0);
            art.Children.Add(new Ellipse
            {
                Width = 156,
                Height = 156,
                Stroke = Theme.Accent,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 0.5, 4.5 },
                StrokeDashCap = PenLineCap.Round,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = ringTurn
            });
            Anim.Forever(ringTurn, RotateTransform.AngleProperty, () => Anim.Repeat(0, 360, 40000), 0);

            art.Children.Add(new Ellipse { Width = 128, Height = 128, Stroke = Theme.B(Color.FromArgb(26, 255, 255, 255)), StrokeThickness = 1 });

            var orbitTurn = new RotateTransform(0);
            var orbit = new Grid { Width = 156, Height = 156, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = orbitTurn, IsHitTestVisible = false };
            orbit.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = Theme.PinkSoft, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -3, 0, 0) });
            orbit.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = Theme.Violet, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -2) });
            art.Children.Add(orbit);
            Anim.Forever(orbitTurn, RotateTransform.AngleProperty, () => Anim.Repeat(0, 360, 9000), 30);

            var logoFloat = new TranslateTransform();
            FrameworkElement logo = Ui.Logo(92);
            logo.RenderTransform = logoFloat;
            art.Children.Add(logo);
            Anim.Forever(logoFloat, TranslateTransform.YProperty, () => Anim.Wave(-3, 3, 2200), 0);
            center.Children.Add(art);

            var name = Ui.Text("osu!lazer Fix", 26, Theme.Txt, FontWeights.SemiBold);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 4, 0, 0);
            center.Children.Add(name);

            txtAboutDesc = Ui.Wrapped("", 13, Theme.Sub);
            txtAboutDesc.TextAlignment = TextAlignment.Center;
            txtAboutDesc.MaxWidth = 300;
            txtAboutDesc.HorizontalAlignment = HorizontalAlignment.Center;
            txtAboutDesc.Margin = new Thickness(0, 6, 0, 0);
            center.Children.Add(txtAboutDesc);

            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            var meta = new TextBlock { FontSize = 12.5, FontFamily = Theme.Sans, VerticalAlignment = VerticalAlignment.Center };
            meta.Inlines.Add(new Run("v" + v.ToString(3)) { FontFamily = Theme.Mono, Foreground = Theme.PinkSoft });
            meta.Inlines.Add(new Run("   by iwili") { Foreground = Theme.Sub });
            center.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 5, 13, 6),
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Theme.Surface2,
                BorderBrush = Theme.Stroke,
                BorderThickness = new Thickness(1),
                Child = meta
            });

            var gh = new FancyButton(46, 14, 14.5, 17) { Click = delegate { OpenUrl(GitHubUrl); }, Width = 190, Margin = new Thickness(0, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            gh.Text = "GitHub";
            gh.SetLook(Look.Secondary, Icons.Code);
            center.Children.Add(gh);
            Grid.SetRow(center, 1);
            g.Children.Add(center);

            txtAboutNote = Ui.Text("", 12, Theme.Dim, FontWeights.Normal);
            txtAboutNote.HorizontalAlignment = HorizontalAlignment.Center;
            txtAboutNote.Margin = new Thickness(0, 0, 0, 26);
            Grid.SetRow(txtAboutNote, 2);
            g.Children.Add(txtAboutNote);
            return g;
        }

        // =====================================================================  dialog and toast

        private Grid BuildOverlay()
        {
            var g = new Grid { Visibility = Visibility.Collapsed };
            Panel.SetZIndex(g, 100);
            dlgBackdrop = new Border { Background = Theme.B(Color.FromArgb(178, 6, 4, 10)), Opacity = 0 };
            g.Children.Add(dlgBackdrop);

            dlgScale = new ScaleTransform(1, 1);
            dlgMove = new TranslateTransform();
            var tg = new TransformGroup();
            tg.Children.Add(dlgScale);
            tg.Children.Add(dlgMove);
            dlgCard = new Border
            {
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(20),
                Background = Theme.B(Color.FromArgb(248, 27, 22, 37)),
                BorderBrush = Theme.StrokeHi,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22, 20, 22, 20),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = tg,
                Opacity = 0
            };
            var sp = new StackPanel();

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dlgIconTile = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) };
            head.Children.Add(dlgIconTile);
            dlgTitle = Ui.Text("", 17, Theme.Txt, FontWeights.SemiBold);
            dlgTitle.Margin = new Thickness(13, 0, 0, 0);
            Grid.SetColumn(dlgTitle, 1);
            head.Children.Add(dlgTitle);
            sp.Children.Add(head);

            dlgText = Ui.Wrapped("", 13.5, Theme.Txt);
            dlgText.VerticalAlignment = VerticalAlignment.Top;
            dlgText.LineHeight = 20;
            dlgScroller = new Scroller(dlgText, 12) { Margin = new Thickness(0, 16, 0, 0), MaxHeight = 380 };
            sp.Children.Add(dlgScroller);

            progressScale = new ScaleTransform(0, 1);
            dlgProgress = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = Theme.B(Color.FromArgb(22, 255, 255, 255)),
                Margin = new Thickness(0, 18, 0, 4),
                Visibility = Visibility.Collapsed,
                Child = new Border { CornerRadius = new CornerRadius(3), Background = Theme.Accent, RenderTransformOrigin = new Point(0, 0.5), RenderTransform = progressScale }
            };
            sp.Children.Add(dlgProgress);

            var buttons = dlgButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            btnDlgCancel = new FancyButton(44, 13, 14, 16) { Click = DialogCancel, MinWidth = 96, Margin = new Thickness(0, 0, 10, 0) };
            btnDlgCancel.SetLook(Look.Secondary, null);
            btnDlgOk = new FancyButton(44, 13, 14, 16) { Click = DialogOk, MinWidth = 96 };
            btnDlgOk.SetLook(Look.Primary, null);
            buttons.Children.Add(btnDlgCancel);
            buttons.Children.Add(btnDlgOk);
            sp.Children.Add(buttons);

            dlgCard.Child = sp;
            g.Children.Add(dlgCard);

            // clicks on the dark backdrop must not reach the page or drag the window
            g.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return g;
        }

        private void SetDialogKind(Kind kind, bool busy = false)
        {
            Color c;
            Geometry icon;
            switch (kind)
            {
                case Kind.Success: c = Theme.MintC; icon = Icons.CheckCircle; break;
                case Kind.Warning: c = Theme.AmberC; icon = Icons.Alert; break;
                case Kind.Error: c = Theme.RedC; icon = Icons.XCircle; break;
                default: c = Theme.C(180, 152, 255); icon = busy ? Icons.Download : Icons.Info; break;
            }
            dlgIconTile.Background = Theme.B(Theme.A(34, c));
            dlgIconTile.BorderBrush = Theme.B(Theme.A(76, c));
            dlgIconTile.Child = Icons.Make(icon, 20, Theme.B(c), 1.8);
        }

        private void OpenOverlay()
        {
            ++overlaySeq;
            bool fresh = overlay.Visibility != Visibility.Visible;
            overlayOpen = true;
            overlay.Visibility = Visibility.Visible;
            overlay.IsHitTestVisible = true;
            if (langPopup != null) langPopup.IsOpen = false;

            if (fresh)
            {
                dlgBackdrop.Opacity = 0;
                dlgCard.Opacity = 0;
                dlgScale.ScaleX = dlgScale.ScaleY = 0.92;
                dlgMove.Y = 16;
            }
            else
            {
                // one dialog right after another: a small bump instead of a full entrance
                Anim.FromTo(dlgScale, ScaleTransform.ScaleXProperty, 0.97, 1, 280, Anim.Pop);
                Anim.FromTo(dlgScale, ScaleTransform.ScaleYProperty, 0.97, 1, 280, Anim.Pop);
            }
            Anim.To(dlgBackdrop, OpacityProperty, 1, 200);
            Anim.To(dlgCard, OpacityProperty, 1, 200);
            if (fresh)
            {
                Anim.To(dlgScale, ScaleTransform.ScaleXProperty, 1, 380, Anim.Pop);
                Anim.To(dlgScale, ScaleTransform.ScaleYProperty, 1, 380, Anim.Pop);
            }
            Anim.To(dlgMove, TranslateTransform.YProperty, 0, 380, Anim.Soft);
        }

        private void CloseOverlay()
        {
            if (!overlayOpen) return;
            overlayOpen = false;
            int seq = ++overlaySeq;
            overlay.IsHitTestVisible = false;
            Anim.To(dlgCard, OpacityProperty, 0, 140);
            Anim.To(dlgScale, ScaleTransform.ScaleXProperty, 0.96, 160, Anim.In);
            Anim.To(dlgScale, ScaleTransform.ScaleYProperty, 0.96, 160, Anim.In);
            Anim.To(dlgBackdrop, OpacityProperty, 0, 180, Anim.Out, 0, delegate
            {
                if (seq == overlaySeq) overlay.Visibility = Visibility.Collapsed;
            });
        }

        private void DialogOk()
        {
            CloseOverlay();
            Action a = dlgOnOk;
            dlgOnOk = null;
            if (a != null) a();
        }

        private void DialogCancel()
        {
            CloseOverlay();
            dlgOnOk = null;
        }

        private void ShowDialog(Kind kind, string title, string text, string okText, string cancelText, Action onOk, bool danger = false)
        {
            SetDialogKind(kind);
            dlgTitle.Text = title;
            dlgText.Text = text;
            dlgScroller.ScrollToTop();
            dlgProgress.Visibility = Visibility.Collapsed;
            dlgButtons.Visibility = Visibility.Visible;
            btnDlgOk.Visibility = Visibility.Visible;
            btnDlgOk.Text = okText;
            btnDlgOk.SetLook(danger ? Look.Danger : Look.Primary, null);
            btnDlgCancel.Visibility = cancelText == null ? Visibility.Collapsed : Visibility.Visible;
            if (cancelText != null) btnDlgCancel.Text = cancelText;
            dlgOnOk = onOk;
            OpenOverlay();
        }

        /// <summary>A dialog without buttons and with a progress bar while something is downloading.</summary>
        private void ShowBusy(string title, string text)
        {
            SetDialogKind(Kind.Info, true);
            dlgTitle.Text = title;
            dlgText.Text = text;
            dlgProgress.Visibility = Visibility.Visible;
            dlgButtons.Visibility = Visibility.Collapsed;
            progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            progressScale.ScaleX = 0;
            btnDlgOk.Visibility = Visibility.Collapsed;
            btnDlgCancel.Visibility = Visibility.Collapsed;
            dlgOnOk = null;
            OpenOverlay();
        }

        private void SetBusy(int percent, string text)
        {
            dlgText.Text = text;
            Anim.To(progressScale, ScaleTransform.ScaleXProperty, Math.Max(0, Math.Min(100, percent)) / 100.0, 260);
        }

        private Border BuildToast()
        {
            toastMove = new TranslateTransform(0, 14);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Theme.Mint, Child = Icons.Make(Icons.Check, 13, Theme.Ink, 2.6) });
            toastText = Ui.Text("", 13, Theme.Txt, FontWeights.SemiBold);
            toastText.Margin = new Thickness(10, 0, 0, 0);
            sp.Children.Add(toastText);
            toast = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 92),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(11, 9, 16, 9),
                Background = Theme.B(Color.FromArgb(244, 34, 29, 47)),
                BorderBrush = Theme.StrokeHi,
                BorderThickness = new Thickness(1),
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransform = toastMove,
                Child = sp
            };
            Panel.SetZIndex(toast, 90);
            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
            toastTimer.Tick += delegate
            {
                toastTimer.Stop();
                int seq = toastSeq;
                Anim.To(toastMove, TranslateTransform.YProperty, 10, 260, Anim.In);
                Anim.To(toast, OpacityProperty, 0, 260, Anim.Out, 0, delegate
                {
                    if (seq == toastSeq) toast.Visibility = Visibility.Collapsed;
                });
            };
            return toast;
        }

        private void ShowToast(string text)
        {
            toastSeq++;
            toastText.Text = text;
            toast.Margin = new Thickness(0, 0, 0, screen == Screen.Main ? 92 : 26);
            if (toast.Visibility != Visibility.Visible)
            {
                toast.Opacity = 0;
                toastMove.Y = 14;
            }
            toast.Visibility = Visibility.Visible;
            Anim.To(toast, OpacityProperty, 1, 200);
            Anim.To(toastMove, TranslateTransform.YProperty, 0, 420, Anim.Pop);
            toastTimer.Stop();
            toastTimer.Start();
        }

        // =====================================================================  language and status

        private void SetLanguage(int index)
        {
            Loc.Lang = index;
            Settings.Save();
            ApplyLanguage();
            UpdateStatus();
            UpdateDownloadsRow();
        }

        private void ApplyLanguage()
        {
            txtTitle.Text = Loc.T("title");
            lblGame.Text = Loc.T("lbl.game");
            txtPlaceholder.Text = Loc.T("search");
            txtSettingsTitle.Text = Loc.T("settings");
            txtAboutTitle.Text = Loc.T("about");
            lblSecGame.Text = Loc.T("sec.game");
            lblSecApp.Text = Loc.T("sec.app");
            lblDual.Text = Loc.T("set.dual");
            lblDualSub.Text = Loc.T("set.dualSub");
            lblLatency.Text = Loc.T("set.latency");
            lblLatencySub.Text = Loc.T("set.latencySub");
            badgeDual.Text = Loc.T("badge.beta");
            badgeLatency.Text = Loc.T("badge.beta");
            lblFolder.Text = Loc.T("set.folder");
            btnBrowse.Text = Loc.T("btn.browse");
            lblLang.Text = Loc.T("set.lang");
            txtLangValue.Text = Loc.Lang == 1 ? "Русский" : "English";
            for (int i = 0; i < langChecks.Count; i++) langChecks[i].Visibility = i == Loc.Lang ? Visibility.Visible : Visibility.Hidden;
            lblAuto.Text = Loc.T("set.auto");
            lblAutoSub.Text = Loc.T("set.autoSub");
            lblAnim.Text = Loc.T("set.anim");
            lblAnimSub.Text = Loc.T("set.animSub");
            lblDownloads.Text = Loc.T("set.downloads");
            btnDeleteDownloads.Text = Loc.T("btn.delete");
            lblCheck.Text = Loc.T("set.check");
            btnCheck.Text = Loc.T("btn.check");
            UpdateCheckRow();
            txtAboutDesc.Text = Loc.T("about.desc");
            txtAboutNote.Text = Loc.T("about.note");
            UpdateBanner();
        }

        private void UpdateCheckRow()
        {
            lblCheckSub.Text = checking && manualCheck ? Loc.T("set.checking") : Loc.T("set.checkSub", Online.CurrentVersion.ToString(3));
        }

        private void UpdateSwitches(bool animate)
        {
            swDual.Set(DualMonitor.IsOn(), animate);
            swLatency.Set(LowLatency.IsOn(), animate);
            swAuto.Set(Settings.AutoUpdate, animate);
            swAnim.Set(Settings.Animations, animate);
        }

        private PatchItem Selected()
        {
            return items.FirstOrDefault(i => i.Key == selectedKey);
        }

        private bool IsInstalled(PatchItem it)
        {
            return it.Enabled &&
                   ((state == InstallState.Online && it.Edition == Patcher.EditionOnline) ||
                    (state == InstallState.PartialOffline && it.Edition == Patcher.EditionOffline));
        }

        private void UpdateStatus()
        {
            game = Patcher.Resolve(currentPath);
            patch = game == null ? null : Payload.Find(game.Version);
            state = Patcher.DetectState(game, patch);
            installedCurrent = state == InstallState.Online ? Patcher.IsCurrentBuild(game, patch, Patcher.EditionOnline)
                             : state == InstallState.PartialOffline ? Patcher.IsCurrentBuild(game, patch, Patcher.EditionOffline)
                             : true;

            txtPath.Text = string.IsNullOrEmpty(currentPath) || game == null ? Loc.T("set.noFolder") : currentPath;
            txtGameVersion.Text = game == null ? "-" : game.Version;
            txtGameVersion.Foreground = game == null ? (Brush)Theme.Dim : Theme.AccentText;

            string statusKey;
            switch (state)
            {
                case InstallState.NoGame: statusKey = "short.nogame"; break;
                case InstallState.Unsupported: statusKey = "short.unsupported"; break;
                case InstallState.Original: statusKey = "short.none"; break;
                case InstallState.Online: statusKey = "short.online"; break;
                case InstallState.PartialOffline: statusKey = "short.offline"; break;
                default: statusKey = "short.unknown"; break;
            }

            bool patched = state == InstallState.Online || state == InstallState.PartialOffline;
            bool outdated = patched && !installedCurrent;
            Color c = outdated ? Theme.AmberC : (patched ? Theme.MintC : (state == InstallState.Original ? Theme.SubC : Theme.AmberC));
            SetStatus(Loc.T(statusKey), c, patched);

            if (outdated)
            {
                txtGameNote.Text = Loc.T("hero.outdated");
                txtGameNote.Foreground = Theme.Amber;
                txtGameNote.FontFamily = Theme.Sans;
            }
            else if (game == null)
            {
                txtGameNote.Text = Loc.T("hero.noFolder");
                txtGameNote.Foreground = Theme.Sub;
                txtGameNote.FontFamily = Theme.Sans;
            }
            else
            {
                txtGameNote.Text = ShortPath(currentPath);
                txtGameNote.Foreground = Theme.Dim;
                txtGameNote.FontFamily = Theme.Mono;
            }

            // the list of patches: the online list merged with what is inside the exe
            items.Clear();
            foreach (PatchSet set in Payload.Sets.OrderByDescending(x => x.GameVersion, StringComparer.Ordinal))
            {
                foreach (string edition in new[] { Patcher.EditionOnline, Patcher.EditionOffline })
                {
                    if (!set.Editions.ContainsKey(edition)) continue;
                    items.Add(new PatchItem
                    {
                        Version = set.GameVersion,
                        Edition = edition,
                        Enabled = game != null && game.Version == set.GameVersion,
                        Download = set.NeedsDownload(edition)
                    });
                }
            }

            // keep the selection if it is still usable, otherwise pick the installed patch, otherwise the first usable one
            PatchItem current = Selected();
            if (current == null || !current.Enabled)
            {
                PatchItem pick = items.FirstOrDefault(i => IsInstalled(i)) ?? items.FirstOrDefault(i => i.Enabled);
                selectedKey = pick == null ? "" : pick.Key;
            }

            RebuildList();
            UpdateButtons();
        }

        private void SetStatus(string text, Color c, bool pulse)
        {
            bool changed = txtStatus.Text != text;
            txtStatus.Text = text;
            double ms = IsLoaded ? 320 : 0;
            Anim.Color(pillBg, Theme.A(28, c), ms);
            Anim.Color(pillStroke, Theme.A(84, c), ms);
            Anim.Color(pillColor, c, ms);
            Anim.Stop(heroGlow0, Theme.A(58, c), ms * 2);
            Anim.Stop(heroGlow1, Theme.A(0, c), ms * 2);
            foreach (object r in ripple) Anim.Want(r, pulse);
            if (changed && IsLoaded) Anim.FromTo(statusPill, OpacityProperty, 0.2, 1, 360);
        }

        /// <summary>C:\Users\name\AppData\Local\osulazer becomes C:\...\Local\osulazer when it is long.</summary>
        private static string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 40) return path ?? "";
            string[] parts = path.TrimEnd('\\', '/').Split('\\', '/');
            if (parts.Length <= 3) return path;
            return parts[0] + "\\...\\" + parts[parts.Length - 2] + "\\" + parts[parts.Length - 1];
        }

        private void UpdateButtons()
        {
            PatchItem sel = Selected();
            bool canInstall = game != null && patch != null && sel != null && sel.Enabled;
            bool installed = canInstall && IsInstalled(sel);
            bool outdated = installed && !installedCurrent;   // an older build of this patch is installed: offer the new one

            if (outdated)
            {
                btnInstall.Text = Loc.T("btn.update");
                btnInstall.SetLook(Look.Update, Icons.ArrowUp);
            }
            else if (installed)
            {
                btnInstall.Text = Loc.T("btn.installed");
                btnInstall.SetLook(Look.Done, Icons.Check);
            }
            else
            {
                btnInstall.Text = Loc.T("btn.install");
                btnInstall.SetLook(canInstall ? Look.Primary : Look.Disabled, Icons.Download);
            }

            bool patched = state == InstallState.Online || state == InstallState.PartialOffline;
            btnRemove.Text = Loc.T("btn.remove");
            if (patched) btnRemove.SetLook(Look.Danger, Icons.Trash);
            else if (Patcher.BackupExists(game)) btnRemove.SetLook(Look.Secondary, Icons.Trash);   // only an old backup is left to clean up
            else btnRemove.SetLook(Look.Disabled, Icons.Trash);
        }

        // =====================================================================  version tabs and patch cards

        private void RebuildList()
        {
            listPanel.Children.Clear();
            groups.Clear();
            string q = (txtSearch.Text ?? "").Trim().ToLowerInvariant();

            foreach (string version in items.Select(i => i.Version).Distinct())
            {
                List<PatchItem> group = items.Where(i => i.Version == version).ToList();
                if (q.Length > 0)
                {
                    bool match = version.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) >= 0 ||
                                 group.Any(i => i.Title.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) >= 0);
                    if (!match) continue;
                }
                GroupView gv = MakeGroup(version, group, q.Length > 0 || expanded.Contains(version));
                groups.Add(gv);
                listPanel.Children.Add(gv.Wrap);
            }

            txtListEmpty.Text = Loc.T(q.Length > 0 ? "list.empty" : "list.none");
            txtListEmpty.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private GroupView MakeGroup(string version, List<PatchItem> group, bool open)
        {
            var gv = new GroupView { Version = version, Open = open };
            bool hasInstalled = group.Any(i => IsInstalled(i));
            bool usable = group.Any(i => i.Enabled);

            gv.Wrap = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var headBg = Theme.Live(Theme.SurfaceC);
            var head = new Border
            {
                Height = 58,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 0, 12, 0),
                Background = headBg,
                BorderBrush = hasInstalled ? Theme.B(Theme.A(90, Theme.MintC)) : Theme.Stroke,
                BorderThickness = new Thickness(1)
            };
            var hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hg.Children.Add(Ui.Text(version, 16.5, usable ? Theme.Txt : Theme.Dim, FontWeights.SemiBold, Theme.Mono));

            var pills = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            if (usable) pills.Children.Add(Pill(Loc.T("tab.current"), Theme.PinkSoftC));
            if (hasInstalled) pills.Children.Add(Pill(Loc.T("tab.installed"), Theme.MintC));
            Grid.SetColumn(pills, 1);
            hg.Children.Add(pills);

            gv.Chevron = new RotateTransform(open ? 180 : 0);
            Path chev = Icons.Make(Icons.Chevron, 15, usable ? Theme.Sub : Theme.Dim, 2.0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = gv.Chevron;
            var chevBox = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Theme.B(Color.FromArgb(14, 255, 255, 255)), Child = chev };
            Grid.SetColumn(chevBox, 2);
            hg.Children.Add(chevBox);
            head.Child = hg;

            head.MouseEnter += delegate { Anim.Color(headBg, Theme.SurfaceHoverC, 140); };
            head.MouseLeave += delegate { Anim.Color(headBg, Theme.SurfaceC, 240); };
            Ui.Clickable(head, delegate { ToggleGroup(gv); }, 0.985);
            gv.Wrap.Children.Add(head);

            gv.BodyMove = new TranslateTransform();
            gv.Body = new StackPanel { Margin = new Thickness(8, 8, 8, 0), RenderTransform = gv.BodyMove };
            foreach (PatchItem it in group)
            {
                CardView cv = MakeCard(it);
                gv.Cards.Add(cv);
                gv.Body.Children.Add(cv.Root);
            }
            gv.BodyHost = new Border { ClipToBounds = true, Child = gv.Body };
            if (!open)
            {
                gv.BodyHost.Height = 0;
                gv.BodyHost.Visibility = Visibility.Collapsed;
            }
            gv.Wrap.Children.Add(gv.BodyHost);
            return gv;
        }

        /// <summary>Opens or closes a version tab with a height animation.</summary>
        private void ToggleGroup(GroupView gv)
        {
            gv.Open = !gv.Open;
            if (gv.Open) expanded.Add(gv.Version); else expanded.Remove(gv.Version);
            Anim.To(gv.Chevron, RotateTransform.AngleProperty, gv.Open ? 180 : 0, 340, Anim.Soft);

            double from = gv.BodyHost.Visibility == Visibility.Visible ? gv.BodyHost.ActualHeight : 0;
            if (gv.Open)
            {
                gv.BodyHost.Visibility = Visibility.Visible;
                double width = gv.Wrap.ActualWidth > 0 ? gv.Wrap.ActualWidth : listPanel.ActualWidth;
                gv.Body.Measure(new Size(width, double.PositiveInfinity));
                double to = gv.Body.DesiredSize.Height;
                Anim.FromTo(gv.BodyHost, HeightProperty, from, to, 360, Anim.Soft, 0, delegate
                {
                    if (!gv.Open) return;
                    gv.BodyHost.BeginAnimation(HeightProperty, null);
                    gv.BodyHost.Height = double.NaN;   // back to automatic height
                    listScroller.Reveal(gv.Wrap);
                });
                Anim.FromTo(gv.Body, OpacityProperty, 0, 1, 300, Anim.Out, 40);
                Anim.FromTo(gv.BodyMove, TranslateTransform.YProperty, -10, 0, 420, Anim.Soft);
            }
            else
            {
                Anim.FromTo(gv.BodyHost, HeightProperty, from, 0, 260, Anim.Out, 0, delegate
                {
                    if (!gv.Open) gv.BodyHost.Visibility = Visibility.Collapsed;
                });
                Anim.To(gv.Body, OpacityProperty, 0, 180);
                Anim.To(gv.BodyMove, TranslateTransform.YProperty, -6, 260);
            }
        }

        private CardView MakeCard(PatchItem it)
        {
            var cv = new CardView { Item = it, Bg = Theme.Live(Theme.Surface2C), Stroke = Theme.Live(Theme.StrokeC), Ring = Theme.Live(Color.FromArgb(80, 255, 255, 255)) };

            cv.Root = new Border
            {
                Height = 70,
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 8),
                Background = cv.Bg,
                BorderBrush = cv.Stroke,
                BorderThickness = new Thickness(1)
            };
            var layers = new Grid();
            cv.SelRing = new Border { CornerRadius = new CornerRadius(14), BorderBrush = Theme.Accent, BorderThickness = new Thickness(1.6), Margin = new Thickness(-1), Opacity = 0, IsHitTestVisible = false };
            layers.Children.Add(cv.SelRing);

            var g = new Grid { Margin = new Thickness(18, 0, 14, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            cv.Title = Ui.Text(it.Title, 16, Theme.Txt, FontWeights.SemiBold);
            cv.Sub = Ui.Text(it.Sub, 12, Theme.Sub, FontWeights.Normal, Theme.Mono);
            cv.Sub.Margin = new Thickness(0, 3, 0, 0);
            texts.Children.Add(cv.Title);
            texts.Children.Add(cv.Sub);
            g.Children.Add(texts);

            // a small arrow: this patch is downloaded when you install it
            if (it.Download && !IsInstalled(it))
            {
                var dl = new Border
                {
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = Brushes.Transparent,
                    Child = Icons.Make(Icons.Download, 16, it.Enabled ? Theme.Sub : Theme.Dim, 1.9),
                    ToolTip = new ToolTip
                    {
                        Content = Loc.T("card.download"),
                        Background = Theme.B(Color.FromArgb(250, 34, 29, 47)),
                        Foreground = Theme.Txt,
                        BorderBrush = Theme.StrokeHi,
                        Padding = new Thickness(10, 6, 10, 6),
                        FontFamily = Theme.Sans,
                        FontSize = 12
                    }
                };
                Grid.SetColumn(dl, 1);
                g.Children.Add(dl);
            }

            var radio = new Grid { Width = 26, Height = 26, VerticalAlignment = VerticalAlignment.Center };
            radio.Children.Add(new Ellipse { Stroke = cv.Ring, StrokeThickness = 1.6 });
            cv.RadioScale = new ScaleTransform(0.5, 0.5);
            cv.RadioCheck = Icons.Make(Icons.Check, 14, Theme.White, 2.6);
            cv.RadioFill = new Border { CornerRadius = new CornerRadius(13), Background = Theme.Accent, Opacity = 0, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = cv.RadioScale, Child = cv.RadioCheck };
            radio.Children.Add(cv.RadioFill);
            Grid.SetColumn(radio, 2);
            g.Children.Add(radio);
            layers.Children.Add(g);
            cv.Root.Child = layers;

            if (it.Enabled)
            {
                cv.Root.MouseEnter += delegate { cv.Hover = true; ApplyCardLook(cv, true); };
                cv.Root.MouseLeave += delegate { cv.Hover = false; ApplyCardLook(cv, true); };
                Ui.Clickable(cv.Root, delegate { Select(it.Key); }, 0.98);
            }
            ApplyCardLook(cv, false);
            return cv;
        }

        private void ApplyCardLook(CardView cv, bool animate)
        {
            PatchItem it = cv.Item;
            bool installed = IsInstalled(it);
            bool sel = it.Enabled && it.Key == selectedKey;
            double ms = animate ? 220 : 0;

            Color bg = installed ? Theme.A(30, Theme.MintC) : (sel ? Theme.A(30, Theme.PinkC) : (cv.Hover && it.Enabled ? Theme.Surface2HoverC : Theme.Surface2C));
            Color stroke = installed ? Theme.A(110, Theme.MintC) : (sel ? Theme.Transparent : Theme.StrokeC);
            Anim.Color(cv.Bg, bg, ms);
            Anim.Color(cv.Stroke, stroke, ms);
            Anim.To(cv.SelRing, UIElement.OpacityProperty, sel && !installed ? 1 : 0, ms);

            cv.Title.Foreground = it.Enabled ? Theme.Txt : Theme.Dim;
            cv.Sub.Foreground = installed ? Theme.Mint : (sel ? Theme.PinkSoft : (it.Enabled ? Theme.Sub : Theme.Dim));
            cv.Root.Opacity = it.Enabled ? 1 : 0.55;

            bool filled = installed || sel;
            cv.RadioFill.Background = installed ? (Brush)Theme.Mint : Theme.Accent;
            cv.RadioCheck.Stroke = installed ? Theme.Ink : Theme.White;
            Anim.Color(cv.Ring, filled ? Theme.Transparent : Color.FromArgb((byte)(it.Enabled ? 80 : 40), 255, 255, 255), ms);

            if (filled != cv.Filled || !animate)
            {
                cv.Filled = filled;
                if (filled && animate)
                {
                    Anim.FromTo(cv.RadioScale, ScaleTransform.ScaleXProperty, 0.35, 1, 420, Anim.Pop);
                    Anim.FromTo(cv.RadioScale, ScaleTransform.ScaleYProperty, 0.35, 1, 420, Anim.Pop);
                    Anim.To(cv.RadioFill, UIElement.OpacityProperty, 1, 140);
                }
                else
                {
                    Anim.To(cv.RadioScale, ScaleTransform.ScaleXProperty, filled ? 1 : 0.5, ms);
                    Anim.To(cv.RadioScale, ScaleTransform.ScaleYProperty, filled ? 1 : 0.5, ms);
                    Anim.To(cv.RadioFill, UIElement.OpacityProperty, filled ? 1 : 0, ms * 0.7);
                }
            }
        }

        private void Select(string key)
        {
            if (selectedKey == key) return;
            selectedKey = key;
            foreach (GroupView gv in groups)
                foreach (CardView cv in gv.Cards)
                    ApplyCardLook(cv, true);
            UpdateButtons();
        }

        // =====================================================================  install and remove

        private void OnInstall()
        {
            PatchItem it = Selected();
            if (game == null || patch == null || it == null || !it.Enabled || (IsInstalled(it) && installedCurrent)) return;

            string text = Loc.T("disc.text");
            if (it.Edition == Patcher.EditionOffline) text += "\n\n" + Loc.T("disc.offline");
            text += "\n\n" + Loc.T("disc.ask");

            GameInfo g = game;
            PatchSet set = patch;
            ShowDialog(Kind.Warning, Loc.T("disc.title"), text, Loc.T("btn.install"), Loc.T("btn.cancel"), delegate
            {
                if (set.NeedsDownload(it.Edition)) DownloadThenInstall(g, set, it.Edition);
                else RunOperation(delegate { Patcher.Install(g, set, it.Edition, Log); }, Loc.T("log.done"));
            });
        }

        /// <summary>Downloads the patch files (checked by SHA-256) on a worker thread, then installs them.</summary>
        private void DownloadThenInstall(GameInfo g, PatchSet set, string edition)
        {
            ShowBusy(Loc.T("dl.title"), Loc.T("dl.text", 0));

            Task.Run(() => Online.DownloadEdition(set, edition, pct =>
            {
                Dispatcher.BeginInvoke(new Action(() => { SetBusy(pct, Loc.T("dl.text", pct)); }));
            })).ContinueWith(t =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted)
                    {
                        Exception ex = t.Exception.GetBaseException();
                        ShowDialog(Kind.Error, Loc.T("msg.error"), Loc.T("dl.fail", ex.Message), Loc.T("btn.ok"), null, null);
                        UpdateStatus();
                        return;
                    }
                    RunOperation(delegate { Patcher.Install(g, set, edition, Log); }, Loc.T("log.done"));
                }));
            });
        }

        private void OnRemove()
        {
            if (game == null) return;

            ShowDialog(Kind.Warning, Loc.T("confirmRevert.title"), Loc.T("confirmRevert"), Loc.T("btn.remove"), Loc.T("btn.cancel"), delegate
            {
                RunOperation(delegate { Patcher.Revert(game, Log); }, Loc.T("log.revertDone"));
            }, true);
        }

        private void RunOperation(Action action, string doneText)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            opLog.Clear();
            try
            {
                action();
                ShowDialog(Kind.Success, Loc.T("msg.done"), doneText, Loc.T("btn.ok"), null, null);
            }
            catch (UserException ex)
            {
                if (ex.NeedsAdmin && !IsAdministrator())
                    ShowDialog(Kind.Error, Loc.T("msg.error"), ex.Message, Loc.T("btnAdmin"), Loc.T("btn.cancel"), RestartAsAdmin);
                else
                    ShowDialog(Kind.Error, Loc.T("msg.error"), ex.Message, Loc.T("btn.ok"), null, null);
            }
            catch (Exception ex)
            {
                ShowDialog(Kind.Error, Loc.T("msg.error"), ex.Message, Loc.T("btn.ok"), null, null);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                UpdateStatus();
            }
        }

        private void Log(string message)
        {
            opLog.Add(message);
        }

        // =====================================================================  settings

        private void BrowseFolder()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = Loc.T("browseDesc");
                if (!string.IsNullOrEmpty(currentPath) && System.IO.Directory.Exists(currentPath)) dlg.SelectedPath = currentPath;
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                currentPath = dlg.SelectedPath;
                UpdateStatus();

                if (game == null)
                {
                    ShowDialog(Kind.Error, Loc.T("msg.error"), Loc.T("log.gameNotFound"), Loc.T("btn.ok"), null, null);
                }
                else
                {
                    Settings.Folder = currentPath;
                    Settings.Save();
                }
            }
        }

        private void ToggleDualMonitor()
        {
            // the game reads the setting when it starts and rewrites framework.ini when it closes
            if (Patcher.IsGameRunning())
            {
                ShowDialog(Kind.Error, Loc.T("msg.error"), Loc.T("err.running"), Loc.T("btn.ok"), null, null);
                return;
            }

            bool turnOn = !DualMonitor.IsOn();
            swDual.Set(turnOn, true);
            swDual.Busy = true;

            Task.Run(() => DualMonitor.Set(turnOn)).ContinueWith(t =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    swDual.Busy = false;
                    swDual.Set(DualMonitor.IsOn(), true);

                    if (t.Status != TaskStatus.RanToCompletion)
                    {
                        string why = t.Exception == null ? "" : t.Exception.GetBaseException().Message;
                        ShowDialog(Kind.Error, Loc.T("msg.error"), Loc.T("set.fail", why), Loc.T("btn.ok"), null, null);
                        return;
                    }
                    ShowDialog(Kind.Info, Loc.T("set.dual"), Loc.T(turnOn ? "dual.on" : "dual.off"), Loc.T("btn.ok"), null, null);
                }));
            });
        }

        /// <summary>Low latency: a user environment variable the patched DLL reads when the game starts.</summary>
        private void ToggleLatency()
        {
            bool turnOn = !LowLatency.IsOn();
            swLatency.Set(turnOn, true);
            swLatency.Busy = true;

            Task.Run(() => LowLatency.Set(turnOn)).ContinueWith(t =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    swLatency.Busy = false;
                    swLatency.Set(LowLatency.IsOn(), true);

                    if (t.Status != TaskStatus.RanToCompletion)
                    {
                        string why = t.Exception == null ? "" : t.Exception.GetBaseException().Message;
                        ShowDialog(Kind.Error, Loc.T("msg.error"), Loc.T("set.fail", why), Loc.T("btn.ok"), null, null);
                        return;
                    }
                    ShowDialog(Kind.Info, Loc.T("set.latency"), Loc.T(turnOn ? "latency.on" : "latency.off"), Loc.T("btn.ok"), null, null);
                }));
            });
        }

        private void ToggleAutoUpdate()
        {
            Settings.AutoUpdate = !Settings.AutoUpdate;
            Settings.Save();
            swAuto.Set(Settings.AutoUpdate, true);
        }

        private void ToggleAnimations()
        {
            Settings.Animations = !Settings.Animations;
            Settings.Save();
            Anim.On = Settings.Animations;
            swAnim.Set(Settings.Animations, true);
            UpdateLively();
        }

        private void UpdateDownloadsRow()
        {
            long size = Payload.CacheSize();
            lblDownloadsSub.Text = size > 0 ? Loc.T("set.downloadsSub", FormatSize(size)) : Loc.T("set.downloadsNone");
            btnDeleteDownloads.SetLook(size > 0 ? Look.Danger : Look.Disabled, Icons.Trash);
        }

        /// <summary>Deletes the patch files the program has downloaded (the cache), after asking.</summary>
        private void OnDeleteDownloads()
        {
            long size = Payload.CacheSize();
            if (size <= 0)
            {
                UpdateDownloadsRow();
                return;
            }

            ShowDialog(Kind.Warning, Loc.T("dl.clearTitle"), Loc.T("dl.clearAsk", FormatSize(size)), Loc.T("btn.delete"), Loc.T("btn.cancel"), delegate
            {
                string error = null;
                try
                {
                    Payload.ClearCache();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                long left = Payload.CacheSize();
                UpdateStatus();   // the patches show the download arrow again
                UpdateDownloadsRow();

                if (error != null && left > 0)
                    ShowDialog(Kind.Error, Loc.T("msg.error"), Loc.T("dl.clearFail", error), Loc.T("btn.ok"), null, null);
                else
                    ShowToast(Loc.T("dl.cleared", FormatSize(Math.Max(0, size - left))));
            }, true);
        }

        private static string FormatSize(long bytes)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            if (Loc.Lang == 1)
            {
                try { ci = CultureInfo.GetCultureInfo("ru-RU"); }
                catch (Exception) { }
            }
            if (bytes < 1024 * 1024) return Loc.T("size.kb", Math.Max(1, (bytes + 1023) / 1024).ToString(ci));
            double mb = bytes / (1024.0 * 1024.0);
            return Loc.T("size.mb", mb.ToString(mb < 100 ? "0.0" : "0", ci));
        }

        // =====================================================================  online list and program updates

        /// <summary>
        /// Downloads the online list (patches + newest program version).
        /// manual = the user pressed "Check": always answer with a dialog.
        /// Otherwise (at start) only the list and the banner change, and with Auto-update on the program updates itself.
        /// </summary>
        private void CheckForUpdates(bool manual)
        {
            if (checking) return;
            checking = true;
            manualCheck = manual;
            if (manual)
            {
                btnCheck.Spin(true);
                UpdateCheckRow();
            }

            Task.Run(() => Online.Fetch()).ContinueWith(t =>
            {
                Manifest m = t.Status == TaskStatus.RanToCompletion ? t.Result : null;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    checking = false;
                    btnCheck.Spin(false);
                    UpdateCheckRow();

                    if (m != null)
                    {
                        Payload.SetOnline(m.Patches);
                        UpdateStatus();
                        appUpdate = Online.IsNewer(m.App) ? m.App : null;
                        UpdateBanner();
                    }

                    if (!manual)
                    {
                        if (appUpdate != null && !appStaged && Settings.AutoUpdate) UpdateProgram(true);
                        return;
                    }

                    if (m == null)
                        ShowDialog(Kind.Error, Loc.T("upd.title"), Loc.T("upd.fail"), Loc.T("btn.ok"), null, null);
                    else if (appStaged)
                        ShowDialog(Kind.Success, Loc.T("upd.title"), Loc.T("upd.ready", appUpdate == null ? "" : appUpdate.Version.ToString(3)), Loc.T("btn.restart"), Loc.T("btn.later"), Online.Restart);
                    else if (appUpdate != null)
                        ShowDialog(Kind.Info, Loc.T("upd.title"), Loc.T("upd.new", appUpdate.Version.ToString(3)), Loc.T("btn.update"), Loc.T("btn.cancel"), delegate { UpdateProgram(false); });
                    else
                        ShowDialog(Kind.Success, Loc.T("upd.title"), Loc.T("upd.latest"), Loc.T("btn.ok"), null, null);
                }));
            });
        }

        /// <summary>Downloads the new exe and puts it in place of this one. silent = automatic, no dialogs unless the user asks.</summary>
        private void UpdateProgram(bool silent)
        {
            AppRelease app = appUpdate;
            if (app == null || updating || appStaged) return;
            updating = true;
            UpdateBanner();

            if (!silent) ShowBusy(Loc.T("upd.title"), Loc.T("upd.downloading", 0));

            Task.Run(() => Online.InstallAppUpdate(app, pct =>
            {
                if (silent) return;
                Dispatcher.BeginInvoke(new Action(() => { SetBusy(pct, Loc.T("upd.downloading", pct)); }));
            })).ContinueWith(t =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    updating = false;

                    if (t.IsFaulted)
                    {
                        UpdateBanner();
                        if (silent) return;   // the banner still offers the update; pressing it shows the error

                        Exception ex = t.Exception.GetBaseException();
                        string text = ex is UnauthorizedAccessException ? Loc.T("upd.noAccess") : Loc.T("upd.failed", ex.Message);
                        ShowDialog(Kind.Error, Loc.T("msg.error"), text, Loc.T("upd.page"), Loc.T("btn.cancel"), delegate { OpenUrl(ReleasesUrl); });
                        return;
                    }

                    appStaged = true;
                    UpdateBanner();
                    if (!silent)
                        ShowDialog(Kind.Success, Loc.T("upd.title"), Loc.T("upd.ready", app.Version.ToString(3)), Loc.T("btn.restart"), Loc.T("btn.later"), Online.Restart);
                }));
            });
        }

        /// <summary>The banner under the game card: "update available" or "update installed, restart".</summary>
        private void UpdateBanner()
        {
            if (appUpdate == null)
            {
                if (bannerBorder.Visibility == Visibility.Visible)
                    Anim.To(bannerBorder, OpacityProperty, 0, 200, Anim.Out, 0, delegate { if (appUpdate == null) bannerBorder.Visibility = Visibility.Collapsed; });
                return;
            }

            string v = appUpdate.Version.ToString(3);
            if (appStaged)
            {
                txtBanner.Text = Loc.T("update.ready", v);
                bannerBtn.Text = Loc.T("btn.restart");
                bannerBtn.SetLook(Look.Primary, Icons.Refresh);
            }
            else
            {
                txtBanner.Text = Loc.T("update.avail", v);
                bannerBtn.Text = updating ? "..." : Loc.T("btn.update");
                bannerBtn.SetLook(updating ? Look.Disabled : Look.Primary, null);
            }

            if (bannerBorder.Visibility != Visibility.Visible)
            {
                bannerBorder.Visibility = Visibility.Visible;
                if (IsLoaded) Anim.FromTo(bannerBorder, OpacityProperty, 0, 1, 360);
            }
        }

        private void OnBannerClick()
        {
            if (appStaged) Online.Restart();
            else if (!updating) UpdateProgram(false);
        }

        // =====================================================================  helpers

        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RestartAsAdmin()
        {
            try
            {
                var psi = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location) { UseShellExecute = true, Verb = "runas" };
                Process.Start(psi);
                Application.Current.Shutdown();
            }
            catch (Win32Exception)
            {
                // the user declined the UAC prompt
            }
        }

        private static void OpenUrl(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // nothing sensible to do
            }
        }

#if PREVIEW
        /// <summary>Only in preview builds (-p:DefineConstants=PREVIEW): puts the window into a state for screenshots. OSUFIX_PREVIEW=settings,dialog,...</summary>
        private void Preview()
        {
            string what = Environment.GetEnvironmentVariable("OSUFIX_PREVIEW") ?? "";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            timer.Tick += delegate
            {
                timer.Stop();
                foreach (string step in what.Split(','))
                {
                    switch (step.Trim())
                    {
                        case "open": if (groups.Count > 0 && !groups[0].Open) ToggleGroup(groups[0]); break;
                        case "offline": Select(items.Count > 1 ? items[1].Key : selectedKey); break;
                        case "settings": ShowPage(Screen.Settings); break;
                        case "scroll": settingsScroller.ScrollTo(10000); break;
                        case "about": ShowPage(Screen.About); break;
                        case "banner": appUpdate = new AppRelease { Version = new Version(1, 3, 0) }; UpdateBanner(); break;
                        case "outdated": installedCurrent = false; UpdateButtons(); break;
                        case "dialog": OnInstall(); break;
                        case "delete": OnDeleteDownloads(); break;
                        case "ok": DialogOk(); break;
                        case "remove": OnRemove(); break;
                        case "latency": ToggleLatency(); break;
                        case "busy": ShowBusy(Loc.T("dl.title"), Loc.T("dl.text", 45)); SetBusy(45, Loc.T("dl.text", 45)); break;
                        case "toast": ShowToast(Loc.T("dl.cleared", FormatSize(4404019))); toastTimer.Stop(); break;
                        case "lang": langPopup.IsOpen = true; break;
                        case "search": txtSearch.Text = "2026"; break;
                        case "ru": SetLanguage(1); break;
                        case "en": SetLanguage(0); break;
                    }
                }
            };
            timer.Start();
        }
#endif
    }
}
