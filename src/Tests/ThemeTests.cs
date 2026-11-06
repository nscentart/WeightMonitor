using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

public partial class Theme {
    public static List<string> Test() {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        Check(ALL.Length == 10 && ALL.Select(t => t.Code).Distinct().Count() == 10, "there are ten themes, with different codes");
        Check(COLORS.All(c => Of(c, false) != null && Of(c, true) != null && !Of(c, false).Light && Of(c, true).Light), "every color exists dark and light");
        Check(ALL[0].Code == "blue-dark" && ALL[0].Label.ToArgb() == Color.FromArgb(0x38, 0xA8, 0xFF).ToArgb(), "the first is the dark blue with #38A8FF");

        Func<Color, bool> LightBlue = c => c.GetHue() >= 190 && c.GetHue() <= 225 && c.GetSaturation() > 0.3f;
        Check(ALL.Where(t => t.ColorName != "blue").All(t => !LightBlue(t.Label) && !LightBlue(t.AccentBar)) && LightBlue(Of("blue", false).AccentBar) && LightBlue(Of("blue", true).AccentBar), "only one light blue is left: Blue");
        foreach (bool light in new[] { false, true }) {
            var l = Of("lemon", light);
            Check(l.Label.GetHue() >= 46 && l.Label.GetHue() <= 56 && l.AccentBar.GetHue() >= 46 && l.AccentBar.GetHue() <= 56, l.Code + ": labels and small bars all yellow");
            Check(Math.Abs(l.Label.GetHue() - Of("green", light).Label.GetHue()) >= 18 && Math.Abs(l.AccentBar.GetHue() - Of("green", light).AccentBar.GetHue()) >= 18, l.Code + ": clearly distinct from the lime of Green");
        }
        Check(Of("green", false).Capsule.ToArgb() == ColorTranslator.FromHtml("#17271F").ToArgb() && Of("green", false).Label.ToArgb() == ColorTranslator.FromHtml("#C9F04D").ToArgb(), "dark green: the forest background from before with the lime accents");

        Check(Of("blue", false).Liquid.ToArgb() == Color.FromArgb(0x38, 0xA8, 0xFF).ToArgb() && COLORS.Select(c => Of(c, false).Liquid.ToArgb()).Distinct().Count() == 5 && COLORS.All(c => Of(c, false).Liquid.ToArgb() == Of(c, true).Liquid.ToArgb()), "the logo liquid: light blue for Blue, one color for each of the others");
        foreach (var t in ALL) {
            var colors = typeof(Theme).GetFields().Where(f => f.FieldType == typeof(Color) && !f.IsStatic).Select(f => (Color)f.GetValue(t)).ToList();
            Check(colors.Count == 21 && colors.All(c => c.A == 255), t.Code + ": all the " + colors.Count + " colors are there and are solid");
            var scarce = new List<string>();
            Action<string, Color, Color, double> Yes = (name, c, up, min) => { if (Contrast(c, up) < min) scarce.Add(name + " " + Contrast(c, up).ToString("0.00")); };
            foreach (var background in new[] { t.Capsule, t.Backdrop, t.Panel }) {
                Yes("values", t.Text, background, 7); Yes("labels", t.Label, background, 4); Yes("muted", t.Muted, background, 4.5);
                Yes("good", t.Good, background, 4); Yes("yellow", t.Yellow, background, 4); Yes("amber", t.Amber, background, 4); Yes("orange", t.Orange, background, 4); Yes("red", t.Red, background, 4);
            }
            foreach (var c in new[] { t.Text, t.Muted }) Yes("on the chart", c, t.Chart, 4.5);
            foreach (var c in new[] { t.Good, t.Yellow, t.Orange, t.Red }) Yes("speed on the chart", c, t.Chart, 3.5);
            Yes("name on the band of the Classic style", t.Label, t.ClassicBackground, 2.8); Yes("number on the band of the Classic style", t.Text, t.ClassicBackground, 4.5);
            Yes("text on the button", t.OnAccentBar, t.AccentBar, 4.5); Yes("small bar on its background", t.AccentBar, t.Empty, 1.9); Yes("scroll knob", t.Muted, t.Empty, 2);
            Check(scarce.Count == 0, t.Code + ": everything is readable" + (scarce.Count == 0 ? "" : ": " + string.Join(", ", scarce)));

            Func<Color, bool> Hot = c => c.GetSaturation() > 0.3f && (c.GetHue() < 45 || c.GetHue() > 345);
            Check(!new[] { t.Label, t.AccentBar, t.Good, t.Modified, t.Capsule, t.Border, t.Text }.Any(Hot) && Hot(t.Red) && Hot(t.Orange), t.Code + ": red and orange only for heat and strain");
        }
        foreach (var l in Language.CODES) {
            foreach (var c in COLORS) if (string.IsNullOrEmpty(Language.Raw(l, "color." + c))) r.Add("ERROR " + c + ": name missing in " + l);
            foreach (var a in LOOKS) if (string.IsNullOrEmpty(Language.Raw(l, "appearance." + a))) r.Add("ERROR " + a + ": name missing in " + l);
        }

        string folder = Path.Combine(Path.GetTempPath(), "WM-test-themes-" + Guid.NewGuid().ToString("N")), file = Path.Combine(folder, "settings.ini");
        string previousColor = color, previousAppearance = appearance; var previousWindows = Windows;
        bool system = false, app = false;
        Windows = entry => entry == SYSTEM_ENTRY ? system : entry == APP_ENTRY ? app : Fail(entry);
        Func<string, string, bool> E = (c, a) => color == c && appearance == a;
        Action<string> Reopen = text => { File.WriteAllText(file, text, new UTF8Encoding(false)); Settings.Reread(); Begin(null, null); };
        try {
            Directory.CreateDirectory(folder);
            Settings.UseFile(file);
            Begin(null, null);
            Check(E("blue", "auto"), "fresh install: Blue and Automatic");
            foreach (var c in COLORS) foreach (var a in LOOKS) {
                Choose(c, a);
                Settings.Reread(); color = "x"; appearance = "x"; Begin(null, null);
                if (!E(c, a)) r.Add("ERROR not remembered " + c + " " + a);
            }
            Check(Settings.Read("color", "") == "purple" && Settings.Read("appearance", "") == "light" && File.ReadAllText(file).Contains("color=purple"), "every choice is remembered on reopening");
            Choose("green", "dark"); Begin("pink", "light");
            Check(E("pink", "light") && now.Code == "pink-light" && bar.Code == "pink-light", "--color and --appearance apply for this time only");
            Settings.Reread(); Begin(null, null);
            Check(E("green", "dark"), "--color and --appearance are not written: the choice stays");
            Begin("nonsense", "nonsense");
            Check(E("green", "dark"), "from a nonsense command line: the choice stays");

            foreach (var testCase in new[] { "acid>blue>dark", "light>blue>light", "lime>lemon>dark", "green>green>dark", "pink>pink>dark", "purple>purple>dark" }) {
                var p = testCase.Split('>');
                Reopen("theme=" + p[0] + "\r\nstyle=b\r\n");
                Check(E(p[1], p[2]), "the old theme " + p[0] + " diventa " + p[1] + " " + p[2]);
            }
            Reopen("theme=lime\r\ncolor=pink\r\nappearance=auto\r\n");
            Check(E("pink", "auto"), "the new choices win over the old theme");
            foreach (var strange in new[] { "klingon", "", "ACIDO", "../../x", new string('x', 300), "theme=light" }) {
                Reopen("theme=" + strange + "\r\ncolor=" + strange + "\r\nappearance=" + strange + "\r\nstyle=b\r\n");
                if (!E("blue", "auto")) r.Add("ERROR nonsense value in the file: " + strange);
            }
            Check(E("blue", "auto"), "a nonsense value in the file breaks nothing: Blue and Automatic");
            Choose("klingon", "dark"); Choose("blue", "klingon");
            Check(E("blue", "auto") && !File.ReadAllText(file).Contains("=klingon\r\ncolor=klingon\r\nappearance=klingon\r\nstyle=b\r\ncolor"), "a color or an appearance that does not exist cannot be chosen");
            Reopen("style=a\r\nlanguage=fr\r\nposition_x=1234\r\n");
            Check(E("blue", "auto") && Settings.Read("style", "") == "a", "update from a version without themes: Blue and Automatic, the other choices stay");
            Choose("purple", "dark");
            Settings.Reread();
            Check(Settings.Read("position_x", "") == "1234" && Settings.Read("language", "") == "fr", "choosing the theme does not touch the other choices");

            system = false; app = false;
            Choose("lemon", "auto");
            Check(!now.Light && !bar.Light && !FollowWindows(), "Automatic, dark Windows: everything dark and nothing to redraw");
            app = true;
            Check(FollowWindows() && now.Code == "lemon-light" && bar.Code == "lemon-dark", "Windows switches the apps to light: flyouts and windows light, bar still dark, without restarting");
            Check(!FollowWindows(), "a second later there is nothing left to redraw");
            system = true;
            Check(FollowWindows() && now.Code == "lemon-light" && bar.Code == "lemon-light" && Key == Color.FromArgb(254, 254, 253), "the system light too: the bar follows");
            system = false; app = false;
            Check(FollowWindows() && !now.Light && !bar.Light && color == "lemon", "Windows goes back to dark: everything dark, the color does not change");
            system = true; app = false;
            Check(FollowWindows() && !now.Light && bar.Light && Of(color, true).Code == "lemon-light", "light system and dark apps: each its own");
            using (var b = new Bitmap(600, 48)) using (var g = Graphics.FromImage(b)) {
                var o = now;
                Drawing.Draw(g, new Rectangle(0, 0, 600, 48), Program.TestSample(), "b", "extended", 0);
                Check(now == o, "after the bar is drawn, flyouts and windows go back to their own theme");
            }
            system = true; app = true;
            Choose("lemon", "dark");
            Check(!now.Light && !bar.Light && !FollowWindows() && !now.Light, "appearance chosen by hand: light Windows does not change it");
            system = false; app = false;
            Choose("lemon", "light");
            Check(now.Light && bar.Light && !FollowWindows(), "the same with light on dark Windows");

            foreach (var t in ALL) {
                Put(t);
                var windows = new Form[] { new SettingsWindow(1f), new ConfirmClose("Test", 1, true), new Welcome(true), new PawnIOChoice(), new LanguageChoice("it"),
                    new UpdateWindow(new UpdateInfo { Label = "0.9", Note = "x" }), new ErrorReport("x") };
                foreach (var f in windows) using (f) if (f.BackColor != t.Backdrop || f.ForeColor != t.Text) r.Add("ERROR " + t.Code + ": " + f.GetType().Name + " does not follow the theme");
                foreach (var e in Drawing.ColorsTest()) r.Add("ERROR " + t.Code + ": " + e);
            }
            r.Add("ok windows and flyouts tested in all " + ALL.Length + " themes");

            Put(ALL[0]);
            using (var f = new SettingsWindow(1f)) {
                foreach (var t in ALL) {
                    Put(t);
                    f.ColorsTest();
                    var c = new List<Control>(); var stack = new Stack<Control>(); stack.Push(f);
                    while (stack.Count > 0) foreach (Control x in stack.Pop().Controls) { stack.Push(x); c.Add(x); }
                    bool fixedItems = c.OfType<Button>().All(b => b.BackColor == t.Capsule || b.BackColor == t.AccentBar || b.BackColor == t.Backdrop) && c.OfType<CheckBox>().All(b => b.BackColor == t.Panel && b.ForeColor == t.Text) && c.OfType<ComboBox>().All(b => b.BackColor == t.Capsule);
                    Check(f.BackColor == t.Backdrop && fixedItems, "Settings open, switches to " + t.Code);
                }
            }
            using (var f = new LicenseWindow(1f)) {
                foreach (var t in ALL) {
                    Put(t);
                    f.ColorsTest();
                    Check(f.BackColor == t.Backdrop && f.Controls.OfType<Button>().All(b => b.BackColor == t.Capsule), "License open, switches to " + t.Code);
                }
            }
        } finally {
            Windows = previousWindows;
            color = previousColor; appearance = previousAppearance; Compute();
            Settings.UseFile(Path.Combine(folder, "x.ini"));
            try { Directory.Delete(folder, true); } catch { }
        }
        return r;
    }
    static bool Fail(string entry) { throw new InvalidOperationException("unexpected Windows entry: " + entry); }
}
