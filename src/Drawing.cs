using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static partial class Drawing {
    public static Color TEXT { get { return Theme.Current.Text; } }
    public static Color MUTED { get { return Theme.Current.Muted; } }
    public static Color LABEL { get { return Theme.Current.Label; } }
    public static Color ACCENT { get { return Theme.Current.AccentBar; } }
    public static Color ON_ACCENT { get { return Theme.Current.OnAccentBar; } }
    public static Color GOOD { get { return Theme.Current.Good; } }
    public static Color YELLOW { get { return Theme.Current.Yellow; } }
    public static Color AMBER { get { return Theme.Current.Amber; } }
    public static Color ORANGE { get { return Theme.Current.Orange; } }
    public static Color RED { get { return Theme.Current.Red; } }
    public static Color BAR_A { get { return Theme.Current.ClassicBackground; } }
    public static Color CAPSULE { get { return Theme.Current.Capsule; } }
    public static Color BORDER { get { return Theme.Current.Border; } }
    public static Color EMPTY { get { return Theme.Current.Empty; } }
    public static Color BACKGROUND { get { return Theme.Current.Backdrop; } }
    public static Color ROW { get { return Theme.Current.Row; } }
    public static Color FIELD { get { return Theme.Current.Field; } }
    static Color CHART { get { return Theme.Current.Chart; } }
    const TextFormatFlags F = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
    static Bitmap logoBase;
    static readonly Dictionary<string, Bitmap> logos = new Dictionary<string, Bitmap>();
    static readonly Dictionary<string, Bitmap> icons = new Dictionary<string, Bitmap>();
    public static int FailTest;

    public static void ResetCaches() {
        logoBase = null;
        logos.Clear();
        icons.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    public static int Cached { get { return (logoBase != null ? 1 : 0) + logos.Count + icons.Count; } }

    public const float MAX_PERCENT = 100.5f, MAX_TEMP = 150f;
    public static bool Odd(double v, double max) { return double.IsNaN(v) || double.IsInfinity(v) || v > max; }
    public static Color Hot(float t) { return t < 0 || Odd(t, MAX_TEMP) ? MUTED : t >= 80 ? RED : t >= 60 ? AMBER : GOOD; }
    public static string FormatPercent(float v) { return Odd(v, MAX_PERCENT) ? SafePaint.MISSING : v < 0 ? "--" : Math.Round(v).ToString("0") + "%"; }
    public static string FormatTemp(float t, bool longValue) { return Odd(t, MAX_TEMP) ? SafePaint.MISSING : t < 0 ? "--" : Math.Round(t).ToString("0") + (longValue ? " °C" : "°"); }
    public const float COMMIT_RESERVE = 10f;
    public static float CommitReserve() {
        float v;
        string s = Settings.Read("commit_reserve", "");
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0 && v < 100000 ? v : COMMIT_RESERVE;
    }
    public static bool CommitLow(SensorData d) { float free = d == null ? -1f : d.CommitFree(); return free >= 0 && free < CommitReserve(); }
    public static Color CommitColor(SensorData d) { return d == null || d.CommitFree() < 0 ? MUTED : CommitLow(d) ? RED : GOOD; }
    public static string FreeGb(float gb, bool shortForm) {
        if (Odd(gb, 1e7)) return SafePaint.MISSING;
        if (gb < 0) return "--";
        return gb.ToString(gb < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + (shortForm ? "G" : " GB");
    }
    static float Fill(float usage) { return Odd(usage, MAX_PERCENT) || usage <= 0 ? 0 : Math.Min(100f, usage); }
    public static Color SpeedColor(double b) {
        if (b < 0 || Odd(b, double.MaxValue)) return MUTED;
        return b < 100 * 1024 ? GOOD : b < 1024 * 1024 ? YELLOW : b < 10 * 1024 * 1024 ? ORANGE : RED;
    }

    public static string Speed(double b) {
        if (Odd(b, double.MaxValue)) return SafePaint.MISSING;
        if (b < 0) return "--";
        double k = b / 1024.0;
        if (k < 1024) return k.ToString("0.0", CultureInfo.InvariantCulture) + " K/s";
        double m = k / 1024.0;
        if (m < 1024) return m.ToString("0.0", CultureInfo.InvariantCulture) + " M/s";
        return (m / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " G/s";
    }
    public static string Quantity(double b) {
        double k = b / 1024.0;
        if (k < 1024) return k.ToString("0", CultureInfo.InvariantCulture) + " KB";
        double m = k / 1024.0;
        if (m < 1024) return m.ToString(m < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " MB";
        return (m / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
    }

    static Bitmap LogoBase() {
        if (logoBase != null) return logoBase;
        try {
            using (var r = Program.Resource("WeightMonitor.png"))
                if (r != null) logoBase = new Bitmap(r);
                else using (var ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath)) logoBase = ic.ToBitmap();
        } catch { logoBase = new Bitmap(1, 1); }
        return logoBase;
    }

    public static Bitmap Logo() {
        var t = Theme.Current;
        Bitmap b;
        if (logos.TryGetValue(t.ColorName, out b)) return b;
        b = ColorizeLiquid(LogoBase(), t.Liquid);
        logos[t.ColorName] = b;
        return b;
    }

    static Bitmap ColorizeLiquid(Bitmap src, Color newOne) {
        if (newOne.ToArgb() == Color.FromArgb(0x38, 0xA8, 0xFF).ToArgb() || src.Width < 8) return src;
        var b = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        float hn = newOne.GetHue(), sn = newOne.GetSaturation(), ln = newOne.GetBrightness();
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++) {
                var c = src.GetPixel(x, y);
                float h = c.GetHue(), s = c.GetSaturation();
                float w = c.A == 0 || h < 195 || h > 215 ? 0 : Math.Max(0, Math.Min(1, (s - 0.45f) / 0.35f));
                if (w <= 0) { b.SetPixel(x, y, c); continue; }
                float report = c.GetBrightness() / 0.61f;
                var t = FromHsl(hn, sn, Math.Max(0, Math.Min(1, ln * report)));
                b.SetPixel(x, y, Color.FromArgb(c.A, (int)Math.Round(c.R + (t.R - c.R) * w), (int)Math.Round(c.G + (t.G - c.G) * w), (int)Math.Round(c.B + (t.B - c.B) * w)));
            }
        return b;
    }
    static Color FromHsl(float h, float s, float l) {
        float q = l < 0.5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        Func<float, int> F = t => {
            t = (t % 1 + 1) % 1;
            float v = t < 1f / 6 ? p + (q - p) * 6 * t : t < 0.5f ? q : t < 2f / 3 ? p + (q - p) * (2f / 3 - t) * 6 : p;
            return (int)Math.Round(Math.Max(0, Math.Min(1, v)) * 255);
        };
        return Color.FromArgb(F(h / 360f + 1f / 3), F(h / 360f), F(h / 360f - 1f / 3));
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    public static Icon LogoIcon(Size dim) {
        using (var bmp = new Bitmap(dim.Width, dim.Height, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(Logo(), 0, 0, dim.Width, dim.Height); }
            IntPtr h = bmp.GetHicon();
            try { using (var i = Icon.FromHandle(h)) return (Icon)i.Clone(); } finally { DestroyIcon(h); }
        }
    }

    public static Bitmap IconOf(App a) {
        Bitmap b;
        if (icons.TryGetValue(a.Key, out b)) return b;
        b = null;
        try { if (a.Path != null && File.Exists(a.Path)) using (var ic = Icon.ExtractAssociatedIcon(a.Path)) b = ic.ToBitmap(); } catch { }
        if (icons.Count > 300) icons.Clear();
        icons[a.Key] = b;
        return b;
    }

    public static void WithoutHalo(Bitmap up, Action<Graphics> draw) {
        using (var gb = Graphics.FromImage(up)) using (var ctx = new BufferedGraphicsContext()) {
            ctx.MaximumBuffer = new Size(up.Width + 1, up.Height + 1);
            using (var b = ctx.Allocate(gb, new Rectangle(0, 0, up.Width, up.Height))) { draw(b.Graphics); b.Render(gb); }
        }
    }

    static int W(Graphics g, string s, Font f) { return TextRenderer.MeasureText(g, s, f, new Size(9999, 999), F).Width; }
    static void T(Graphics g, string s, Font f, int x, int y, int h, Color c) {
        var m = TextRenderer.MeasureText(g, s, f, new Size(9999, 999), F);
        TextRenderer.DrawText(g, s, f, new Point(x, y + (h - m.Height) / 2), c, F);
    }
    static void TRight(Graphics g, string s, Font f, int right, int y, int h, Color c) { T(g, s, f, right - W(g, s, f), y, h, c); }
    static void TTrim(Graphics g, string s, Font f, int x, int y, int width, int h, Color c) {
        TextRenderer.DrawText(g, s, f, new Rectangle(x, y, Math.Max(1, width), h), c, F | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }

    static int TTwoRows(Graphics g, string s, Font f, int x, int y, int width, int h, Color c) {
        if (W(g, s, f) <= width) { TTrim(g, s, f, x, y, width, h, c); return h; }
        TextRenderer.DrawText(g, s, f, new Rectangle(x, y, Math.Max(1, width), 2 * h), c, TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis);
        return 2 * h;
    }

    static GraphicsPath Round(Rectangle r, int radius) {
        var p = new GraphicsPath(); int d = Math.Max(2, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
        return p;
    }
    static void DiskFull(Graphics g, Rectangle r, int radius, Color c) {
        var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Round(r, radius)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        g.SmoothingMode = old;
    }
    static void Image(Graphics g, Image img, Rectangle r) {
        var q = g.InterpolationMode; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, r); g.InterpolationMode = q;
    }

    static Font FontA(int height) { return new Font("Segoe UI Semibold", Math.Max(9f, height / 2f * 0.72f), GraphicsUnit.Pixel); }
    static Font FontV(int height) { return new Font("Segoe UI Semibold", Math.Max(9f, height / 2f * 0.70f), GraphicsUnit.Pixel); }
    static Font FontN(int height) { return new Font("Segoe UI", Math.Max(8f, height / 2f * 0.58f), FontStyle.Bold, GraphicsUnit.Pixel); }

    static string DISKS { get { return Language.T("bar.disks"); } }
    static string COMMIT { get { return Language.T("bar.commit"); } }

    public static int Width(Graphics g, int height, string style, string shape, int letters) {
        return Draw(g, new Rectangle(0, 0, 0, height), null, style, shape, letters).Count == 0 ? 0 : lastWidth;
    }
    static int lastWidth;

    public static bool LightBar;
    static bool Glass(string style) { return style != "b" && (LightBar || Theme.Current.Light); }

    public static List<Zone> Draw(Graphics g, Rectangle r, SensorData d, string style, string shape, int letters) {
        if (FailTest > 0) { FailTest--; throw new OutOfMemoryException("paint test"); }
        var windows = Theme.Swap(Theme.Bar);
        try { return DrawBar(g, r, d, style, shape, letters); } finally { Theme.Swap(windows); }
    }
    static List<Zone> DrawBar(Graphics g, Rectangle r, SensorData d, string style, string shape, int letters) {
        var zones = new List<Zone>();
        int height = r.Height, row = height / 2, x = r.X;
        bool draw = d != null;
        var entries = d == null ? Enumerable.Range(0, letters).Select(i => new KeyValuePair<char, Disk>('E', null)).ToList() : d.Letters();
        if (shape == "compact") {
            int w = height + height / 3;
            if (draw) {
                if (style == "b" || Theme.Current.Light) Capsule(g, new Rectangle(x, r.Y, w, height));
                int side = height * 78 / 100;
                Image(g, Logo(), new Rectangle(x + (w - side) / 2, r.Y + (height - side) / 2, side, side));
            }
            zones.Add(new Zone(new Rectangle(x, r.Y, w, height), "logo"));
            lastWidth = w;
            return zones;
        }
        bool collected = shape == "collected" || entries.Count == 0;
        int logoEnd;
        if (Glass(style)) {
            if (draw) {
                Draw(g, new Rectangle(0, 0, 0, height), null, style, shape, entries.Count);
                Capsule(g, new Rectangle(x, r.Y, lastWidth, height));
            }
            x += height / 6;
        }
        {
            int w = style == "b" ? height : height * 72 / 100, side = height * (style == "b" ? 64 : 72) / 100;
            if (draw) {
                if (style == "b") Capsule(g, new Rectangle(x, r.Y, w, height));
                Image(g, Logo(), new Rectangle(x + (w - side) / 2, r.Y + (height - side) / 2, side, side));
            }
            zones.Add(new Zone(new Rectangle(x, r.Y, w, height), "logo"));
            logoEnd = x + w;
            x += w + (style == "b" ? height / 6 : height / 4);
            if (style != "b" && (BarChoice() != "logo" || Show("commit"))) { if (draw) Separate(g, x, r.Y, height); x += height / 4; }
        }
        bool vDisks = Show("disks"), vNetwork = Show("network");
        var cores = new[] { "cpu", "gpu", "ram", "commit" }.Where(Show).ToArray();
        if (!vDisks && !vNetwork && cores.Length == 0) { lastWidth = logoEnd - r.X; return zones; }
        if (style == "b") {
            using (Font fv = FontV(height)) using (Font fn = FontN(height)) {
                int pad = height / 4, gapV = height / 4, gapC = height / 6;
                int vd = Entry(g, fn, fv, "E"), vr = Entry(g, fn, fv, DISKS), vn = Entry(g, fn, fv, "CPU");
                if (vDisks) {
                    int wDisks = collected ? pad * 2 + vr : pad * 2 + entries.Count * vd + (entries.Count - 1) * gapV;
                    if (draw) {
                        Capsule(g, new Rectangle(x, r.Y, wDisks, height));
                        int xi = x + pad;
                        if (collected) EntryB(g, fn, fv, xi, r.Y, height, vr, DISKS, d.MaxUsage(), d.TempMax());
                        else foreach (var v in entries) { EntryB(g, fn, fv, xi, r.Y, height, vd, v.Key.ToString(), v.Value.Usage, v.Value.Temp); xi += vd + gapV; }
                    }
                    zones.Add(new Zone(new Rectangle(x, r.Y, wDisks, height), "disks"));
                    x += wDisks + gapC;
                }
                if (vNetwork) {
                    int wNetwork = pad * 2 + W(g, "↑ 999.9 M/s", fv);
                    if (draw) {
                        Capsule(g, new Rectangle(x, r.Y, wNetwork, height));
                        T(g, "↑ " + Speed(d.Up), fv, x + pad, r.Y + 1, row - 1, SpeedColor(d.Up));
                        T(g, "↓ " + Speed(d.Down), fv, x + pad, r.Y + row - 1, row, SpeedColor(d.Down));
                    }
                    zones.Add(new Zone(new Rectangle(x, r.Y, wNetwork, height), "network"));
                    x += wNetwork + gapC;
                }
                if (cores.Length > 0) {
                    int vc = Math.Max(vn, W(g, COMMIT, fn) + 4 + Math.Max(W(g, "100%", fv), W(g, "999G", fv)) + 5 + 4);
                    Func<string, int> wb = c => c == "commit" ? vc : vn;
                    int wCore = pad * 2 + cores.Sum(wb) + (cores.Length - 1) * gapV;
                    if (draw) Capsule(g, new Rectangle(x, r.Y, wCore, height));
                    int xi = x + pad;
                    for (int i = 0; i < cores.Length; i++) {
                        int vi = wb(cores[i]);
                        if (draw) PieceB(g, fn, fv, xi, r.Y, height, vi, d, cores[i]);
                        int sin = i == 0 ? x : xi - gapV / 2, des = i == cores.Length - 1 ? x + wCore : xi + vi + gapV / 2;
                        zones.Add(new Zone(new Rectangle(sin, r.Y, des - sin, height), cores[i]));
                        xi += vi + gapV;
                    }
                    x += wCore + gapC;
                }
                x -= gapC;
            }
        } else {
            using (Font f = FontA(height)) {
                int gap = row * 6 / 10;
                int disk = Math.Max(W(g, "E 100%", f), W(g, "100 °C", f)), collectedWidth = Math.Max(W(g, DISKS + " 100%", f), W(g, "100 °C", f));
                int network = W(g, "↑ 999.9 M/s", f), core = Math.Max(W(g, "CPU 100%", f), W(g, "100 °C", f));
                bool first = true;
                if (vDisks) {
                    int x0 = x;
                    if (collected) { if (draw) ColumnA(g, f, x, r.Y, row, collectedWidth, DISKS, d.MaxUsage(), d.TempMax()); x += collectedWidth + gap; }
                    else foreach (var v in entries) { if (draw) ColumnA(g, f, x, r.Y, row, disk, v.Key.ToString(), v.Value.Usage, v.Value.Temp); x += disk + gap; }
                    zones.Add(new Zone(new Rectangle(x0, r.Y, x - x0, height), "disks"));
                    first = false;
                }
                if (vNetwork) {
                    if (!first) { if (draw) Separate(g, x, r.Y, height); x += gap; }
                    if (draw) { T(g, "↑ " + Speed(d.Up), f, x, r.Y, row, SpeedColor(d.Up)); T(g, "↓ " + Speed(d.Down), f, x, r.Y + row, row, SpeedColor(d.Down)); }
                    zones.Add(new Zone(new Rectangle(x - gap / 2, r.Y, network + gap, height), "network"));
                    x += network + gap;
                    first = false;
                }
                if (cores.Length > 0) {
                    if (!first) { if (draw) Separate(g, x, r.Y, height); x += gap; }
                    int commitWidth = Math.Max(core, Math.Max(W(g, COMMIT + " 100%", f), W(g, "999 GB", f)));
                    for (int i = 0; i < cores.Length; i++) {
                        int wi = cores[i] == "commit" ? commitWidth : core;
                        if (draw) PieceA(g, f, x, r.Y, row, wi, d, cores[i]);
                        zones.Add(new Zone(new Rectangle(x - (i == 0 ? 0 : gap / 2), r.Y, wi + (i == 0 ? gap / 2 : gap), height), cores[i]));
                        x += wi + gap;
                    }
                }
            }
        }
        lastWidth = x - r.X;
        return zones;
    }

    static Reading Core(SensorData d, string which) { return which == "cpu" ? d.Cpu : which == "gpu" ? d.Gpu : d.Ram; }

    public const string BAR_ALL = "disks,network,cpu,gpu,ram";
    public static string ScreenshotTest;
    public static string BarChoice() {
        string v = ScreenshotTest ?? Settings.Read("bar_show", BAR_ALL);
        return string.IsNullOrWhiteSpace(v) ? BAR_ALL : v;
    }
    public static bool Show(string piece) {
        if (piece == "commit" && ScreenshotTest == null) return Settings.Read("bar_commit", "yes") != "no";
        return BarChoice().Split(',').Contains(piece);
    }

    static void PieceA(Graphics g, Font f, int x, int y, int row, int width, SensorData d, string piece) {
        if (piece == "commit") { ColumnA(g, f, x, y, row, width, COMMIT, d.CommitPercent(), FreeGb(d.CommitFree(), false), CommitColor(d), CommitLow(d)); return; }
        var m = Core(d, piece);
        ColumnA(g, f, x, y, row, width, piece.ToUpperInvariant(), m.Usage, FormatTemp(m.Temp, true), Hot(m.Temp), false);
    }
    static void PieceB(Graphics g, Font fn, Font fv, int x, int y, int height, int width, SensorData d, string piece) {
        if (piece == "commit") { EntryB(g, fn, fv, x, y, height, width, COMMIT, d.CommitPercent(), FreeGb(d.CommitFree(), true), CommitColor(d), CommitLow(d)); return; }
        var m = Core(d, piece);
        EntryB(g, fn, fv, x, y, height, width, piece.ToUpperInvariant(), m.Usage, FormatTemp(m.Temp, false), Hot(m.Temp), false);
    }
    static void ColumnA(Graphics g, Font f, int x, int y, int row, int width, string name, float usage, float temp) {
        ColumnA(g, f, x, y, row, width, name, usage, FormatTemp(temp, true), Hot(temp), false);
    }
    static void ColumnA(Graphics g, Font f, int x, int y, int row, int width, string name, float usage, string second, Color secondColor, bool alarm) {
        if (Fill(usage) > 0) using (var b = new SolidBrush(BAR_A)) g.FillRectangle(b, x, y + 2, Math.Max(1, (int)(width * Fill(usage) / 100f)), row - 4);
        int wn = W(g, name + " ", f);
        T(g, name + " ", f, x, y, row, LABEL);
        T(g, FormatPercent(usage), f, x + wn, y, row, alarm ? RED : TEXT);
        T(g, second, f, x, y + row, row, secondColor);
    }
    static void Separate(Graphics g, int x, int y, int height) {
        using (var p = new Pen(BORDER)) g.DrawLine(p, x - 1, y + height / 6, x - 1, y + height - height / 6);
    }
    static int Entry(Graphics g, Font fn, Font fv, string name) {
        return W(g, name, fn) + 4 + Math.Max(W(g, "100%", fv), W(g, "100°", fv)) + 5 + 4;
    }
    static void Capsule(Graphics g, Rectangle r) {
        var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), Math.Max(4, r.Height / 4)))
        using (var b = new SolidBrush(CAPSULE)) using (var pen = new Pen(BORDER)) { g.FillPath(b, p); g.DrawPath(pen, p); }
        g.SmoothingMode = old;
    }
    static void EntryB(Graphics g, Font fn, Font fv, int x, int y, int height, int width, string name, float usage, float temp) {
        EntryB(g, fn, fv, x, y, height, width, name, usage, FormatTemp(temp, false), Hot(temp), false);
    }
    static void EntryB(Graphics g, Font fn, Font fv, int x, int y, int height, int width, string name, float usage, string second, Color secondColor, bool alarm) {
        int row = height / 2, wn = W(g, name, fn);
        T(g, name, fn, x, y, height, LABEL);
        int xv = x + wn + 4;
        T(g, FormatPercent(usage), fv, xv, y + 1, row - 1, alarm ? RED : TEXT);
        T(g, second, fv, xv, y + row - 1, row, secondColor);
        int hb = height * 62 / 100, yb = y + (height - hb) / 2, xb = x + width - 4;
        using (var v = new SolidBrush(EMPTY)) g.FillRectangle(v, xb, yb, 3, hb);
        if (Fill(usage) > 0) {
            int h = Math.Max(2, (int)(hb * Fill(usage) / 100f));
            using (var b = new SolidBrush(usage >= 90 || alarm ? RED : ACCENT)) g.FillRectangle(b, xb, yb + hb - h, 3, h);
        }
    }

    public static string Tooltip(string zone, SensorData d, Network network) {
        if (d == null) return "";
        string core = "CPU " + FormatPercent(d.Cpu.Usage) + " " + FormatTemp(d.Cpu.Temp, false) + " · GPU " + FormatPercent(d.Gpu.Usage) + " " + FormatTemp(d.Gpu.Temp, false)
                      + " · RAM " + FormatPercent(d.Ram.Usage) + (d.Ram.Temp < 0 ? "" : " " + FormatTemp(d.Ram.Temp, false));
        string hatch = "↑ " + Speed(d.Up) + "   ↓ " + Speed(d.Down);
        if (network != null && network.Activate) {
            var r = network.Rows(1).FirstOrDefault();
            if (r != null) hatch += "\n" + Language.T("tip.most_active", "name", Explanations.Name(r.App));
        }
        string disks = string.Join("\n", d.Disks.Select(x => x.Codes() + "  " + x.Model + "  " + FormatTemp(x.Temp, false)));
        string click = Language.T("tip.details");
        switch (zone) {
            case "logo": return Language.T("tip.overview") + "\n" + core + "\n" + hatch;
            case "disks": return disks + "\n" + click;
            case "network": return hatch;
            case "core": return (d.CpuName.Length > 0 ? d.CpuName + "\n" : "") + (d.GpuName.Length > 0 ? d.GpuName + "\n" : "") + core;
            case "cpu": return (d.CpuName.Length > 0 ? d.CpuName + "\n" : "") + "CPU " + FormatPercent(d.Cpu.Usage) + " " + FormatTemp(d.Cpu.Temp, false) + "\n" + click;
            case "gpu": return (d.GpuName.Length > 0 ? d.GpuName + "\n" : "") + "GPU " + FormatPercent(d.Gpu.Usage) + " " + FormatTemp(d.Gpu.Temp, false) + "\n" + click;
            case "commit": return CommitText(d, "tip.commit") + (CommitLow(d) ? "\n" + Language.T("tip.commit_low", "reserve", FreeGb(CommitReserve(), false)) : "") + "\n" + click;
            case "ram": return "RAM " + FormatPercent(d.Ram.Usage) + (d.TotalRam > 0 ? Language.T("tip.ram_gb", "used", d.UsedRam.ToString("0.0", CultureInfo.InvariantCulture), "total", Math.Round(d.TotalRam).ToString("0")) : "")
                               + (d.Ram.Temp < 0 ? "" : " " + FormatTemp(d.Ram.Temp, false)) + "\n" + click;
        }
        return "";
    }

    static string CommitText(SensorData d, string key) {
        return Language.T(key, "used", FreeGb(d.CommitUsed, false), "total", FreeGb(d.CommitTotal, false), "free", FreeGb(d.CommitFree(), false));
    }

    public static int FlyoutWidth(string type, float s) { return (int)((type == "all" ? 430 : 470) * s); }

    public static int Flyout(Graphics g, string type, int width, float s, SensorData d, Network network, int tab, List<Zone> zones, ScrollList list = null) {
        using (var title = new Font("Segoe UI Semibold", 15f * s, GraphicsUnit.Pixel))
        using (var text = new Font("Segoe UI", 13f * s, GraphicsUnit.Pixel))
        using (var bold = new Font("Segoe UI Semibold", 13f * s, GraphicsUnit.Pixel))
        using (var small = new Font("Segoe UI", 11.5f * s, GraphicsUnit.Pixel)) {
            var k = new Kit { G = g, S = s, Width = width, Pad = (int)(16 * s), Title = title, Text = text, Bold = bold, Small = small, Zone = zones };
            if (type == "network") return NetworkFlyout(k, d, network, tab, list ?? new ScrollList());
            var x = Details.Instance ?? (Details.Instance = Details.SampleData());
            if (type == "disks") return DiskDetailFlyout(k, d, x, tab);
            if (type == "cpu") return CpuFlyout(k, d, x, tab);
            if (type == "gpu") return GpuFlyout(k, d, x, tab);
            if (type == "ram") return RamFlyout(k, d, x, tab);
            if (type == "commit") return CommitFlyout(k, d, x, tab);
            return OverviewFlyout(k, d, network);
        }
    }

    class Kit {
        public Graphics G; public float S; public int Width, Pad; public Font Title, Text, Bold, Small; public List<Zone> Zone;
        public int P(float v) { return (int)Math.Round(v * S); }
    }

    static int Header(Kit k, int y, string title, string right, Color cRight) {
        int h = k.P(26);
        T(k.G, title, k.Title, k.Pad, y, h, TEXT);
        if (right != null) TRight(k.G, right, k.Small, k.Width - k.Pad, y, h, cRight);
        return y + h + k.P(8);
    }
    static int Line(Kit k, int y) {
        using (var p = new Pen(ROW)) k.G.DrawLine(p, k.Pad, y, k.Width - k.Pad, y);
        return y + 1;
    }
    static void AccentBar(Kit k, int x, int y, int w, float usage) {
        int h = Math.Max(2, k.P(3));
        DiskFull(k.G, new Rectangle(x, y, w, h), h / 2, EMPTY);
        if (usage > 0) DiskFull(k.G, new Rectangle(x, y, Math.Max(h, (int)(w * usage / 100f)), h), h / 2, usage >= 90 ? RED : ACCENT);
    }

    static int DiskRow(Kit k, int y, Disk x, bool shortText) {
        string free = shortText ? null : x.Free();
        int h = shortText ? k.P(30) : k.P(46), colLetters = Math.Max(k.P(64), W(k.G, x.Codes(), shortText ? k.Bold : k.Title) + k.P(8)), right = k.Width - k.Pad;
        int wTemp = k.P(44), wUsage = k.P(58);
        T(k.G, x.Codes(), shortText ? k.Bold : k.Title, k.Pad, y, h, x.Letters.Count == 0 ? MUTED : LABEL);
        int xm = k.Pad + colLetters, wm = right - wTemp - wUsage - k.P(12) - xm;
        if (shortText) TTrim(k.G, x.Model, k.Text, xm, y, wm, h, TEXT);
        else {
            TTrim(k.G, x.Model, k.Text, xm, y + k.P(4), wm, h / 2 - k.P(2), TEXT);
            TTrim(k.G, x.Description(), k.Small, xm, y + h / 2, wm, h / 2 - k.P(4), MUTED);
        }
        int xu = right - wTemp - wUsage;
        T(k.G, FormatPercent(x.Usage), k.Bold, xu, y - k.P(3), h, TEXT);
        AccentBar(k, xu, y + h / 2 + k.P(9), wUsage - k.P(14), x.Usage);
        TRight(k.G, FormatTemp(x.Temp, false), k.Title, right, y, h, Hot(x.Temp));
        if (free == null) return y + h;
        bool full = x.AlmostFull();
        TTrim(k.G, full ? Language.T("disks.almost_full", "disk", free) : free, k.Small, xm, y + h - k.P(4), right - xm, k.P(18), full ? AMBER : MUTED);
        return y + h + k.P(14);
    }
    static string DiskSummary(SensorData d) {
        string summary = Language.N("disks.count", d.Disks.Count);
        return d.TempMax() > 0 ? summary + Language.T("disks.hottest", "temperature", FormatTemp(d.TempMax(), false)) : summary;
    }

    static int DiskFlyout(Kit k, SensorData d) {
        int y = k.Pad;
        y = Header(k, y, Language.T("common.disks"), DiskSummary(d), MUTED);
        bool first = true;
        foreach (var x in d.Disks) {
            if (!first) y = Line(k, y);
            y = DiskRow(k, y, x, false);
            first = false;
        }
        if (d.Disks.Count == 0) { T(k.G, Language.T("disks.none"), k.Text, k.Pad, y, k.P(30), MUTED); y += k.P(30); }
        if (d.Disks.Any(x => x.Temp < 0)) {
            y += k.P(6);
            TTrim(k.G, Language.T("disks.no_temperature"), k.Small, k.Pad, y, k.Width - 2 * k.Pad, k.P(20), MUTED);
            y += k.P(20);
        }
        return y + k.Pad;
    }

    static string[] TabTitles() { return new[] { Language.T("common.last_second"), Language.T("common.last_minute"), Language.T("common.last_hour") }; }

    static int NetworkFlyout(Kit k, SensorData d, Network network, int tab, ScrollList el) {
        int y = k.Pad, right = k.Width - k.Pad;
        y = Header(k, y, Language.T("common.network"), null, MUTED);
        string downNow = "↓ " + Speed(d.Down);
        TRight(k.G, downNow, k.Bold, right, k.Pad, k.P(26), SpeedColor(d.Down));
        TRight(k.G, "↑ " + Speed(d.Up) + "    ", k.Bold, right - W(k.G, downNow, k.Bold), k.Pad, k.P(26), SpeedColor(d.Up));

        y = Tabs(k, y, tab);

        bool perMinute = tab == 2;
        var series = network == null ? new List<double[]>() : network.Series(perMinute);
        if (perMinute && series.Count > 1) series.RemoveAt(series.Count - 1);
        int hg = k.P(64), wg = k.Width - 2 * k.Pad;
        DiskFull(k.G, new Rectangle(k.Pad, y, wg, hg), k.P(6), CHART);
        double max = Math.Max(1024, series.Count == 0 ? 0 : series.Max(v => Math.Max(v[0], v[1])));
        if (series.Count > 1) {
            var old = k.G.SmoothingMode; k.G.SmoothingMode = SmoothingMode.AntiAlias;
            for (int toward = 1; toward >= 0; toward--) {
                var points = new List<PointF>();
                for (int i = 0; i < series.Count; i++) {
                    float px = k.Pad + k.P(4) + (wg - k.P(8)) * (60 - series.Count + i) / 59f;
                    float py = y + hg - k.P(4) - (float)((hg - k.P(26)) * series[i][toward] / max);
                    points.Add(new PointF(px, py));
                }
                var color = toward == 0 ? LABEL : GOOD;
                if (toward == 1) {
                    var area = new List<PointF>(points);
                    area.Add(new PointF(points.Last().X, y + hg - k.P(4))); area.Add(new PointF(points[0].X, y + hg - k.P(4)));
                    using (var b = new SolidBrush(Color.FromArgb(50, color))) k.G.FillPolygon(b, area.ToArray());
                }
                using (var p = new Pen(color, Math.Max(1f, 1.5f * k.S))) k.G.DrawLines(p, points.ToArray());
            }
            k.G.SmoothingMode = old;
        }
        string scale = perMinute ? Language.T("network.per_minute", "amount", Quantity(max)) : Speed(max);
        TRight(k.G, Language.T("common.peak", "value", scale), k.Small, k.Pad + wg - k.P(8), y + k.P(3), k.P(16), MUTED);
        y += hg + k.P(12);

        string[] tUp = { Language.T("network.up_now"), Language.T("network.up_average"), Language.T("network.sent") }, tDown = { Language.T("network.down_now"), Language.T("network.down_average"), Language.T("network.received") };
        int colUp = right - Math.Max(k.P(92), W(k.G, tDown[tab], k.Small) + k.P(14)), colDown = right;
        T(k.G, Language.T("common.program"), k.Small, k.Pad, y, k.P(18), MUTED);
        TRight(k.G, tUp[tab], k.Small, colUp, y, k.P(18), MUTED);
        TRight(k.G, tDown[tab], k.Small, colDown, y, k.P(18), MUTED);
        y += k.P(22);
        int hr = k.P(28), vh = hr * ScrollList.ROWS;
        if (el.Alt > 0) vh = Math.Max(hr * 3, Math.Min(vh, el.Alt - y - k.Pad));
        el.Step = hr; el.Visible = vh; el.Total = 0;
        el.Frame = new Rectangle(0, y, k.Width, vh); el.Track = el.Knob = Rectangle.Empty;
        if (network == null || !network.Activate) {
            string reason = network == null ? "" : network.Error;
            TTrim(k.G, Language.T("network.cannot_see", "reason", reason), k.Text, k.Pad, y, k.Width - 2 * k.Pad, k.P(26), MUTED);
            el.MoveBy(0);
            return y + vh + k.Pad;
        }
        var rows = network.Rows(tab);
        double seconds = tab == 0 ? 1 : Math.Max(1, Math.Min(60, network.CollectedSeconds()));
        el.Total = rows.Count * hr;
        el.MoveBy(0);
        if (rows.Count == 0) T(k.G, Language.T(tab == 0 ? "network.none_now" : "network.none"), k.Text, k.Pad, y, k.P(26), MUTED);
        var rowZones = k.Zone == null ? null : new List<Zone>();
        if (rows.Count > 0) using (var piece = new Bitmap(k.Width, vh, System.Drawing.Imaging.PixelFormat.Format32bppRgb)) {
            WithoutHalo(piece, g => {
                g.Clear(BACKGROUND);
                var kr = new Kit { G = g, S = k.S, Width = k.Width, Pad = k.Pad, Title = k.Title, Text = k.Text, Bold = k.Bold, Small = k.Small, Zone = rowZones };
                for (int i = el.ScrollOffset / hr; i < rows.Count && i * hr - el.ScrollOffset < vh; i++) OneNetworkRow(kr, rows[i], i * hr - el.ScrollOffset, hr, colUp, colDown, tab, seconds);
            });
            k.G.DrawImageUnscaled(piece, 0, y);
        }
        if (rowZones != null) foreach (var z in rowZones) {
            var r = Rectangle.Intersect(new Rectangle(z.R.X, z.R.Y + y, z.R.Width, z.R.Height), el.Frame);
            if (r.Height > 0) k.Zone.Add(new Zone(r, z.Name));
        }
        if (el.Scrolls) {
            int hp = Math.Max(k.P(28), vh * vh / el.Total), wb = k.P(7), xb = k.Width - k.P(11);
            el.Track = new Rectangle(k.Width - k.Pad, y, k.Pad, vh);
            el.Knob = new Rectangle(el.Track.X, y + (vh - hp) * el.ScrollOffset / (el.Total - vh), k.Pad, hp);
            DiskFull(k.G, new Rectangle(xb, y, wb, vh), wb / 2, EMPTY);
            DiskFull(k.G, new Rectangle(xb, el.Knob.Y, wb, hp), wb / 2, el.Started ? LABEL : MUTED);
        }
        return y + vh + k.Pad;
    }

    static void OneNetworkRow(Kit k, NetworkRow r, int y, int hr, int colUp, int colDown, int tab, double seconds) {
        int side = k.P(18);
        var ic = IconOf(r.App);
        var ri = new Rectangle(k.Pad, y + (hr - side) / 2, side, side);
        if (ic != null) Image(k.G, ic, ri);
        else {
            DiskFull(k.G, ri, k.P(4), EMPTY);
            string start = r.App.Name.Length > 0 ? r.App.Name.Substring(0, 1).ToUpperInvariant() : "?";
            TextRenderer.DrawText(k.G, start, k.Small, ri, TEXT, F | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        int xn = k.Pad + side + k.P(10);
        int textEnd = CloseButton(k, r.App, y, hr, colUp - k.P(92));
        TTrim(k.G, Explanations.Name(r.App), k.Text, xn, y, textEnd - k.P(4) - xn, hr, TEXT);
        if (tab == 2) {
            TRight(k.G, Quantity(r.Up), k.Text, colUp, y, hr, r.Up > 0 ? TEXT : MUTED);
            TRight(k.G, Quantity(r.Down), k.Text, colDown, y, hr, r.Down > 0 ? TEXT : MUTED);
        } else {
            double up = r.Up / seconds, down = r.Down / seconds;
            TRight(k.G, Speed(up), k.Text, colUp, y, hr, r.Up > 0 ? SpeedColor(up) : MUTED);
            TRight(k.G, Speed(down), k.Text, colDown, y, hr, r.Down > 0 ? SpeedColor(down) : MUTED);
        }
        int xf = colUp - k.P(86);
        T(k.G, "↑", k.Bold, xf, y, hr, r.UpNow ? LABEL : EMPTY);
        T(k.G, "↓", k.Bold, xf + k.P(10), y, hr, r.DownNow ? GOOD : EMPTY);
    }

    static int CoreRow(Kit k, int y, string code, string name, string detail, float usage, float temp) {
        int h = k.P(44), right = k.Width - k.Pad, wTemp = k.P(44), wUsage = k.P(58);
        T(k.G, code, k.Title, k.Pad, y, h, LABEL);
        int xm = k.Pad + k.P(64), wm = right - wTemp - wUsage - k.P(12) - xm;
        TTrim(k.G, name, k.Text, xm, y + k.P(4), wm, h / 2 - k.P(2), TEXT);
        TTrim(k.G, detail, k.Small, xm, y + h / 2, wm, h / 2 - k.P(4), MUTED);
        int xu = right - wTemp - wUsage;
        T(k.G, FormatPercent(usage), k.Bold, xu, y - k.P(3), h, TEXT);
        AccentBar(k, xu, y + h / 2 + k.P(9), wUsage - k.P(14), usage);
        TRight(k.G, FormatTemp(temp, false), k.Title, right, y, h, Hot(temp));
        return y + h;
    }

    static int VirtualRow(Kit k, int y, SensorData d) {
        int h = k.P(44), right = k.Width - k.Pad, wTemp = k.P(44), wUsage = k.P(58);
        bool low = CommitLow(d);
        T(k.G, COMMIT, k.Title, k.Pad, y, h, LABEL);
        int xm = k.Pad + k.P(64), wm = right - wTemp - wUsage - k.P(12) - xm;
        TTrim(k.G, Language.T("commit.title"), k.Text, xm, y + k.P(4), wm, h / 2 - k.P(2), TEXT);
        TTrim(k.G, CommitText(d, "overview.commit"), k.Small, xm, y + h / 2, wm, h / 2 - k.P(4), low ? RED : MUTED);
        int xu = right - wTemp - wUsage;
        float usage = d.CommitPercent();
        T(k.G, FormatPercent(usage), k.Bold, xu, y - k.P(3), h, low ? RED : TEXT);
        AccentBar(k, xu, y + h / 2 + k.P(9), wUsage - k.P(14), usage);
        TRight(k.G, FreeGb(d.CommitFree(), true), k.Bold, right, y, h, CommitColor(d));
        return y + h;
    }

    static int SentenceOnTop(Kit k, int y) {
        var f = Notification.Current ?? new Sentence { Text = Language.T("status.waiting") };
        int ya = y, h = k.P(24), side = k.P(8), xt = k.Pad + side + k.P(8);
        DiskFull(k.G, new Rectangle(k.Pad, y + (h - side) / 2, side, side), side / 2, f.Struggling ? AMBER : GOOD);
        y += TTwoRows(k.G, f.Text, k.Bold, xt, y, k.Width - k.Pad - xt, h, f.Struggling ? AMBER : TEXT);
        if (f.Subtext != null) y += TTwoRows(k.G, f.Subtext, k.Small, xt, y, k.Width - k.Pad - xt, k.P(18), MUTED);
        y += k.P(4);
        if (k.Zone != null && f.Struggling && f.Flyout != "all") k.Zone.Add(new Zone(new Rectangle(0, ya, k.Width, y - ya), "open:" + f.Flyout));
        return y;
    }

    const TextFormatFlags F_CAPTION = F | TextFormatFlags.NoPrefix;
    static void VersionCaption(Kit k, int left, int y, int h) {
        string s = LicenseLabel.FlyoutCaption();
        int w = TextRenderer.MeasureText(k.G, s, k.Small, new Size(9999, 999), F_CAPTION).Width, width = k.Width - k.Pad - left;
        var r = w <= width ? new Rectangle(k.Width - k.Pad - w, y, w, h) : new Rectangle(left, y, Math.Max(1, width), h);
        TextRenderer.DrawText(k.G, s, k.Small, r, MUTED, F_CAPTION | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }

    static int OverviewFlyout(Kit k, SensorData d, Network network) {
        int y = k.Pad, side = k.P(24);
        Image(k.G, Logo(), new Rectangle(k.Pad, y + k.P(1), side, side));
        T(k.G, "WeightMonitor", k.Title, k.Pad + side + k.P(8), y, k.P(26), TEXT);
        VersionCaption(k, k.Pad + side + k.P(8) + W(k.G, "WeightMonitor", k.Title) + k.P(12), y, k.P(26));
        y += k.P(34);
        y = Line(k, SentenceOnTop(k, y));
        int ya = y;
        y = CoreRow(k, y, "CPU", d.CpuName.Length > 0 ? d.CpuName : Language.T("overview.not_read"), Language.T("overview.processor"), d.Cpu.Usage, d.Cpu.Temp);
        if (k.Zone != null) k.Zone.Add(new Zone(new Rectangle(0, ya, k.Width, y - ya), "open:cpu"));
        y = Line(k, y); ya = y;
        y = CoreRow(k, y, "GPU", d.GpuName.Length > 0 ? d.GpuName : Language.T("overview.no_gpu"), Language.T(d.IntegratedGpu ? "overview.integrated_in_cpu" : "overview.dedicated"), d.Gpu.Usage, d.Gpu.Temp);
        if (k.Zone != null) k.Zone.Add(new Zone(new Rectangle(0, ya, k.Width, y - ya), "open:gpu"));
        y = Line(k, y); ya = y;
        string ram = d.TotalRam > 0 ? Language.T("overview.ram_in_use", "used", d.UsedRam.ToString("0.0", CultureInfo.InvariantCulture), "total", Math.Round(d.TotalRam).ToString("0")) : Language.T("overview.in_use");
        y = CoreRow(k, y, "RAM", ram, Language.T(d.Ram.Temp < 0 ? "common.no_module_temperature" : "overview.hottest_module"), d.Ram.Usage, d.Ram.Temp);
        if (k.Zone != null) k.Zone.Add(new Zone(new Rectangle(0, ya, k.Width, y - ya), "open:ram"));
        if (d.CommitFree() >= 0) {
            y = Line(k, y); ya = y;
            y = VirtualRow(k, y, d);
            if (k.Zone != null) k.Zone.Add(new Zone(new Rectangle(0, ya, k.Width, y - ya), "open:commit"));
        }
        y = Line(k, y);
        int y0 = y;
        y += k.P(6);
        T(k.G, Language.T("common.disks"), k.Bold, k.Pad, y, k.P(22), MUTED);
        y += k.P(22);
        foreach (var x in d.Disks) y = DiskRow(k, y, x, true);
        if (k.Zone != null) k.Zone.Add(new Zone(new Rectangle(0, y0, k.Width, y - y0), "open:disks"));
        y = Line(k, y + k.P(4));
        y0 = y;
        y += k.P(6);
        T(k.G, Language.T("common.network"), k.Bold, k.Pad, y, k.P(22), MUTED);
        TRight(k.G, "↑ " + Speed(d.Up) + "   ↓ " + Speed(d.Down), k.Bold, k.Width - k.Pad, y, k.P(22), TEXT);
        y += k.P(26);
        if (network != null && network.Activate) {
            int seconds = Math.Max(1, network.CollectedSeconds()), hr = k.P(26), li = k.P(16);
            foreach (var r in network.Rows(1).Take(3)) {
                var ic = IconOf(r.App);
                if (ic != null) Image(k.G, ic, new Rectangle(k.Pad, y + (hr - li) / 2, li, li));
                string howMuch = "↑ " + Speed(r.Up / (double)seconds) + "   ↓ " + Speed(r.Down / (double)seconds);
                int textEnd = CloseButton(k, r.App, y, hr, k.Width - k.Pad - W(k.G, howMuch, k.Small) - k.P(10));
                TTrim(k.G, Explanations.Name(r.App), k.Text, k.Pad + li + k.P(8), y, textEnd - k.P(4) - (k.Pad + li + k.P(8)), hr, TEXT);
                TRight(k.G, howMuch, k.Small, k.Width - k.Pad, y, hr, MUTED);
                y += hr;
            }
        }
        if (k.Zone != null) k.Zone.Add(new Zone(new Rectangle(0, y0, k.Width, y - y0), "open:network"));
        return y + k.Pad;
    }
}
