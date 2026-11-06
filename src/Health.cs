using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

public partial class Health {
    const double ENTER = 90, EXIT = 75, STABLE = 30, CALM = 20, GAP = 3, AFTER_FREEZE = 120, CULPRIT = 25;
    static readonly string[] PIECES = { "cpu", "gpu", "ram" };

    class State { public double From = double.NaN, High = double.NegativeInfinity, Above = double.NegativeInfinity; public bool Struggling; }
    readonly Dictionary<string, State> states = new Dictionary<string, State>();
    readonly Dictionary<string, double> averages = new Dictionary<string, double>();
    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    double seen = double.NegativeInfinity, frozen = double.NegativeInfinity;
    readonly State CpuHot = new State(), GpuHot = new State();

    bool Burns(State s, double degrees, double threshold, double now) {
        if (degrees < 0 || degrees > Notifications.HOT_IMPLAUSIBLE) { s.From = double.NaN; s.Struggling = false; return false; }
        if (degrees >= threshold) {
            if (double.IsNaN(s.From)) s.From = now;
            s.Above = now;
            if (now - s.From >= Notifications.HOT_STABLE) s.Struggling = true;
        } else if (now - s.Above > GAP) s.From = double.NaN;
        if (degrees >= threshold - 5) s.High = now;
        else if (s.Struggling && now - s.High >= CALM) s.Struggling = false;
        return s.Struggling;
    }

    public Sentence Tick(Sample c, double now) {
        if (now - seen > 5) { states.Clear(); averages.Clear(); entries.Clear(); CpuHot.From = GpuHot.From = double.NaN; CpuHot.Struggling = GpuHot.Struggling = false; }
        seen = now;
        if (c.Stall >= 1) frozen = now;
        foreach (var k in averages.Keys.ToList()) averages[k] *= 0.8;
        foreach (var v in c.Entries) {
            string k = v.Piece + ":" + v.Key;
            double m;
            averages.TryGetValue(k, out m);
            averages[k] = m + v.Percent * 0.2;
            entries[k] = v;
        }
        double[] values = { c.Cpu, c.Gpu, c.Ram };
        int worstIndex = -1;
        for (int i = 0; i < 3; i++)
            if (IsCounted(PIECES[i], values[i], now) && (worstIndex < 0 || values[i] > values[worstIndex])) worstIndex = i;
        foreach (var k in states.Keys.Where(x => x.StartsWith("disk") && !c.DiskUsage.ContainsKey(x)).ToList()) states.Remove(k);
        string worstDisk = null;
        foreach (var kv in c.DiskUsage.OrderByDescending(x => x.Value).ThenBy(x => x.Key).ToList())
            if (IsCounted(kv.Key, kv.Value, now) && worstDisk == null) worstDisk = kv.Key;
        string system = c.SystemDisk != null && c.DiskUsage.ContainsKey(c.SystemDisk) && states[c.SystemDisk].Struggling ? c.SystemDisk : null;
        bool isCpuHot = Burns(CpuHot, c.CpuTemp, Notifications.CPU_HOT, now), isGpuHot = Burns(GpuHot, c.GpuTemp, Notifications.GPU_HOT, now);
        if (isCpuHot) return new Sentence { Struggling = true, Text = Language.T("status.hot_cpu"), Flyout = "cpu", Subtext = Language.T("status.air", "temperature", (int)Math.Round(c.CpuTemp)) };
        if (isGpuHot) return new Sentence { Struggling = true, Text = Language.T("status.hot_gpu"), Flyout = "gpu", Subtext = Language.T("status.air", "temperature", (int)Math.Round(c.GpuTemp)) };
        if (system != null && (worstIndex < 0 || c.DiskUsage[system] > values[worstIndex])) return DiskStruggling(system, c, true);
        if (worstIndex >= 0) return StrugglingPhrase(worstIndex, values[worstIndex]);
        if (now - frozen < AFTER_FREEZE) return new Sentence { Struggling = true, Text = Language.T("status.frozen"), Subtext = Language.T("status.if_often") };
        if (worstDisk != null) return DiskStruggling(worstDisk, c, false);
        if (c.Cpu < 0 && c.Gpu < 0 && c.Ram < 0) return new Sentence { Text = Language.T("status.waiting") };
        return new Sentence { Text = Language.T("status.fine") };
    }

    bool IsCounted(string piece, double v, double now) {
        State s;
        if (!states.TryGetValue(piece, out s)) { s = new State(); states[piece] = s; }
        if (v < 0) { states[piece] = new State(); return false; }
        if (v >= ENTER) {
            if (double.IsNaN(s.From)) s.From = now;
            s.Above = now;
            if (now - s.From >= STABLE) s.Struggling = true;
        } else if (now - s.Above > GAP) s.From = double.NaN;
        if (v >= EXIT) s.High = now;
        else if (s.Struggling && now - s.High >= CALM) s.Struggling = false;
        return s.Struggling;
    }

    Entry Culprit(string piece) {
        string prefix = piece + ":";
        Entry who = null;
        double best = CULPRIT;
        foreach (var kv in averages)
            if (kv.Key.StartsWith(prefix) && kv.Value >= best) { best = kv.Value; who = entries[kv.Key]; }
        return who;
    }

    Sentence DiskStruggling(string piece, Sample c, bool isSystem) {
        string name;
        if (!c.DiskNames.TryGetValue(piece, out name)) name = "";
        int howMuch = (int)Math.Round(c.DiskUsage[piece]);
        Entry who = Culprit(piece);
        string end = Language.T(isSystem ? (who == null ? "status.unknown_cause" : who.Windows ? "status.wait" : "notification.close_it") : (who == null ? "status.data_nobody" : who.Windows ? "status.data_wait" : "status.data_close_it"));
        string text = isSystem
            ? (who == null ? Language.T("status.system_disk_only", "disk", name) : who.Windows ? Language.T("status.windows") : Language.T("status.blame", "name", who.Name))
            : (who == null ? Language.T("status.data_disk_only", "disk", name) : who.Windows ? Language.T("status.data_windows", "disk", name) : Language.T("status.data_blame", "disk", name, "name", who.Name));
        return new Sentence { Struggling = true, Text = text, Flyout = "disks", Subtext = isSystem ? Language.T("status.disk_busy", "disk", name, "percent", howMuch, "ending", end) : Language.T("status.data_busy", "percent", howMuch, "ending", end) };
    }

    Sentence StrugglingPhrase(int i, double value) {
        Entry who = Culprit(PIECES[i]);
        string text = who == null ? Language.T("status.nobody") : who.Windows ? Language.T("status.windows") : Language.T("status.blame", "name", who.Name);
        string end = Language.T(who == null ? "notification.close_programs" : who.Windows ? "status.wait" : "notification.close_it");
        return new Sentence { Struggling = true, Text = text, Flyout = PIECES[i], Subtext = Language.T("status.busy." + PIECES[i], "percent", (int)Math.Round(value), "ending", end) };
    }
}
