using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static partial class Drawing {
    public static List<string> LightBarTest(SensorData d) {
        var r = new List<string>();
        var previous = Theme.Current;
        foreach (var t in Theme.ALL) {
            Theme.Put(t);
            foreach (bool light in new[] { true, false }) {
                LightBar = light;
                var background = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
                bool glass = Glass("a");
                int right = 0, wrong = 0;
                using (var b = new Bitmap(1200, 48)) using (var g = Graphics.FromImage(b)) {
                    g.Clear(background);
                    Draw(g, new Rectangle(0, 0, 1200, 48), d, "a", "extended", 0);
                    int end = lastWidth;
                    for (int x = 48; x < end - 14; x++) if (b.GetPixel(x, 1).ToArgb() == (glass ? t.Capsule : background).ToArgb()) right++; else wrong++;
                }
                bool wait = light || t.Light;
                r.Add((wrong == 0 && right > 0 && glass == wait ? "ok " : "ERROR ") + "theme " + t.Code + ", Classic style on a bar " + (light ? "light" : "dark") + ": " + (glass ? "theme glass" : "no glass"));
            }
        }
        LightBar = false;
        Theme.Put(previous);
        return r;
    }

    public static List<string> ColorsTest() {
        var r = new List<string>();
        var d = Program.TestSample();
        var network = new Network(); network.SampleData(); network.Activate = true;
        var p = Theme.ALL[0];
        var old = Theme.Current == p ? new int[0] : new[] { p.Backdrop, p.Panel, p.Capsule, p.Row, p.Empty, Color.FromArgb(38, 40, 46), Color.FromArgb(96, 112, 140) }.Select(c => c.ToArgb()).ToArray();
        foreach (string type in new[] { "all", "cpu", "gpu", "ram", "disks", "network" }) {
            int width = FlyoutWidth(type, 1f), h;
            using (var b1 = new Bitmap(width, 4000)) using (var g1 = Graphics.FromImage(b1)) h = Flyout(g1, type, width, 1f, d, network, 1, null);
            using (var b = new Bitmap(width, h)) {
                using (var g = Graphics.FromImage(b)) global::Flyout.Draw(g, type, width, h, h, 0, 1f, d, network, 1, null);
                if (b.GetPixel(2, 2).ToArgb() != BACKGROUND.ToArgb()) r.Add("flyout " + type + ": the background is not the theme's");
                for (int y = 0; y < h; y++) for (int x = 0; x < width; x++) if (old.Contains(b.GetPixel(x, y).ToArgb())) { r.Add("flyout " + type + ": a fixed color remains in " + x + "," + y); x = width; y = h; }
            }
        }
        foreach (string style in new[] { "a", "b" }) foreach (string shape in new[] { "extended", "collected", "compact" })
            using (var b = new Bitmap(1400, 48)) using (var g = Graphics.FromImage(b)) {
                g.Clear(Theme.Key);
                if (Draw(g, new Rectangle(0, 0, 1400, 48), d, style, shape, 0).Count == 0) r.Add("bar " + style + " " + shape + ": no zones");
            }
        return r;
    }

    public static List<string> ChoiceTest(SensorData d) {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        string f = Path.GetTempFileName();
        Settings.UseFile(f);
        try {
            using (var b = new Bitmap(1600, 48)) using (var g = Graphics.FromImage(b)) {
                foreach (string style in new[] { "a", "b" }) {
                    Func<string, List<string>> Zone = choice => {
                        Settings.Write("bar_show", choice);
                        Settings.Write("bar_commit", "no");
                        Settings.Reread();
                        return Draw(g, new Rectangle(0, 0, 1600, 48), d, style, "extended", 0).Select(z => z.Name).ToList();
                    };
                    int all, little;
                    var z1 = Zone(BAR_ALL); all = lastWidth;
                    Check(z1.SequenceEqual(new[] { "logo", "disks", "network", "cpu", "gpu", "ram" }), "style " + style + ": all chosen, all the pieces");
                    var z2 = Zone("cpu,ram"); little = lastWidth;
                    Check(z2.SequenceEqual(new[] { "logo", "cpu", "ram" }) && little < all, "style " + style + ": only processor and memory, narrower (" + little + " out of " + all + ")");
                    Check(Width(g, 48, style, "extended", d.Letters().Count) == little, "style " + style + ": the measure equals the drawing");
                    Check(Zone("network").SequenceEqual(new[] { "logo", "network" }), "style " + style + ": only the network");
                    Check(Zone("logo").SequenceEqual(new[] { "logo" }), "style " + style + ": nothing chosen, the logo stays");
                    Check(Zone("cpu,gpu,ram,disks").Contains("disks"), "style " + style + ": the written order does not matter");
                    Settings.Write("bar_show", BAR_ALL); Settings.Write("bar_commit", "yes"); Settings.Reread();
                    var z3 = Draw(g, new Rectangle(0, 0, 1600, 48), d, style, "extended", 0).Select(x => x.Name).ToList();
                    Check(z3.SequenceEqual(new[] { "logo", "disks", "network", "cpu", "gpu", "ram", "commit" }) && lastWidth > all, "style " + style + ": committed memory chosen, after the memory and wider (" + lastWidth + " out of " + all + ")");
                    Check(Width(g, 48, style, "extended", d.Letters().Count) == lastWidth, "style " + style + ": the measure equals the drawing with committed memory");
                    Settings.Write("bar_show", "logo"); Settings.Reread();
                    Check(Draw(g, new Rectangle(0, 0, 1600, 48), d, style, "extended", 0).Select(x => x.Name).SequenceEqual(new[] { "logo", "commit" }), "style " + style + ": only committed memory, next to the logo");
                }
            }
            Settings.Write("bar_show", "gpu");
            Settings.Reread();
            Check(Show("gpu") && !Show("cpu"), "the choice stays after the fake restart");
        } finally { try { File.Delete(f); } catch { } }
        return r;
    }

    public static List<string> LicenseCaptionTest(string of, bool whole) {
        var r = new List<string>();
        foreach (float s in new[] { 1f, 1.5f, 2f })
            using (var bmp = new Bitmap(10, 10)) using (var g = Graphics.FromImage(bmp))
            using (var title = new Font("Segoe UI Semibold", 15f * s, GraphicsUnit.Pixel)) using (var small = new Font("Segoe UI", 11.5f * s, GraphicsUnit.Pixel)) {
                int width = FlyoutWidth("all", s), pad = (int)(16 * s), side = (int)Math.Round(24 * s), left = pad + side + (int)Math.Round(8 * s) + W(g, "WeightMonitor", title) + (int)Math.Round(12 * s);
                int place = width - pad - left, w = TextRenderer.MeasureText(g, LicenseLabel.FlyoutCaption(), small, new Size(9999, 999), F_CAPTION).Width;
                bool fine = whole ? w <= place : place > (int)(100 * s);
                r.Add((fine ? "OK    " : "ERROR ") + of + "flyout at " + s + "x: " + w + " out of " + place + " of space" + (whole ? ", whole" : ""));
            }
        return r;
    }
}
