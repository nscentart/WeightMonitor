using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

public static partial class Drawing {
    static readonly CultureInfo IT = CultureInfo.InvariantCulture;

    public static Color PercentColor(double p) { return p < 0 ? MUTED : p < 25 ? GOOD : p < 50 ? YELLOW : p < 75 ? ORANGE : RED; }
    public static Color MemoryColor(double b) { double g = b / 1073741824.0; return g < 0.5 ? GOOD : g < 2 ? YELLOW : g < 8 ? ORANGE : RED; }
    public static Color DiskColor(double b) { double m = b / 1048576.0; return b <= 0 ? MUTED : m < 1 ? GOOD : m < 20 ? YELLOW : m < 200 ? ORANGE : RED; }

    public static string Giga(double b) {
        double g = b / 1073741824.0;
        return g >= 10 ? g.ToString("0", IT) + " GB" : g >= 1 ? g.ToString("0.0", IT) + " GB" : (b / 1048576.0).ToString("0", IT) + " MB";
    }
    static string FormatPercent(double v) { return FormatPercent((float)v); }
    static string Percent1(double v) { return v < 0 ? "--" : v < 0.05 ? "0%" : v < 10 ? v.ToString("0.0", IT) + "%" : Math.Round(v).ToString("0") + "%"; }
    static string Duration(double s) {
        long m = (long)(s / 60), h = m / 60, g = h / 24;
        return g > 0 ? Language.T("time.days_hours", "days", g, "hours", h % 24) : h > 0 ? Language.T("time.hours_minutes", "hours", h, "minutes", m % 60) : Language.T("time.minutes", "minutes", m);
    }
    static string Thousands(long n) { return Language.Number(n); }

    static int Tabs(Kit k, int y, int tab) {
        int hs = k.P(28), x = k.Pad;
        var TABS = TabTitles();
        for (int i = 0; i < TABS.Length; i++) {
            bool activate = i == tab;
            int w = W(k.G, TABS[i], k.Bold) + k.P(20);
            if (activate) DiskFull(k.G, new Rectangle(x, y, w, hs), k.P(6), ROW);
            T(k.G, TABS[i], k.Bold, x + k.P(10), y, hs, activate ? TEXT : MUTED);
            if (k.Zone != null) k.Zone.Add(new Zone(new Rectangle(x, y, w, hs), "tab:" + i));
            x += w + k.P(6);
        }
        return y + hs + k.P(10);
    }

    static void Chart(Kit k, Rectangle r, List<double[]> series, int[] columns, Color[] colors, double max, string caption) {
        DiskFull(k.G, r, k.P(6), CHART);
        if (series.Count > 1 && max > 0) {
            var old = k.G.SmoothingMode; k.G.SmoothingMode = SmoothingMode.AntiAlias;
            for (int c = columns.Length - 1; c >= 0; c--) {
                var points = new List<PointF>();
                for (int i = 0; i < series.Count; i++) {
                    double v = columns[c] < series[i].Length ? series[i][columns[c]] : 0;
                    float px = r.X + k.P(4) + (r.Width - k.P(8)) * (60 - series.Count + i) / 59f;
                    float py = r.Bottom - k.P(4) - (float)((r.Height - k.P(22)) * Math.Min(1, v / max));
                    points.Add(new PointF(px, py));
                }
                if (c == 0) {
                    var area = new List<PointF>(points);
                    area.Add(new PointF(points.Last().X, r.Bottom - k.P(4))); area.Add(new PointF(points[0].X, r.Bottom - k.P(4)));
                    using (var b = new SolidBrush(Color.FromArgb(50, colors[c]))) k.G.FillPolygon(b, area.ToArray());
                }
                using (var p = new Pen(colors[c], Math.Max(1f, 1.5f * k.S))) k.G.DrawLines(p, points.ToArray());
            }
            k.G.SmoothingMode = old;
        }
        if (caption != null) TRight(k.G, caption, k.Small, r.Right - k.P(8), r.Y + k.P(3), k.P(16), MUTED);
    }

    static List<double[]> SeriesOf(History st, int tab) {
        var series = st.Series(tab == 2);
        if (tab == 2 && series.Count > 1) series.RemoveAt(series.Count - 1);
        return series;
    }

