using System;
using System.Collections.Generic;
using System.Linq;

public class History {
    class Piece { public double[] Total; public Dictionary<App, double[]> App; public int Seconds; }
    readonly int size;
    readonly Queue<Piece> seconds = new Queue<Piece>(), minutes = new Queue<Piece>();
    Piece minute;
    long minuteNumber = -1;

    public History(int size) { this.size = size; }

    static void Sum(Dictionary<App, double[]> where, App a, double[] v) {
        double[] c;
        if (!where.TryGetValue(a, out c)) { c = new double[v.Length]; where[a] = c; }
        for (int i = 0; i < v.Length && i < c.Length; i++) c[i] += v[i];
    }

    public void Add(double[] total, Dictionary<App, double[]> app) { Add(total, app, DateTime.Now.Ticks / TimeSpan.TicksPerMinute); }
    public void Add(double[] total, Dictionary<App, double[]> app, long m) {
        lock (this) {
            seconds.Enqueue(new Piece { Total = total, App = app, Seconds = 1 });
            while (seconds.Count > 60) seconds.Dequeue();
            if (m != minuteNumber) {
                if (minute != null) { minutes.Enqueue(minute); while (minutes.Count > 59) minutes.Dequeue(); }
                minute = new Piece { Total = new double[size], App = new Dictionary<App, double[]>() };
                minuteNumber = m;
            }
            for (int i = 0; i < total.Length && i < size; i++) minute.Total[i] += total[i];
            foreach (var kv in app) Sum(minute.App, kv.Key, kv.Value);
            minute.Seconds++;
        }
    }

    IEnumerable<Piece> Window(int tab) {
        if (tab == 2) return minute == null ? minutes.ToList() : minutes.Concat(new[] { minute }).ToList();
        if (tab == 0) return seconds.Skip(Math.Max(0, seconds.Count - 1)).ToList();
        return seconds.ToList();
    }

    public List<double[]> Series(bool perMinute) {
        lock (this) {
            if (!perMinute) return seconds.Select(p => p.Total).ToList();
            return Window(2).Where(p => p.Seconds > 0).Select(p => p.Total.Select(v => v / p.Seconds).ToArray()).ToList();
        }
    }

    public double[] Average(int tab) {
        lock (this) {
            var r = new double[size]; int n = 0;
            foreach (var p in Window(tab)) { for (int i = 0; i < size && i < p.Total.Length; i++) r[i] += p.Total[i]; n += p.Seconds; }
            if (n > 0) for (int i = 0; i < size; i++) r[i] /= n;
            return r;
        }
    }

    public double[] Last() { lock (this) return seconds.Count == 0 ? new double[size] : seconds.Last().Total; }

    public List<DetailRow> Rows(int tab, int order, out int amount) {
        var total = new Dictionary<App, double[]>();
        Dictionary<App, double[]> last;
        lock (this) {
            amount = 0;
            foreach (var p in Window(tab)) { foreach (var kv in p.App) Sum(total, kv.Key, kv.Value); amount += p.Seconds; }
            last = seconds.Count == 0 ? null : seconds.Last().App;
        }
        var rows = new List<DetailRow>();
        foreach (var kv in total) {
            if (kv.Value.All(v => v <= 0)) continue;
            double[] u = null;
            if (last != null) last.TryGetValue(kv.Key, out u);
            rows.Add(new DetailRow { App = kv.Key, Sum = kv.Value, Current = u ?? new double[kv.Value.Length] });
        }
        rows.Sort((a, b) => b.Sum[order].CompareTo(a.Sum[order]));
        amount = Math.Max(1, amount);
        return rows;
    }
}
