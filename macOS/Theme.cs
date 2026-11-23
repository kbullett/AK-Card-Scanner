using Avalonia;
using Avalonia.Media;

namespace AKCardScanner;

// Colours and type taken from the Animal Kaiser+ site: ember browns, fire gradient, lightning yellow, gold, crimson.
public static class AK
{
    public static readonly FontFamily Font = new("avares://AKCardScanner/Assets/Fonts#Hind Siliguri");

    public static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));
    public static Color C(string hex) => Color.Parse(hex);

    public static readonly IBrush Ink = B("#FFF6E5"), Muted = B("#E3CFAE"), Gold = B("#F8CF4A"), GoldDeep = B("#D4A645"),
        Brown = B("#2B160A"), Brown2 = B("#1E130B"), Line = B("#6A4526"), Field = B("#2B1A0F"), App = B("#140B06"), Dim = B("#C9B08A");

    public static Color Rarity(string r) => C(r switch
    {
        "Bronze" => "#C27A45", "Silver" => "#C9D1DC", "Gold" => "#F1C24B", "Ultra" => "#E0457B", "Kaiser" => "#A57BFF", _ => "#8A93A6"
    });
    public static bool DarkChipText(string r) => r is "Gold" or "Silver";
    public static Color Attack(string a) => C(a switch { "Power" => "#FF7A59", "Guts" => "#5AAEFF", "Tech" => "#4FD394", _ => "#4A2E19" });

    public static IBrush Vertical(string top, string bottom) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(C(top), 0), new GradientStop(C(bottom), 1) }
    };

    public static IBrush Fire => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops = { new(C("#5E0B02"), 0), new(C("#D9300B"), .18), new(C("#FFB000"), .36), new(C("#FFE36B"), .5), new(C("#FFB000"), .64), new(C("#D9300B"), .82), new(C("#5E0B02"), 1) }
    };

    public static IBrush Lightning => new RadialGradientBrush
    {
        Center = new RelativePoint(.5, .3, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.5, .3, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(.7, RelativeUnit.Relative), RadiusY = new RelativeScalar(.7, RelativeUnit.Relative),
        GradientStops = { new(C("#FFFBD6"), 0), new(C("#FFE53B"), .22), new(C("#FFB81C"), .48), new(C("#F07A0B"), .75), new(C("#A8360A"), 1) }
    };

    // Diagonal stripes in the rarity colour, as a stand-in for card art.
    public static IBrush Stripes(Color c, double size, double a1 = .2, double a2 = .1) => new LinearGradientBrush
    {
        SpreadMethod = GradientSpreadMethod.Repeat,
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = new RelativePoint(size, size, RelativeUnit.Absolute),
        GradientStops =
        {
            new(Color.FromArgb((byte)(255 * a1), c.R, c.G, c.B), 0), new(Color.FromArgb((byte)(255 * a1), c.R, c.G, c.B), .5),
            new(Color.FromArgb((byte)(255 * a2), c.R, c.G, c.B), .5), new(Color.FromArgb((byte)(255 * a2), c.R, c.G, c.B), 1)
        }
    };

    public static IBrush RedButton => Vertical("#E8461F", "#A8231A");
    public static IBrush GoldButton => Vertical("#FFE36B", "#E9A916");

    public const string PawIcon = "M6 7a2 2 0 1 1 0 4 2 2 0 0 1 0-4zm4-4a2 2 0 1 1 0 4 2 2 0 0 1 0-4zm4 0a2 2 0 1 1 0 4 2 2 0 0 1 0-4zm4 4a2 2 0 1 1 0 4 2 2 0 0 1 0-4zM12 11c-3 0-5 3-5 6 0 2 2 3 5 3s5-1 5-3c0-3-2-6-5-6z";
    public const string BoltIcon = "M13 2L4 14h7l-1 8 9-12h-7z";
    public const string SunIcon = "M12 7a5 5 0 1 1 0 10 5 5 0 0 1 0-10zM12 1v3M12 20v3M1 12h3M20 12h3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1";
    public static string Icon(string pile) => pile switch { "A" => PawIcon, "S" => BoltIcon, _ => SunIcon };
}