    static void Cell(Kit k, Rectangle r, double fraction, Color color, string text) {
        DiskFull(k.G, r, k.P(3), CHART);
        int h = (int)(r.Height * Math.Max(0, Math.Min(1, fraction)));
        if (h > 0) {
            var old = k.G.Clip;
            k.G.SetClip(new Rectangle(r.X, r.Bottom - h, r.Width, h));
            DiskFull(k.G, r, k.P(3), Color.FromArgb(110, color));
            k.G.Clip = old;
        }
        TextRenderer.DrawText(k.G, text, k.Small, r, TEXT, F | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    static int Grid(Kit k, int y, string title, string right, int n, Func<int, double> fraction, Func<int, Color> color, Func<int, string> text) {
        if (n == 0) return y;
        T(k.G, title, k.Small, k.Pad, y, k.P(18), MUTED);
        if (right != null) TRight(k.G, right, k.Small, k.Width - k.Pad, y, k.P(18), MUTED);
        y += k.P(20);
        int wg = k.Width - 2 * k.Pad, gap = k.P(3);
        int perRow = Math.Max(1, Math.Min(n, (wg + gap) / (k.P(24) + gap)));
        int rows = (n + perRow - 1) / perRow;
        perRow = (n + rows - 1) / rows;
        int w = (wg - (perRow - 1) * gap) / perRow, h = k.P(24);
        for (int i = 0; i < n; i++) {
            int x = k.Pad + (i % perRow) * (w + gap), yy = y + (i / perRow) * (h + gap);
            Cell(k, new Rectangle(x, yy, w, h), fraction(i), color(i), text(i));
        }
        return y + rows * (h + gap) + k.P(6);
    }

    static int Temperature(Kit k, int y, List<KeyValuePair<string, float>> temp, string without) {
        int h = k.P(22), x = k.Pad;
        string title = Language.T("temps.title");
        T(k.G, title, k.Small, x, y, h, MUTED);
        x += Math.Max(k.P(92), W(k.G, title, k.Small) + k.P(10));
        if (temp.Count == 0) { TTrim(k.G, without, k.Small, x, y, k.Width - k.Pad - x, h, MUTED); return y + h; }
        foreach (var t in temp) {
            string name = Sensors.VisibleName(t.Key);
            T(k.G, name, k.Small, x, y, h, MUTED); x += W(k.G, name, k.Small) + k.P(5);
            string g = FormatTemp(t.Value, false);
            T(k.G, g, k.Bold, x, y, h, Hot(t.Value)); x += W(k.G, g, k.Bold) + k.P(16);
        }
        return y + h;
    }

    static int AppRow(Kit k, int y, int h, App a, bool started, int nameEnd, string[] values, Color[] colors, int[] rights) {
        int side = k.P(18);
        var ic = IconOf(a);
        var ri = new Rectangle(k.Pad, y + (h - side) / 2, side, side);
        if (ic != null) Image(k.G, ic, ri);
        else {
            DiskFull(k.G, ri, k.P(4), EMPTY);
            string start = a.Name.Length > 0 ? a.Name.Substring(0, 1).ToUpperInvariant() : "?";
            TextRenderer.DrawText(k.G, start, k.Small, ri, TEXT, F | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        int xn = k.Pad + side + k.P(10);
        int textEnd = CloseButton(k, a, y, h, nameEnd - k.P(14));
        TTrim(k.G, Explanations.Name(a), k.Text, xn, y, textEnd - k.P(4) - xn, h, TEXT);
        int dot = k.P(6);
        DiskFull(k.G, new Rectangle(nameEnd - k.P(10), y + (h - dot) / 2, dot, dot), dot / 2, started ? GOOD : EMPTY);
        for (int i = 0; i < values.Length; i++) TRight(k.G, values[i], k.Text, rights[i], y, h, colors[i]);
        y += h;
        string explain = Explanations.Row(a);
        if (explain != null) { TTrim(k.G, explain, k.Small, xn, y - k.P(5), k.Width - k.Pad - xn, k.P(16), MUTED); y += k.P(13); }
        return y;
    }

    static int CloseButton(Kit k, App a, int y, int h, int right) {
        if (!ProgramCloser.Yes(a)) return right;
        string CLOSE_BUTTON = Language.T("common.close");
        int w = W(k.G, CLOSE_BUTTON, k.Small) + k.P(12), height = k.P(19);
        var r = new Rectangle(right - w, y + (h - height) / 2, w, height);
        DiskFull(k.G, r, k.P(4), CAPSULE);
        TextRenderer.DrawText(k.G, CLOSE_BUTTON, k.Small, r, TEXT, F | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (k.Zone != null) { ProgramCloser.Remember(a); k.Zone.Add(new Zone(r, "close:" + a.Key)); }
        return r.X;
    }

    static int AppTitles(Kit k, int y, string[] titles, int[] rights) {
        T(k.G, Language.T("common.program"), k.Small, k.Pad, y, k.P(18), MUTED);
        for (int i = 0; i < titles.Length; i++) TRight(k.G, titles[i], k.Small, rights[i], y, k.P(18), MUTED);
        return y + k.P(22);
    }

    const int PROGRAMS = 10;
    static int Other(Kit k, int y, int amount) {
        if (amount <= PROGRAMS) return y;
        T(k.G, Language.T("common.other_programs", "count", amount - PROGRAMS), k.Small, k.Pad, y, k.P(20), MUTED);
        return y + k.P(20);
    }
    static int None(Kit k, int y, string text) { T(k.G, text, k.Text, k.Pad, y, k.P(26), MUTED); return y + k.P(26); }

    static int Header(Kit k, string title, float usage, float temp, string row1, string row2) {
        int y = k.Pad, h = k.P(26), right = k.Width - k.Pad;
        T(k.G, title, k.Title, k.Pad, y, h, TEXT);
        string g = FormatTemp(temp, false);
        TRight(k.G, g, k.Title, right, y, h, Hot(temp));
        TRight(k.G, FormatPercent(usage), k.Title, right - W(k.G, g, k.Title) - k.P(12), y, h, PercentColor(usage));
        y += h + k.P(2);
        if (row1 != null) { TTrim(k.G, row1, k.Small, k.Pad, y, k.Width - 2 * k.Pad, k.P(18), MUTED); y += k.P(18); }
        if (row2 != null) { TTrim(k.G, row2, k.Small, k.Pad, y, k.Width - 2 * k.Pad, k.P(18), MUTED); y += k.P(18); }
        return y + k.P(10);
    }

    static string Join(params string[] parts) { return string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p))); }

    static int CpuFlyout(Kit k, SensorData d, Details x, int tab) {
        var o = x.Current;
        string row1 = Join(d.CpuName, o.Core > 0 ? Language.T("cpu.cores_threads", "cores", o.Core, "threads", o.Thread) : null);
        string row2 = Join(o.GhzAverage > 0 ? o.GhzAverage.ToString("0.00", IT) + " GHz" : null, o.CpuWatt > 0 ? o.CpuWatt.ToString("0", IT) + " W" : null,
                              o.Processes > 0 ? Language.T("cpu.processes", "count", Thousands(o.Processes)) : null, o.ThreadTotal > 0 ? Language.T("cpu.threads", "count", Thousands(o.ThreadTotal)) : null,
                              o.Uptime > 0 ? Language.T("cpu.uptime", "duration", Duration(o.Uptime)) : null);
        int y = Header(k, "CPU", d.Cpu.Usage, d.Cpu.Temp, row1, row2);
        y = Tabs(k, y, tab);

        var series = SeriesOf(x.Cpu, tab);
        var average = x.Cpu.Average(tab == 0 ? 1 : tab);
        double peak = series.Count == 0 ? 0 : series.Max(v => v[0]);
        Chart(k, new Rectangle(k.Pad, y, k.Width - 2 * k.Pad, k.P(64)), series, new[] { 0 }, new[] { LABEL }, 100,
                Language.T("temps.average_peak", "average", FormatPercent(average[0]), "peak", FormatPercent(peak)));
        y += k.P(64) + k.P(12);

        var thread = tab == 0 ? x.Cpu.Last() : x.Cpu.Average(tab);
        int nt = Math.Max(0, thread.Length - 1);
        y = Grid(k, y, Language.T("cpu.thread_usage", "average", Language.T(tab == 0 ? "temps.now" : "temps.average")), null, nt, i => thread[1 + i] / 100.0, i => PercentColor(thread[1 + i]),
                    i => Math.Round(thread[1 + i]).ToString("0"));
        if (o.CoreGhz.Length > 0) {
            float max = Math.Max(1f, o.CoreGhz.Max());
            y = Grid(k, y, Language.T("cpu.frequency"), "GHz", o.CoreGhz.Length, i => o.CoreGhz[i] / max,
                        i => ACCENT, i => o.CoreGhz[i].ToString("0.0", IT));
        }
        y = Temperature(k, y, o.CpuTemp, Language.T("cpu.needs_driver"));
        y = Line(k, y + k.P(8)) + k.P(8);

        int colUsage = k.Width - k.Pad;
        y = AppTitles(k, y, new[] { Language.T(tab == 0 ? "cpu.col_now" : "cpu.col_average") }, new[] { colUsage });
        int amount;
        var rows = x.Cpu.Rows(tab, 0, out amount);
        if (rows.Count == 0) y = None(k, y, Language.T("cpu.none"));
        int hr = k.P(28);
        foreach (var r in rows.Take(PROGRAMS)) {
            double v = r.Sum[0] / amount;
            y = AppRow(k, y, hr, r.App, r.Current[0] >= 0.5, colUsage - k.P(70), new[] { Percent1(v) }, new[] { PercentColor(v) }, new[] { colUsage });
        }
        return Other(k, y, rows.Count) + k.Pad;
    }

    static int GpuFlyout(Kit k, SensorData d, Details x, int tab) {
        var o = x.Current;
        string vram = o.TotalVram > 0 && o.UsedVram >= 0 ? Language.T("gpu.memory_used_of_total", "used", Giga(o.UsedVram * 1048576.0), "total", Giga(o.TotalVram * 1048576.0)) : null;
        string row1 = Join(d.GpuName.Length > 0 ? d.GpuName : Language.T("gpu.none_readable"), d.GpuName.Length > 0 ? Language.T(d.IntegratedGpu ? "gpu.integrated" : "gpu.dedicated") : null);
        string row2 = Join(vram, o.GpuMhz > 0 ? (o.GpuMhz / 1000f).ToString("0.00", IT) + " GHz" : null, o.GpuWatt > 0 ? o.GpuWatt.ToString("0", IT) + " W" : null,
                              o.Fan >= 0 ? (o.Fan > 0 ? Language.T("gpu.fans", "rpm", o.Fan.ToString("0", IT)) : Language.T("gpu.fans_stopped")) : null);
        int y = Header(k, "GPU", d.Gpu.Usage, d.Gpu.Temp, row1, row2);
        y = Tabs(k, y, tab);

        var series = SeriesOf(x.Gpu, tab);
        var average = x.Gpu.Average(tab == 0 ? 1 : tab);
        Chart(k, new Rectangle(k.Pad, y, k.Width - 2 * k.Pad, k.P(64)), series, new[] { 0, 1 }, new[] { LABEL, GOOD }, 100,
                Language.T("gpu.usage_average", "average", FormatPercent(average[0]), "memory", FormatPercent(average[1])));
        y += k.P(64) + k.P(12);

        if (o.Engines.Count > 0) {
            T(k.G, Language.T("gpu.engines"), k.Small, k.Pad, y, k.P(18), MUTED);
            y += k.P(22);
            int column = (k.Width - 2 * k.Pad - k.P(20)) / 2, hm = k.P(24);
            for (int i = 0; i < o.Engines.Count; i++) {
                int xm = k.Pad + (i % 2) * (column + k.P(20)), ym = y + (i / 2) * hm;
                var m = o.Engines[i];
                TTrim(k.G, Sensors.VisibleName(m.Key), k.Small, xm, ym, column - k.P(84), hm - k.P(6), TEXT);
                TRight(k.G, FormatPercent(m.Value), k.Bold, xm + column, ym, hm - k.P(6), PercentColor(m.Value));
                int xb = xm + column - k.P(80), wb = k.P(38), hb = Math.Max(2, k.P(4));
                DiskFull(k.G, new Rectangle(xb, ym + (hm - k.P(6) - hb) / 2, wb, hb), hb / 2, EMPTY);
                if (m.Value > 0) DiskFull(k.G, new Rectangle(xb, ym + (hm - k.P(6) - hb) / 2, Math.Max(hb, (int)(wb * m.Value / 100f)), hb), hb / 2, PercentColor(m.Value));
            }
            y += (o.Engines.Count + 1) / 2 * hm + k.P(4);
        }
        if (o.Shared >= 0) {
            T(k.G, Language.T("gpu.shared_memory", "value", Giga(o.Shared * 1048576.0)), k.Small, k.Pad, y, k.P(20), MUTED);
            y += k.P(20);
        }
        y = Temperature(k, y, o.GpuTemp, Language.T("gpu.no_temperature"));
        y = Line(k, y + k.P(8)) + k.P(8);

        string tVram = Language.T("gpu.col_memory");
        int colVram = k.Width - k.Pad, colUsage = colVram - Math.Max(k.P(100), W(k.G, tVram, k.Small) + k.P(14));
        y = AppTitles(k, y, new[] { Language.T(tab == 0 ? "gpu.col_now" : "gpu.col_average"), tVram }, new[] { colUsage, colVram });
        int amount;
        var rows = x.Gpu.Rows(tab, 0, out amount);
        rows.Sort((a, b) => (b.Sum[0] * 1e12 + b.Sum[1]).CompareTo(a.Sum[0] * 1e12 + a.Sum[1]));
        if (rows.Count == 0) y = None(k, y, Language.T("gpu.none"));
        int hr = k.P(28);
        foreach (var r in rows.Take(PROGRAMS)) {
            double v = r.Sum[0] / amount, mem = r.Sum[1] / amount;
            y = AppRow(k, y, hr, r.App, r.Current[0] >= 0.5, colUsage - k.P(56), new[] { Percent1(v), mem > 0 ? Giga(mem) : "--" },
                    new[] { v > 0 ? PercentColor(v) : MUTED, mem > 0 ? MemoryColor(mem) : MUTED }, new[] { colUsage, colVram });
        }
        return Other(k, y, rows.Count) + k.Pad;
    }

    static int RamFlyout(Kit k, SensorData d, Details x, int tab) {
        var o = x.Current;
        string row1 = o.TotalRam > 0 ? Language.T("ram.used_of_total", "used", Giga(o.UsedRam), "total", Giga(o.TotalRam)) : null;
        int y = Header(k, "RAM", d.Ram.Usage, d.Ram.Temp, row1, Language.T(d.Ram.Temp < 0 ? "common.no_module_temperature" : "ram.module_temperature"));
        y = Tabs(k, y, tab);

        var series = SeriesOf(x.Ram, tab);
        var average = x.Ram.Average(tab == 0 ? 1 : tab);
        double peak = series.Count == 0 ? 0 : series.Max(v => v[0]);
        Chart(k, new Rectangle(k.Pad, y, k.Width - 2 * k.Pad, k.P(64)), series, new[] { 0 }, new[] { LABEL }, 100,
                Language.T("temps.average_peak", "average", FormatPercent(average[0]), "peak", FormatPercent(peak)));
        y += k.P(64) + k.P(14);

        if (o.TotalRam > 0) {
            var pieces = new[] {
                new KeyValuePair<double, Color>(o.UsedRam, ACCENT), new KeyValuePair<double, Color>(o.Modified, Theme.Current.Modified),
                new KeyValuePair<double, Color>(Math.Max(0, o.Cache - o.Modified), Theme.Current.Cache),
                new KeyValuePair<double, Color>(o.PlaceDetached, EMPTY) };
            int wb = k.Width - 2 * k.Pad, hb = k.P(14), xb = k.Pad;
            var old = k.G.Clip;
            using (var p = Round(new Rectangle(k.Pad, y, wb, hb), k.P(4))) {
                k.G.SetClip(p);
                foreach (var pz in pieces) {
                    int w = (int)Math.Round(wb * pz.Key / o.TotalRam);
                    using (var b = new SolidBrush(pz.Value)) k.G.FillRectangle(b, xb, y, w, hb);
                    xb += w;
                }
                using (var b = new SolidBrush(EMPTY)) if (xb < k.Pad + wb) k.G.FillRectangle(b, xb, y, k.Pad + wb - xb, hb);
                k.G.Clip = old;
            }
            y += hb + k.P(8);
            var entries = new[] {
                new[] { Language.T("ram.in_use"), Giga(o.UsedRam) }, new[] { Language.T("ram.committed") + " ›", Language.T("ram.pair_of_total", "used", Giga(o.Committed), "total", Giga(o.Limit)) },
                new[] { Language.T("ram.cached"), Giga(o.Cache) }, new[] { Language.T("ram.modified"), Giga(o.Modified) },
                new[] { Language.T("ram.free"), Giga(o.PlaceDetached) }, new[] { Language.T("ram.compressed"), o.Compressed > 0 ? Giga(o.Compressed) : "--" },
                new[] { Language.T("ram.paged_pool"), Giga(o.Paging) }, new[] { Language.T("ram.nonpaged_pool"), Giga(o.NonPaging) } };
            var marks = new Color[] { ACCENT, Color.Empty, Theme.Current.Cache, Theme.Current.Modified, EMPTY, Color.Empty, Color.Empty, Color.Empty };
            bool lowCommit = o.Limit > 0 && o.Committed >= 0 && (o.Limit - o.Committed) / 1073741824.0 < CommitReserve();
            int column = (k.Width - 2 * k.Pad - k.P(20)) / 2, hv = k.P(21);
            for (int i = 0; i < entries.Length; i++) {
                int xv = k.Pad + (i % 2) * (column + k.P(20)), yv = y + (i / 2) * hv, q = k.P(8);
                if (marks[i] != Color.Empty) DiskFull(k.G, new Rectangle(xv, yv + (hv - q) / 2, q, q), k.P(2), marks[i]);
                TTrim(k.G, entries[i][0], k.Small, xv + k.P(14), yv, column - k.P(14) - W(k.G, entries[i][1], k.Bold) - k.P(6), hv, MUTED);
                TRight(k.G, entries[i][1], k.Bold, xv + column, yv, hv, i == 1 && lowCommit ? RED : TEXT);
                if (i == 1 && k.Zone != null) k.Zone.Add(new Zone(new Rectangle(xv, yv, column, hv), "open:commit"));
            }
            y += (entries.Length + 1) / 2 * hv;
        }
        y = Line(k, y + k.P(8)) + k.P(8);

        int colMem = k.Width - k.Pad;
        y = AppTitles(k, y, new[] { Language.T(tab == 0 ? "ram.col_now" : "ram.col_average") }, new[] { colMem });
        int amount;
        var rows = x.Ram.Rows(tab, 0, out amount);
        if (rows.Count == 0) y = None(k, y, Language.T("ram.none"));
        int hr = k.P(28);
        foreach (var r in rows.Take(PROGRAMS)) {
            double v = r.Sum[0] / amount;
            y = AppRow(k, y, hr, r.App, r.Current[0] > 0, colMem - k.P(80), new[] { Giga(v) }, new[] { MemoryColor(v) }, new[] { colMem });
        }
        return Other(k, y, rows.Count) + k.Pad;
    }

    const int TOP_COMMIT = 5;
    static string GigaOrNone(double b) { return b < 0 ? "--" : Giga(b); }

    static int CommitFlyout(Kit k, SensorData d, Details x, int tab) {
        var o = x.Current;
        double free = o.Limit > 0 ? Math.Max(0, o.Limit - o.Committed) : -1;
        bool low = free >= 0 && free / 1073741824.0 < CommitReserve();
        double percent = o.Limit > 0 ? Math.Min(100, o.Committed / o.Limit * 100.0) : -1;
        int y = k.Pad, h = k.P(26);
        T(k.G, Language.T("commit.title"), k.Title, k.Pad, y, h, TEXT);
        TRight(k.G, FormatPercent(percent), k.Title, k.Width - k.Pad, y, h, low ? RED : PercentColor(percent));
        y += h + k.P(2);
        if (o.Limit > 0) {
            TTrim(k.G, Language.T("commit.used_of_limit", "used", Giga(o.Committed), "limit", Giga(o.Limit), "free", Giga(free)), k.Small, k.Pad, y, k.Width - 2 * k.Pad, k.P(18), low ? RED : MUTED);
            y += k.P(18);
        }
        y += TTwoRows(k.G, Language.T("commit.explain"), k.Small, k.Pad, y, k.Width - 2 * k.Pad, k.P(18), MUTED) + k.P(10);
        y = Tabs(k, y, tab);

        var series = SeriesOf(x.Virtual, tab);
        var average = x.Virtual.Average(tab == 0 ? 1 : tab);
        double peak = series.Count == 0 ? 0 : series.Max(v => v[0]);
        Chart(k, new Rectangle(k.Pad, y, k.Width - 2 * k.Pad, k.P(64)), series, new[] { 0 }, new[] { low ? RED : LABEL }, 100,
                Language.T("temps.average_peak", "average", FormatPercent(average[0]), "peak", FormatPercent(peak)));
        y += k.P(64) + k.P(14);

        double inFile = Math.Max(0, o.PageFileUsed), inRam = Math.Max(0, o.Committed - inFile);
        if (o.Limit > 0) {
            int wb = k.Width - 2 * k.Pad, hb = k.P(14), xb = k.Pad;
            var old = k.G.Clip;
            using (var p = Round(new Rectangle(k.Pad, y, wb, hb), k.P(4))) {
                k.G.SetClip(p);
                using (var b = new SolidBrush(EMPTY)) k.G.FillRectangle(b, k.Pad, y, wb, hb);
                foreach (var pz in new[] { new KeyValuePair<double, Color>(inRam, ACCENT), new KeyValuePair<double, Color>(inFile, ORANGE) }) {
                    int w = (int)Math.Round(wb * Math.Min(1, pz.Key / o.Limit));
                    using (var b = new SolidBrush(pz.Value)) k.G.FillRectangle(b, xb, y, w, hb);
                    xb += w;
                }
                k.G.Clip = old;
            }
            double reserve = CommitReserve() * 1073741824.0;
            if (reserve > 0 && reserve < o.Limit) {
                int xr = k.Pad + (int)Math.Round(wb * (o.Limit - reserve) / o.Limit);
                using (var p = new Pen(RED, Math.Max(1f, 2f * k.S))) k.G.DrawLine(p, xr, y - k.P(3), xr, y + hb + k.P(3));
            }
            y += hb + k.P(8);
        }
        bool known = o.Limit > 0;
        var entries = new[] {
            new[] { Language.T("commit.used"), GigaOrNone(known ? o.Committed : -1) }, new[] { Language.T("commit.limit"), GigaOrNone(known ? o.Limit : -1) },
            new[] { Language.T("commit.in_ram"), GigaOrNone(known ? inRam : -1) }, new[] { Language.T("commit.free"), GigaOrNone(free) },
            new[] { Language.T("commit.in_paging_file"), GigaOrNone(o.PageFileUsed) }, new[] { Language.T("commit.paging_file_size"), GigaOrNone(o.PageFileSize) },
            new[] { Language.T("commit.peak"), GigaOrNone(o.CommitPeak) }, new[] { Language.T("commit.physical"), GigaOrNone(o.TotalRam > 0 ? o.TotalRam : -1) } };
        var marks = new Color[] { Color.Empty, Color.Empty, ACCENT, EMPTY, ORANGE, Color.Empty, Color.Empty, Color.Empty };
        int column = (k.Width - 2 * k.Pad - k.P(20)) / 2, hv = k.P(21);
        for (int i = 0; i < entries.Length; i++) {
            int xv = k.Pad + (i % 2) * (column + k.P(20)), yv = y + (i / 2) * hv, q = k.P(8);
            if (marks[i] != Color.Empty) DiskFull(k.G, new Rectangle(xv, yv + (hv - q) / 2, q, q), k.P(2), marks[i]);
            TTrim(k.G, entries[i][0], k.Small, xv + k.P(14), yv, column - k.P(14) - W(k.G, entries[i][1], k.Bold) - k.P(6), hv, MUTED);
            TRight(k.G, entries[i][1], k.Bold, xv + column, yv, hv, i == 3 && low ? RED : TEXT);
        }
        y += (entries.Length + 1) / 2 * hv + k.P(6);
        if (free >= 0) {
            string advice = Language.T(low ? "commit.low" : "commit.ok", "free", Giga(free));
            y += TTwoRows(k.G, advice, low ? k.Bold : k.Small, k.Pad, y, k.Width - 2 * k.Pad, k.P(18), low ? RED : GOOD);
        }
        y = Line(k, y + k.P(8)) + k.P(8);

        int colMem = k.Width - k.Pad;
        y = AppTitles(k, y, new[] { Language.T(tab == 0 ? "commit.col_now" : "commit.col_average") }, new[] { colMem });
        int amount;
        var rows = x.Virtual.Rows(tab, 0, out amount);
        if (rows.Count == 0) y = None(k, y, Language.T("commit.none"));
        int hr = k.P(28);
        foreach (var r in rows.Take(TOP_COMMIT)) {
            double v = r.Sum[0] / amount;
            y = AppRow(k, y, hr, r.App, r.Current[0] > 0, colMem - k.P(80), new[] { Giga(v) }, new[] { MemoryColor(v) }, new[] { colMem });
        }
        if (rows.Count > TOP_COMMIT) {
            T(k.G, Language.T("common.other_programs", "count", rows.Count - TOP_COMMIT), k.Small, k.Pad, y, k.P(20), MUTED);
            y += k.P(20);
        }
        return y + k.Pad;
    }

    static int DiskDetailFlyout(Kit k, SensorData d, Details x, int tab) {
        int y = k.Pad, right = k.Width - k.Pad;
        y = Header(k, y, Language.T("common.disks"), DiskSummary(d), MUTED);
        y = Tabs(k, y, tab);

        var series = SeriesOf(x.Disks, tab);
        var average = x.Disks.Average(tab);
        bool perMinute = tab == 2;
        bool first = true;
        foreach (var disk in d.Disks) {
            if (!first) y = Line(k, y + k.P(4)) + k.P(2);
            first = false;
            y = DiskRow(k, y, disk, false);
            int n = disk.Number;
            if (n < 0 || n >= Details.DISKS) continue;
            double max = Math.Max(1048576, series.Count == 0 ? 0 : series.Max(v => Math.Max(v[2 * n], v[2 * n + 1])));
            var r = new Rectangle(k.Pad, y + k.P(2), k.Width - 2 * k.Pad, k.P(44));
            Chart(k, r, series, new[] { 2 * n, 2 * n + 1 }, new[] { LABEL, GOOD }, max, Language.T("common.peak", "value", Speed(max)));
            string l = Language.T("disks.read", "value", Speed(average[2 * n])), s = Language.T("disks.written", "value", Speed(average[2 * n + 1]));
            T(k.G, l, k.Small, r.X + k.P(8), r.Y + k.P(3), k.P(16), average[2 * n] > 0 ? DiskColor(average[2 * n]) : MUTED);
            T(k.G, s, k.Small, r.X + k.P(16) + W(k.G, l, k.Small), r.Y + k.P(3), k.P(16), average[2 * n + 1] > 0 ? DiskColor(average[2 * n + 1]) : MUTED);
            y = r.Bottom + k.P(2);
        }
        if (d.Disks.Count == 0) y = None(k, y, Language.T("disks.none"));
        if (d.Disks.Any(z => z.Temp < 0)) {
            TTrim(k.G, Language.T("disks.no_temperature"), k.Small, k.Pad, y + k.P(4), k.Width - 2 * k.Pad, k.P(18), MUTED);
            y += k.P(22);
        }
        y = Line(k, y + k.P(8)) + k.P(8);

        string[] tRead = { Language.T("disks.col_read_per_second"), Language.T("disks.col_down_average"), Language.T("disks.col_read") }, tWritten = { Language.T("disks.col_written_per_second"), Language.T("disks.col_up_average"), Language.T("disks.col_written") };
        int colWritten = right, colRead = right - Math.Max(k.P(92), W(k.G, tWritten[tab], k.Small) + k.P(14));
        y = AppTitles(k, y, new[] { tRead[tab], tWritten[tab] }, new[] { colRead, colWritten });
        int amount;
        var rows = x.Disks.Rows(tab, 0, out amount);
        rows.Sort((a, b) => (b.Sum[0] + b.Sum[1]).CompareTo(a.Sum[0] + a.Sum[1]));
        if (rows.Count == 0) y = None(k, y, Language.T(tab == 0 ? "disks.none_now" : "disks.no_program"));
        int hr = k.P(28);
        foreach (var r in rows.Take(PROGRAMS)) {
            string[] v; Color[] c;
            if (perMinute) {
                v = new[] { Quantity(r.Sum[0]), Quantity(r.Sum[1]) };
                c = new[] { r.Sum[0] > 0 ? TEXT : MUTED, r.Sum[1] > 0 ? TEXT : MUTED };
            } else {
                double le = r.Sum[0] / amount, sc = r.Sum[1] / amount;
                v = new[] { Speed(le), Speed(sc) };
                c = new[] { DiskColor(le), DiskColor(sc) };
            }
            y = AppRow(k, y, hr, r.App, r.Current[0] + r.Current[1] > 0, colRead - k.P(84), v, c, new[] { colRead, colWritten });
        }
        y = Other(k, y, rows.Count);
        if (!x.Current.DiskEtw) {
            y += k.P(4);
            TTrim(k.G, Language.T("disks.no_admin"), k.Small, k.Pad, y, k.Width - 2 * k.Pad, k.P(18), MUTED);
            y += k.P(18);
        }
        return y + k.Pad;
    }
}
