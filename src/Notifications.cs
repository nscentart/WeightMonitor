using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public class Notifications {
    const double STABLE = 45, GAP = 3, NOT_RESPONDING = 3;
    const double SAME_CAUSE = 30 * 60, BETWEEN_NOTICES = 5 * 60, BETWEEN_URGENT = 60, FULL_DISK = 24 * 60 * 60;
    const double PIECE_THRESHOLD = 90, PROGRAM_THRESHOLD = 50, CULPRIT_THRESHOLD = 25;
    public const double CPU_HOT = 97, GPU_HOT = 90, HOT_STABLE = 60, HOT_IMPLAUSIBLE = 125;

    class Tally { public double StartTime, Above, Sum; public int Count; public Entry Entry; public double Average { get { return Sum / Math.Max(1, Count); } } }
    readonly Dictionary<string, Tally> tallies = new Dictionary<string, Tally>();
    readonly Dictionary<string, double> last = new Dictionary<string, double>();
    double seen = double.NegativeInfinity, lastNotice = double.NegativeInfinity, lastUrgent = double.NegativeInfinity;
    Sample previous;

    static readonly string[] WINDOWS_PROCESSES = { "MsMpEng", "NisSrv", "SecurityHealthService", "SearchIndexer", "dwm", "csrss", "lsass", "services", "smss",
        "wininit", "winlogon", "svchost", "fontdrvhost", "spoolsv", "audiodg", "ntoskrnl", "Registry", "explorer", "TiWorker", "TrustedInstaller", "wuauclt" };
    static readonly string[] ACCESSORIES = { "notepad", "mspaint", "pbrush", "wordpad", "write", "calc", "SnippingTool", "cmd", "powershell", "powershell_ise",
        "mstsc", "regedit", "charmap", "magnify", "osk", "taskmgr", "perfmon", "resmon", "mmc", "control" };
    static readonly string WINDOWS = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";

    public static bool InWindowsFiles(string path) {
        return path.StartsWith(WINDOWS, StringComparison.OrdinalIgnoreCase) || path.IndexOf("\\Windows Defender\\", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool Accessory(App a) {
        return a.Path != null && a.Path.StartsWith(WINDOWS, StringComparison.OrdinalIgnoreCase)
            && ACCESSORIES.Contains(Path.GetFileNameWithoutExtension(a.Path), StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsWindowsName(string file) { return WINDOWS_PROCESSES.Contains(file, StringComparer.OrdinalIgnoreCase); }

    public static bool OfWindows(App a) {
        if (Accessory(a)) return false;
        if (a.Key == "system" || a.Windows) return true;
        string p = a.Path ?? "";
        if (InWindowsFiles(p)) return true;
        string file = a.Key != null && a.Key.StartsWith("name:") ? Path.GetFileNameWithoutExtension(a.Key.Substring(5)) : Path.GetFileNameWithoutExtension(p);
        return WINDOWS_PROCESSES.Contains(file, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsSelf(App a) { return a.Path != null && string.Equals(a.Path, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase); }

    public static Entry EntryOf(string piece, App a, double percent) {
        string name = Explanations.Name(a) ?? a.Key ?? "";
        return new Entry { App = a, Piece = piece, Key = a.Key, Name = name.Length <= 40 ? name : name.Substring(0, 37) + "...", Explain = Explanations.Row(a), Windows = OfWindows(a), Percent = percent };
    }

    public List<Notice> Tick(Sample c, double now, Func<string> silence) {
        var r = new List<Notice>();
        if (now - seen > 5) tallies.Clear();
        seen = now;
        Piece("cpu", c.Cpu, now); Piece("gpu", c.Gpu, now); Piece("ram", c.Ram, now);
        Hot("cpu", c.CpuTemp, CPU_HOT, now); Hot("gpu", c.GpuTemp, GPU_HOT, now);
        foreach (var kv in c.AlmostFullDisks) Mark("full:" + kv.Key, kv.Value, null, now);
        if (c.CommitFree >= 0 && c.CommitFree < c.CommitReserve) Mark("commit", c.CommitFree, null, now);
        foreach (var v in c.Entries) if (v.Percent >= PROGRAM_THRESHOLD) Mark("app:" + v.Piece + ":" + v.Key, v.Percent, v, now);
        if (c.NotResponding != null) Mark("hung:" + c.NotResponding.Key, 100, c.NotResponding, now);
        foreach (var k in tallies.Where(kv => now - kv.Value.Above > GAP).Select(kv => kv.Key).ToList()) tallies.Remove(k);

        var a = Choose(c, now);
        if (a != null) {
            string reason = silence == null ? null : silence();
            if (reason == null) {
                Marked(a.Cause, now);
                lastNotice = now;
                if (a.Level == 2) lastUrgent = now;
                r.Add(a);
            }
        }
        previous = c;
        return r;
    }

    void Piece(string piece, double value, double now) {
        if (value < 0) tallies.Remove("tot:" + piece);
        else if (value >= PIECE_THRESHOLD) Mark("tot:" + piece, value, null, now);
    }

    void Hot(string piece, double degrees, double threshold, double now) {
        if (degrees < 0 || degrees > HOT_IMPLAUSIBLE) tallies.Remove("hot:" + piece);
        else if (degrees >= threshold) Mark("hot:" + piece, degrees, null, now);
    }

    void Mark(string key, double value, Entry v, double now) {
        Tally k;
        if (!tallies.TryGetValue(key, out k)) { k = new Tally { StartTime = now }; tallies[key] = k; }
        k.Above = now; k.Sum += value; k.Count++; k.Entry = v;
    }

    bool PlaceDetached(string cause, double now) { double u; return !last.TryGetValue(cause, out u) || now - u >= (cause.StartsWith("full:") ? FULL_DISK : SAME_CAUSE); }

    void Marked(string cause, double now) {
        last[cause] = now;
        if (cause.StartsWith("app:")) last["tot:" + cause.Split(':')[1]] = now;
        foreach (var k in siblings) last[k] = now;
        siblings.Clear();
    }
    readonly List<string> siblings = new List<string>();

    Notice Choose(Sample c, double now) {
        siblings.Clear();
        if (c.Stall >= 1 && PlaceDetached("stall", now)) return Freeze(c);
        if (now - lastUrgent >= BETWEEN_URGENT)
            foreach (var kv in tallies)
                if (kv.Key.StartsWith("hung:") && kv.Value.Above == now && now - kv.Value.StartTime >= NOT_RESPONDING && PlaceDetached(kv.Key, now)) return NotResponding(kv.Key, kv.Value.Entry);
        if (now - lastNotice < BETWEEN_NOTICES) return null;
        var ready = tallies.Where(kv => !kv.Key.StartsWith("hot:") && kv.Value.Above == now && now - kv.Value.StartTime >= STABLE && PlaceDetached(kv.Key, now)).ToList();
        foreach (var kv in tallies.Where(x => x.Key.StartsWith("hot:") && x.Value.Above == now && now - x.Value.StartTime >= HOT_STABLE && PlaceDetached(x.Key, now)).OrderByDescending(x => x.Value.Average))
            return Heat(kv.Key, kv.Value);
        if (ready.Any(x => x.Key == "commit")) return CommitLow(c);
        foreach (var kv in ready.Where(x => x.Key.StartsWith("app:")).OrderByDescending(x => x.Value.Average)) return ProgramNotice(kv.Key, kv.Value);
        foreach (var kv in ready.Where(x => x.Key.StartsWith("tot:")).OrderByDescending(x => x.Value.Average)) return Total(kv.Key, kv.Value, c);
        foreach (var kv in ready.Where(x => x.Key.StartsWith("full:")).OrderBy(x => x.Key)) return DiskFull(kv.Key, c);
        return null;
    }

    public static string Quota(string piece, double v, Sample c) {
        string percent = PercentPhrase(v);
        if (piece == "cpu" || piece == "gpu" || piece == "ram") return Language.T("notification.share." + piece, "percent", percent);
        string n;
        return c.DiskNames.TryGetValue(piece, out n) && n.Length > 0 ? Language.T("notification.share.disk", "percent", percent, "disk", n) : Language.T("notification.share.a_disk", "percent", percent);
    }
    static string FlyoutOf(string piece) { return piece.StartsWith("disk") ? "disks" : piece; }
    public static string FormatPercent(double v) { return (int)Math.Round(v) + "%"; }
    public static string PercentPhrase(double v) { int n = (int)Math.Round(v); return Language.T(n == 8 || n == 11 || (n >= 80 && n < 90) ? "notification.percent_elision" : "notification.percent", "percent", n); }
    public static string WindowsPiece(Entry v) {
        if (v.Explain != null) return Language.T("notification.quoted_name", "name", v.Name);
        return Language.T("notification.windows_part", "parenthesis", v.Name.Contains("Windows") ? "" : " (" + v.Name + ")");
    }
    public static string Large(string s) { return char.ToUpper(s[0]) + s.Substring(1); }

    Notice ProgramNotice(string cause, Tally k) {
        var v = k.Entry;
        string share = Quota(v.Piece, k.Average, names);
        string usage = v.Windows ? Language.T("notification.use.windows", "who", Large(WindowsPiece(v)), "share", share) : Language.T("notification.use.program", "name", v.Name, "share", share);
        string end = v.Windows ? Language.T("notification.leave_it") + " " + Language.T("notification.restart_if_lasts") : Language.T("notification.close_it");
        return new Notice { Level = 1, Cause = cause, Title = Language.T("notification.title.might"), Flyout = FlyoutOf(v.Piece), App = v.Windows ? null : v.App,
                            Text = usage + " " + (v.Explain != null ? v.Explain + " " : "") + Language.T("notification.slows") + " " + end };
    }

    static Notice CommitLow(Sample c) {
        return new Notice { Level = 1, Cause = "commit", Title = Language.T("notification.title.might"), Flyout = "ram",
                            Text = Language.T("notification.commit_low", "free", Drawing.FreeGb((float)c.CommitFree, false), "reserve", Drawing.FreeGb((float)c.CommitReserve, false)) };
    }

    static Notice DiskFull(string cause, Sample c) {
        char letter = cause[5];
        return new Notice { Level = 1, Cause = cause, Title = Language.T("notification.title.might"), Flyout = "disks", Text = Language.T("notification.disk_full", "disk", letter, "free", Disk.Trim(c.AlmostFullDisks[letter])) };
    }

    Notice Heat(string cause, Tally k) {
        string piece = cause.Substring(4);
        return new Notice { Level = 1, Cause = cause, Title = Language.T("notification.title.might"), Flyout = piece,
                            Text = Language.T(piece == "cpu" ? "notification.hot_cpu" : "notification.hot_gpu", "temperature", (int)Math.Round(k.Average)) };
    }

    Notice Total(string cause, Tally k, Sample c) {
        string piece = cause.Substring(4);
        string which = Language.T(piece == "cpu" ? "notification.cpu_max" : piece == "gpu" ? "notification.gpu_max" : "notification.ram_full", "percent", FormatPercent(k.Average));
        var who = c.Entries.Where(v => v.Piece == piece).OrderByDescending(v => v.Percent).FirstOrDefault();
        string end;
        if (who == null) end = Language.T("notification.close_programs");
        else {
            siblings.Add("app:" + piece + ":" + who.Key);
            end = who.Windows ? Language.T("notification.top_windows", "who", WindowsPiece(who))
                               : Language.T("notification.top_program", "name", who.Name, "percent", FormatPercent(who.Percent));
        }
        return new Notice { Level = 1, Cause = cause, Title = Language.T("notification.title.might"), Flyout = piece, App = who == null || who.Windows ? null : who.App, Text = which + " " + Language.T("notification.slows") + " " + end };
    }

    Notice Freeze(Sample c) {
        int sec = Math.Max(1, (int)Math.Round(c.Stall));
        string howMuch = Language.N("notification.frozen", sec) + " ";
        Entry who = null; Sample where = c;
        foreach (var source in new[] { c, previous }) {
            if (source == null) continue;
            foreach (var v in source.Entries)
                if (v.Percent >= CULPRIT_THRESHOLD && (v.Piece != "ram" || c.Ram >= PIECE_THRESHOLD) && (who == null || v.Percent > who.Percent)) { who = v; where = source; }
        }
        var a = new Notice { Level = 2, Cause = "stall", Title = Language.T("notification.title.is"), Flyout = who == null ? "all" : FlyoutOf(who.Piece), App = who == null || who.Windows ? null : who.App };
        if (who == null) a.Text = howMuch + Language.T("notification.freeze_unknown");
        else if (who.Windows) a.Text = howMuch + Language.T("notification.freeze_windows", "who", WindowsPiece(who), "share", Quota(who.Piece, who.Percent, where));
        else a.Text = howMuch + Language.T("notification.freeze_program", "name", who.Name, "share", Quota(who.Piece, who.Percent, where)) + " " + Language.T("notification.close_it");
        return a;
    }

    static Notice NotResponding(string cause, Entry v) {
        return new Notice { Level = 2, Cause = cause, Title = Language.T("notification.title.is"), Flyout = "all", App = v.Windows ? null : v.App,
            Text = v.Windows ? Language.T("notification.unresponsive_windows", "who", Large(WindowsPiece(v))) : Language.T("notification.unresponsive_program", "name", v.Name) };
    }

    readonly Sample names = new Sample();
    public void RememberNames(Sample c) { foreach (var kv in c.DiskNames) names.DiskNames[kv.Key] = kv.Value; }
}
