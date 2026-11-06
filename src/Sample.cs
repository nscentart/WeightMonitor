using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Sample {
    public double Cpu = -1, Gpu = -1, Ram = -1, Stall;
    public double CpuTemp = -1, GpuTemp = -1;
    public List<Entry> Entries = new List<Entry>();
    public Dictionary<string, string> DiskNames = new Dictionary<string, string>();
    public Entry NotResponding;
    public Dictionary<char, long> AlmostFullDisks = new Dictionary<char, long>();
    public Dictionary<string, double> DiskUsage = new Dictionary<string, double>();
    public string SystemDisk;

    public void AddDisks(SensorData d) {
        string system = Path.GetPathRoot(Environment.SystemDirectory);
        foreach (var disk in d.Disks) {
            if (disk.Number < 0 || disk.Usage < 0) continue;
            string piece = "disk" + disk.Number;
            DiskNames[piece] = disk.Letters.Count > 0 ? string.Join(" ", disk.Letters.Select(l => l + ":")) : disk.Model;
            DiskUsage[piece] = disk.Usage;
            if (system.Length > 0 && disk.Letters.Contains(char.ToUpperInvariant(system[0]))) SystemDisk = piece;
        }
    }

    public void Add(string piece, App a, double percent) {
        if (percent >= 10 && !Notifications.IsSelf(a)) Entries.Add(Notifications.EntryOf(piece, a, percent));
    }

    public static Sample From(SensorData d, Snapshot snap, List<DetailRow> cpu, List<DetailRow> gpu, List<DetailRow> ram) {
        var c = new Sample { Cpu = d.Cpu.Usage, Gpu = d.Gpu.Usage, Ram = d.Ram.Usage, CpuTemp = d.Cpu.Temp, GpuTemp = d.Gpu.Temp };
        foreach (var r in cpu) c.Add("cpu", r.App, r.Sum[0]);
        foreach (var r in gpu) c.Add("gpu", r.App, r.Sum[0]);
        if (snap.TotalRam > 0) foreach (var r in ram) c.Add("ram", r.App, r.Sum[0] / snap.TotalRam * 100.0);
        c.AddDisks(d);
        foreach (var disk in d.Disks)
            foreach (var kv in disk.Space)
                if (Disk.AlmostFull(kv.Value[0], kv.Value[1])) c.AlmostFullDisks[kv.Key] = kv.Value[0];
        if (snap.DiskEtw)
            foreach (var disk in d.Disks) {
                Dictionary<App, double> bytesByApp;
                if (disk.Usage < 0 || !snap.AppDisk.TryGetValue(disk.Number, out bytesByApp)) continue;
                double total = bytesByApp.Values.Sum();
                if (total <= 0) continue;
                string piece = "disk" + disk.Number;
                foreach (var kv in bytesByApp) c.Add(piece, kv.Key, disk.Usage * kv.Value / total);
            }
        return c;
    }
}
