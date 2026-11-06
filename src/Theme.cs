using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public partial class Theme {
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int dim);

    public static readonly string[] COLORS = { "blue", "green", "lemon", "pink", "purple" };
    public static readonly string[] LOOKS = { "auto", "dark", "light" };
    const string INITIAL_COLOR = "blue", INITIAL_APPEARANCE = "auto";
    public const string SYSTEM_ENTRY = "SystemUsesLightTheme", APP_ENTRY = "AppsUseLightTheme";

    public static readonly Theme[] ALL = {
        new Theme("blue",    false, "#2C2F36", "#3E424C", "#38A8FF", "#F2F4F8", "#C9F04D", "#38A8FF", "#464A54"),
        new Theme("blue",    true,  "#F2F5FA", "#CFD8E6", "#1F6FED", "#1B2230", "#477F00", "#1F6FED", "#D5DCE8"),
        new Theme("green",  false, "#17271F", "#2D4438", "#C9F04D", "#EFFAF4", "#C9F04D", "#C9F04D", "#35503F"),
        new Theme("green",  true,  "#F1F7EC", "#CFE0C3", "#4A7000", "#18261B", "#1E7A34", "#6E9E00", "#D5E3CA"),
        new Theme("lemon", false, "#23211A", "#423E2E", "#FFE135", "#F8F6EC", "#A5E05A", "#FFE135", "#4A4636"),
        new Theme("lemon", true,  "#FBF8E6", "#E6DFB8", "#7A6200", "#26230F", "#2F7D1E", "#B89800", "#EDE8CC"),
        new Theme("pink",   false, "#2B1E28", "#493445", "#FF7AC6", "#FBF0F7", "#C9F04D", "#FF5CB4", "#544050"),
        new Theme("pink",   true,  "#FBF2F7", "#EBD3E2", "#BE1A78", "#2B1E28", "#477F00", "#C4237F", "#EDDCE7"),
        new Theme("purple",  false, "#241F33", "#3E3656", "#B79CFF", "#F3F0FB", "#C9F04D", "#9672FF", "#463E60"),
        new Theme("purple",  true,  "#F6F3FC", "#DDD5F0", "#6B3FD9", "#241F33", "#477F00", "#7A50E6", "#E2DCF3"),
    };

    public readonly string Code, ColorName;
    public readonly bool Light;
    public readonly Color Capsule, Border, Label, Text, Good, AccentBar, Empty;
    public readonly Color Backdrop, Panel, Row, Chart, Field, Muted, ClassicBackground, OnAccentBar, Modified, Cache;
    public readonly Color Yellow, Amber, Orange, Red;
    public Color Liquid { get { return ColorTranslator.FromHtml(ColorName == "green" ? "#2ED47A" : ColorName == "lemon" ? "#FFE135" : ColorName == "pink" ? "#FF5CB4" : ColorName == "purple" ? "#9672FF" : "#38A8FF"); } }

    Theme(string color, bool light, string capsule, string border, string label, string text, string good, string accentBar, string empty) {
        ColorName = color; Light = light; Code = color + (light ? "-light" : "-dark");
        Capsule = ColorTranslator.FromHtml(capsule); Border = ColorTranslator.FromHtml(border); Label = ColorTranslator.FromHtml(label);
        Text = ColorTranslator.FromHtml(text); Good = ColorTranslator.FromHtml(good); AccentBar = ColorTranslator.FromHtml(accentBar); Empty = ColorTranslator.FromHtml(empty);
        if (Light) {
            Backdrop = Mix(Capsule, Color.White, 0.4f); Panel = Color.FromArgb(255, 255, 255); Row = Mix(Capsule, Border, 0.6f);
            Chart = Mix(Capsule, Empty, 0.4f); Field = Color.FromArgb(255, 255, 255); Muted = Mix(Text, Capsule, 0.33f);
            ClassicBackground = Mix(Capsule, AccentBar, 0.2f);
            Yellow = ColorTranslator.FromHtml("#7F6A00"); Amber = ColorTranslator.FromHtml("#9E5900");
            Orange = ColorTranslator.FromHtml("#B84C00"); Red = ColorTranslator.FromHtml("#C62828");
        } else {
            Backdrop = Mix(Color.Black, Capsule, 0.72f); Panel = Mix(Color.Black, Capsule, 0.86f); Row = Mix(Capsule, Border, 0.25f);
            Chart = Panel; Field = Mix(Color.Black, Capsule, 0.55f); Muted = Mix(Text, Capsule, 0.35f);
            ClassicBackground = Mix(Capsule, AccentBar, 0.3f);
            Yellow = Color.FromArgb(255, 222, 89); Amber = Color.FromArgb(255, 181, 71);
            Orange = Color.FromArgb(255, 150, 60); Red = Color.FromArgb(255, 90, 82);
        }
        OnAccentBar = Contrast(Color.White, AccentBar) >= 4.5 ? Color.FromArgb(255, 255, 255) : ColorTranslator.FromHtml("#14181F");
        Modified = Mix(AccentBar, Text, 0.5f); Cache = Mix(Muted, Empty, 0.5f);
    }

    static string color = INITIAL_COLOR, appearance = "dark";
    static Theme now = ALL[0], bar = ALL[0];
    public static Theme Current { get { return now; } }
    public static Theme Bar { get { return bar; } }
    public static string CurrentColor { get { return color; } }
    public static string Appearance { get { return appearance; } }

    public static Color Key { get { return bar.Light ? Color.FromArgb(254, 254, 253) : Color.FromArgb(1, 0, 0); } }

    public static Theme Of(string color, bool light) { return ALL.First(t => t.ColorName == color && t.Light == light); }

    static string[] Converted(string old) {
        switch (old) {
            case "acid": return new[] { "blue", "dark" };
            case "light": return new[] { "blue", "light" };
            case "lime": return new[] { "lemon", "dark" };
            case "green": case "pink": case "purple": return new[] { old, "dark" };
        }
        return null;
    }

    public static bool WindowsEntry(string entry) {
        try {
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                return k != null && (k.GetValue(entry) as int?) == 1;
        } catch { return false; }
    }
    static Func<string, bool> Windows = WindowsEntry;

    static void Compute() {
        now = Of(color, appearance == "auto" ? Windows(APP_ENTRY) : appearance == "light");
        bar = Of(color, appearance == "auto" ? Windows(SYSTEM_ENTRY) : appearance == "light");
    }

    public static void Begin(string colorFromCommand, string appearanceFromCommand) {
        var old = Converted(Settings.Read("theme", ""));
        Func<string[], string, string> Valid = (list, v) => list.Contains(v) ? v : null;
        color = Valid(COLORS, colorFromCommand) ?? Valid(COLORS, Settings.Read("color", "")) ?? (old != null ? old[0] : INITIAL_COLOR);
        appearance = Valid(LOOKS, appearanceFromCommand) ?? Valid(LOOKS, Settings.Read("appearance", "")) ?? (old != null ? old[1] : INITIAL_APPEARANCE);
        Compute();
    }

    public static void Choose(string newColor, string newAppearance) {
        if (!COLORS.Contains(newColor) || !LOOKS.Contains(newAppearance)) return;
        color = newColor; appearance = newAppearance;
        Settings.Write("color", color);
        Settings.Write("appearance", appearance);
        Compute();
    }

    public static bool FollowWindows() {
        if (appearance != "auto") return false;
        var o = now; var b = bar;
        Compute();
        return o != now || b != bar;
    }

    public static void Put(Theme t) { color = t.ColorName; appearance = t.Light ? "light" : "dark"; Compute(); }
    public static Theme Swap(Theme t) { var previous = now; now = t; return previous; }

    public static void Title(IntPtr window) {
        int dark = now.Light ? 0 : 1;
        try { DwmSetWindowAttribute(window, 20, ref dark, 4); } catch { }
    }
    public static void Follow(Form f) { f.HandleCreated += delegate { Title(f.Handle); }; }

    static Color Mix(Color a, Color b, float amountB) {
        Func<int, int, int> M = (x, y) => (int)Math.Round(x + (y - x) * amountB);
        return Color.FromArgb(M(a.R, b.R), M(a.G, b.G), M(a.B, b.B));
    }
    static double Luminance(Color c) {
        Func<int, double> L = v => { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); };
        return 0.2126 * L(c.R) + 0.7152 * L(c.G) + 0.0722 * L(c.B);
    }
    public static double Contrast(Color a, Color b) {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
