using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

public class SensorData {
    public Reading Cpu = new Reading(), Gpu = new Reading(), Ram = new Reading();
    public string CpuName = "", GpuName = "";
    public bool IntegratedGpu;
    public float UsedRam = -1f, TotalRam = -1f;
    public float CommitUsed = -1f, CommitTotal = -1f;
    public List<Disk> Disks = new List<Disk>();
    public double Up = -1, Down = -1;

    public List<KeyValuePair<char, Disk>> Letters() {
        var l = new List<KeyValuePair<char, Disk>>();
        foreach (var d in Disks) foreach (char c in d.Letters) l.Add(new KeyValuePair<char, Disk>(c, d));
        l.Sort((a, b) => a.Key.CompareTo(b.Key));
        return l;
    }
    public float CommitFree() { return Real(CommitTotal) && Real(CommitUsed) && CommitTotal > 0 && CommitUsed <= CommitTotal ? CommitTotal - CommitUsed : -1f; }
    public float CommitPercent() { return CommitFree() < 0 ? -1f : CommitUsed / CommitTotal * 100f; }
    static bool Real(float v) { return v >= 0 && !float.IsNaN(v) && !float.IsInfinity(v); }
    public float TempMax() { return Disks.Count == 0 ? -1f : Disks.Max(d => d.Temp); }
    public float MaxUsage() { return Disks.Count == 0 ? -1f : Disks.Max(d => d.Usage); }
}
