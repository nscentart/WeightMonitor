using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

public class Details {
    [DllImport("ntdll.dll")] static extern int NtQuerySystemInformation(int cls, IntPtr buf, int length, out int needs);
    [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORY m);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherits, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flag, StringBuilder text, ref int length);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct MEMORY {
        public uint Length, Load; public ulong Physical, FreePhysical, Commitment, FreeCommitment, Virtual, FreeVirtual, Extended;
    }

    public static bool Commit(out double used, out double total) {
        used = -1; total = -1;
        var m = new MEMORY(); m.Length = (uint)Marshal.SizeOf(typeof(MEMORY));
        if (!GlobalMemoryStatusEx(ref m) || m.Commitment == 0 || m.FreeCommitment > m.Commitment) return false;
        total = m.Commitment; used = m.Commitment - m.FreeCommitment;
        return true;
    }

    public static Details Instance;
    public const int DISKS = 16;

    public readonly History Cpu, Gpu = new History(2), Ram = new History(1), Disks = new History(DISKS * 2);
    public volatile Snapshot Current = new Snapshot();
    readonly int thread, core;

    Pdh pdh;
    IntPtr buf = IntPtr.Zero; int longBuf;
    class Proc { public long Born, Cpu, Read, Written; public App App; }
    Dictionary<int, Proc> processes = new Dictionary<int, Proc>();
    readonly Dictionary<string, App> app = new Dictionary<string, App>();
    readonly Dictionary<int, int> processOfThread = new Dictionary<int, int>();
    DateTime previous = DateTime.MinValue;

    public Details() : this(true, Environment.ProcessorCount) { }
    Details(bool real, int thread) {
        this.thread = thread;
        int c = 0;
        if (real) {
            try { using (var q = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor")) foreach (ManagementObject o in q.Get()) c += Convert.ToInt32(o["NumberOfCores"]); } catch { }
        }
        core = c > 0 ? c : thread;
        Cpu = new History(1 + thread);
        if (!real) return;
        try {
            pdh = new Pdh();
            pdh.Add("performance", @"\Processor Information(*)\% Processor Performance");
            pdh.Add("frequency", @"\Processor Information(*)\Processor Frequency");
            pdh.Add("engines", @"\GPU Engine(*)\Utilization Percentage");
            pdh.Add("vram", @"\GPU Process Memory(*)\Dedicated Usage");
            pdh.Add("reads", @"\PhysicalDisk(*)\Disk Read Bytes/sec");
            pdh.Add("writes", @"\PhysicalDisk(*)\Disk Write Bytes/sec");
            pdh.Add("modified", @"\Memory\Modified Page List Bytes");
            pdh.Add("cache1", @"\Memory\Standby Cache Core Bytes");
            pdh.Add("cache2", @"\Memory\Standby Cache Normal Priority Bytes");
            pdh.Add("cache3", @"\Memory\Standby Cache Reserve Bytes");
            pdh.Add("free", @"\Memory\Free & Zero Page List Bytes");
            pdh.Add("paging", @"\Memory\Pool Paged Bytes");
            pdh.Add("nonpaging", @"\Memory\Pool Nonpaged Bytes");
            pdh.Collect();
        } catch (Exception ex) { pdh = null; Program.Log("details, counters: " + ex.Message); }
    }

    public void RedoDisks() {
        if (pdh == null) return;
        pdh.Redo("reads", @"\PhysicalDisk(*)\Disk Read Bytes/sec");
        pdh.Redo("writes", @"\PhysicalDisk(*)\Disk Write Bytes/sec");
    }

    public static volatile bool EtwDisks;
    static readonly object events = new object();
    static Dictionary<long, long[]> perThread = new Dictionary<long, long[]>();

    public static void DiskEvent(int tid, int pid, int toward, long amount, int disk) {
        long key = ((long)tid << 32) | (uint)pid;
        lock (events) {
            long[] c;
            if (!perThread.TryGetValue(key, out c)) { c = new long[2 + DISKS]; perThread[key] = c; }
            c[toward] += amount;
            if (disk >= 0 && disk < DISKS) c[2 + disk] += amount;
        }
    }

    public App AppByPid(int pid) { Proc p; return processes.TryGetValue(pid, out p) ? p.App : null; }

    static string PathOf(int pid) {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return null;
        try {
            var sb = new StringBuilder(1024); int n = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : null;
        } finally { CloseHandle(h); }
    }

    App AppOf(int pid, string image) {
        string path = pid <= 4 ? null : PathOf(pid);
        if (path == null && pid > 4 && image.Length > 0) {
            try { string test = Path.Combine(Environment.SystemDirectory, image); if (File.Exists(test)) path = test; } catch { }
        }
        string key = path != null ? path.ToLowerInvariant() : pid <= 4 ? "system" : "name:" + image.ToLowerInvariant();
        App a;
        if (app.TryGetValue(key, out a)) return a;
        a = new App { Key = key, Path = path };
        string file = path != null ? Path.GetFileNameWithoutExtension(path) : Path.GetFileNameWithoutExtension(image);
        if (pid <= 4) { a.Name = Language.T("name.system"); a.Windows = true; }
        else if (file.Equals("Memory Compression", StringComparison.OrdinalIgnoreCase)) { a.Name = Language.T("name.compressed"); a.Windows = true; a.Compression = true; }
        else if (file.Equals("svchost", StringComparison.OrdinalIgnoreCase)) { a.Name = Language.T("name.svchost"); a.Windows = true; }
        else {
            string desc = null;
            try { if (path != null) desc = FileVersionInfo.GetVersionInfo(path).FileDescription; } catch { }
            a.Name = string.IsNullOrWhiteSpace(desc) ? file : desc.Trim();
        }
        if (app.Count > 2000) app.Clear();
        app[key] = a;
        return a;
    }

    static void Sum(Dictionary<App, double[]> where, App a, int i, double v, int dim) {
        double[] c;
        if (!where.TryGetValue(a, out c)) { c = new double[dim]; where[a] = c; }
        c[i] += v;
    }

    bool Capture() {
        while (true) {
            if (buf == IntPtr.Zero) { longBuf = 4 << 20; buf = Marshal.AllocHGlobal(longBuf); }
            int needs;
            int r = NtQuerySystemInformation(5, buf, longBuf, out needs);
            if (r == 0) return true;
            if (r != unchecked((int)0xC0000004) || longBuf > (64 << 20)) return false;
            Marshal.FreeHGlobal(buf); longBuf = Math.Max(needs + (1 << 20), longBuf * 2); buf = Marshal.AllocHGlobal(longBuf);
        }
    }

    public void Tick(SensorData d, Lhm l) {
        try { Inside(d, l); } catch (Exception ex) { Program.Log("details: " + ex); }
    }

    void Inside(SensorData d, Lhm l) {
        var now = DateTime.UtcNow;
        double sec = previous == DateTime.MinValue ? 1 : Math.Max(0.2, (now - previous).TotalSeconds);
        bool first = previous == DateTime.MinValue;
        previous = now;
        var snap = new Snapshot { Thread = thread, Core = core, Uptime = GetTickCount64() / 1000.0, DiskEtw = EtwDisks };
        snap.CpuTemp = l.CpuTemp; snap.GpuTemp = l.GpuTemp; snap.Engines = l.Engines; snap.CpuWatt = l.CpuWatt;
        snap.UsedVram = l.UsedVram; snap.TotalVram = l.TotalVram; snap.Shared = l.Shared;
        snap.GpuMhz = l.GpuMhz; snap.GpuWatt = l.GpuWatt; snap.Fan = l.Fan;
        if (pdh != null) pdh.Collect();

        var cpu = new Dictionary<App, double[]>(); var ram = new Dictionary<App, double[]>(); var io = new Dictionary<App, double[]>();
        var newOnes = new Dictionary<int, Proc>();
        processOfThread.Clear();
        if (Capture()) {
            int off = 0;
            while (true) {
                IntPtr p = new IntPtr(buf.ToInt64() + off);
                int next = Marshal.ReadInt32(p, 0), nThread = Marshal.ReadInt32(p, 4);
                int pid = (int)Marshal.ReadInt64(p, 80);
                snap.Processes++; snap.ThreadTotal += nThread; snap.Handle += Marshal.ReadInt32(p, 96);
                if (EtwDisks) for (int t = 0; t < nThread; t++) processOfThread[(int)Marshal.ReadInt64(p, 256 + t * 80 + 48)] = pid;
                if (pid != 0) {
                    long born = Marshal.ReadInt64(p, 32), time = Marshal.ReadInt64(p, 40) + Marshal.ReadInt64(p, 48);
                    long read = Marshal.ReadInt64(p, 232), written = Marshal.ReadInt64(p, 240);
                    Proc pr;
                    if (!processes.TryGetValue(pid, out pr) || pr.Born != born) {
                        int nameLength = (ushort)Marshal.ReadInt16(p, 56);
                        IntPtr namePointer = Marshal.ReadIntPtr(p, 64);
                        string image = namePointer == IntPtr.Zero || nameLength == 0 ? Language.Fixed("fixed.details.system") : Marshal.PtrToStringUni(namePointer, nameLength / 2);
                        pr = new Proc { Born = born, Cpu = time, Read = read, Written = written, App = AppOf(pid, image) };
                    } else {
                        double usage = (time - pr.Cpu) / (sec * 1e7 * thread) * 100.0;
                        if (usage > 0.01) Sum(cpu, pr.App, 0, Math.Min(100, usage), 1);
                        if (!EtwDisks) {
                            if (read > pr.Read) Sum(io, pr.App, 0, (read - pr.Read) / sec, 2);
                            if (written > pr.Written) Sum(io, pr.App, 1, (written - pr.Written) / sec, 2);
                        }
                        pr.Cpu = time; pr.Read = read; pr.Written = written;
                    }
                    newOnes[pid] = pr;
                    long privateKey = Marshal.ReadInt64(p, 8);
                    if (privateKey > 0) Sum(ram, pr.App, 0, privateKey, 1);
                    if (pr.App.Compression) snap.Compressed = Marshal.ReadInt64(p, 144);
                }
                if (next == 0) break;
                off += next;
            }
        }
        processes = newOnes;

        if (EtwDisks) {
            Dictionary<long, long[]> taken;
            lock (events) { taken = perThread; perThread = new Dictionary<long, long[]>(); }
            foreach (var kv in taken) {
                int tid = (int)(kv.Key >> 32), pidEv = (int)(kv.Key & 0xffffffff), pid;
                if (tid <= 0 || !processOfThread.TryGetValue(tid, out pid)) pid = pidEv;
                Proc pr;
                App a = processes.TryGetValue(pid, out pr) ? pr.App : AppOf(4, "");
                Sum(io, a, 0, kv.Value[0] / sec, 2); Sum(io, a, 1, kv.Value[1] / sec, 2);
                for (int n = 0; n < DISKS; n++) {
                    if (kv.Value[2 + n] == 0) continue;
                    Dictionary<App, double> onDisk; double v;
                    if (!snap.AppDisk.TryGetValue(n, out onDisk)) { onDisk = new Dictionary<App, double>(); snap.AppDisk[n] = onDisk; }
                    onDisk.TryGetValue(a, out v);
                    onDisk[a] = v + kv.Value[2 + n] / sec;
                }
            }
        }

        var totalCpu = new double[1 + thread];
        totalCpu[0] = Math.Max(0, d.Cpu.Usage);
        for (int i = 0; i < thread && i < l.CoreUsage.Length; i++) totalCpu[1 + i] = Math.Max(0, l.CoreUsage[i]);
        if (pdh != null) {
            var perf = Numbered(pdh.Values("performance")); var freq = Numbered(pdh.Values("frequency"));
            var ghz = new float[Math.Min(perf.Count, freq.Count)];
            for (int i = 0; i < ghz.Length; i++) ghz[i] = (float)(freq[i] * perf[i] / 100.0 / 1000.0);
            int perCore = ghz.Length > 0 && ghz.Length == core * 2 ? 2 : 1;
            snap.CoreGhz = new float[ghz.Length / perCore];
            for (int i = 0; i < snap.CoreGhz.Length; i++) snap.CoreGhz[i] = perCore == 2 ? Math.Max(ghz[2 * i], ghz[2 * i + 1]) : ghz[i];
            if (ghz.Length > 0) snap.GhzAverage = ghz.Average();
        }

        var gpu = new Dictionary<App, double[]>();
        if (pdh != null) {
            var perEngine = new Dictionary<string, double>();
            foreach (var kv in pdh.Values("engines")) {
                int e = kv.Key.IndexOf("_engtype_");
                if (e > 0 && kv.Value > 0) { double v; perEngine.TryGetValue(kv.Key.Substring(0, e), out v); perEngine[kv.Key.Substring(0, e)] = v + kv.Value; }
            }
            var perPid = new Dictionary<int, double>();
            foreach (var kv in perEngine) {
                int pid = PidOf(kv.Key); double v;
                perPid.TryGetValue(pid, out v); perPid[pid] = Math.Max(v, kv.Value);
            }
            foreach (var kv in perPid) { Proc pr; if (processes.TryGetValue(kv.Key, out pr)) Sum(gpu, pr.App, 0, Math.Min(100, kv.Value), 2); }
            foreach (var kv in pdh.Values("vram")) {
                Proc pr;
                if (kv.Value > 0 && processes.TryGetValue(PidOf(kv.Key), out pr)) Sum(gpu, pr.App, 1, kv.Value, 2);
            }
            foreach (var a in gpu.Values) a[0] = Math.Min(100, a[0]);
        }
        double vramPercent = l.TotalVram > 0 && l.UsedVram >= 0 ? l.UsedVram / l.TotalVram * 100.0 : 0;

        var m = new MEMORY(); m.Length = (uint)Marshal.SizeOf(typeof(MEMORY));
        if (GlobalMemoryStatusEx(ref m)) {
            snap.TotalRam = m.Physical; snap.UsedRam = m.Physical - m.FreePhysical;
            snap.Limit = m.Commitment; snap.Committed = m.Commitment - m.FreeCommitment;
        }
        if (pdh != null) {
            snap.Modified = Math.Max(0, pdh.One("modified"));
            snap.Cache = snap.Modified + Math.Max(0, pdh.One("cache1")) + Math.Max(0, pdh.One("cache2")) + Math.Max(0, pdh.One("cache3"));
            snap.PlaceDetached = Math.Max(0, pdh.One("free"));
            snap.Paging = Math.Max(0, pdh.One("paging")); snap.NonPaging = Math.Max(0, pdh.One("nonpaging"));
        }

        var totalDisks = new double[DISKS * 2];
        if (pdh != null)
            for (int toward = 0; toward < 2; toward++)
                foreach (var kv in pdh.Values(toward == 0 ? "reads" : "writes")) {
                    int sp = kv.Key.IndexOf(' '), n;
                    if (sp > 0 && int.TryParse(kv.Key.Substring(0, sp), out n) && n >= 0 && n < DISKS) totalDisks[2 * n + toward] = Math.Max(0, kv.Value);
                }

        Current = snap;
        if (first) return;
        Cpu.Add(totalCpu, cpu);
        Gpu.Add(new[] { Math.Max(0, d.Gpu.Usage), vramPercent }, gpu);
        Ram.Add(new double[] { Math.Max(0, d.Ram.Usage) }, ram);
        Disks.Add(totalDisks, io);
    }

    static int PidOf(string instance) {
        if (!instance.StartsWith("pid_")) return -1;
        int f = instance.IndexOf('_', 4), pid;
        return f > 4 && int.TryParse(instance.Substring(4, f - 4), out pid) ? pid : -1;
    }

    static List<double> Numbered(List<KeyValuePair<string, double>> v) {
        var r = new List<KeyValuePair<long, double>>();
        foreach (var kv in v) {
            int c = kv.Key.IndexOf(','), g, n;
            if (c > 0 && int.TryParse(kv.Key.Substring(0, c), out g) && int.TryParse(kv.Key.Substring(c + 1), out n))
                r.Add(new KeyValuePair<long, double>(g * 1000L + n, kv.Value));
        }
        return r.OrderBy(x => x.Key).Select(x => x.Value).ToList();
    }

    public static Details SampleData() {
        int nt = 32;
        var x = new Details(false, nt);
        var testCase = new Random(11);
        var names = new[] {
            new[] { "Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe" },
            new[] { "Thunderbird", @"C:\Program Files\Mozilla Thunderbird\thunderbird.exe" },
            new[] { "Claude", @"C:\Esempio\Claude.exe" },
            new[] { "Spotify", @"C:\Esempio\Spotify.exe" },
            new[] { "svchost", @"C:\Windows\System32\svchost.exe" },
            new[] { "dwm", @"C:\Windows\System32\dwm.exe" },
            new[] { "explorer", @"C:\Windows\explorer.exe" },
            new[] { "system", null } };
        var apps = names.Select(n => new App { Key = n[0], Name = n[0], Path = n[1], Windows = n[1] == null }).ToList();
        for (int s = 0; s < 60 * 60; s++) {
            double wave = 14 + 10 * Math.Sin(s / 300.0) + testCase.Next(8);
            var tc = new double[1 + nt]; tc[0] = wave;
            for (int i = 0; i < nt; i++) tc[1 + i] = Math.Min(100, Math.Max(0, wave * (i % 8 == 1 ? 3.5 : i % 3 == 0 ? 1.6 : 0.4) + testCase.Next(6)));
            var ac = new Dictionary<App, double[]>(); var ag = new Dictionary<App, double[]>();
            var ar = new Dictionary<App, double[]>(); var ad = new Dictionary<App, double[]>();
            for (int i = 0; i < apps.Count; i++) {
                if (testCase.Next(5) > 0) ac[apps[i]] = new[] { wave / (i + 1.5) + testCase.NextDouble() };
                if (i < 4) ag[apps[i]] = new[] { (i == 0 ? 7.0 : 2.0 / (i + 1)) + testCase.NextDouble(), (1.6 - i * 0.35) * 1073741824 };
                ar[apps[i]] = new[] { (3.2 - i * 0.38) * 1073741824 + testCase.Next(20000000) };
                if (testCase.Next(3) == 0) ad[apps[i]] = new[] { testCase.Next(9000000 / (i + 1)) * 1.0, testCase.Next(3000000 / (i + 1)) * 1.0 };
            }
            var td = new double[DISKS * 2];
            td[2] = testCase.Next(4) == 0 ? testCase.Next(80000000) : testCase.Next(900000); td[3] = testCase.Next(6000000);
            td[4] = testCase.Next(2000000); td[5] = testCase.Next(300000);
            td[0] = s % 40 < 8 ? 150000000 + testCase.Next(40000000) : testCase.Next(100000); td[1] = s % 40 < 8 ? 2000000 : 0;
            x.Cpu.Add(tc, ac, s / 60);
            x.Gpu.Add(new[] { 9 + 6 * Math.Sin(s / 200.0) + testCase.Next(5), 14 + testCase.NextDouble() }, ag, s / 60);
            x.Ram.Add(new[] { 42 + 3 * Math.Sin(s / 900.0) + testCase.NextDouble() }, ar, s / 60);
            x.Disks.Add(td, ad, s / 60);
        }
        var snap = new Snapshot { Thread = nt, Core = 16, Processes = 412, ThreadTotal = 6153, Handle = 214332, Uptime = 2 * 86400 + 3 * 3600 + 25 * 60 };
        snap.CoreGhz = Enumerable.Range(0, 16).Select(i => (float)(i < 8 ? 5.2 + testCase.Next(40) / 100.0 : 4.1 + testCase.Next(60) / 100.0)).ToArray();
        snap.GhzAverage = 4.71f; snap.CpuWatt = 118.4f;
        snap.CpuTemp.Add(new KeyValuePair<string, float>("Tctl", 74)); snap.CpuTemp.Add(new KeyValuePair<string, float>("CCD1", 69));
        snap.CpuTemp.Add(new KeyValuePair<string, float>("CCD2", 58));
        snap.GpuTemp.Add(new KeyValuePair<string, float>("Core", 48)); snap.GpuTemp.Add(new KeyValuePair<string, float>("Hot spot", 57));
        snap.GpuTemp.Add(new KeyValuePair<string, float>(Sensors.MEMORY, 64));
        foreach (var mo in new[] { "3D|9", "copy|1", "decode|23", "encode|0", "compute|4", "controller|6" }) {
            var p = mo.Split('|'); snap.Engines.Add(new KeyValuePair<string, float>(p[0], float.Parse(p[1], CultureInfo.InvariantCulture)));
        }
        snap.UsedVram = 4590; snap.TotalVram = 32607; snap.Shared = 156; snap.GpuMhz = 2407; snap.GpuWatt = 54.3f; snap.Fan = 0;
        double gb = 1073741824;
        snap.TotalRam = 127.6 * gb; snap.UsedRam = 53.1 * gb; snap.Committed = 61.4 * gb; snap.Limit = 146 * gb;
        snap.Cache = 47.2 * gb; snap.Modified = 0.6 * gb; snap.PlaceDetached = 27.9 * gb; snap.Paging = 1.1 * gb; snap.NonPaging = 0.7 * gb; snap.Compressed = 0.4 * gb;
        snap.DiskEtw = true;
        x.Current = snap;
        return x;
    }
}
