using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public partial class Network {
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern uint StartTrace(out ulong session, string name, IntPtr properties);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern uint ControlTrace(ulong session, string name, IntPtr properties, uint code);
    [DllImport("advapi32.dll")] static extern uint EnableTraceEx2(ulong session, ref Guid provider, uint code, byte level,
                                                                  ulong any, ulong all, uint wait, IntPtr parameters);
    [DllImport("advapi32.dll", EntryPoint = "OpenTraceW", SetLastError = true)] static extern ulong OpenTrace(IntPtr logfile);
    [DllImport("advapi32.dll")] static extern uint ProcessTrace(ulong[] traces, uint count, IntPtr start, IntPtr end);
    [DllImport("advapi32.dll")] static extern uint CloseTrace(ulong trace);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherits, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flag, StringBuilder text, ref int length);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Callback(IntPtr evt);

    static readonly Guid KERNEL_NETWORK = new Guid("7DD42A49-5329-4832-8DFD-43D979153A88");
    static readonly Guid KERNEL_DISK = new Guid("C7BDE69A-E1E0-4177-B6EF-283AD1525271");
    const string SESSION = Legacy.ETW_SESSION;
    const int PROPERTIES = 120, NAMES = 1024, LOGFILE = 448;

    readonly string sessionName;
    readonly ManualResetEvent enough = new ManualResetEvent(false);
    readonly object startup = new object();
    public Network(string how = SESSION) { sessionName = how; }

    Callback callback;
    IntPtr properties = IntPtr.Zero, logfile = IntPtr.Zero, name = IntPtr.Zero;
    ulong session, trace = ulong.MaxValue;
    public volatile bool Activate;
    public string Error = "";

    readonly object tallies = new object();
    Dictionary<int, long[]> totals = new Dictionary<int, long[]>();
    readonly Dictionary<int, long> seenEvents = new Dictionary<int, long>();

    readonly object history = new object();
    readonly Dictionary<int, App> perPid = new Dictionary<int, App>();
    readonly Dictionary<string, App> app = new Dictionary<string, App>();
    readonly Queue<Dictionary<App, long[]>> seconds = new Queue<Dictionary<App, long[]>>();
    readonly Queue<Dictionary<App, long[]>> minutes = new Queue<Dictionary<App, long[]>>();
    readonly Queue<double[]> totalSeconds = new Queue<double[]>(), totalMinutes = new Queue<double[]>();
    Dictionary<App, long[]> currentMinute;
    double[] currentTotalMinute;
    long minuteNumber = -1;
    DateTime cleanup = DateTime.UtcNow;

    void Prepare() {
        for (int i = 0; i < PROPERTIES + NAMES; i++) Marshal.WriteByte(properties, i, 0);
        Marshal.WriteInt32(properties, 0, PROPERTIES + NAMES);
        Marshal.WriteInt32(properties, 40, 1);
        Marshal.WriteInt32(properties, 44, 0x00020000);
        Marshal.WriteInt32(properties, 48, 64);
        Marshal.WriteInt32(properties, 64, 0x100);
        Marshal.WriteInt32(properties, 68, 1);
        Marshal.WriteInt32(properties, 116, PROPERTIES);
    }

    public void Start() {
        bool started = TurnOn(), first = true;
        if (!started && !Program.Admin()) return;
        var t = new Thread(() => Restart.Turn(
            () => { if (first) { first = false; return started; } return TurnOn(); },
            Listen,
            seconds => !enough.WaitOne(seconds * 1000)));
        t.IsBackground = true; t.Name = "network-etw"; t.Start();
    }

    bool TurnOn() {
        lock (startup) {
            if (enough.WaitOne(0)) return false;
            try {
                if (properties == IntPtr.Zero) properties = Marshal.AllocHGlobal(PROPERTIES + NAMES);
                Prepare();
                ControlTrace(0, sessionName, properties, 1);
                Prepare();
                uint r = StartTrace(out session, sessionName, properties);
                if (r != 0) { session = 0; Error = r == 5 ? Language.T("network.reason_admin") : "StartTrace " + r; return false; }
                Guid g = KERNEL_NETWORK;
                r = EnableTraceEx2(session, ref g, 1, 5, ulong.MaxValue, 0, 0, IntPtr.Zero);
                if (r != 0) { Error = "EnableTraceEx2 " + r; TurnOff(); return false; }
                Guid disks = KERNEL_DISK;
                uint rd = EnableTraceEx2(session, ref disks, 1, 5, ulong.MaxValue, 0, 0, IntPtr.Zero);
                if (rd != 0) Program.Log("per-program disks off: EnableTraceEx2 " + rd);

                if (callback == null) callback = Event;
                if (logfile == IntPtr.Zero) logfile = Marshal.AllocHGlobal(LOGFILE);
                for (int i = 0; i < LOGFILE; i++) Marshal.WriteByte(logfile, i, 0);
                if (name == IntPtr.Zero) name = Marshal.StringToHGlobalUni(sessionName);
                Marshal.WriteIntPtr(logfile, 8, name);
                Marshal.WriteInt32(logfile, 28, 0x100 | 0x10000000);
                Marshal.WriteIntPtr(logfile, 424, Marshal.GetFunctionPointerForDelegate(callback));
                trace = OpenTrace(logfile);
                if (trace == ulong.MaxValue) { Error = "OpenTrace " + Marshal.GetLastWin32Error(); TurnOff(); return false; }
                Error = "";
                Activate = true;
                Details.EtwDisks = rd == 0;
                return true;
            } catch (Exception ex) { Error = ex.Message; return false; }
        }
    }

    double Listen() {
        var t0 = DateTime.UtcNow;
        try { ProcessTrace(new[] { trace }, 1, IntPtr.Zero, IntPtr.Zero); } catch (Exception ex) { Error = ex.Message; }
        double duration = (DateTime.UtcNow - t0).TotalSeconds;
        lock (startup) TurnOff();
        if (!enough.WaitOne(0)) {
            Error = Language.T("network.stopped");
            Program.Log("network and per-program disks: reading interrupted after " + (int)duration + " seconds, restarting by itself");
        }
        return duration;
    }

    void TurnOff() {
        Activate = false; Details.EtwDisks = false;
        try { if (session != 0 && properties != IntPtr.Zero) { Prepare(); ControlTrace(session, null, properties, 1); session = 0; } } catch { }
        try { if (trace != ulong.MaxValue) { CloseTrace(trace); trace = ulong.MaxValue; } } catch { }
    }

    public void Stop() {
        enough.Set();
        lock (startup) TurnOff();
    }

    void Event(IntPtr e) {
        try {
            int id = (ushort)Marshal.ReadInt16(e, 40);
            if (Marshal.ReadInt32(e, 24) == unchecked((int)0xC7BDE69A)) { DiskEvent(e, id); return; }
            int toward; bool v6;
            switch (id) {
                case 10: case 42: toward = 0; v6 = false; break;
                case 11: case 43: toward = 1; v6 = false; break;
                case 26: case 58: toward = 0; v6 = true; break;
                case 27: case 59: toward = 1; v6 = true; break;
                default:
                    lock (tallies) { long n; seenEvents.TryGetValue(id, out n); seenEvents[id] = n + 1; }
                    return;
            }
            int length = (ushort)Marshal.ReadInt16(e, 86);
            IntPtr ud = Marshal.ReadIntPtr(e, 96);
            if (ud == IntPtr.Zero || length < 8) return;
            int pid = Marshal.ReadInt32(ud, 0);
            long amount = (uint)Marshal.ReadInt32(ud, 4);
            if (!v6 && length >= 16 && Marshal.ReadByte(ud, 8) == 127) return;
            if (v6 && length >= 24) {
                bool locale = Marshal.ReadByte(ud, 23) == 1;
                for (int i = 8; i < 23 && locale; i++) if (Marshal.ReadByte(ud, i) != 0) locale = false;
                if (locale) return;
            }
            lock (tallies) {
                long[] c;
                if (!totals.TryGetValue(pid, out c)) { c = new long[2]; totals[pid] = c; }
                c[toward] += amount;
                long n; seenEvents.TryGetValue(id, out n); seenEvents[id] = n + 1;
            }
        } catch { }
    }

    static long diskEvents;
    void DiskEvent(IntPtr e, int id) {
        if (id != 10 && id != 11) return;
        int length = (ushort)Marshal.ReadInt16(e, 86);
        IntPtr ud = Marshal.ReadIntPtr(e, 96);
        if (ud == IntPtr.Zero || length < 12) return;
        long amount = (uint)Marshal.ReadInt32(ud, 8);
        int tid = length >= 52 ? Marshal.ReadInt32(ud, 48) : 0;
        Details.DiskEvent(tid, Marshal.ReadInt32(e, 12), id == 10 ? 0 : 1, amount, Marshal.ReadInt32(ud, 0));
        Interlocked.Increment(ref diskEvents);
    }

    static string PathOf(int pid) {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return null;
        try {
            var sb = new StringBuilder(1024); int n = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : null;
        } finally { CloseHandle(h); }
    }

    App AppOf(int pid) {
        App a;
        if (perPid.TryGetValue(pid, out a)) return a;
        string path = pid <= 4 ? null : PathOf(pid);
        string key = path != null ? path.ToLowerInvariant() : pid <= 4 ? "system" : "pid:" + pid;
        if (!app.TryGetValue(key, out a)) {
            if (app.Count > 2000) app.Clear();
            a = new App { Key = key, Path = path };
            if (pid <= 4) { a.Name = Language.T("name.system"); a.Windows = true; }
            else if (path == null) { a.Name = Language.T("name.closed", "pid", pid); a.PidClosed = pid; }
            else {
                string file = Path.GetFileNameWithoutExtension(path), desc = null;
                try { desc = FileVersionInfo.GetVersionInfo(path).FileDescription; } catch { }
                a.Name = string.IsNullOrWhiteSpace(desc) ? file : desc.Trim();
                if (file.Equals("svchost", StringComparison.OrdinalIgnoreCase)) { a.Name = Language.T("name.svchost"); a.Windows = true; }
            }
            app[key] = a;
        }
        perPid[pid] = a;
        return a;
    }

    static void Sum(Dictionary<App, long[]> where, App a, long up, long down) {
        long[] c;
        if (!where.TryGetValue(a, out c)) { c = new long[2]; where[a] = c; }
        c[0] += up; c[1] += down;
    }

    public void Tick(double up, double down) {
        Dictionary<int, long[]> taken;
        lock (tallies) { taken = totals; totals = new Dictionary<int, long[]>(); }
        up = Math.Max(0, up); down = Math.Max(0, down);
        lock (history) {
            if ((DateTime.UtcNow - cleanup).TotalMinutes >= 5) { perPid.Clear(); cleanup = DateTime.UtcNow; }
            var sec = new Dictionary<App, long[]>();
            foreach (var kv in taken) Sum(sec, AppOf(kv.Key), kv.Value[0], kv.Value[1]);
            seconds.Enqueue(sec); while (seconds.Count > 60) seconds.Dequeue();
            totalSeconds.Enqueue(new[] { up, down }); while (totalSeconds.Count > 60) totalSeconds.Dequeue();
            long m = DateTime.Now.Ticks / TimeSpan.TicksPerMinute;
            if (m != minuteNumber) {
                if (currentMinute != null) {
                    minutes.Enqueue(currentMinute); while (minutes.Count > 59) minutes.Dequeue();
                    totalMinutes.Enqueue(currentTotalMinute); while (totalMinutes.Count > 59) totalMinutes.Dequeue();
                }
                currentMinute = new Dictionary<App, long[]>(); currentTotalMinute = new double[2]; minuteNumber = m;
            }
            foreach (var kv in sec) Sum(currentMinute, kv.Key, kv.Value[0], kv.Value[1]);
            currentTotalMinute[0] += up; currentTotalMinute[1] += down;
        }
    }

    public List<NetworkRow> Rows(int tab) {
        var total = new Dictionary<App, long[]>();
        Dictionary<App, long[]> last = null;
        lock (history) {
            IEnumerable<Dictionary<App, long[]>> source = tab == 2 ? minutes.Concat(new[] { currentMinute })
                : tab == 0 ? seconds.Skip(Math.Max(0, seconds.Count - 1)) : seconds;
            foreach (var s in source) if (s != null) foreach (var kv in s) Sum(total, kv.Key, kv.Value[0], kv.Value[1]);
            last = seconds.LastOrDefault();
        }
        var rows = new List<NetworkRow>();
        foreach (var kv in total) {
            if (kv.Value[0] + kv.Value[1] == 0) continue;
            var r = new NetworkRow { App = kv.Key, Up = kv.Value[0], Down = kv.Value[1] };
            long[] u;
            if (last != null && last.TryGetValue(kv.Key, out u)) { r.UpNow = u[0] > 0; r.DownNow = u[1] > 0; }
            rows.Add(r);
        }
        rows.Sort((a, b) => (b.Up + b.Down).CompareTo(a.Up + a.Down));
        return rows;
    }

    public List<double[]> Series(bool perMinute) {
        lock (history) {
            var l = (perMinute ? totalMinutes : totalSeconds).Select(x => new[] { x[0], x[1] }).ToList();
            if (perMinute && currentTotalMinute != null) l.Add(new[] { currentTotalMinute[0], currentTotalMinute[1] });
            return l;
        }
    }

    public int CollectedSeconds() { lock (history) return seconds.Count; }

    public IEnumerable<string> Diagnosis() {
        var r = new List<string>();
        r.Add(Language.Fixed("fixed.network.line", "state", Activate ? Language.Fixed("fixed.network.on") : Language.Fixed("fixed.network.off", "error", Error)));
        lock (tallies) r.Add("  " + Language.Fixed("fixed.network.events", "list", string.Join(", ", seenEvents.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value))));
        r.Add(Language.Fixed("fixed.disks.line", "state", Details.EtwDisks ? Language.Fixed("fixed.disks.on", "count", Interlocked.Read(ref diskEvents)) : Language.Fixed("fixed.disks.off")));
        return r;
    }

    public void SampleData(int amount = 6) {
        var names = new[] {
            new[] { "Thunderbird", @"C:\Program Files\Mozilla Thunderbird\thunderbird.exe" },
            new[] { "Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe" },
            new[] { "Claude", @"C:\Windows\explorer.exe" },
            new[] { "Spotify", @"C:\Windows\System32\notepad.exe" },
            new[] { "Servizi di Windows (svchost)", @"C:\Windows\System32\svchost.exe" },
            new[] { Language.T("name.system"), null } };
        var apps = names.Select(n => new App { Key = n[0], Name = n[0], Path = n[1] }).Take(amount).ToList();
        for (int i = apps.Count; i < amount; i++) apps.Add(new App { Key = "Programma " + (i + 1), Name = "Programma " + (i + 1), Path = @"C:\Windows\System32\notepad.exe" });
        var testCase = new Random(7);
        lock (history) {
            for (int s = 0; s < 60; s++) {
                var sec = new Dictionary<App, long[]>();
                for (int i = 0; i < apps.Count; i++)
                    if (testCase.Next(4) > 0) sec[apps[i]] = new long[] { testCase.Next(30000 / (i + 1)), testCase.Next(90000 / (i + 1)) };
                seconds.Enqueue(sec);
                totalSeconds.Enqueue(new double[] { sec.Values.Sum(v => v[0]), sec.Values.Sum(v => v[1]) });
            }
            for (int m = 0; m < 59; m++) {
                var min = new Dictionary<App, long[]>();
                for (int i = 0; i < apps.Count; i++) min[apps[i]] = new long[] { testCase.Next(2000000 / (i + 1)), testCase.Next(9000000 / (i + 1)) };
                minutes.Enqueue(min);
                totalMinutes.Enqueue(new double[] { min.Values.Sum(v => v[0]), min.Values.Sum(v => v[1]) });
            }
            currentMinute = seconds.Last(); currentTotalMinute = totalSeconds.Last();
        }
    }
}
