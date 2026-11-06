using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class Health {
    public static List<string> Test() {
        var r = new List<string>();
        var chrome = new App { Key = "chrome", Name = "Google Chrome", Path = @"C:\Programs\Google\chrome.exe" };
        var svchost = new App { Key = "svchost", Name = "svchost", Windows = true, Path = Path.Combine(Environment.SystemDirectory, "svchost.exe") };
        string FINE = Language.T("status.fine"), WAIT = Language.T("status.waiting"), BLAME = Language.T("status.blame", "name", "Google Chrome"), WINDOWS = Language.T("status.windows"), NONE = Language.T("status.nobody");
        string DATA_NOBODY = Language.T("status.data_nobody"), UNKNOWN_CAUSE = Language.T("status.unknown_cause");
        Action<string, int, Func<int, Sample>, string, bool> Case = (name, seconds, data, expected, struggling) => {
            var l = new Health();
            Sentence f = null;
            for (int t = 0; t < seconds; t++) f = l.Tick(data(t), 1000 + t);
            bool ok = f.Text.Contains(expected) && f.Struggling == struggling && !f.Text.Contains("\u2014") && (f.Subtext == null || !f.Subtext.Contains("\u2014"));
            r.Add((ok ? "ok " : "ERROR ") + "sentence, " + name + ": \"" + f.Text + "\"" + (f.Subtext == null ? "" : " / " + f.Subtext));
        };
        Func<double, double, double, Sample> B = (cpu, gpu, ram) => new Sample { Cpu = cpu, Gpu = gpu, Ram = ram };
        Case("all quiet", 120, t => B(12, 9, 42), FINE, false);
        Case("no data", 120, t => B(-1, -1, -1), WAIT, false);
        Case("a 25-second peak is not strain", 26, t => { var c = B(99, 5, 40); c.Add("cpu", chrome, 80); return c; }, FINE, false);
        Case("up and down without ever 30 seconds in a row", 300, t => B(t % 25 < 20 ? 97 : 20, 5, 40), FINE, false);
        Case("full processor with a program that stands out", 60, t => { var c = B(96, 5, 40); c.Add("cpu", chrome, 70); return c; }, BLAME, true);
        Case("a piece of Windows is not blamed by name", 60, t => { var c = B(96, 5, 40); c.Add("cpu", svchost, 70); return c; }, WINDOWS, true);
        Case("many small programs: no culprit", 60, t => { var c = B(96, 5, 40); c.Add("cpu", chrome, 15); return c; }, NONE, true);
        Case("full memory", 60, t => { var c = B(10, 5, 93); c.Add("ram", chrome, 40); return c; }, BLAME, true);
        Case("the processor drops to 80%: the strain remains", 100, t => B(t < 60 ? 96 : 80, 5, 40), NONE, true);
        Case("the processor really drops: it passes", 100, t => B(t < 60 ? 96 : 30, 5, 40), FINE, false);
        Case("a heavy program on a piece that is not full: all is well", 120, t => { var c = B(70, 5, 40); c.Add("cpu", chrome, 60); return c; }, FINE, false);
        Case("a value that disappears resets", 198, t => B(t % 20 == 19 ? -1 : 96, -1, -1), FINE, false);
        Func<double, double, Sample> T = (cpu, gpu) => new Sample { Cpu = 40, Gpu = 40, Ram = 40, CpuTemp = cpu, GpuTemp = gpu };
        Case("very hot processor", 70, t => T(99, 50), Language.T("status.hot_cpu"), true);
        Case("very hot graphics card", 70, t => T(50, 93), Language.T("status.hot_gpu"), true);
        Case("95 degrees: all is well", 200, t => T(95, 50), FINE, false);
        Case("the heat passes", 120, t => T(t < 80 ? 99 : 60, 50), FINE, false);
        Func<double, Sample> DataDiskSample = usage => { var c = B(10, 5, 40); c.DiskUsage["disk0"] = usage; c.DiskNames["disk0"] = "E:"; c.DiskUsage["disk1"] = 3; c.DiskNames["disk1"] = "C:"; c.SystemDisk = "disk1"; return c; };
        Func<double, Sample> System = usage => { var c = B(10, 5, 40); c.DiskUsage["disk1"] = usage; c.DiskNames["disk1"] = "C:"; c.SystemDisk = "disk1"; return c; };
        Case("data disk busy, without a culprit: no name", 60, t => DataDiskSample(99), Language.T("status.data_disk_only", "disk", "E:"), true);
        Case("data disk busy, with a culprit", 60, t => { var c = DataDiskSample(99); c.Add("disk0", chrome, 80); return c; }, Language.T("status.data_blame", "disk", "E:", "name", "Google Chrome"), true);
        Case("data disk busy because of Windows", 60, t => { var c = DataDiskSample(99); c.Add("disk0", svchost, 80); return c; }, Language.T("status.data_windows", "disk", "E:"), true);
        Case("Windows disk busy, with a culprit", 60, t => { var c = System(99); c.Add("disk1", chrome, 80); return c; }, BLAME, true);
        Case("Windows disk busy, without a culprit", 60, t => System(99), Language.T("status.system_disk_only", "disk", "C:"), true);
        Case("a 25-second disk peak is not strain", 26, t => DataDiskSample(99), FINE, false);
        Case("the disk drops to 80%: it stays busy", 100, t => DataDiskSample(t < 60 ? 99 : 80), Language.T("status.data_disk_only", "disk", "E:"), true);
        Case("the disk becomes free: it passes", 100, t => DataDiskSample(t < 60 ? 99 : 20), FINE, false);
        Case("a disk we no longer know anything about starts over", 100, t => { var c = DataDiskSample(99); if (t >= 60) { c.DiskUsage.Remove("disk0"); } return c; }, FINE, false);
        Case("full processor and busy data disk: the processor wins", 60, t => { var c = DataDiskSample(99); c.Cpu = 96; return c; }, NONE, true);
        Case("full processor and busier Windows disk: the disk wins", 60, t => { var c = System(99); c.Cpu = 92; return c; }, Language.T("status.system_disk_only", "disk", "C:"), true);
        Action<string, Func<int, Sample>, string, string> SubtextCase = (name, data, flyout, below) => {
            var l = new Health();
            Sentence f = null;
            for (int t = 0; t < 60; t++) f = l.Tick(data(t), 1000 + t);
            bool ok = f.Flyout == flyout && f.Subtext != null && f.Subtext.Contains(below) && f.Text.Contains("Chrome") == (below == Language.T("status.data_close_it"));
            r.Add((ok ? "ok " : "ERROR ") + "sentence, " + name + ": \"" + f.Text + "\" / " + f.Subtext + " / opens " + f.Flyout);
        };
        SubtextCase("data disk without a culprit: none is invented and the click opens the disks", t => DataDiskSample(99), "disks", DATA_NOBODY);
        SubtextCase("data disk with the culprit: the click opens the disks", t => { var c = DataDiskSample(99); c.Add("disk0", chrome, 80); return c; }, "disks", Language.T("status.data_close_it"));
        SubtextCase("Windows disk without a culprit", t => System(99), "disks", UNKNOWN_CAUSE);
        SubtextCase("the program is on another disk: it is not blamed", t => { var c = DataDiskSample(99); c.Add("disk1", chrome, 80); return c; }, "disks", DATA_NOBODY);
        SubtextCase("the program uses that disk little: it is not blamed", t => { var c = DataDiskSample(99); c.Add("disk0", chrome, 15); return c; }, "disks", DATA_NOBODY);
        {
            var d = new SensorData();
            char letter = char.ToUpperInvariant(Environment.SystemDirectory[0]);
            d.Disks.Add(new Disk { Number = 3, Letters = new List<char> { letter }, Usage = 50 });
            d.Disks.Add(new Disk { Number = 4, Letters = new List<char> { 'Z' == letter ? 'Y' : 'Z' }, Usage = -1 });
            d.Disks.Add(new Disk { Number = 5, Model = "Disk without letters", Usage = 7 });
            var c = new Sample();
            c.AddDisks(d);
            bool ok = c.SystemDisk == "disk3" && c.DiskUsage.Count == 2 && !c.DiskUsage.ContainsKey("disk4") && c.DiskNames["disk5"] == "Disk without letters" && c.DiskNames["disk3"] == letter + ":";
            r.Add((ok ? "ok " : "ERROR ") + "sentence, recognizes the Windows disk and skips the one without data");
        }
        Case("just after a freeze", 30, t => { var c = B(10, 5, 40); if (t == 5) c.Stall = 2; return c; }, Language.T("status.frozen"), true);
        Case("two minutes after a freeze", 140, t => { var c = B(10, 5, 40); if (t == 5) c.Stall = 2; return c; }, FINE, false);
        return r;
    }
}
