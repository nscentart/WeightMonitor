using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public static partial class Notification {
    public static List<string> Test(int sinceLive) {
        var r = new List<string>();
        var chrome = new App { Key = @"c:\programs\google\chrome.exe", Name = "Google Chrome", Path = @"C:\Programs\Google\chrome.exe" };
        var video = new App { Key = @"c:\programs\video\editing.exe", Name = "Editing program", Path = @"C:\Programs\Video\editing.exe" };
        var longValue = new App { Key = "long", Name = new string('x', 80), Path = @"C:\Programs\long.exe" };
        var svchost = new App { Key = @"c:\windows\system32\svchost.exe", Name = "svchost", Windows = true, Path = Path.Combine(Environment.SystemDirectory, "svchost.exe") };
        string CLOSE_IT = Language.T("notification.close_it"), LEAVE_IT = Language.T("notification.leave_it"), PIECE = Language.T("notification.windows_part", "parenthesis", ""), UNKNOWN = Language.T("notification.freeze_unknown");
        Func<string, string, double, string> Use = (name, piece, percent) => Language.T("notification.use.program", "name", name, "share", Notifications.Quota(piece, percent, new Sample()));
        Func<string, string> CHROME_BLAME = share => Language.T("notification.freeze_program", "name", "Google Chrome", "share", share);
        var defender = new App { Key = "name:msmpeng.exe", Name = "MsMpEng" };
        var io = new App { Key = Application.ExecutablePath.ToLowerInvariant(), Name = "WeightMonitor", Path = Application.ExecutablePath };

        Func<double, double, double, Sample> Base = (cpu, gpu, ram) => new Sample { Cpu = cpu, Gpu = gpu, Ram = ram };
        Action<string, int, int, Func<int, Sample>, Func<int, string>, string, string> Case = (name, seconds, expected, data, quiet, must, mustNot) => {
            var l = new Notifications();
            var outputs = new List<KeyValuePair<int, Notice>>();
            for (int t = 0; t < seconds; t++) {
                int now = t;
                var c = data(t);
                l.RememberNames(c);
                foreach (var a in l.Tick(c, 1000 + t, quiet == null ? (Func<string>)null : () => quiet(now))) outputs.Add(new KeyValuePair<int, Notice>(t, a));
            }
            bool fine = outputs.Count == expected;
            foreach (var u in outputs) {
                if (u.Value.Title.Length > 63 || u.Value.Text.Length > 255 || u.Value.Text.Contains("\u2014")) fine = false;
                if (must != null && !u.Value.Text.Contains(must)) fine = false;
                if (mustNot != null && u.Value.Text.Contains(mustNot)) fine = false;
            }
            r.Add((fine ? "ok " : "ERROR ") + name + ": " + outputs.Count + " notifications, expected " + expected);
            foreach (var u in outputs) r.Add("    after " + u.Key + " s, \"" + u.Value.Title + "\" (opens " + u.Value.Flyout + "): " + u.Value.Text);
        };

        Case("short 20-second peak", 120, 0, t => Base(t < 20 ? 99 : 10, 5, 40), null, null, null);
        Case("processor at 94% steady, with a program using more", 120, 1, t => { var c = Base(94, 5, 40); c.Add("cpu", chrome, 30); return c; }, null, Language.T("notification.top_program", "name", "Google Chrome", "percent", "30%"), null);
        Case("processor at 94% steady, no program in view", 120, 1, t => Base(94, 5, 40), null, Language.T("notification.close_programs"), null);
        Case("up and down, never 45 seconds in a row", 300, 0, t => Base(t % 40 < 30 ? 95 : 20, 5, 40), null, null, null);
        Case("a 2-second dip does not reset", 120, 1, t => Base(t == 20 || t == 21 ? 40 : 95, 5, 40), null, null, null);
        Case("memory almost full", 120, 1, t => Base(10, 5, 93), null, Language.T("notification.ram_full", "percent", "93%"), null);
        Case("graphics card at maximum", 120, 1, t => Base(10, 97, 40), null, Language.T("notification.gpu_max", "percent", "97%"), null);
        Case("a program above 50% of the processor", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", chrome, 62); return c; }, null, Use("Google Chrome", "cpu", 62), null);
        Case("a program above 50% of the graphics card", 120, 1, t => { var c = Base(10, 70, 40); c.Add("gpu", video, 66); return c; }, null, Notifications.Quota("gpu", 66, new Sample()), null);
        Case("a program above 50% of the memory", 120, 1, t => { var c = Base(10, 5, 70); c.Add("ram", chrome, 55); return c; }, null, Notifications.Quota("ram", 55, new Sample()), null);
        Case("a piece of Windows: never advise closing it", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", svchost, 55); return c; }, null, LEAVE_IT, CLOSE_IT);
        Case("Windows Defender without administrator: it is Windows even without a path", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", defender, 55); return c; }, null, Language.T("notification.quoted_name", "name", Language.T("explain.MsMpEng.name")), CLOSE_IT);
        var notepad = new App { Key = "notepad", Name = "Notepad", Path = Path.Combine(Environment.SystemDirectory, "notepad.exe") };
        var unknown = new App { Key = "unknown", Name = "Strange tool", Path = Path.Combine(Environment.SystemDirectory, "tool.exe") };
        var fake = new App { Key = "fake", Name = "Fake", Path = @"D:\Downloads\svchost.exe" };
        Case("Notepad is not a piece of Windows", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", notepad, 55); return c; }, null, Use("Notepad", "cpu", 55), PIECE);
        Case("Notepad: it can be said to close it", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", notepad, 55); return c; }, null, CLOSE_IT, LEAVE_IT);
        Case("a piece of Windows without explanation", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", unknown, 55); return c; }, null, Language.T("notification.windows_part", "parenthesis", " (Strange tool)").Substring(1), CLOSE_IT);
        Case("the explanation arrives in the notification", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", svchost, 55); return c; }, null, Language.T("explain.svchost.summary"), null);
        r.Add((Explanations.Row(fake) == null ? "ok " : "ERROR ") + "an svchost outside the Windows files gets no explanation");
        r.Add((Explanations.Name(svchost) == Language.T("explain.svchost.name") && Explanations.Name(chrome) == "Google Chrome" ? "ok " : "ERROR ") + "the plain name only for the Windows programs");
        int veryLong = 0;
        foreach (string file in Explanations.All) {
            var a = new App { Key = "x", Name = file, Path = Path.Combine(Environment.SystemDirectory, file + ".exe") };
            var l = new Notifications();
            for (int t = 0; t < 60; t++) {
                var c = Base(70, 5, 40); c.Add("cpu", a, 55);
                foreach (var u in l.Tick(c, 1000 + t, null)) veryLong = Math.Max(veryLong, u.Text.Length);
            }
        }
        r.Add((veryLong > 0 && veryLong <= 255 ? "ok " : "ERROR ") + "the explanations fit in the notification: at most " + veryLong + " characters out of 255");
        Case("WeightMonitor does not blame itself", 300, 0, t => { var c = Base(70, 5, 40); c.Add("cpu", io, 80); return c; }, null, null, null);
        Case("WeightMonitor does not blame itself even for a freeze", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", io, 80); if (t == 70) c.Stall = 3; return c; }, null, UNKNOWN, "WeightMonitor");
        Case("missing data: no alarms", 300, 0, t => Base(-1, -1, -1), null, null, null);
        Case("a value that disappears resets the count", 300, 0, t => Base(t % 30 == 29 ? -1 : 95, -1, -1), null, null, null);
        Case("pace: same cause every 30 minutes", 3600, 2, t => { var c = Base(70, 5, 40); c.Add("cpu", chrome, 60); return c; }, null, null, null);
        Case("pace: a warning about the program also counts for the total", 600, 1, t => { var c = Base(95, 5, 40); c.Add("cpu", chrome, 60); return c; }, null, Use("Google Chrome", "cpu", 60), null);
        Case("pace: two programs together, 5 minutes between them", 400, 2, t => { var c = Base(70, 5, 70); c.Add("cpu", chrome, 60); c.Add("ram", video, 55); return c; }, null, null, null);
        Case("silence for 3 minutes: the warning waits", 240, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", chrome, 60); return c; }, t => t < 180 ? "full screen" : null, null, null);
        Case("silence always: nothing", 600, 0, t => { var c = Base(95, 95, 95); c.Add("cpu", chrome, 60); return c; }, t => "bar hidden", null, null);
        Case("freeze during silence: it is lost", 120, 0, t => { var c = Base(10, 5, 40); if (t == 10) c.Stall = 2; return c; }, t => t < 60 ? "game" : null, null, null);
        Case("freeze with the culprit, said with \"probably\"", 30, 1, t => { var c = Base(80, 5, 40); c.Add("cpu", chrome, 70); if (t == 10) c.Stall = 3; return c; }, null, CHROME_BLAME(Notifications.Quota("cpu", 70, new Sample())), null);
        Case("freeze with the culprit seen only the second before", 30, 1, t => { var c = Base(10, 5, 40); if (t == 9) c.Add("cpu", chrome, 70); if (t == 10) c.Stall = 2; return c; }, null, CHROME_BLAME(Notifications.Quota("cpu", 70, new Sample())), null);
        Case("freeze of a piece of Windows", 30, 1, t => { var c = Base(80, 5, 40); c.Add("cpu", svchost, 70); if (t == 10) c.Stall = 2; return c; }, null, Language.T("notification.freeze_windows", "who", Notifications.WindowsPiece(Notifications.EntryOf("cpu", svchost, 70)), "share", Notifications.Quota("cpu", 70, new Sample())), CLOSE_IT);
        Case("freeze without a culprit", 30, 1, t => { var c = Base(10, 5, 40); if (t == 10) c.Stall = 1.2; return c; }, null, Language.N("notification.frozen", 1), "Google Chrome");
        Case("a lot of memory but not full: it is not the culprit of the freeze", 30, 1, t => { var c = Base(10, 5, 50); c.Add("ram", chrome, 45); if (t == 10) c.Stall = 2; return c; }, null, UNKNOWN, null);
        Case("two freezes in 5 minutes: only one", 300, 1, t => { var c = Base(10, 5, 40); if (t == 10 || t == 200) c.Stall = 2; return c; }, null, null, null);
        Case("program that does not respond", 30, 1, t => { var c = Base(10, 5, 40); c.NotResponding = Notifications.EntryOf("", chrome, 100); return c; }, null, Language.T("notification.unresponsive_program", "name", "Google Chrome"), null);
        Case("a piece of Windows that does not respond", 30, 1, t => { var c = Base(10, 5, 40); c.NotResponding = Notifications.EntryOf("", svchost, 100); return c; }, null, Language.T("notification.unresponsive_windows", "who", Language.T("notification.quoted_name", "name", Language.T("explain.svchost.name"))), CLOSE_IT);
        Case("a single second without response: it is nothing", 30, 0, t => { var c = Base(10, 5, 40); if (t == 5) c.NotResponding = Notifications.EntryOf("", chrome, 100); return c; }, null, null, null);
        Func<double, double, Sample> Temp = (cpu, gpu) => new Sample { Cpu = 40, Gpu = 40, Ram = 40, CpuTemp = cpu, GpuTemp = gpu };
        Case("processor at 95 degrees under load: normal for many, no warning", 600, 0, t => Temp(95, 60), null, null, null);
        Case("processor at 98 degrees for a minute", 120, 1, t => Temp(98, 60), null, Language.T("notification.hot_cpu", "temperature", 98), null);
        Case("graphics card at 92 degrees for a minute", 120, 1, t => Temp(60, 92), null, Language.T("notification.hot_gpu", "temperature", 92), null);
        Case("graphics card at 85 degrees: no warning", 600, 0, t => Temp(60, 85), null, null, null);
        Case("a 40-second heat peak: no warning", 300, 0, t => Temp(t > 20 && t < 60 ? 100 : 60, 60), null, null, null);
        Case("heat up and down, never a minute in a row", 600, 0, t => Temp(t % 50 < 40 ? 99 : 70, 60), null, null, null);
        Case("temperature missing: no alarm", 300, 0, t => Temp(-1, -1), null, null, null);
        Case("absurd sensor reading: no alarm", 300, 0, t => Temp(255, 60), null, null, null);
        Case("the heat returns at most every 30 minutes", 2000, 2, t => Temp(99, 60), null, null, null);
        Case("during a full-screen game it waits", 300, 0, t => Temp(99, 95), t => "full screen", null, null);
        Func<int, Sample> FullSample = t => { var c = Base(10, 5, 40); c.AlmostFullDisks['E'] = 12300000000L; return c; };
        Case("disk almost full", 120, 1, FullSample, null, Language.T("notification.disk_full", "disk", "E", "free", "12 GB"), null);
        Case("disk almost full: once a day, not every half hour", 7200, 1, FullSample, null, null, null);
        Case("disk almost full, two days in a row", 90000, 2, FullSample, null, null, null);
        Case("two disks almost full: 5 minutes between them", 400, 2, t => { var c = FullSample(t); c.AlmostFullDisks['D'] = 1000000000L; return c; }, null, null, null);
        Case("disk almost full during silence: it waits", 300, 0, FullSample, t => "full screen", null, null);
        Action<string, Func<int, Sample>, bool> Bring = (name, data, wants) => {
            var l = new Notifications();
            Notice first = null;
            for (int t = 0; t < 120 && first == null; t++) foreach (var a in l.Tick(data(t), 1000 + t, null)) { first = a; break; }
            bool ok = first != null && (first.App != null) == wants;
            r.Add((ok ? "ok " : "ERROR ") + "click on the notification, " + name + ": " + (wants ? "it offers to close it with the confirmation" : "it opens only the flyout"));
        };
        Bring("a normal program", t => { var c = Base(70, 5, 40); c.Add("cpu", chrome, 60); return c; }, true);
        Bring("the culprit of the full processor", t => { var c = Base(95, 5, 40); c.Add("cpu", chrome, 30); return c; }, true);
        Bring("a program that does not respond", t => { var c = Base(10, 5, 40); c.NotResponding = Notifications.EntryOf("", chrome, 100); return c; }, true);
        Bring("a piece of Windows", t => { var c = Base(70, 5, 40); c.Add("cpu", svchost, 60); return c; }, false);
        Bring("Windows that does not respond", t => { var c = Base(10, 5, 40); c.NotResponding = Notifications.EntryOf("", svchost, 100); return c; }, false);
        Bring("the heat", t => { var c = Base(10, 5, 40); c.CpuTemp = 99; return c; }, false);
        Case("very long name", 120, 1, t => { var c = Base(70, 5, 40); c.Add("cpu", longValue, 60); return c; }, null, "...", null);

        Func<bool, Sample> Disks = etw => {
            var d = new SensorData();
            d.Cpu.Usage = 10; d.Gpu.Usage = 5; d.Ram.Usage = 40;
            d.Disks.Add(new Disk { Number = 0, Usage = 90, Letters = new List<char> { 'C' } });
            var snap = new Snapshot { DiskEtw = etw };
            snap.AppDisk[0] = new Dictionary<App, double> { { video, 900 }, { chrome, 100 } };
            return Sample.From(d, snap, new List<DetailRow>(), new List<DetailRow>(), new List<DetailRow>());
        };
        Case("a program above 50% of a disk", 120, 1, t => Disks(true), null, Language.T("notification.share.disk", "percent", Notifications.PercentPhrase(81), "disk", "C:"), null);
        Case("disks without administrator: no alarms", 300, 0, t => Disks(false), null, null, null);

        r.AddRange(Health.Test());
        bool fields = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance) != null
                  && typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance) != null;
        r.Add((fields ? "ok " : "ERROR ") + "the silent route finds the icon window");

        if (sinceLive > 0) {
            var s = new Sensors();
            s.Read();
            for (int i = 0; i < sinceLive; i++) {
                Thread.Sleep(1000);
                s.Read();
                var c = Collect(s.Current, s.Details);
                c.NotResponding = Front(s.Details);
                r.Add("live: processor " + c.Cpu + ", graphics card " + c.Gpu + ", memory " + c.Ram + ", programs above 10%: " + c.Entries.Count
                      + (c.NotResponding == null ? "" : ", in front it does not respond") + ", silence: " + (Silence() ?? "no"));
            }
            s.Close();
        }
        return r;
    }
}
