using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AKCardScanner;

public class MainWindow : Window
{
    // The app is drawn on a fixed 1280×800 surface and scaled to the window, so its shape never changes.
    private const double W = 1280, H = 800, PanelW = 340, StageW = W - PanelW;
    private static readonly string[] Piles = { "A", "S", "M" };
    private static readonly Dictionary<string, string> PileName = new() { ["A"] = "Animal", ["S"] = "Strong", ["M"] = "Miracle" };

    private readonly List<Card> _all;
    private readonly Settings _settings = Settings.Load();
    private readonly PlayClient _play = new();

    private string _pile = "A", _version = "", _query = "";
    private readonly Dictionary<string, int> _idx = new() { ["A"] = 0, ["S"] = 0, ["M"] = 0 };
    private readonly Dictionary<string, Card> _hand = new() { ["A"] = null, ["S"] = null, ["M"] = null };
    private bool _scanning;
    private string _conn = "";

    // Controls
    private readonly Grid _root = new() { Width = W, Height = H, Background = AK.App, ClipToBounds = true, Focusable = true };
    private readonly Canvas _fan = new() { ClipToBounds = true };
    private readonly TextBlock _pos = new(), _empty = new();
    private readonly Button _prev, _next;
    private readonly DockPanel _detail = new();
    private readonly StackPanel _slots = new() { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _scan = new();
    private readonly TextBlock _scanLabel = new();
    private readonly Border _scanProg = new();
    private readonly Dictionary<string, Button> _tabs = new();
    private readonly Dictionary<string, TextBlock> _tabCounts = new();
    private readonly TextBox _search = new();
    private readonly ComboBox _ver = new();
    private readonly Button _manBtn = new(), _portBtn = new();
    private readonly Ellipse _headDot = Dot(), _popDot = Dot();
    private readonly TextBlock _portLabel = new();
    private readonly Border _portPop = new(), _manPop = new();
    private readonly TextBox _portIn = new(), _manIn = new();
    private readonly TextBlock _portStatus = new(), _manStatus = new();
    private readonly Border _toast = new();
    private readonly TextBlock _toastText = new();
    private IDisposable _toastTimer;

    // Fan state: one control per card so moves animate
    private readonly Dictionary<string, Border> _fanEls = new();
    private string _fanKey = "";
    private int? _lastSel;

    public MainWindow()
    {
        Title = "AK Card Scanner";
        Width = W; Height = H; MinWidth = 640; MinHeight = 400;
        Background = Brushes.Black;
        FontFamily = AK.Font;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _all = CardStore.Load();
        _play.Port = _settings.DefaultPort;
        _version = Array.IndexOf(CardStore.Versions, _settings.Version) >= 0 ? _settings.Version : "";

        _prev = RoundButton("M15 5l-7 7 7 7", "Previous card", () => Move(-1));
        _next = RoundButton("M9 5l7 7-7 7", "Next card", () => Move(1));

        _root.RowDefinitions = new RowDefinitions("77,*,124");
        var header = BuildHeader(); Grid.SetRow(header, 0); _root.Children.Add(header);
        var main = BuildMain(); Grid.SetRow(main, 1); _root.Children.Add(main);
        var footer = BuildFooter(); Grid.SetRow(footer, 2); _root.Children.Add(footer);
        BuildPopovers();
        BuildToast();

        Content = new Viewbox { Stretch = Stretch.Uniform, Child = _root };

        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        _root.AddHandler(PointerPressedEvent, OnRootPointer, RoutingStrategies.Tunnel);
        Opened += (_, _) => { SelectSeed(); Render(); _root.Focus(); };
        Closing += (_, _) => { _settings.DefaultPort = _play.Port; _settings.Version = _version; _settings.Save(); };
    }

    // ───────────────────────── Layout ─────────────────────────

    private Control BuildHeader()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0) };
        row.Children.Add(Heading("AK Card Scanner", 24));

        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var p in Piles)
        {
            var count = new TextBlock { FontWeight = FontWeight.Medium, Opacity = .75, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var label = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = PileName[p], FontWeight = FontWeight.SemiBold, FontStyle = FontStyle.Italic, Padding = new Thickness(0, 0, 4, 0) }, count } };
            var b = new Button { Content = label, Height = 40, Padding = new Thickness(16, 0), CornerRadius = new CornerRadius(10), FontSize = 15, VerticalContentAlignment = VerticalAlignment.Center, Focusable = false };
            string pile = p; b.Click += (_, _) => SetPile(pile);
            _tabs[p] = b; _tabCounts[p] = count; tabs.Children.Add(b);
        }
        row.Children.Add(tabs);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        // Search
        _search.Watermark = "Find a card…"; _search.Width = 190; _search.Height = 40; _search.FontSize = 14;
        _search.Background = AK.Field; _search.BorderBrush = AK.Line; _search.Foreground = AK.Ink; _search.CornerRadius = new CornerRadius(10);
        _search.VerticalContentAlignment = VerticalAlignment.Center; _search.Padding = new Thickness(12, 0);
        _search.TextChanged += (_, _) => { _query = _search.Text ?? ""; foreach (var p in Piles) _idx[p] = 0; Render(); };
        right.Children.Add(_search);
        // Version
        _ver.Items.Add("All versions"); foreach (var v in CardStore.Versions) _ver.Items.Add(v);
        _ver.SelectedIndex = _version == "" ? 0 : Array.IndexOf(CardStore.Versions, _version) + 1;
        _ver.Height = 40; _ver.CornerRadius = new CornerRadius(10); _ver.FontSize = 14; _ver.FontWeight = FontWeight.Bold;
        _ver.Background = AK.Brown; _ver.BorderBrush = AK.Gold; _ver.Foreground = AK.Gold; _ver.Padding = new Thickness(12, 0, 8, 0);
        _ver.VerticalContentAlignment = VerticalAlignment.Center; _ver.Focusable = false;
        _ver.SelectionChanged += (_, _) => OnVersion();
        right.Children.Add(_ver);
        // Manual barcode (icon only)
        _manBtn.Content = Glyph("M3 5v14M6 5v14M9.5 5v14M13 5v14M15.5 5v14M18 5v14M21 5v14", 20, AK.Muted, 1.8);
        StyleHeaderButton(_manBtn); _manBtn.Width = 40; _manBtn.Padding = new Thickness(0); ToolTip.SetTip(_manBtn, "Enter a barcode");
        _manBtn.Click += (_, _) => ToggleMan(!_manPop.IsVisible);
        right.Children.Add(_manBtn);
        // Port
        _portLabel.Text = _play.Port.ToString();
        _portBtn.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center,
            Children = { _headDot, new TextBlock { Text = "Play! ·", VerticalAlignment = VerticalAlignment.Center }, _portLabel, Glyph("M6 9l6 6 6-6", 12, AK.Ink, 2.4) }
        };
        StyleHeaderButton(_portBtn);
        _portBtn.Click += (_, _) => TogglePort(!_portPop.IsVisible);
        right.Children.Add(_portBtn);

        var dock = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(row, Avalonia.Controls.Dock.Left); DockPanel.SetDock(right, Avalonia.Controls.Dock.Right);
        right.Margin = new Thickness(0, 0, 24, 0);
        dock.Children.Add(row); dock.Children.Add(right);

        return new Grid
        {
            RowDefinitions = new RowDefinitions("72,5"),
            Children =
            {
                new Border { Background = AK.Vertical("#4A2814", "#2B160A"), Child = dock },
                Put(new Border { Background = AK.Fire }, 1)
            }
        };
    }

    private Control BuildMain()
    {
        var stage = new Grid { Background = AK.Lightning, ClipToBounds = true };
        stage.Children.Add(_fan);
        _empty.Text = "No cards match"; _empty.FontSize = 20; _empty.FontWeight = FontWeight.Bold; _empty.FontStyle = FontStyle.Italic; _empty.Padding = new Thickness(0, 0, 4, 0);
        _empty.Foreground = AK.B("#5E1A05"); _empty.HorizontalAlignment = HorizontalAlignment.Center; _empty.VerticalAlignment = VerticalAlignment.Center; _empty.IsVisible = false;
        stage.Children.Add(_empty);
        _pos.FontSize = 14; _pos.FontWeight = FontWeight.Bold; _pos.Foreground = AK.B("#3A1405"); _pos.TextAlignment = TextAlignment.Center;
        var nav = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 18),
            Children = { _prev, new Border { Background = AK.B("#BFFFF6E5"), CornerRadius = new CornerRadius(999), Padding = new Thickness(14, 4), MinWidth = 84, VerticalAlignment = VerticalAlignment.Center, Child = _pos }, _next }
        };
        stage.Children.Add(nav);

        var aside = new Border { Background = AK.Brown2, BorderBrush = AK.B("#4A2E19"), BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(28), Child = _detail };

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{PanelW}") };
        g.Children.Add(stage); Grid.SetColumn(aside, 1); g.Children.Add(aside);
        return g;
    }

    private Control BuildFooter()
    {
        _scanProg.Height = 5; _scanProg.Width = 0; _scanProg.Background = AK.B("#FFE36B"); _scanProg.HorizontalAlignment = HorizontalAlignment.Left; _scanProg.VerticalAlignment = VerticalAlignment.Bottom;
        _scanLabel.FontSize = 20; _scanLabel.FontWeight = FontWeight.Bold; _scanLabel.FontStyle = FontStyle.Italic; _scanLabel.Padding = new Thickness(0, 0, 4, 0); _scanLabel.HorizontalAlignment = HorizontalAlignment.Center; _scanLabel.VerticalAlignment = VerticalAlignment.Center;
        _scan.Content = new Grid { Children = { _scanLabel, _scanProg } };
        _scan.Height = 64; _scan.MinWidth = 200; _scan.CornerRadius = new CornerRadius(14); _scan.Padding = new Thickness(0); _scan.Focusable = false;
        _scan.HorizontalContentAlignment = HorizontalAlignment.Stretch; _scan.VerticalContentAlignment = VerticalAlignment.Stretch; _scan.ClipToBounds = true;
        _scan.Click += async (_, _) => await Scan();

        var dock = new DockPanel { Margin = new Thickness(24, 0) };
        DockPanel.SetDock(_scan, Avalonia.Controls.Dock.Right); _scan.VerticalAlignment = VerticalAlignment.Center;
        dock.Children.Add(_scan); dock.Children.Add(_slots);
        return new Border { Background = AK.Vertical("#2B160A", "#0D0704"), BorderBrush = AK.GoldDeep, BorderThickness = new Thickness(0, 3, 0, 0), Child = dock };
    }

    private void BuildPopovers()
    {
        // Play! connection
        _portIn.Text = _play.Port.ToString(); StyleField(_portIn);
        _portIn.TextChanged += (_, _) =>
        {
            var digits = new string((_portIn.Text ?? "").Where(char.IsDigit).Take(5).ToArray());
            if (digits != _portIn.Text) { _portIn.Text = digits; _portIn.CaretIndex = digits.Length; }
            SetConn("", "Not tested yet");
        };
        var test = PopButton("Test connection", false); test.Click += async (_, _) => await TestPort();
        var save = PopButton("Save", true); save.Click += (_, _) => SavePort();
        _portStatus.FontSize = 13; _portStatus.TextWrapping = TextWrapping.Wrap; _portStatus.Text = "Not tested yet";
        FillPop(_portPop, 310, new Thickness(0, 84, 24, 0), new Control[]
        {
            Heading("Play! connection", 20),
            Small("Match the Arcade I/O HTTP Server port set in Play! under Settings → General."),
            Labeled("Port", _portIn),
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { test, Put(save, 0, 2) } },
            new DockPanel { Children = { Docked(new Border { Margin = new Thickness(0, 5, 8, 0), VerticalAlignment = VerticalAlignment.Top, Child = _popDot }, Avalonia.Controls.Dock.Left), _portStatus } }
        });

        // Manual barcode
        StyleField(_manIn); _manIn.FontFamily = new FontFamily("Menlo, Consolas, monospace");
        _manIn.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await SendManual(); } };
        var send = PopButton("Send", true); send.Width = 84; send.Click += async (_, _) => await SendManual();
        _manStatus.FontSize = 13; _manStatus.TextWrapping = TextWrapping.Wrap;
        FillPop(_manPop, 330, new Thickness(0, 84, 172, 0), new Control[]
        {
            Heading("Enter a barcode", 20),
            Small("Type a code, or click in the box and scan with a USB barcode scanner."),
            new DockPanel { Children = {  Docked(send, Avalonia.Controls.Dock.Right), new Border { Margin = new Thickness(0, 0, 8, 0), Child = _manIn } } },
            _manStatus
        });
    }

    private void BuildToast()
    {
        _toastText.FontSize = 14; _toastText.FontWeight = FontWeight.SemiBold; _toastText.Foreground = AK.Ink;
        _toast.Child = _toastText; _toast.Background = AK.B("#EB140B06"); _toast.BorderBrush = AK.GoldDeep; _toast.BorderThickness = new Thickness(1);
        _toast.CornerRadius = new CornerRadius(999); _toast.Padding = new Thickness(16, 8);
        _toast.HorizontalAlignment = HorizontalAlignment.Center; _toast.VerticalAlignment = VerticalAlignment.Top; _toast.Margin = new Thickness(0, 84, PanelW, 0);
        _toast.Opacity = 0; _toast.IsHitTestVisible = false; _toast.ZIndex = 200;
        _toast.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) } };
        Grid.SetRowSpan(_toast, 3); _root.Children.Add(_toast);
    }

    // ───────────────────────── Data ─────────────────────────

    private List<Card> Visible(string pile)
    {
        int vmax = CardStore.VersionIndex(_version);
        string q = _query.Trim();
        return _all.Where(c => c.Pile == pile
            && (_version == "" || (!string.IsNullOrEmpty(c.Version) && CardStore.VersionIndex(c.Version) <= vmax))
            && (q == "" || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Number_.Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private void SelectSeed()
    {
        foreach (var p in Piles)
        {
            var l = Visible(p);
            int i = l.FindIndex(c => c.Code == "bn28jw6A" || c.Code == "bn6lSz9C");
            _idx[p] = i >= 0 ? i : Math.Min(5, Math.Max(0, l.Count - 1));
        }
    }

    // ───────────────────────── Rendering ─────────────────────────

    private void Render() { RenderTabs(); RenderFan(); RenderSlots(); }

    private void RenderTabs()
    {
        foreach (var p in Piles)
        {
            bool on = p == _pile;
            var b = _tabs[p];
            b.Background = on ? AK.GoldButton : AK.B("#40000000");
            b.BorderBrush = on ? AK.Gold : AK.B("#8A6034"); b.BorderThickness = new Thickness(1);
            b.Foreground = on ? AK.Brown : AK.Ink;
            _tabCounts[p].Text = Visible(p).Count.ToString();
        }
    }

    private static (TransformOperations t, int z, double o) Pose(int d, double extraY = 0)
    {
        int ad = Math.Abs(d), cl = Math.Min(ad, 5), sg = d < 0 ? -1 : 1;
        double x = sg * (cl * 118 + Math.Max(0, ad - 5) * 60), y = cl * cl * 6 + cl * 10 + extraY, rot = sg * cl * 6, sc = d == 0 ? 1.1 : Math.Max(.7, .88 - cl * .03);
        var t = TransformOperations.Parse(FormattableString.Invariant($"scale({sc}) rotate({rot}deg) translate({x}px, {y}px)"));
        return (t, 40 - ad, ad > 4 ? 0 : 1);
    }

    private void RenderFan()
    {
        var list = Visible(_pile);
        int n = list.Count;
        if (_idx[_pile] >= n) _idx[_pile] = Math.Max(0, n - 1);
        int sel = _idx[_pile];
        _empty.IsVisible = n == 0;

        string key = $"{_pile}|{_version}|{_query}";
        bool fresh = key != _fanKey; _fanKey = key;
        if (fresh) { _fan.Children.Clear(); _fanEls.Clear(); _lastSel = null; }

        // The fan wraps: each card sits at its shortest distance round the loop from the centre
        const int R = 6;
        var offs = new List<(int i, int d)>();
        if (n <= 2 * R + 1) for (int j = 0; j < n; j++) { int dd = ((j - sel) % n + n) % n; if (dd > n / 2.0) dd -= n; offs.Add((j, dd)); }
        else for (int dd = -R; dd <= R; dd++) offs.Add((((sel + dd) % n + n) % n, dd));

        var keep = new HashSet<string>();
        foreach (var (i, d) in offs)
        {
            var c = list[i];
            keep.Add(c.Code);
            var target = Pose(d);
            if (!_fanEls.TryGetValue(c.Code, out var el))
            {
                el = new Border { Width = 220, Height = 314, RenderTransformOrigin = new RelativePoint(.5, 1.2, RelativeUnit.Relative), Cursor = new Cursor(StandardCursorType.Hand) };
                Canvas.SetLeft(el, StageW / 2 - 110); Canvas.SetTop(el, 64);
                el.PointerPressed += OnCardPressed;
                // enter from just beyond the edge it comes from, or rise into place on a fresh fan
                var from = fresh ? Pose(d, 40) : Pose(d + (d < 0 ? -2 : 2));
                el.RenderTransform = from.t; el.Opacity = 0; el.ZIndex = from.z;
                _fan.Children.Add(el); _fanEls[c.Code] = el;
                var delay = fresh ? TimeSpan.FromMilliseconds(Math.Abs(d) * 35) : TimeSpan.Zero;
                var e2 = el; var tgt = target;
                Dispatcher.UIThread.Post(() =>
                {
                    e2.Transitions = new Transitions
                    {
                        new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(460), Easing = new SplineEasing(.22, .9, .24, 1), Delay = delay },
                        new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(360), Delay = delay }
                    };
                    if (_fanEls.TryGetValue(c.Code, out var still) && still == e2) { e2.RenderTransform = PoseFor(e2).t; e2.Opacity = PoseFor(e2).o; }
                }, DispatcherPriority.Background);
            }
            el.Tag = (i, d, c);
            el.Child = CardFace(c, d == 0);
            el.ZIndex = target.z;
            if (el.Transitions != null) { el.RenderTransform = target.t; el.Opacity = target.o; }
        }

        // Cards leaving the window slide out past the edge, then go
        int delta = 0;
        if (_lastSel is int ls && n > 0) { delta = ((sel - ls) % n + n) % n; if (delta > n / 2.0) delta -= n; }
        foreach (var code in _fanEls.Keys.ToList())
        {
            if (keep.Contains(code)) continue;
            var el = _fanEls[code]; _fanEls.Remove(code);
            var (_, od, oc) = ((int, int, Card))el.Tag;
            int nd = od - delta;
            var p = Pose(nd + (nd < 0 ? -2 : 2));
            el.IsHitTestVisible = false;
            el.Child = CardFace(oc, false);
            el.RenderTransform = p.t; el.Opacity = 0;
            DispatcherTimer.RunOnce(() => _fan.Children.Remove(el), TimeSpan.FromMilliseconds(600));
        }
        _lastSel = sel;

        _pos.Text = n > 0 ? $"{sel + 1} / {n}" : "0 / 0";
        _prev.IsEnabled = _next.IsEnabled = n > 1;
        RenderDetail(n > 0 ? list[sel] : null);
    }

    private (TransformOperations t, int z, double o) PoseFor(Border el) => Pose((((int, int, Card))el.Tag).Item2);

    private Control CardFace(Card c, bool focus)
    {
        var rc = AK.Rarity(c.Rarity);
        var art = CardStore.ArtPath(c);
        Control face;
        if (art != null)
        {
            face = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, Child = new Image { Source = LoadBitmap(art), Stretch = Stretch.UniformToFill } };
        }
        else
        {
            var (s1, s2) = c.StatLine();
            var top = new DockPanel { Children = {  Docked(Chip(c.Rarity), Avalonia.Controls.Dock.Right), new TextBlock { Text = c.Number_, FontSize = 11, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center } } };
            var artBox = new Border
            {
                CornerRadius = new CornerRadius(8), Background = AK.Stripes(rc, 14),
                Child = new Path { Data = Geometry.Parse(AK.Icon(c.Pile)), Stroke = AK.Ink, StrokeThickness = 1.4, Width = 64, Height = 64, Stretch = Stretch.Uniform, Opacity = .5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            var name = new TextBlock { Text = c.Name, FontSize = 15, FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic, Padding = new Thickness(0, 0, 4, 0), TextWrapping = TextWrapping.Wrap, LineHeight = 17, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, Height = 35 };
            var stats = new DockPanel { Children = {  Docked(new TextBlock { Text = s2, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = AK.B("#F6E6C6") }, Avalonia.Controls.Dock.Right), new TextBlock { Text = s1, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = AK.B("#F6E6C6") } } };
            var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 8 };
            g.Children.Add(top); g.Children.Add(Put(artBox, 1)); g.Children.Add(Put(name, 2)); g.Children.Add(Put(stats, 3));
            face = new Border
            {
                CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(6), BorderBrush = new SolidColorBrush(rc),
                Background = AK.Vertical("#3E2414", "#170C05"), Padding = new Thickness(10), Child = g
            };
        }
        var shadow = focus
            ? BoxShadows.Parse("0 0 0 3 #FFF6E5, 0 0 24 6 #E6FFF096, 0 24 48 0 #8C5A1400")
            : BoxShadows.Parse("0 12 28 0 #735A1900");
        var host = new Border { CornerRadius = new CornerRadius(14), BoxShadow = shadow, Child = face };
        var wrap = new Panel { Children = { host } };
        if (_hand[c.Pile]?.Code == c.Code) wrap.Children.Add(SelectedBadge());
        return wrap;
    }

    private static Control SelectedBadge() => new Border
    {
        Background = AK.RedButton, BorderBrush = AK.Gold, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(999), Padding = new Thickness(10, 3),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -14, 0, 0),
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 4,
            Children = { Glyph("M5 12l5 5 9-10", 12, AK.Ink, 3), new TextBlock { Text = "SELECTED", FontSize = 11, FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic, Padding = new Thickness(0, 0, 4, 0), Foreground = AK.Ink, LetterSpacing = .6 } }
        }
    };

    private void RenderDetail(Card c)
    {
        _detail.Children.Clear();
        if (c == null) return;
        var body = new StackPanel { Spacing = 16 };
        var meta = new WrapPanel { ItemSpacing = 8 };
        var bits = new[] { c.Number_, c.Version ?? "" }.Where(x => x != "").ToList();
        for (int i = 0; i < bits.Count; i++) { if (i > 0) meta.Children.Add(MetaText("·")); meta.Children.Add(MetaText(bits[i])); }
        meta.Children.Add(Chip(c.Rarity));
        body.Children.Add(meta);

        // Name on one line: shrink from 30px to fit, down to 15px, then ellipsis
        var name = new TextBlock { Text = c.Name, FontSize = 30, FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic, Padding = new Thickness(0, 0, 5, 0), Foreground = AK.Gold, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        double avail = PanelW - 56;
        name.Measure(Size.Infinity);
        double w = name.DesiredSize.Width;
        if (w > avail) name.FontSize = Math.Max(15, Math.Floor(30 * avail / w));
        ToolTip.SetTip(name, c.Name);
        name.Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 2, BlurRadius = 3, Color = AK.C("#2B160A"), Opacity = 1 };
        body.Children.Add(name);

        var stats = new StackPanel { Spacing = 14 };
        var tags = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        if (c.Pile == "A")
        {
            stats.Children.Add(StatBar("Strength", c.Strength, 10000, "#FF7A59"));
            stats.Children.Add(StatBar("Health", c.Health, 10000, "#4FD394"));
            if (!string.IsNullOrEmpty(c.GoldAttack)) tags.Children.Add(TagChip("Gold Attack · " + c.GoldAttack, AK.Attack(c.GoldAttack), true));
            if (!string.IsNullOrEmpty(c.Kind)) tags.Children.Add(TagChip(c.Kind + " type"));
        }
        else if (c.Pile == "S")
        {
            stats.Children.Add(StatBar("Guts", c.Guts, 5, "#5AAEFF"));
            stats.Children.Add(StatBar("Tech", c.Tech, 5, "#4FD394"));
            stats.Children.Add(StatBar("Power", c.Power, 5, "#FF7A59"));
            if (!string.IsNullOrEmpty(c.Aura)) tags.Children.Add(TagChip("Aura · " + c.Aura));
            if (!string.IsNullOrEmpty(c.Focus)) tags.Children.Add(TagChip("Focus · " + c.Focus));
        }
        else
        {
            stats.Children.Add(StatBar("Miracle power", c.MiraclePower, 6000, "#A57BFF"));
            if (!string.IsNullOrEmpty(c.Effect)) tags.Children.Add(TagChip(c.Effect, AK.C("#A57BFF"), true));
            if (!string.IsNullOrEmpty(c.Kind)) tags.Children.Add(TagChip(c.Kind + " type"));
            if (!string.IsNullOrEmpty(c.Focus)) tags.Children.Add(TagChip("Focus · " + c.Focus));
        }
        body.Children.Add(stats);
        body.Children.Add(tags);

        bool inHand = _hand[c.Pile]?.Code == c.Code, occupied = _hand[c.Pile] != null;
        var select = new Button
        {
            Content = new TextBlock { Text = inHand ? "Deselect" : occupied ? "Swap in this card" : "Select this card", Padding = new Thickness(0, 0, 4, 0) },
            Height = 54, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 19, FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic, CornerRadius = new CornerRadius(12), Focusable = false,
            Background = inHand ? Brushes.Transparent : AK.GoldButton, Foreground = inHand ? AK.Gold : AK.Brown,
            BorderBrush = inHand ? AK.Gold : AK.Ink, BorderThickness = new Thickness(2)
        };
        select.Click += (_, _) => ToggleCurrent();
        _detail.Children.Add( Docked(select, Avalonia.Controls.Dock.Bottom));
        _detail.Children.Add(body);
    }

    private void RenderSlots()
    {
        _slots.Children.Clear();
        foreach (var p in Piles)
        {
            var c = _hand[p];
            var col = c == null ? AK.C("#6A4526") : AK.Rarity(c.Rarity);
            Control mini;
            if (c == null) mini = new Rectangle { Width = 56, Height = 80, RadiusX = 6, RadiusY = 6, Stroke = AK.Line, StrokeThickness = 2, StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 2 } };
            else
            {
                var art = CardStore.ArtPath(c);
                mini = new Border
                {
                    Width = 56, Height = 80, CornerRadius = new CornerRadius(6), BorderBrush = new SolidColorBrush(col), BorderThickness = new Thickness(art == null ? 3 : 0), ClipToBounds = true,
                    Background = AK.Vertical("#3E2414", "#170C05"), Padding = new Thickness(art == null ? 3 : 0),
                    Child = art != null ? new Image { Source = LoadBitmap(art), Stretch = Stretch.UniformToFill } : new Grid
                    {
                        RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 2,
                        Children =
                        {
                            new TextBlock { Text = c.Number_, FontSize = 7, FontWeight = FontWeight.Bold },
                            Put(new Border { CornerRadius = new CornerRadius(3), Background = AK.Stripes(col, 7, .25, .1) }, 1),
                            Put(new TextBlock { Text = c.Name, FontSize = 7, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, MaxLines = 2, LineHeight = 8 }, 2)
                        }
                    }
                };
            }
            var txt = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(new TextBlock { Text = PileName[p].ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = AK.Muted, LetterSpacing = .8 });
            txt.Children.Add(new TextBlock { Text = c?.Name ?? "None", FontSize = 15, FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic, Padding = new Thickness(0, 0, 4, 0), Foreground = c == null ? AK.Dim : AK.Ink, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 150 });
            if (c != null) { var (s1, _) = c.StatLine(); txt.Children.Add(new TextBlock { Text = c.Rarity + (s1 != "" ? " · " + s1 : ""), FontSize = 12, Foreground = AK.Muted }); }

            var row = new DockPanel();
            if (c != null)
            {
                var x = new Button
                {
                    Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Padding = new Thickness(0), Focusable = false, VerticalAlignment = VerticalAlignment.Top,
                    Background = Brushes.Transparent, BorderBrush = AK.B("#8A6034"), BorderThickness = new Thickness(1),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    Content = Glyph("M6 6l12 12M18 6L6 18", 14, AK.Muted, 2.4)
                };
                ToolTip.SetTip(x, "Remove " + c.Name);
                string pile = p; x.Click += (_, e) => { e.Handled = true; if (_scanning) return; _hand[pile] = null; RenderFan(); RenderSlots(); };
                row.Children.Add( Docked(x, Avalonia.Controls.Dock.Right));
            }
            row.Children.Add( Docked(mini, Avalonia.Controls.Dock.Left));
            txt.Margin = new Thickness(10, 0, 4, 0); row.Children.Add(txt);

            var slot = new Border
            {
                Width = 270, Height = 96, CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 8),
                BorderThickness = new Thickness(2), BorderBrush = c == null ? AK.Line : new SolidColorBrush(col),
                Background = c == null ? Brushes.Transparent : AK.Field, Child = row, Tag = p,
                Cursor = c == null ? Cursor.Default : new Cursor(StandardCursorType.Hand)
            };
            slot.PointerPressed += (_, _) => JumpToHand(slot.Tag as string);
            if (c == null)
            {
                slot.BorderThickness = new Thickness(0);
                _slots.Children.Add(new Panel { Tag = p, Children = { slot, new Rectangle { Width = 270, Height = 96, RadiusX = 12, RadiusY = 12, Stroke = AK.Line, StrokeThickness = 2, StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 2 }, IsHitTestVisible = false } } });
            }
            else _slots.Children.Add(slot);
        }
        int n = Picked().Count;
        _scan.Cursor = n > 0 && !_scanning ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        if (!_scanning) _scanLabel.Text = n > 0 ? $"Scan {n} card{(n > 1 ? "s" : "")}" : "Scan cards";
        bool live = n > 0 || _scanning;
        _scan.Background = live ? AK.RedButton : AK.B("#3A2414");
        _scan.BorderBrush = live ? AK.Gold : AK.Line; _scan.BorderThickness = new Thickness(2);
        _scanLabel.Foreground = live ? AK.Ink : AK.Dim;
    }

    // ───────────────────────── Actions ─────────────────────────

    private List<string> Picked() => Piles.Where(p => _hand[p] != null).ToList();

    private void Move(int d)
    {
        var n = Visible(_pile).Count; if (n < 2) return;
        _idx[_pile] = ((_idx[_pile] + d) % n + n) % n;
        RenderFan();
    }

    private void SetPile(string p) { _pile = p; Render(); }

    private void ToggleCurrent()
    {
        if (_scanning) return;
        var list = Visible(_pile); if (list.Count == 0) return;
        var c = list[_idx[_pile]];
        if (_hand[c.Pile]?.Code == c.Code) { _hand[c.Pile] = null; Toast(c.Name + " deselected"); }
        else { var prev = _hand[c.Pile]; _hand[c.Pile] = c; Toast(prev != null ? $"Swapped {prev.Name} for {c.Name}" : c.Name + " selected"); }
        RenderFan(); RenderSlots();
    }

    private void JumpToHand(string p)
    {
        if (p == null || _hand[p] == null) return;
        _pile = p;
        var i = Visible(p).FindIndex(c => c.Code == _hand[p].Code);
        if (i >= 0) _idx[p] = i;
        Render();
    }

    private void OnCardPressed(object sender, PointerPressedEventArgs e)
    {
        if (sender is not Border el || el.Tag is not ValueTuple<int, int, Card> t) return;
        e.Handled = true; _root.Focus();
        if (t.Item2 == 0) ToggleCurrent();
        else { _idx[_pile] = t.Item1; RenderFan(); }
    }

    private void OnVersion()
    {
        _version = _ver.SelectedIndex <= 0 ? "" : CardStore.Versions[_ver.SelectedIndex - 1];
        _settings.Version = _version; _settings.Save();
        var dropped = new List<string>();
        var saveQ = _query; _query = "";
        foreach (var p in Piles)
            if (_hand[p] != null && !Visible(p).Any(c => c.Code == _hand[p].Code)) { dropped.Add(_hand[p].Name); _hand[p] = null; }
        _query = saveQ;
        if (dropped.Count > 0) Toast($"Removed {string.Join(", ", dropped)}: not in {_version}");
        Render();
        _root.Focus();
    }

    // Animal first; after it the game plays an animation, so wait 4 s; 1 s between later cards.
    private async Task Scan()
    {
        var ps = Picked(); if (ps.Count == 0 || _scanning) return;
        _scanning = true; _scanLabel.Text = "Scanning…"; RenderSlots();
        var plan = new List<(string p, int at)>(); int t = 0; string prev = null;
        foreach (var p in ps) { if (prev == "A") t += 4000; else if (prev != null) t += 1000; plan.Add((p, t)); prev = p; }
        int total = plan[^1].at + 600;
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) => { var f = Math.Min(1, (DateTime.UtcNow - started).TotalMilliseconds / total); _scanProg.Width = f * _scan.Bounds.Width; };
        timer.Start();
        bool failed = false;
        foreach (var (p, at) in plan)
        {
            var wait = at - (DateTime.UtcNow - started).TotalMilliseconds;
            if (wait > 0) await Task.Delay((int)wait);
            var c = _hand[p];
            HighlightSlot(p, true);
            var (ok, err) = await _play.Send(c.Code);
            HighlightSlot(p, false);
            if (!ok) { failed = true; SetConn("fail", $"No answer on port {_play.Port}. Is the server enabled in Play!?"); Toast($"Couldn't reach Play! on port {_play.Port}. Check the port and that the server is on in Play!", true); break; }
            SetConn("ok", $"Play! is answering on port {_play.Port}");
            Toast("Sent " + c.Name);
        }
        if (!failed) { var rest = total - (DateTime.UtcNow - started).TotalMilliseconds; if (rest > 0) await Task.Delay((int)rest); }
        timer.Stop(); _scanProg.Width = 0;
        _scanning = false; RenderSlots();
    }

    private void HighlightSlot(string p, bool on)
    {
        foreach (var s in _slots.Children.OfType<Border>())
            if ((s.Tag as string) == p) s.BoxShadow = on ? BoxShadows.Parse("0 0 0 3 #FFF6E5, 0 0 18 4 #99FFF096") : default;
    }

    private async Task TestPort()
    {
        if (!int.TryParse(_portIn.Text, out int port) || port is < 1 or > 65535) { SetConn("fail", "Enter a port number"); return; }
        SetConn("", "Testing…");
        var (ok, _) = await _play.Ping(port);
        SetConn(ok ? "ok" : "fail", ok ? $"Play! is answering on port {port}" : $"No answer on port {port}. Is the server enabled in Play!?");
    }

    private void SavePort()
    {
        if (!int.TryParse(_portIn.Text, out int port) || port is < 1 or > 65535) return;
        _play.Port = port; _portLabel.Text = port.ToString();
        _settings.DefaultPort = port; _settings.Save();
        TogglePort(false); Toast($"Port set to {port}");
    }

    private async Task SendManual()
    {
        var v = (_manIn.Text ?? "").Trim(); if (v == "") return;
        var c = _all.FirstOrDefault(x => x.Code == v);
        var (ok, _) = await _play.Send(v);
        string what = c?.Name ?? $"“{v}”";
        _manStatus.Text = ok ? $"Sent {what}" : $"Couldn't reach Play! on port {_play.Port}";
        _manStatus.Foreground = ok ? AK.Ink : AK.B("#FF8A70");
        SetConn(ok ? "ok" : "fail", ok ? $"Play! is answering on port {_play.Port}" : $"No answer on port {_play.Port}. Is the server enabled in Play!?");
        if (ok) Toast("Sent " + what);
        _manIn.Text = ""; _manIn.Focus();
    }

    private void SetConn(string state, string text)
    {
        _conn = state;
        var b = state == "ok" ? AK.B("#49D18B") : state == "fail" ? AK.B("#FF5A3C") : AK.Dim;
        _headDot.Fill = b; _popDot.Fill = b; _portStatus.Text = text;
    }

    private void TogglePort(bool open)
    {
        if (open) ToggleMan(false);
        _portPop.IsVisible = open;
        _portBtn.BorderBrush = open ? AK.Gold : AK.Line;
        if (open) { _portIn.Text = _play.Port.ToString(); _portIn.Focus(); _portIn.SelectAll(); } else _root.Focus();
    }

    private void ToggleMan(bool open)
    {
        if (open) TogglePort(false);
        _manPop.IsVisible = open;
        _manBtn.BorderBrush = open ? AK.Gold : AK.Line;
        ((Path)_manBtn.Content).Stroke = open ? AK.Gold : AK.Muted;
        if (open) { _manStatus.Text = ""; _manIn.Text = ""; _manIn.Focus(); } else _root.Focus();
    }

    private void Toast(string msg, bool error = false)
    {
        _toastText.Text = msg; _toast.Opacity = 1;
        _toast.BorderBrush = error ? AK.B("#FF5A3C") : AK.GoldDeep;
        _toastTimer?.Dispose();
        _toastTimer = DispatcherTimer.RunOnce(() => _toast.Opacity = 0, TimeSpan.FromMilliseconds(error ? 5000 : 1800));
    }

    private void OnRootPointer(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual v) return;
        bool In(Visual target) => v == target || v.GetVisualAncestors().Contains(target);
        if (_portPop.IsVisible && !In(_portPop) && !In(_portBtn)) TogglePort(false);
        if (_manPop.IsVisible && !In(_manPop) && !In(_manBtn)) ToggleMan(false);
        if (!In(_search) && !In(_portPop) && !In(_manPop)) _root.Focus();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { TogglePort(false); ToggleMan(false); e.Handled = true; return; }
        if (_ver.IsDropDownOpen) return;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox) return;   // typing in search, port or barcode boxes
        switch (e.Key)
        {
            case Key.Left: Move(-1); e.Handled = true; break;
            case Key.Right: Move(1); e.Handled = true; break;
            case Key.Space: case Key.Enter: ToggleCurrent(); e.Handled = true; break;
            case Key.D1: case Key.NumPad1: SetPile("A"); e.Handled = true; break;
            case Key.D2: case Key.NumPad2: SetPile("S"); e.Handled = true; break;
            case Key.D3: case Key.NumPad3: SetPile("M"); e.Handled = true; break;
        }
    }

    // ───────────────────────── Small builders ─────────────────────────

    private static readonly Dictionary<string, Bitmap> _bitmaps = new();
    private static Bitmap LoadBitmap(string path)
    {
        if (!_bitmaps.TryGetValue(path, out var b)) { try { b = new Bitmap(path); } catch { b = null; } _bitmaps[path] = b; }
        return b;
    }

    private static Ellipse Dot() => new() { Width = 9, Height = 9, Fill = AK.Dim, VerticalAlignment = VerticalAlignment.Center };

    private static Path Glyph(string data, double size, IBrush stroke, double thickness) => new()
    {
        Data = Geometry.Parse(data), Stroke = stroke, StrokeThickness = thickness, Width = size, Height = size, Stretch = Stretch.Uniform,
        StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
    };

    private static TextBlock Heading(string text, double size) => new()
    {
        Text = text, FontSize = size, FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic, Foreground = AK.Gold, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(0, 0, 4, 0),
        Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 2, BlurRadius = 3, Color = AK.C("#2B160A"), Opacity = 1 }
    };

    private static TextBlock Small(string text) => new() { Text = text, FontSize = 12, Foreground = AK.Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 17 };
    private static TextBlock MetaText(string t) => new() { Text = t, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = AK.Muted, LetterSpacing = .6, VerticalAlignment = VerticalAlignment.Center };

    private static Border Chip(string rarity) => new()
    {
        Background = new SolidColorBrush(AK.Rarity(rarity)), CornerRadius = new CornerRadius(999), Padding = new Thickness(8, 1), VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = rarity, FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = .6, Foreground = AK.DarkChipText(rarity) ? AK.B("#151515") : Brushes.White }
    };

    private static Border TagChip(string text, Color? fill = null, bool dark = false) => new()
    {
        Background = new SolidColorBrush(fill ?? AK.C("#4A2E19")), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5),
        Child = new TextBlock { Text = text, FontSize = 12, FontWeight = dark ? FontWeight.Bold : FontWeight.SemiBold, Foreground = dark ? AK.App : AK.Ink }
    };

    private static Control StatBar(string label, int? value, double max, string color)
    {
        double pct = value is int v ? Math.Min(1, v / max) : 0;
        var bar = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = AK.B("#4A2E19"), ClipToBounds = true };
        bar.Child = new Border { Background = AK.B(color), HorizontalAlignment = HorizontalAlignment.Left, Width = (PanelW - 56) * pct };
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new DockPanel { Children = {  Docked(new TextBlock { Text = value?.ToString() ?? "—", FontSize = 13, FontWeight = FontWeight.Bold }, Avalonia.Controls.Dock.Right), new TextBlock { Text = label, FontSize = 13, Foreground = AK.Muted } } },
                bar
            }
        };
    }

    private Button RoundButton(string path, string label, Action click)
    {
        var b = new Button
        {
            Width = 48, Height = 48, CornerRadius = new CornerRadius(24), Padding = new Thickness(0), Focusable = false,
            Background = AK.Brown, BorderBrush = AK.Gold, BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = Glyph(path, 20, AK.Gold, 2.2)
        };
        ToolTip.SetTip(b, label);
        b.Click += (_, _) => click();
        return b;
    }

    private static void StyleHeaderButton(Button b)
    {
        b.Height = 40; b.Padding = new Thickness(12, 0); b.CornerRadius = new CornerRadius(10); b.Focusable = false;
        b.Background = AK.B("#40000000"); b.BorderBrush = AK.Line; b.BorderThickness = new Thickness(1); b.Foreground = AK.Ink; b.FontSize = 14;
        b.VerticalContentAlignment = VerticalAlignment.Center; b.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private static void StyleField(TextBox t)
    {
        t.Height = 40; t.FontSize = 16; t.CornerRadius = new CornerRadius(10); t.Padding = new Thickness(12, 0); t.VerticalContentAlignment = VerticalAlignment.Center;
        t.Background = AK.Field; t.BorderBrush = AK.B("#8A6034"); t.Foreground = AK.Ink;
    }

    private static Button PopButton(string text, bool solid) => new()
    {
        Content = text, Height = 44, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        FontSize = 14, FontWeight = FontWeight.Bold, CornerRadius = new CornerRadius(10), Focusable = false,
        Background = solid ? AK.GoldButton : Brushes.Transparent, Foreground = solid ? AK.Brown : AK.Gold,
        BorderBrush = solid ? AK.Ink : AK.Gold, BorderThickness = new Thickness(solid ? 2 : 1)
    };

    private static Control Labeled(string label, Control field) => new StackPanel
    {
        Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = AK.Muted }, field }
    };

    private void FillPop(Border pop, double width, Thickness margin, Control[] children)
    {
        var sp = new StackPanel { Spacing = 12 };
        foreach (var c in children) sp.Children.Add(c);
        pop.Child = sp; pop.Width = width; pop.Padding = new Thickness(18); pop.CornerRadius = new CornerRadius(14);
        pop.Background = AK.Brown2; pop.BorderBrush = AK.GoldDeep; pop.BorderThickness = new Thickness(2);
        pop.BoxShadow = BoxShadows.Parse("0 18 40 0 #99000000");
        pop.HorizontalAlignment = HorizontalAlignment.Right; pop.VerticalAlignment = VerticalAlignment.Top; pop.Margin = margin;
        pop.IsVisible = false; pop.ZIndex = 100;
        Grid.SetRowSpan(pop, 3); _root.Children.Add(pop);
    }

    private static T Put<T>(T c, int row, int col = 0) where T : Control { Grid.SetRow(c, row); Grid.SetColumn(c, col); return c; }
    private static T Docked<T>(T c, Avalonia.Controls.Dock d) where T : Control { DockPanel.SetDock(c, d); return c; }
}
