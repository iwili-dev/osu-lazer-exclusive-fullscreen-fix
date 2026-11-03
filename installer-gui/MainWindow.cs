using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

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
    /// The window, built in code (no XAML) to match design/mockup-v3.png:
    /// dark cards, soft pink accent, mint green for "installed", rounded buttons.
    /// Icons come from the Segoe MDL2 Assets font that ships with Windows 10 and 11.
    /// </summary>
    internal sealed class MainWindow : Window
    {
        private const string GitHubUrl = "https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix";
        private const string ReleasesUrl = GitHubUrl + "/releases";

        // ---- palette (same values as the mockup) ----
        private static SolidColorBrush Br(byte r, byte g, byte b)
        {
            var x = new SolidColorBrush(Color.FromRgb(r, g, b));
            x.Freeze();
            return x;
        }

        private static readonly SolidColorBrush WinBg = Br(25, 24, 27);
        private static readonly SolidColorBrush Card = Br(37, 36, 40);
        private static readonly SolidColorBrush Card2 = Br(50, 48, 54);
        private static readonly SolidColorBrush Line = Br(55, 53, 59);
        private static readonly SolidColorBrush Pink = Br(255, 186, 216);
        private static readonly SolidColorBrush Txt = Br(246, 244, 247);
        private static readonly SolidColorBrush Sub = Br(152, 149, 158);
        private static readonly SolidColorBrush Dark = Br(43, 20, 33);
        private static readonly SolidColorBrush Dim = Br(92, 89, 98);
        private static readonly SolidColorBrush PinkSub = Br(122, 66, 94);
        private static readonly SolidColorBrush SoftRed = Br(240, 128, 134);
        private static readonly SolidColorBrush RedText = Br(58, 16, 22);
        private static readonly SolidColorBrush Mint = Br(146, 224, 178);
        private static readonly SolidColorBrush MintTint = Br(31, 52, 42);
        private static readonly SolidColorBrush MintLine = Br(62, 112, 84);
        private static readonly SolidColorBrush MintDark = Br(16, 46, 30);
        private static readonly SolidColorBrush MintSub = Br(50, 98, 68);
        private static readonly SolidColorBrush ThumbBrush = Br(88, 86, 95);

        private static readonly FontFamily Sans = new FontFamily("Segoe UI");
        private static readonly FontFamily Mono = new FontFamily("Consolas");
        private static readonly FontFamily Icons = new FontFamily("Segoe MDL2 Assets");

        private enum Look { Primary, Secondary, Danger, Done, Disabled }
        private enum Screen { Main, Settings, About }

        // ---- controls that change at run time ----
        private Grid mainPage, settingsPage, aboutPage, overlay;
        private Border frame;
        private TextBlock txtTitle, txtTitleSub, lblGame, txtGameVersion, lblStatus, txtStatus, txtPlaceholder, txtBanner, txtBannerBtn;
        private TextBlock txtSettingsTitle, txtAboutTitle, lblLang, lblAuto, lblFolder, txtLangValue, txtPath, txtCheck;
        private TextBlock txtAboutNote, dlgTitle, dlgText, autoGlyph, lblDual, lblDualSub, dualGlyph;
        private Border dotStatus, bannerBorder, autoBox, dualBox, scrollTrack, scrollThumb;
        private Border btnInstall, btnRemove, btnDlgOk, btnDlgCancel;
        private TextBox txtSearch;
        private StackPanel listPanel;
        private ScrollViewer listScroll;
        private Popup langPopup;
        private Action dlgOnOk;
        private int langClosedTick;

        // ---- state ----
        private string currentPath = "";
        private GameInfo game;
        private PatchSet patch;
        private InstallState state = InstallState.NoGame;
        private string selectedKey = "";
        private bool installedCurrent = true;   // the installed fix is the newest build of its edition
        private bool checking;                  // the online list is being downloaded
        private bool updating;                  // a new version of the program is being downloaded
        private AppRelease appUpdate;           // a newer version of the program, if the online list has one
        private bool appStaged;                 // the new exe is already in place, a restart starts it
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly List<PatchItem> items = new List<PatchItem>();
        private readonly List<string> opLog = new List<string>();

        public MainWindow()
        {
            Settings.Load();

            // the online list saved last time, so the patches are there even without internet
            Manifest cached = Online.LoadCached();
            if (cached != null) Payload.SetOnline(cached.Patches);

            BuildUi();

            if (!string.IsNullOrEmpty(Settings.Folder) && Patcher.Resolve(Settings.Folder) != null)
                currentPath = Settings.Folder;
            else if (Patcher.Resolve(Patcher.DefaultRoot()) != null)
                currentPath = Patcher.DefaultRoot();

            ApplyLanguage();
            UpdateAutoBox();
            UpdateDualBox();
            UpdateStatus();

            // always refresh the list of patches; the program itself updates on its own only if Auto-update is on
            CheckForUpdates(false);
        }

        // =====================================================================  building blocks

        private static TextBlock Tb(string text, double size, Brush fg, bool bold, FontFamily font)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = fg,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                FontFamily = font ?? Sans,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        private static TextBlock Glyph(string glyph, double size, Brush fg)
        {
            return new TextBlock
            {
                Text = glyph,
                FontFamily = Icons,
                FontSize = size,
                Foreground = fg,
                Width = size,
                Height = size,
                TextAlignment = TextAlignment.Center,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>A thin check mark drawn as a line (the logo of the program).</summary>
        private static FrameworkElement ThinCheck(double size, Brush color, double thickness)
        {
            return new System.Windows.Shapes.Polyline
            {
                Points = new PointCollection { new Point(0.0, 0.55), new Point(0.38, 0.92), new Point(1.0, 0.08) },
                Stroke = color,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = size,
                Height = size * 0.78,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Border Logo(double size, double radius, double checkSize, double thickness)
        {
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(radius),
                Background = Pink,
                Child = ThinCheck(checkSize, Dark, thickness)
            };
        }

        /// <summary>Makes a Border behave like a button (hover + click). Disabled borders ignore the mouse.</summary>
        private static void Wire(Border b, Action onClick)
        {
            b.Cursor = Cursors.Hand;
            b.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                if (b.IsEnabled && onClick != null) onClick();
            };
            b.MouseEnter += delegate { if (b.IsEnabled) b.Opacity = 0.88; };
            b.MouseLeave += delegate { b.Opacity = 1.0; };
        }

        private static Border IconButton(string glyph, Action onClick)
        {
            var b = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(11),
                Background = Card2,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Glyph(glyph, 14, Txt)
            };
            Wire(b, onClick);
            return b;
        }

        /// <summary>Minimise and close. Shown at the top right of every screen.</summary>
        private StackPanel WindowButtons()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var minimize = IconButton("\uE921", delegate { WindowState = WindowState.Minimized; });
            minimize.Margin = new Thickness(0, 0, 6, 0);
            panel.Children.Add(minimize);
            panel.Children.Add(IconButton("\uE711", delegate { Close(); }));
            return panel;
        }

        private static Border TextButton(double height, double radius, double fontSize, bool bold, Action onClick)
        {
            var b = new Border
            {
                Height = height,
                CornerRadius = new CornerRadius(radius),
                Padding = new Thickness(18, 0, 18, 0),
                Child = new TextBlock
                {
                    FontSize = fontSize,
                    FontFamily = Sans,
                    FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Wire(b, onClick);
            return b;
        }

        private static void SetLook(Border b, Look look)
        {
            var tb = (TextBlock)b.Child;
            b.Effect = null;
            switch (look)
            {
                case Look.Primary:
                    b.Background = Pink; tb.Foreground = Dark; b.IsEnabled = true;
                    break;
                case Look.Secondary:
                    b.Background = Card2; tb.Foreground = Txt; b.IsEnabled = true;
                    break;
                case Look.Danger:   // soft red glow while a patch is installed
                    b.Background = SoftRed; tb.Foreground = RedText; b.IsEnabled = true;
                    b.Effect = new DropShadowEffect { Color = Color.FromRgb(240, 128, 134), BlurRadius = 22, ShadowDepth = 0, Opacity = 0.65 };
                    break;
                case Look.Done:     // the selected patch is already installed
                    b.Background = Mint; tb.Foreground = MintDark; b.IsEnabled = false;
                    break;
                default:
                    b.Background = Card2; tb.Foreground = Dim; b.IsEnabled = false;
                    break;
            }
            b.Opacity = 1.0;
        }

        private static Border Tile(string glyph)
        {
            return new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(11),
                Background = Card2,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Glyph(glyph, 16, Pink)
            };
        }

        // =====================================================================  window and pages

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
            SnapsToDevicePixels = true;
            LoadWindowIcon();

            frame = new Border
            {
                Margin = new Thickness(20),
                Background = WinBg,
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 8, Direction = 270, Opacity = 0.65, Color = Colors.Black }
            };

            var root = new Grid();
            frame.Child = root;
            Content = frame;

            mainPage = BuildMainPage();
            settingsPage = BuildSettingsPage();
            aboutPage = BuildAboutPage();
            overlay = BuildOverlay();
            root.Children.Add(mainPage);
            root.Children.Add(settingsPage);
            root.Children.Add(aboutPage);
            root.Children.Add(overlay);

            // drag the window by its top area
            frame.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.OriginalSource is TextBox) return;
                if (e.GetPosition(frame).Y >= 76) return;
                try { DragMove(); } catch (InvalidOperationException) { }
            };

            ShowPage(Screen.Main);
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

        private void ShowPage(Screen p)
        {
            mainPage.Visibility = p == Screen.Main ? Visibility.Visible : Visibility.Collapsed;
            settingsPage.Visibility = p == Screen.Settings ? Visibility.Visible : Visibility.Collapsed;
            aboutPage.Visibility = p == Screen.About ? Visibility.Visible : Visibility.Collapsed;
        }

        private Grid PageHeader(Border backButton, out TextBlock title)
        {
            var h = new Grid { Margin = new Thickness(24, 20, 24, 0), Height = 40, VerticalAlignment = VerticalAlignment.Top };
            h.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            h.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            h.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(backButton, 0);
            h.Children.Add(backButton);
            title = Tb("", 17, Txt, true, null);
            title.Margin = new Thickness(14, 0, 12, 0);
            Grid.SetColumn(title, 1);
            h.Children.Add(title);
            var buttons = WindowButtons();
            Grid.SetColumn(buttons, 2);
            h.Children.Add(buttons);
            return h;
        }

        // ---------------------------------------------------------------- main page

        private Grid BuildMainPage()
        {
            var g = new Grid();
            for (int i = 0; i < 6; i++)
                g.RowDefinitions.Add(new RowDefinition { Height = i == 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            // row 0: header. Logo, title on two lines (so it never runs into the buttons), then the buttons in one evenly spaced row.
            var header = new Grid { Margin = new Thickness(24, 20, 24, 0), Height = 40 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var logo = Logo(32, 9, 16, 1.7);
            logo.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(logo, 0);
            header.Children.Add(logo);

            var titleStack = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            txtTitle = Tb("", 16, Txt, true, null);
            txtTitleSub = Tb("by iwili", 11.5, Sub, false, null);
            txtTitleSub.Margin = new Thickness(0, 1, 0, 0);
            titleStack.Children.Add(txtTitle);
            titleStack.Children.Add(txtTitleSub);
            Grid.SetColumn(titleStack, 1);
            header.Children.Add(titleStack);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var gear = IconButton("\uE713", delegate { ShowPage(Screen.Settings); });
            gear.Margin = new Thickness(0, 0, 6, 0);
            buttons.Children.Add(gear);
            var about = IconButton("\uE930", delegate { ShowPage(Screen.About); });
            about.Margin = new Thickness(0, 0, 6, 0);
            buttons.Children.Add(about);
            // a thin divider between the page buttons and the window buttons
            buttons.Children.Add(new Border { Width = 1, Height = 20, Background = Line, Margin = new Thickness(2, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            buttons.Children.Add(WindowButtons());
            Grid.SetColumn(buttons, 2);
            header.Children.Add(buttons);

            Grid.SetRow(header, 0);
            g.Children.Add(header);

            // row 1: status card
            var statusCard = new Border { Margin = new Thickness(24, 14, 24, 0), Background = Card, CornerRadius = new CornerRadius(18), Padding = new Thickness(20, 14, 20, 16) };
            var sg = new Grid();
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new StackPanel();
            lblGame = Tb("", 11.5, Sub, false, null);
            txtGameVersion = Tb("-", 21, Txt, true, Mono);
            txtGameVersion.Margin = new Thickness(0, 6, 0, 0);
            left.Children.Add(lblGame);
            left.Children.Add(txtGameVersion);
            Grid.SetColumn(left, 0);
            sg.Children.Add(left);

            var right = new StackPanel { Margin = new Thickness(24, 0, 0, 0) };
            lblStatus = Tb("", 11.5, Sub, false, null);
            var statusRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dotStatus = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Dim, VerticalAlignment = VerticalAlignment.Center };
            txtStatus = Tb("", 13.5, Txt, false, Mono);
            txtStatus.Margin = new Thickness(10, 0, 0, 0);
            txtStatus.TextWrapping = TextWrapping.Wrap;
            txtStatus.TextTrimming = TextTrimming.None;
            Grid.SetColumn(dotStatus, 0);
            Grid.SetColumn(txtStatus, 1);
            statusRow.Children.Add(dotStatus);
            statusRow.Children.Add(txtStatus);
            right.Children.Add(lblStatus);
            right.Children.Add(statusRow);
            Grid.SetColumn(right, 1);
            sg.Children.Add(right);

            statusCard.Child = sg;
            Grid.SetRow(statusCard, 1);
            g.Children.Add(statusCard);

            // row 2: update banner (shown when a newer release exists)
            bannerBorder = new Border { Margin = new Thickness(24, 12, 24, 0), Background = Pink, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 10, 10, 10), Visibility = Visibility.Collapsed };
            var bg = new Grid();
            bg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var bIcon = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = Dark, Child = Glyph("\uE72C", 13, Pink) };
            Grid.SetColumn(bIcon, 0);
            bg.Children.Add(bIcon);
            txtBanner = Tb("", 13.5, Dark, true, null);
            txtBanner.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(txtBanner, 1);
            bg.Children.Add(txtBanner);
            var bBtn = TextButton(32, 11, 12.5, true, OnBannerClick);
            bBtn.Background = Dark;
            txtBannerBtn = (TextBlock)bBtn.Child;
            txtBannerBtn.Foreground = Pink;
            Grid.SetColumn(bBtn, 2);
            bg.Children.Add(bBtn);
            bannerBorder.Child = bg;
            Grid.SetRow(bannerBorder, 2);
            g.Children.Add(bannerBorder);

            // row 3: search
            var searchBox = new Border { Margin = new Thickness(24, 12, 24, 0), Background = Card, CornerRadius = new CornerRadius(14), Height = 42 };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var sIcon = Glyph("\uE721", 15, Sub);
            Grid.SetColumn(sIcon, 0);
            searchGrid.Children.Add(sIcon);
            txtSearch = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Txt,
                CaretBrush = Pink,
                SelectionBrush = Pink,
                FontSize = 14,
                FontFamily = Sans,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 14, 0)
            };
            txtSearch.TextChanged += delegate
            {
                txtPlaceholder.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
                RebuildList();
            };
            Grid.SetColumn(txtSearch, 1);
            searchGrid.Children.Add(txtSearch);
            txtPlaceholder = Tb("", 14, Dim, false, null);
            txtPlaceholder.IsHitTestVisible = false;
            Grid.SetColumn(txtPlaceholder, 1);
            searchGrid.Children.Add(txtPlaceholder);
            searchBox.Child = searchGrid;
            Grid.SetRow(searchBox, 3);
            g.Children.Add(searchBox);

            // row 4: the list of game versions (tabs) with our own scroll bar
            Grid list = BuildList();
            Grid.SetRow(list, 4);
            g.Children.Add(list);

            // row 5: actions
            var actions = new Grid { Margin = new Thickness(24, 6, 24, 22) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            btnInstall = TextButton(52, 16, 17, true, OnInstall);
            btnRemove = TextButton(52, 16, 15, false, OnRemove);
            btnRemove.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(btnInstall, 0);
            Grid.SetColumn(btnRemove, 1);
            actions.Children.Add(btnInstall);
            actions.Children.Add(btnRemove);
            Grid.SetRow(actions, 5);
            g.Children.Add(actions);

            return g;
        }

        /// <summary>The scrolling list plus a thin scroll bar that works with the mouse wheel and with dragging.</summary>
        private Grid BuildList()
        {
            listPanel = new StackPanel();
            listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,   // the wheel still works; we draw our own bar
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = listPanel
            };

            var host = new Grid { Margin = new Thickness(24, 14, 24, 0) };
            host.Children.Add(listScroll);

            scrollTrack = new Border
            {
                Width = 6,
                CornerRadius = new CornerRadius(3),
                Background = Card,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, -14, 2),
                Visibility = Visibility.Collapsed
            };
            scrollThumb = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = ThumbBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 40,
                Cursor = Cursors.Hand
            };
            scrollTrack.Child = scrollThumb;
            host.Children.Add(scrollTrack);

            double dragStartY = 0, dragStartOffset = 0;

            scrollThumb.MouseLeftButtonDown += (s, e) =>
            {
                scrollThumb.CaptureMouse();
                dragStartY = e.GetPosition(scrollTrack).Y;
                dragStartOffset = listScroll.VerticalOffset;
                e.Handled = true;
            };
            scrollThumb.MouseMove += (s, e) =>
            {
                if (!scrollThumb.IsMouseCaptured) return;
                double room = scrollTrack.ActualHeight - scrollThumb.ActualHeight;
                if (room <= 0) return;
                double dy = e.GetPosition(scrollTrack).Y - dragStartY;
                listScroll.ScrollToVerticalOffset(dragStartOffset + dy * (listScroll.ExtentHeight - listScroll.ViewportHeight) / room);
            };
            scrollThumb.MouseLeftButtonUp += (s, e) => { scrollThumb.ReleaseMouseCapture(); };
            scrollThumb.MouseEnter += (s, e) => { scrollThumb.Background = Sub; };
            scrollThumb.MouseLeave += (s, e) => { scrollThumb.Background = ThumbBrush; };

            // a click on the empty track scrolls by one page
            scrollTrack.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == scrollThumb) return;
                double y = e.GetPosition(scrollTrack).Y;
                double direction = y < scrollThumb.Margin.Top ? -1 : 1;
                listScroll.ScrollToVerticalOffset(listScroll.VerticalOffset + direction * listScroll.ViewportHeight);
                e.Handled = true;
            };

            listScroll.ScrollChanged += (s, e) => { UpdateScrollThumb(); };
            host.SizeChanged += (s, e) => { UpdateScrollThumb(); };

            return host;
        }

        private void UpdateScrollThumb()
        {
            double extent = listScroll.ExtentHeight;
            double view = listScroll.ViewportHeight;
            if (view <= 0 || extent <= view + 0.5)
            {
                scrollTrack.Visibility = Visibility.Collapsed;
                return;
            }

            scrollTrack.Visibility = Visibility.Visible;
            double trackHeight = scrollTrack.ActualHeight > 0 ? scrollTrack.ActualHeight : view - 4;
            double thumbHeight = Math.Max(36, trackHeight * view / extent);
            double room = Math.Max(0, trackHeight - thumbHeight);
            double top = room * listScroll.VerticalOffset / (extent - view);

            scrollThumb.Height = thumbHeight;
            scrollThumb.Margin = new Thickness(0, top, 0, 0);
        }

        // ---------------------------------------------------------------- settings page

        private Border SettingsRow(string glyph, out TextBlock label, UIElement control)
        {
            TextBlock unused;
            return SettingsRow(glyph, out label, out unused, control, false);
        }

        private Border SettingsRow(string glyph, out TextBlock label, out TextBlock sub, UIElement control, bool withSub)
        {
            var row = new Border { Background = Card, CornerRadius = new CornerRadius(18), Height = 76, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(16, 0, 16, 0) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tile = Tile(glyph);
            Grid.SetColumn(tile, 0);
            g.Children.Add(tile);
            label = Tb("", 15, Txt, true, null);
            sub = null;
            if (withSub)
            {
                var texts = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                texts.Children.Add(label);
                sub = Tb("", 11.5, Sub, false, null);
                sub.TextWrapping = TextWrapping.Wrap;
                sub.TextTrimming = TextTrimming.None;
                sub.Margin = new Thickness(0, 3, 0, 0);
                texts.Children.Add(sub);
                Grid.SetColumn(texts, 1);
                g.Children.Add(texts);
            }
            else
            {
                label.Margin = new Thickness(14, 0, 0, 0);
                Grid.SetColumn(label, 1);
                g.Children.Add(label);
            }
            var c = (FrameworkElement)control;
            c.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(c, 2);
            g.Children.Add(c);
            row.Child = g;
            return row;
        }

        private Grid BuildSettingsPage()
        {
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var back = IconButton("\uE72B", delegate { ShowPage(Screen.Main); });
            Grid header = PageHeader(back, out txtSettingsTitle);
            Grid.SetRow(header, 0);
            g.Children.Add(header);

            var body = new StackPanel { Margin = new Thickness(24, 24, 24, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetRow(body, 1);

            // language dropdown
            var langBox = new Border { Width = 136, Height = 40, CornerRadius = new CornerRadius(12), Background = Card2 };
            var lg = new Grid();
            lg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            txtLangValue = Tb("", 14, Txt, false, null);
            txtLangValue.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(txtLangValue, 0);
            lg.Children.Add(txtLangValue);
            var chev = Glyph("\uE70D", 11, Sub);
            Grid.SetColumn(chev, 1);
            lg.Children.Add(chev);
            langBox.Child = lg;

            var popupItems = new StackPanel();
            string[] names = { "English", "Русский" };
            for (int n = 0; n < 2; n++)
            {
                int index = n;
                var item = new Border { Width = 128, Height = 36, CornerRadius = new CornerRadius(9), Background = Brushes.Transparent, Child = Tb(names[n], 14, Txt, false, null) };
                ((TextBlock)item.Child).Margin = new Thickness(12, 0, 0, 0);
                item.MouseEnter += delegate { item.Background = Card; };
                item.MouseLeave += delegate { item.Background = Brushes.Transparent; };
                Wire(item, delegate { SetLanguage(index); langPopup.IsOpen = false; });
                popupItems.Children.Add(item);
            }
            langPopup = new Popup
            {
                PlacementTarget = langBox,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 6,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = new Border { Background = Card2, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(4), Child = popupItems }
            };
            langPopup.Closed += delegate { langClosedTick = Environment.TickCount; };
            Wire(langBox, delegate
            {
                if (Environment.TickCount - langClosedTick < 250) return;   // this click just closed it
                langPopup.IsOpen = true;
            });
            body.Children.Add(SettingsRow("\uE774", out lblLang, langBox));

            // auto-update checkbox
            autoBox = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1.6) };
            autoGlyph = Glyph("\uE73E", 14, Dark);
            autoBox.Child = autoGlyph;
            Wire(autoBox, delegate
            {
                Settings.AutoUpdate = !Settings.AutoUpdate;
                Settings.Save();
                UpdateAutoBox();
            });
            body.Children.Add(SettingsRow("\uE72C", out lblAuto, autoBox));

            // two-monitor mode (experimental): the game stays in exclusive fullscreen while a window on another monitor is active
            dualBox = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1.6) };
            dualGlyph = Glyph("\uE73E", 14, Dark);
            dualBox.Child = dualGlyph;
            Wire(dualBox, ToggleDualMonitor);
            var dualRow = SettingsRow("\uE7F4", out lblDual, out lblDualSub, dualBox, true);
            dualRow.Height = double.NaN;
            dualRow.MinHeight = 76;
            dualRow.Padding = new Thickness(16, 12, 16, 12);
            body.Children.Add(dualRow);

            // check for updates button
            var check = new Border { Height = 48, CornerRadius = new CornerRadius(14), Background = Card2, Margin = new Thickness(0, 0, 0, 12) };
            var checkRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var checkIcon = Glyph("\uE895", 15, Pink);
            checkIcon.Margin = new Thickness(0, 0, 10, 0);
            txtCheck = Tb("", 14.5, Txt, true, null);
            checkRow.Children.Add(checkIcon);
            checkRow.Children.Add(txtCheck);
            check.Child = checkRow;
            Wire(check, delegate { CheckForUpdates(true); });
            body.Children.Add(check);

            // game folder
            var folderCard = new Border { Background = Card, CornerRadius = new CornerRadius(18), Padding = new Thickness(16) };
            var fs = new StackPanel();
            var fhead = new StackPanel { Orientation = Orientation.Horizontal };
            fhead.Children.Add(Tile("\uE8B7"));
            lblFolder = Tb("", 15, Txt, true, null);
            lblFolder.Margin = new Thickness(14, 0, 0, 0);
            fhead.Children.Add(lblFolder);
            fs.Children.Add(fhead);

            var frow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            frow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            frow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var pathBox = new Border { Height = 38, CornerRadius = new CornerRadius(11), Background = Card2, Padding = new Thickness(14, 0, 14, 0) };
            txtPath = Tb("", 12, Sub, false, Mono);
            pathBox.Child = txtPath;
            Grid.SetColumn(pathBox, 0);
            frow.Children.Add(pathBox);
            var browse = new Border { Width = 46, Height = 38, CornerRadius = new CornerRadius(11), Background = Pink, Margin = new Thickness(8, 0, 0, 0), Child = Glyph("\uE8B7", 15, Dark) };
            Wire(browse, BrowseFolder);
            Grid.SetColumn(browse, 1);
            frow.Children.Add(browse);
            fs.Children.Add(frow);
            folderCard.Child = fs;
            body.Children.Add(folderCard);

            g.Children.Add(body);
            return g;
        }

        // ---------------------------------------------------------------- about page

        private Grid BuildAboutPage()
        {
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var back = IconButton("\uE72B", delegate { ShowPage(Screen.Main); });
            Grid header = PageHeader(back, out txtAboutTitle);
            Grid.SetRow(header, 0);
            g.Children.Add(header);

            var body = new Grid();
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(body, 1);

            var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 40) };
            var logo = Logo(104, 26, 52, 3.2);
            logo.HorizontalAlignment = HorizontalAlignment.Center;
            center.Children.Add(logo);
            var name = Tb("osu!lazer Fix", 24, Txt, true, null);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 22, 0, 0);
            center.Children.Add(name);
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            var ver = Tb("v" + v.ToString(3), 15, Sub, false, Mono);
            ver.HorizontalAlignment = HorizontalAlignment.Center;
            ver.Margin = new Thickness(0, 8, 0, 0);
            center.Children.Add(ver);
            var by = Tb("by iwili", 16, Txt, false, null);
            by.HorizontalAlignment = HorizontalAlignment.Center;
            by.Margin = new Thickness(0, 8, 0, 0);
            center.Children.Add(by);
            var gh = TextButton(48, 16, 15, true, delegate { OpenUrl(GitHubUrl); });
            gh.Width = 180;
            gh.Margin = new Thickness(0, 28, 0, 0);
            gh.Background = Card2;
            ((TextBlock)gh.Child).Foreground = Txt;
            ((TextBlock)gh.Child).Text = "GitHub";
            center.Children.Add(gh);
            body.Children.Add(center);

            txtAboutNote = Tb("", 12, Dim, false, null);
            txtAboutNote.HorizontalAlignment = HorizontalAlignment.Center;
            txtAboutNote.Margin = new Thickness(0, 0, 0, 30);
            Grid.SetRow(txtAboutNote, 1);
            body.Children.Add(txtAboutNote);

            g.Children.Add(body);
            return g;
        }

        // ---------------------------------------------------------------- dialog overlay

        private Grid BuildOverlay()
        {
            var g = new Grid { Background = new SolidColorBrush(Color.FromArgb(176, 0, 0, 0)), Visibility = Visibility.Collapsed };
            Panel.SetZIndex(g, 100);

            var card = new Border { Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Margin = new Thickness(26), Padding = new Thickness(22), VerticalAlignment = VerticalAlignment.Center };
            var sp = new StackPanel();
            dlgTitle = Tb("", 17, Txt, true, null);
            sp.Children.Add(dlgTitle);

            dlgText = Tb("", 13.5, Txt, false, null);
            dlgText.TextWrapping = TextWrapping.Wrap;
            dlgText.TextTrimming = TextTrimming.None;
            dlgText.VerticalAlignment = VerticalAlignment.Top;
            sp.Children.Add(new ScrollViewer { Margin = new Thickness(0, 14, 0, 0), MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = dlgText });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            btnDlgCancel = TextButton(44, 14, 14, false, delegate { overlay.Visibility = Visibility.Collapsed; dlgOnOk = null; });
            SetLook(btnDlgCancel, Look.Secondary);
            btnDlgCancel.Margin = new Thickness(0, 0, 10, 0);
            btnDlgOk = TextButton(44, 14, 14, true, delegate
            {
                overlay.Visibility = Visibility.Collapsed;
                Action a = dlgOnOk;
                dlgOnOk = null;
                if (a != null) a();
            });
            SetLook(btnDlgOk, Look.Primary);
            buttons.Children.Add(btnDlgCancel);
            buttons.Children.Add(btnDlgOk);
            sp.Children.Add(buttons);

            card.Child = sp;
            g.Children.Add(card);
            return g;
        }

        private void ShowDialog(string title, string text, string okText, string cancelText, Action onOk)
        {
            dlgTitle.Text = title;
            dlgText.Text = text;
            btnDlgOk.Visibility = Visibility.Visible;
            ((TextBlock)btnDlgOk.Child).Text = okText;
            btnDlgCancel.Visibility = cancelText == null ? Visibility.Collapsed : Visibility.Visible;
            if (cancelText != null) ((TextBlock)btnDlgCancel.Child).Text = cancelText;
            dlgOnOk = onOk;
            overlay.Visibility = Visibility.Visible;
        }

        /// <summary>A dialog without buttons while something is downloading. Change the text with dlgText.Text.</summary>
        private void ShowBusy(string title, string text)
        {
            dlgTitle.Text = title;
            dlgText.Text = text;
            btnDlgOk.Visibility = Visibility.Collapsed;
            btnDlgCancel.Visibility = Visibility.Collapsed;
            dlgOnOk = null;
            overlay.Visibility = Visibility.Visible;
        }

        // =====================================================================  language and status

        private void SetLanguage(int index)
        {
            Loc.Lang = index;
            Settings.Save();
            ApplyLanguage();
            UpdateStatus();
        }

        private void ApplyLanguage()
        {
            txtTitle.Text = Loc.T("title");
            lblGame.Text = Loc.T("lbl.game");
            lblStatus.Text = Loc.T("lbl.status");
            txtPlaceholder.Text = Loc.T("search");
            ((TextBlock)btnRemove.Child).Text = Loc.T("btn.remove");
            txtSettingsTitle.Text = Loc.T("settings");
            txtAboutTitle.Text = Loc.T("about");
            lblLang.Text = Loc.T("set.lang");
            lblAuto.Text = Loc.T("set.auto");
            lblDual.Text = Loc.T("set.dual");
            lblDualSub.Text = Loc.T("set.dualSub");
            lblFolder.Text = Loc.T("set.folder");
            txtCheck.Text = Loc.T("set.check");
            txtLangValue.Text = Loc.Lang == 1 ? "Русский" : "English";
            txtAboutNote.Text = Loc.T("about.note");
            UpdateBanner();
        }

        private void UpdateDualBox()
        {
            bool on = DualMonitor.IsOn();
            dualBox.Background = on ? (Brush)Pink : Card2;
            dualBox.BorderBrush = on ? (Brush)Pink : Line;
            dualGlyph.Visibility = on ? Visibility.Visible : Visibility.Hidden;
        }

        private void ToggleDualMonitor()
        {
            // the game reads the setting when it starts and rewrites framework.ini when it closes
            if (Patcher.IsGameRunning())
            {
                ShowDialog(Loc.T("msg.error"), Loc.T("err.running"), Loc.T("btn.ok"), null, null);
                return;
            }

            bool turnOn = !DualMonitor.IsOn();
            dualBox.IsEnabled = false;

            Task.Run(() => DualMonitor.Set(turnOn)).ContinueWith(t =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    dualBox.IsEnabled = true;
                    UpdateDualBox();

                    if (t.Status != TaskStatus.RanToCompletion)
                    {
                        string why = t.Exception == null ? "" : t.Exception.GetBaseException().Message;
                        ShowDialog(Loc.T("msg.error"), Loc.T("dual.fail", why), Loc.T("btn.ok"), null, null);
                        return;
                    }

                    ShowDialog(Loc.T("set.dual"), Loc.T(turnOn ? "dual.on" : "dual.off"), Loc.T("btn.ok"), null, null);
                }));
            });
        }

        private void UpdateAutoBox()
        {
            bool on = Settings.AutoUpdate;
            autoBox.Background = on ? (Brush)Pink : Card2;
            autoBox.BorderBrush = on ? (Brush)Pink : Line;
            autoGlyph.Visibility = on ? Visibility.Visible : Visibility.Hidden;
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

            txtPath.Text = string.IsNullOrEmpty(currentPath) ? "-" : currentPath;
            txtGameVersion.Text = game == null ? "-" : game.Version;

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
            txtStatus.Text = Loc.T(statusKey);

            // a small green dot that glows while a patch is installed
            bool patched = state == InstallState.Online || state == InstallState.PartialOffline;
            dotStatus.Background = patched ? (Brush)Mint : Dim;
            dotStatus.Effect = patched ? new DropShadowEffect { Color = Color.FromRgb(146, 224, 178), BlurRadius = 14, ShadowDepth = 0, Opacity = 1.0 } : null;
            txtStatus.Foreground = patched ? (Brush)Mint : Txt;

            // the list of patches comes from what is embedded in the exe
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

            // all version tabs start closed; the user opens the one they need
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

        private void UpdateButtons()
        {
            PatchItem sel = Selected();
            bool canInstall = game != null && patch != null && sel != null && sel.Enabled;
            bool installed = canInstall && IsInstalled(sel);
            bool outdated = installed && !installedCurrent;   // an older build of this patch is installed: offer the new one

            ((TextBlock)btnInstall.Child).Text = Loc.T(outdated ? "btn.update" : (installed ? "btn.installed" : "btn.install"));
            SetLook(btnInstall, outdated ? Look.Primary : (installed ? Look.Done : (canInstall ? Look.Primary : Look.Disabled)));

            bool patched = state == InstallState.Online || state == InstallState.PartialOffline;
            if (patched) SetLook(btnRemove, Look.Danger);                              // glows soft red while a patch is installed
            else if (Patcher.BackupExists(game)) SetLook(btnRemove, Look.Secondary);   // only an old backup is left to clean up
            else SetLook(btnRemove, Look.Disabled);
        }

        // =====================================================================  version tabs and patch cards

        private void RebuildList()
        {
            listPanel.Children.Clear();
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

                listPanel.Children.Add(MakeGroup(version, group, q.Length > 0 || expanded.Contains(version)));
            }
        }

        private StackPanel MakeGroup(string version, List<PatchItem> group, bool open)
        {
            bool hasInstalled = group.Any(i => IsInstalled(i));
            bool usable = group.Any(i => i.Enabled);

            var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var head = new Border
            {
                Height = 64,
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(20, 0, 18, 0),
                Background = hasInstalled ? (Brush)MintTint : Card,
                BorderBrush = hasInstalled ? (Brush)MintLine : Brushes.Transparent,
                BorderThickness = new Thickness(1)
            };
            var hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Brush textColor = hasInstalled ? (Brush)Mint : (usable ? (Brush)Txt : Dim);
            hg.Children.Add(Tb(version, 18, textColor, true, Mono));

            // the arrow tells people that the tab can be opened
            var arrow = Glyph(open ? "\uE70E" : "\uE70D", 12, hasInstalled ? (Brush)Mint : (usable ? Sub : Dim));
            Grid.SetColumn(arrow, 1);
            hg.Children.Add(arrow);
            head.Child = hg;

            Wire(head, delegate
            {
                if (expanded.Contains(version)) expanded.Remove(version); else expanded.Add(version);
                RebuildList();
            });
            wrap.Children.Add(head);

            if (open)
            {
                var body = new StackPanel { Margin = new Thickness(10, 8, 10, 0) };
                foreach (PatchItem it in group) body.Children.Add(MakeCard(it));
                wrap.Children.Add(body);
            }
            return wrap;
        }

        private Border MakeCard(PatchItem it)
        {
            bool installed = IsInstalled(it);
            bool sel = it.Key == selectedKey && it.Enabled;
            bool dim = !it.Enabled;

            Brush bg = installed ? (Brush)Mint : (sel ? (Brush)Pink : Card2);
            Brush titleColor = installed ? (Brush)MintDark : (sel ? (Brush)Dark : (dim ? (Brush)Dim : Txt));
            Brush subColor = installed ? (Brush)MintSub : (sel ? (Brush)PinkSub : (dim ? (Brush)Dim : Sub));

            var card = new Border
            {
                Height = 72,
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(20, 0, 16, 0),
                Background = bg
            };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // a small cloud: this patch will be downloaded when you install it
            if (it.Download && !installed)
            {
                var cloud = Glyph("\uE753", 15, subColor);
                cloud.Margin = new Thickness(0, 0, 14, 0);
                cloud.ToolTip = Loc.T("card.download");
                Grid.SetColumn(cloud, 1);
                g.Children.Add(cloud);
            }

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(Tb(it.Title, 16.5, titleColor, true, null));
            var sub = Tb(it.Sub, 12, subColor, false, Mono);
            sub.Margin = new Thickness(0, 4, 0, 0);
            texts.Children.Add(sub);
            Grid.SetColumn(texts, 0);
            g.Children.Add(texts);

            var mark = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), VerticalAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1.8) };
            if (installed)
            {
                mark.Background = MintDark;
                mark.BorderBrush = MintDark;
                mark.Child = Glyph("\uE73E", 12, Mint);
            }
            else if (sel)
            {
                mark.Background = Dark;
                mark.BorderBrush = Dark;
                mark.Child = Glyph("\uE73E", 12, Pink);
            }
            else
            {
                mark.BorderBrush = dim ? Dim : Br(84, 82, 90);
            }
            Grid.SetColumn(mark, 2);
            g.Children.Add(mark);

            card.Child = g;

            if (it.Enabled)
            {
                Wire(card, delegate
                {
                    selectedKey = it.Key;
                    RebuildList();
                    UpdateButtons();
                });
            }
            return card;
        }

        // =====================================================================  actions

        private void OnInstall()
        {
            PatchItem it = Selected();
            if (game == null || patch == null || it == null || !it.Enabled || (IsInstalled(it) && installedCurrent)) return;

            string text = Loc.T("disc.text");
            if (it.Edition == Patcher.EditionOffline) text += "\n\n" + Loc.T("disc.offline");
            text += "\n\n" + Loc.T("disc.ask");

            GameInfo g = game;
            PatchSet set = patch;
            ShowDialog(Loc.T("disc.title"), text, Loc.T("btn.install"), Loc.T("btn.cancel"), delegate
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
                Dispatcher.BeginInvoke(new Action(() => { dlgText.Text = Loc.T("dl.text", pct); }));
            })).ContinueWith(t =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted)
                    {
                        Exception ex = t.Exception.GetBaseException();
                        ShowDialog(Loc.T("msg.error"), Loc.T("dl.fail", ex.Message), Loc.T("btn.ok"), null, null);
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

            ShowDialog(Loc.T("confirmRevert.title"), Loc.T("confirmRevert"), Loc.T("btn.remove"), Loc.T("btn.cancel"), delegate
            {
                RunOperation(delegate { Patcher.Revert(game, Log); }, Loc.T("log.revertDone"));
            });
        }

        private void RunOperation(Action action, string doneText)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            opLog.Clear();
            try
            {
                action();
                ShowDialog(Loc.T("msg.done"), doneText, Loc.T("btn.ok"), null, null);
            }
            catch (UserException ex)
            {
                if (ex.NeedsAdmin && !IsAdministrator())
                    ShowDialog(Loc.T("msg.error"), ex.Message, Loc.T("btnAdmin"), Loc.T("btn.cancel"), RestartAsAdmin);
                else
                    ShowDialog(Loc.T("msg.error"), ex.Message, Loc.T("btn.ok"), null, null);
            }
            catch (Exception ex)
            {
                ShowDialog(Loc.T("msg.error"), ex.Message, Loc.T("btn.ok"), null, null);
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
                    ShowDialog(Loc.T("msg.error"), Loc.T("log.gameNotFound"), Loc.T("btn.ok"), null, null);
                }
                else
                {
                    Settings.Folder = currentPath;
                    Settings.Save();
                }
            }
        }

        // =====================================================================  online list and program updates

        /// <summary>
        /// Downloads the online list (patches + newest program version).
        /// manual = the user pressed "Check for updates": always answer with a dialog.
        /// Otherwise (at start) only the list and the banner change, and with Auto-update on the program updates itself.
        /// </summary>
        private void CheckForUpdates(bool manual)
        {
            if (checking) return;
            checking = true;
            if (manual) txtCheck.Text = "...";

            Task.Run(() => Online.Fetch()).ContinueWith(t =>
            {
                Manifest m = t.Status == TaskStatus.RanToCompletion ? t.Result : null;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    checking = false;
                    txtCheck.Text = Loc.T("set.check");

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
                        ShowDialog(Loc.T("upd.title"), Loc.T("upd.fail"), Loc.T("btn.ok"), null, null);
                    else if (appStaged)
                        ShowDialog(Loc.T("upd.title"), Loc.T("upd.ready", appUpdate == null ? "" : appUpdate.Version.ToString(3)), Loc.T("btn.restart"), Loc.T("btn.later"), Online.Restart);
                    else if (appUpdate != null)
                        ShowDialog(Loc.T("upd.title"), Loc.T("upd.new", appUpdate.Version.ToString(3)), Loc.T("btn.update"), Loc.T("btn.cancel"), delegate { UpdateProgram(false); });
                    else
                        ShowDialog(Loc.T("upd.title"), Loc.T("upd.latest"), Loc.T("btn.ok"), null, null);
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
                Dispatcher.BeginInvoke(new Action(() => { dlgText.Text = Loc.T("upd.downloading", pct); }));
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
                        ShowDialog(Loc.T("msg.error"), text, Loc.T("upd.page"), Loc.T("btn.cancel"), delegate { OpenUrl(ReleasesUrl); });
                        return;
                    }

                    appStaged = true;
                    UpdateBanner();
                    if (!silent)
                        ShowDialog(Loc.T("upd.title"), Loc.T("upd.ready", app.Version.ToString(3)), Loc.T("btn.restart"), Loc.T("btn.later"), Online.Restart);
                }));
            });
        }

        /// <summary>The pink banner under the status card: "update available" or "update installed, restart".</summary>
        private void UpdateBanner()
        {
            if (appUpdate == null)
            {
                bannerBorder.Visibility = Visibility.Collapsed;
                return;
            }

            string v = appUpdate.Version.ToString(3);
            if (appStaged)
            {
                txtBanner.Text = Loc.T("update.ready", v);
                txtBannerBtn.Text = Loc.T("btn.restart");
            }
            else
            {
                txtBanner.Text = Loc.T("update.avail", v);
                txtBannerBtn.Text = updating ? "..." : Loc.T("btn.update");
            }
            bannerBorder.Visibility = Visibility.Visible;
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
    }
}
