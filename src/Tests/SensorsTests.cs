using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibreHardwareMonitor.Hardware;

public partial class Sensors {
    public static List<string> RealTest() {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        string f = Path.GetTempFileName();
        Settings.UseFile(f);
        Sensors x = null;
        try {
            x = new Sensors();
            x.Read();
            var previous = x.Current.Disks.Select(d => d.Number + ":" + d.Codes()).ToList();
            Check(previous.Count > 0, "disks read: " + previous.Count);
            x.tGuard = DateTime.MinValue;
            x.Read();
            Check(x.redone == 0, "without changes the counters are not rebuilt");
            x.guard.Changed(new[] { "1 Z:" });
            x.tGuard = DateTime.MinValue;
            x.Read();
            Check(x.redone == 1, "fake change: counters rebuilt");
            System.Threading.Thread.Sleep(1100);
            x.Read();
            Check(x.Current.Disks.Select(d => d.Number + ":" + d.Codes()).SequenceEqual(previous), "after the change the same disks with the same letters");
            Check(x.Current.Disks.All(d => d.Usage >= 0), "after the change the disk activity is read");
            var read = ReadSpace(x.Current.Letters().Select(kv => kv.Key).ToList());
            Check(read.Count > 0 && read.Values.All(v => v[0] >= 0 && v[0] <= v[1]), "free space read for " + read.Count + " letters, writing nothing");
            Check(Disk.AlmostFull(1000000000L, 64000000000L), "1 GB free out of 64 GB: almost full");
            Check(!Disk.AlmostFull(4000000000L, 64000000000L), "4 GB free out of 64 GB: not yet");
            Check(Disk.AlmostFull(12000000000L, 256000000000L), "12 GB free out of 256 GB: almost full");
            Check(!Disk.AlmostFull(240000000000L, 8000000000000L), "240 GB free out of 8 TB, 3%: not a problem");
            Check(Disk.AlmostFull(19000000000L, 1000000000000L), "19 GB free out of 1 TB: almost full");
            Check(!Disk.AlmostFull(-1, -1) && !Disk.AlmostFull(0, 0), "space not read: no alarm");
            x.TempDisks();
            r.Add("the second route of the temperatures ran without stopping the program; that it frees its resources at once can only be seen in the code");
        } finally { if (x != null) x.Close(); try { File.Delete(f); } catch { } }
        return r;
    }
}
