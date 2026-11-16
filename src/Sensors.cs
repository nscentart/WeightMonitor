using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using LibreHardwareMonitor.Hardware;

public partial class Sensors {
    readonly Computer percent = new Computer();
    readonly List<Disk> disks = new List<Disk>();
    readonly Dictionary<int, PerformanceCounter> task = new Dictionary<int, PerformanceCounter>();
    readonly Dictionary<int, IHardware> lhmByDisk = new Dictionary<int, IHardware>();
    readonly List<IHardware> modules = new List<IHardware>();
    readonly List<string> trace = new List<string>();
    IHardware cpu, gpu, memory;
    bool integratedGpu;
    List<NetworkInterface> networks = new List<NetworkInterface>();
    string idNetworks = "";
    long rxPrevious = -1, txPrevious = -1;
    DateTime tPrevious, tNetworks = DateTime.MinValue, tDisks = DateTime.MinValue, tGuard = DateTime.MinValue, tSpace = DateTime.MinValue;
    volatile Dictionary<char, long[]> spaces = new Dictionary<char, long[]>();
    int spaceInProgress;
    readonly DiskGuard guard = new DiskGuard();
    int redone;
    public volatile SensorData Current = new SensorData();
    public readonly Details Details;

    public Sensors() {
        percent.IsCpuEnabled = true; percent.IsGpuEnabled = true; percent.IsMemoryEnabled = true; percent.IsStorageEnabled = true;
        percent.Open();
        Inventory();
        Details = new Details();
        Details.Instance = Details;
    }

    public void Close() { try { percent.Close(); } catch { } }

    static IEnumerable<ISensor> All(IHardware h) { return h.Sensors.Concat(h.SubHardware.SelectMany(x => x.Sensors)); }

    static bool Integrated(IHardware h) {
        string n = h.Name;
        return n.Contains("(TM) Graphics") || n == "AMD Radeon Graphics" || n.Contains("UHD Graphics")
            || n.Contains("Iris") || n.StartsWith("Intel(R) Graphics");
    }

    static bool TempReal(ISensor s) {
        string n = s.Name.ToLowerInvariant();
        foreach (string no in new[] { "limit", "critical", "warning", "high", "low", "threshold", "max", "min", "resolution" })
            if (n.Contains(no)) return false;
        return s.SensorType == SensorType.Temperature && s.Value.HasValue && s.Value.Value > 0f && s.Value.Value < 125f;
    }

    public void Inventory() {
        lock (trace) InventoryInside();
    }

    void InventoryInside() {
        trace.Clear();
        trace.Add(Language.Fixed("fixed.sensors.title", "version", Program.VERSION, "date", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
        trace.Add(Language.Fixed("fixed.sensors.admin", "state", Language.Fixed(Program.Admin() ? "fixed.sensors.admin_yes" : "fixed.sensors.admin_no")));
        foreach (var h in percent.Hardware) { try { h.Update(); foreach (var s in h.SubHardware) s.Update(); } catch { } }

        cpu = percent.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        trace.Add(Language.Fixed("fixed.sensors.cpu", "name", cpu == null ? Language.Fixed("fixed.sensors.not_found") : cpu.Name));

        var gpus = percent.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd
                                       || h.HardwareType == HardwareType.GpuIntel).ToList();
        gpu = gpus.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia && !Integrated(h))
           ?? gpus.FirstOrDefault(h => !Integrated(h)) ?? gpus.FirstOrDefault();
        integratedGpu = gpu != null && Integrated(gpu);
        foreach (var g in gpus) trace.Add(Language.Fixed("fixed.sensors.gpu", "name", g.Name, "kind", Language.Fixed(Integrated(g) ? "fixed.sensors.integrated" : "fixed.sensors.dedicated"), "shown", g == gpu ? "  " + Language.Fixed("fixed.sensors.shown") : ""));
        if (gpus.Count == 0) trace.Add(Language.Fixed("fixed.sensors.no_gpu"));

        memory = percent.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory && h.Name == "Total Memory")
               ?? percent.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory && !h.Name.Contains("Virtual"));
        modules.Clear();
        foreach (var h in percent.Hardware)
            if (h != memory && (h.HardwareType == HardwareType.Memory || h.Name.Contains("DIMM")) && !h.Name.Contains("Virtual")
                && All(h).Any(s => s.SensorType == SensorType.Temperature)) modules.Add(h);
        trace.Add(Language.Fixed("fixed.sensors.ram", "name", memory == null ? Language.Fixed("fixed.sensors.not_found") : memory.Name, "count", modules.Count));
        foreach (var m in modules)
            foreach (var s in All(m).Where(x => x.SensorType == SensorType.Temperature))
                trace.Add("  " + m.Name + " | " + s.Name + " = " + s.Value + "  " + Language.Fixed(TempReal(s) ? "fixed.sensors.used" : "fixed.sensors.discarded"));

        InventoryDisks();
        tNetworks = DateTime.MinValue;
        tDisks = DateTime.MinValue;
    }

    static IEnumerable<string> DiskSignature() {
        return new PerformanceCounterCategory("PhysicalDisk").GetInstanceNames().Concat(new[] { string.Join(",", Environment.GetLogicalDrives()) });
    }

    void AskSpace(List<char> letters) {
        if (System.Threading.Interlocked.CompareExchange(ref spaceInProgress, 1, 0) != 0) return;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            try { spaces = ReadSpace(letters); }
            catch (Exception ex) { Error("free space", ex.Message); }
            finally { System.Threading.Interlocked.Exchange(ref spaceInProgress, 0); }
        });
    }

    static Dictionary<char, long[]> ReadSpace(List<char> letters) {
        var r = new Dictionary<char, long[]>();
        foreach (char l in letters) {
            try {
                var u = new DriveInfo(l.ToString());
                if (u.DriveType == DriveType.Fixed && u.IsReady && u.TotalSize > 0) r[l] = new[] { u.TotalFreeSpace, u.TotalSize };
            } catch { }
        }
        return r;
    }

    void CheckDisks() {
        try {
            if (!guard.Changed(DiskSignature())) return;
            lock (trace) InventoryDisks();
            tDisks = DateTime.MinValue;
            redone++;
            if (Details != null) Details.RedoDisks();
            Program.Log("the disks or their letters changed: counters rebuilt");
        } catch (Exception ex) { Error("disk check", ex.Message); }
    }

    void InventoryDisks() {
        disks.Clear(); lhmByDisk.Clear();
        foreach (var c in task.Values) c.Dispose();
        task.Clear();
        var perNumber = new Dictionary<int, Disk>();
        try {
            var ms = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            using (var q = new ManagementObjectSearcher(ms, new ObjectQuery("SELECT DeviceId, FriendlyName, MediaType, BusType, Size FROM MSFT_PhysicalDisk")))
                foreach (ManagementObject o in q.Get()) {
                    int n;
                    if (!int.TryParse(Convert.ToString(o["DeviceId"]), out n)) continue;
                    var d = new Disk { Number = n, Model = Convert.ToString(o["FriendlyName"]).Trim() };
                    try { d.Byte = Convert.ToInt64(o["Size"]); } catch { }
                    d.Type = TypeOf(Convert.ToInt32(o["MediaType"]));
                    d.Bus = BusNameOf(Convert.ToInt32(o["BusType"]));
                    perNumber[n] = d;
                }
            using (var q = new ManagementObjectSearcher(ms, new ObjectQuery("SELECT DiskNumber, DriveLetter FROM MSFT_Partition")))
                foreach (ManagementObject o in q.Get()) {
                    int n = Convert.ToInt32(o["DiskNumber"]), c = Convert.ToInt32(o["DriveLetter"]);
                    Disk d;
                    if (c > 0 && perNumber.TryGetValue(n, out d)) {
                        char l = char.ToUpperInvariant((char)c);
                        if (!d.Letters.Contains(l)) d.Letters.Add(l);
                    }
                }
        } catch (Exception ex) { trace.Add(Language.Fixed("fixed.sensors.storage_error", "message", ex.Message)); }
        if (perNumber.Count == 0) DisksFromWin32(perNumber);
        foreach (var d in perNumber.Values) d.Letters.Sort();
        disks.AddRange(perNumber.Values.OrderBy(d => d.Letters.Count == 0 ? 'Z' + 1 : d.Letters[0]).ThenBy(d => d.Number));

        try {
            guard.Changed(DiskSignature());
            foreach (string snap in new PerformanceCounterCategory("PhysicalDisk").GetInstanceNames()) {
                int sp = snap.IndexOf(' '), n;
                if (sp > 0 && int.TryParse(snap.Substring(0, sp), out n) && perNumber.ContainsKey(n)) {
                    var c = new PerformanceCounter("PhysicalDisk", "% Idle Time", snap);
                    c.NextValue(); task[n] = c;
                }
            }
        } catch (Exception ex) { trace.Add(Language.Fixed("fixed.sensors.activity_error", "message", ex.Message)); }

        var memories = percent.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();
        foreach (var d in disks) {
            var h = memories.FirstOrDefault(x => Similar(x.Name, d.Model));
            if (h != null) lhmByDisk[d.Number] = h;
            trace.Add(Language.Fixed("fixed.sensors.disk", "number", d.Number, "model", d.Model, "description", d.Description(true), "codes", d.Codes(true),
                       "sensor", h == null ? Language.Fixed("fixed.sensors.disk_no_sensor") : h.Name));
        }
        if (memories.Count == 0) trace.Add(Language.Fixed("fixed.sensors.no_disks"));
        foreach (var h in memories) trace.Add("  " + Language.Fixed("fixed.sensors.lhm_disk", "name", h.Name));
    }

    void DisksFromWin32(Dictionary<int, Disk> perNumber) {
        try {
            using (var q = new ManagementObjectSearcher("SELECT DeviceID, Index, Model, Size, InterfaceType FROM Win32_DiskDrive"))
                foreach (ManagementObject o in q.Get()) {
                    var d = new Disk { Number = Convert.ToInt32(o["Index"]), Model = Convert.ToString(o["Model"]).Trim(),
                                        Bus = Convert.ToString(o["InterfaceType"]) };
                    try { d.Byte = Convert.ToInt64(o["Size"]); } catch { }
                    string q1 = "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + Convert.ToString(o["DeviceID"]).Replace("\\", "\\\\")
                              + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                    using (var parts = new ManagementObjectSearcher(q1))
                        foreach (ManagementObject p in parts.Get()) {
                            string q2 = "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + Convert.ToString(p["DeviceID"])
                                      + "'} WHERE AssocClass=Win32_LogicalDiskToPartition";
                            using (var logical = new ManagementObjectSearcher(q2))
                                foreach (ManagementObject ld in logical.Get()) {
                                    string id = Convert.ToString(ld["DeviceID"]);
                                    if (id.Length > 0) d.Letters.Add(char.ToUpperInvariant(id[0]));
                                }
                        }
                    perNumber[d.Number] = d;
                }
        } catch (Exception ex) { trace.Add(Language.Fixed("fixed.sensors.win32_error", "message", ex.Message)); }
    }

    static string TypeOf(int m) { return m == 3 ? "HDD" : m == 4 ? "SSD" : m == 5 ? "SCM" : ""; }
    static string BusNameOf(int b) {
        switch (b) {
            case 17: return "NVMe"; case 11: return "SATA"; case 3: return "ATA"; case 7: return "USB";
            case 10: return "SAS"; case 8: return "RAID"; case 12: return "SD"; case 14: return "Virtual";
            default: return "";
        }
    }

    static bool Similar(string a, string b) {
        a = (a ?? "").Trim(); b = (b ?? "").Trim();
        return a.Length > 0 && b.Length > 0 && (a.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static List<NetworkInterface> ChooseNetworks() {
        var up = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up
            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
            && !n.Description.Contains("Hyper-V") && !n.Name.StartsWith("vEthernet") && !n.Description.Contains("Virtual")
            && !n.Description.Contains("VPN") && !n.Description.Contains("Bluetooth")).ToList();
        var withGateway = up.Where(n => { try { return n.GetIPProperties().GatewayAddresses.Count > 0; } catch { return false; } }).ToList();
        if (withGateway.Count > 0) return withGateway;
        var r = up.OrderByDescending(n => n.GetIPStatistics().BytesReceived).FirstOrDefault();
        return r == null ? new List<NetworkInterface>() : new List<NetworkInterface> { r };
    }

    ISensor Find(IHardware h, SensorType type, Func<ISensor, bool> choice) {
        if (h == null) return null;
        foreach (var s in All(h)) if (s.SensorType == type && s.Value.HasValue && choice(s)) return s;
        return null;
    }

    static float Value(ISensor s) { return s == null ? -1f : Math.Max(0f, Math.Min(100f, s.Value.Value)); }
    static float Degrees(ISensor s) { return (s == null || !s.Value.HasValue || s.Value.Value <= 0f || s.Value.Value >= 150f) ? -1f : s.Value.Value; }

    void TempDisks() {
        foreach (var h in lhmByDisk.Values) { try { h.Update(); } catch (Exception ex) { Error(h.Name, ex.Message); } }
        bool missing = false;
        foreach (var d in disks) {
            IHardware h; float t = -1f;
            if (lhmByDisk.TryGetValue(d.Number, out h))
                t = Degrees(Find(h, SensorType.Temperature, s => s.Name == "Temperature" || s.Name == "Composite Temperature")
                       ?? Find(h, SensorType.Temperature, s => s.Value.Value > 0));
            d.Temp = t;
            if (t < 0) missing = true;
        }
        if (!missing) return;
        try {
            var ms = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            using (var q = new ManagementObjectSearcher(ms, new ObjectQuery("SELECT DeviceId, Temperature FROM MSFT_StorageReliabilityCounter")))
            using (var all = q.Get())
                foreach (ManagementObject o in all)
                    using (o) {
                        int n; if (!int.TryParse(Convert.ToString(o["DeviceId"]), out n)) continue;
                        float t = Convert.ToSingle(o["Temperature"]);
                        foreach (var d in disks) if (d.Number == n && d.Temp < 0 && t > 0 && t < 150) d.Temp = t;
                    }
        } catch (Exception ex) { Error("disk temperatures, second route", ex.Message); }
    }

    readonly Dictionary<string, DateTime> errors = new Dictionary<string, DateTime>();
    readonly bool admin = Program.Admin();
    void Error(string where, string what) {
        DateTime u;
        if (errors.TryGetValue(where, out u) && (DateTime.UtcNow - u).TotalMinutes < 10) return;
        errors[where] = DateTime.UtcNow;
        Program.Log("sensors, " + where + ": " + what);
    }

    public void Read() {
        var now = DateTime.UtcNow;
        foreach (var h in new[] { cpu, gpu, memory }.Concat(modules))
            if (h != null) { try { h.Update(); foreach (var s in h.SubHardware) s.Update(); } catch (Exception ex) { Error(h.Name, ex.Message); } }
        if ((now - tGuard).TotalSeconds >= 5) { tGuard = now; CheckDisks(); }
        if ((now - tDisks).TotalSeconds >= 10) { tDisks = now; TempDisks(); }
        if ((now - tSpace).TotalSeconds >= 30) { tSpace = now; lock (trace) AskSpace(disks.SelectMany(z => z.Letters).ToList()); }

        var d = new SensorData();
        if (cpu != null) d.CpuName = cpu.Name.Trim();
        d.Cpu.Usage = Value(Find(cpu, SensorType.Load, s => s.Name == "CPU Total"));
        d.Cpu.Temp = Degrees(Find(cpu, SensorType.Temperature, s => s.Name == "Core (Tctl/Tdie)")
                        ?? Find(cpu, SensorType.Temperature, s => s.Name.Contains("Package"))
                        ?? Find(cpu, SensorType.Temperature, s => s.Value.Value > 0));

        if (admin && cpu != null && d.Cpu.Temp < 0) Error("processor temperature", "also missing as administrator");

        if (gpu != null) { d.GpuName = gpu.Name.Trim(); d.IntegratedGpu = integratedGpu; }
        d.Gpu.Usage = Value(Find(gpu, SensorType.Load, s => s.Name == "GPU Core") ?? Find(gpu, SensorType.Load, s => s.Name == "D3D 3D"));
        d.Gpu.Temp = Degrees(Find(gpu, SensorType.Temperature, s => s.Name == "GPU Core"));

        d.Ram.Usage = Value(Find(memory, SensorType.Load, s => s.Name == "Memory"));
        var used = Find(memory, SensorType.Data, s => s.Name == "Memory Used");
        var free = Find(memory, SensorType.Data, s => s.Name == "Memory Available");
        if (used != null) d.UsedRam = used.Value.Value;
        if (used != null && free != null) d.TotalRam = used.Value.Value + free.Value.Value;
        float tRam = -1f;
        foreach (var m in modules) foreach (var s in All(m)) if (TempReal(s)) tRam = Math.Max(tRam, s.Value.Value);
        d.Ram.Temp = tRam;
        double cu, ct;
        if (Details.Commit(out cu, out ct)) { d.CommitUsed = (float)(cu / 1073741824.0); d.CommitTotal = (float)(ct / 1073741824.0); }

        foreach (var disk in disks) {
            PerformanceCounter c;
            if (task.TryGetValue(disk.Number, out c)) {
                try { disk.Usage = Math.Max(0f, Math.Min(100f, 100f - c.NextValue())); } catch { disk.Usage = -1f; }
            }
            var sp = spaces;
            var copy = disk.Copy();
            foreach (char l in copy.Letters) { long[] v; if (sp.TryGetValue(l, out v)) copy.Space[l] = v; }
            d.Disks.Add(copy);
        }

        if ((now - tNetworks).TotalSeconds >= 30) {
            tNetworks = now;
            try {
                networks = ChooseNetworks();
                string id = string.Join(",", networks.Select(n => n.Id));
                if (id != idNetworks) { idNetworks = id; rxPrevious = -1; txPrevious = -1; }
            } catch { }
        }
        long rx = 0, tx = 0;
        foreach (var n in networks) { try { var st = n.GetIPStatistics(); rx += st.BytesReceived; tx += st.BytesSent; } catch { } }
        if (rxPrevious >= 0 && networks.Count > 0) {
            double sec = Math.Max(0.2, (now - tPrevious).TotalSeconds);
            d.Up = Math.Max(0, (tx - txPrevious) / sec);
            d.Down = Math.Max(0, (rx - rxPrevious) / sec);
        }
        rxPrevious = rx; txPrevious = tx; tPrevious = now;
        Current = d;
        if (Details != null) Details.Tick(d, ForDetails());
    }

    public const string MEMORY = "memory";
    public static string VisibleName(string n) {
        switch (n) {
            case MEMORY: return Language.T("sensors.memory");
            case "copy": return Language.T("sensors.copy");
            case "decode": return Language.T("sensors.decode");
            case "encode": return Language.T("sensors.encode");
            case "processing": return Language.T("sensors.processing");
            case "engine": return Language.T("sensors.engine");
            case "controller": return Language.T("sensors.controller");
            case "compute": return Language.T("sensors.compute");
        }
        return n;
    }

    static string Name(string n) {
        switch (n) {
            case "Core (Tctl/Tdie)": return "Tctl";
            case "CCD1 (Tdie)": return "CCD1"; case "CCD2 (Tdie)": return "CCD2";
            case "GPU Core": return "Core"; case "GPU Hot Spot": return "Hot spot"; case "GPU Memory Junction": return MEMORY;
            case "3D": return "3D"; case "Copy": return "copy"; case "Video Decode": return "decode";
            case "Video Encode": return "encode"; case "Video Processing": return "processing";
            case "GPU Video Engine": return "engine"; case "GPU Memory Controller": return "controller";
        }
        if (n.StartsWith("Compute")) return "compute";
        return n;
    }

    Lhm ForDetails() {
        var l = new Lhm();
        if (cpu != null) {
            l.CoreUsage = All(cpu).Where(s => s.SensorType == SensorType.Load && s.Name.StartsWith("CPU Core #") && s.Value.HasValue)
                                  .Select(s => s.Value.Value).ToArray();
            foreach (var s in All(cpu))
                if (TempReal(s) && !s.Name.StartsWith("Core #") && l.CpuTemp.Count < 4) l.CpuTemp.Add(new KeyValuePair<string, float>(Name(s.Name), s.Value.Value));
            var w = Find(cpu, SensorType.Power, s => s.Name == "Package" && s.Value.Value > 0);
            if (w != null) l.CpuWatt = w.Value.Value;
        }
        if (gpu != null) {
            foreach (var s in All(gpu))
                if (TempReal(s) && l.GpuTemp.Count < 3) l.GpuTemp.Add(new KeyValuePair<string, float>(Name(s.Name), s.Value.Value));
            var engines = new Dictionary<string, float>();
            foreach (var s in All(gpu)) {
                if (s.SensorType != SensorType.Load || !s.Value.HasValue) continue;
                string n = s.Name.StartsWith("D3D ") ? Name(s.Name.Substring(4)) : s.Name == "GPU Video Engine" || s.Name == "GPU Memory Controller" ? Name(s.Name) : null;
                if (n == null || n.StartsWith("Timer") || n == "Security" || n == "VR") continue;
                float v; engines.TryGetValue(n, out v); engines[n] = Math.Max(v, s.Value.Value);
            }
            var order = new[] { "3D", "copy", "decode", "encode", "compute", "engine", "controller" };
            l.Engines = engines.OrderBy(kv => Array.IndexOf(order, kv.Key) < 0 ? 99 : Array.IndexOf(order, kv.Key))
                             .Where(kv => Array.IndexOf(order, kv.Key) >= 0 || kv.Value > 0).Take(8).ToList();
            Func<string, float> mb = name => { var s = Find(gpu, SensorType.SmallData, x => x.Name == name); return s == null ? -1f : s.Value.Value; };
            l.UsedVram = mb("GPU Memory Used"); if (l.UsedVram < 0) l.UsedVram = mb("D3D Dedicated Memory Used");
            l.TotalVram = mb("GPU Memory Total"); if (l.TotalVram < 0) l.TotalVram = mb("D3D Dedicated Memory Total");
            l.Shared = mb("D3D Shared Memory Used");
            var c = Find(gpu, SensorType.Clock, s => s.Name == "GPU Core" && s.Value.Value > 0); if (c != null) l.GpuMhz = c.Value.Value;
            var w = Find(gpu, SensorType.Power, s => (s.Name == "GPU Package" || s.Name == "GPU Power") && s.Value.Value > 0); if (w != null) l.GpuWatt = w.Value.Value;
            var f = All(gpu).Where(s => s.SensorType == SensorType.Fan && s.Value.HasValue).ToList();
            if (f.Count > 0) l.Fan = f.Max(s => s.Value.Value);
        }
        return l;
    }

    public List<string> Summary() {
        lock (trace) {
            var r = new List<string>(trace);
            foreach (var n in networks) r.Add(Language.Fixed("fixed.sensors.network", "description", n.Description));
            return r;
        }
    }

    public void WriteInventory(string file, IEnumerable<string> other) {
        var rows = new List<string>(trace);
        foreach (var n in networks) rows.Add(Language.Fixed("fixed.sensors.network_named", "name", n.Name, "description", n.Description));
        rows.AddRange(other);
        rows.Add("");
        rows.Add(Language.Fixed("fixed.sensors.all"));
        foreach (var h in percent.Hardware)
            foreach (var s in All(h))
                rows.Add(h.HardwareType + " | " + h.Name + " | " + s.SensorType + " | " + s.Name + " | " + s.Value);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllLines(file, rows, System.Text.Encoding.UTF8);
    }
}
