using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

public static partial class Program {
    static void WriteTest(string file, string title, List<string> rows) {
        if (file != null) File.WriteAllLines(file, rows, Encoding.UTF8);
        else foreach (var r in rows) Log(title + ": " + r);
    }

    static List<string> SampleScrollTest() {
        var network = new Network();
        network.SampleData(); network.Activate = true;
        return Flyout.ScrollTest(SampleData(), network);
    }

    static List<string> SecurityTest(string signature) {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        foreach (var u in new[] { "https://github.com/nscentart/WeightMonitor/releases/tag/v0.6", "https://github.com/nscentart/WeightMonitor/releases/latest" })
            Check(UpdateChecker.GoodAddress(u), "address accepted: " + u);
        foreach (var u in new[] { "https://github.com/nscentart/WeightMonitorFake/releases/download/v1/WeightMonitor.exe", "https://github.com/other/WeightMonitor/WeightMonitor.exe",
                                  "http://github.com/nscentart/WeightMonitor/WeightMonitor.exe", "https://github.com.fake.example/nscentart/WeightMonitor/WeightMonitor.exe",
                                  "https://github.com@fake.example/nscentart/WeightMonitor/WeightMonitor.exe", "https://github.com/nscentart/WeightMonitor/../Other/WeightMonitor.exe",
                                  "https://github.com/nscentart/WeightMonitor/%2e%2e/Other/WeightMonitor.exe", "https://github.com/nscentart/WeightMonitor/\" --other",
                                  "https://github.com/nscentart/WeightMonitor/a b", "https://github.com/nscentart/WeightMonitor/?x=1", "https://github.com/nscentart/WeightMonitor/#x", "", null })
            Check(!UpdateChecker.GoodAddress(u), "address rejected: " + u);

        Func<string, string, string, UpdateInfo> Response = (tag, page, file) => UpdateChecker.FromResponse("{\"tag_name\":\"" + tag + "\",\"body\":\"note\",\"html_url\":\"" + page + "\",\"assets\":[{\"name\":\"" + file + "\"}]}", new Version("1.1.0"));
        const string GOOD = "https://github.com/nscentart/WeightMonitor/releases/tag/v1.2";
        var nv = Response("v1.2", GOOD, "WeightMonitor.exe");
        Check(nv != null && nv.Label == "1.2" && nv.Page == GOOD, "new version: the release page opens");
        Check(Response("v1.1", GOOD, "WeightMonitor.exe") == null, "same version: no notice");
        Check(Response("v1.0", GOOD, "WeightMonitor.exe") == null, "old version: no notice");
        Check(Response("v1.2", GOOD, "other.zip") == null, "release without an exe: no notice");
        Check(Response("v1.2", "https://bad.example/x", "WeightMonitor.exe").Page == "https://github.com/nscentart/WeightMonitor/releases/latest", "page outside the repository: the official one opens");
        Check(typeof(UpdateInfo).GetField("Url") == null && typeof(UpdateInfo).GetField("Fingerprint") == null, "the news no longer carries the address of a file to download");
        Check(typeof(UpdateChecker).GetMethod("FromAdministrator") == null && typeof(UpdateChecker).GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Static) == null, "the code that downloads and installs is gone");
        r.AddRange(UpdateChecker.WindowTest());

        string c = Path.Combine(Path.GetTempPath(), "wm-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(c, "user"));
        try {
            string old = Path.Combine(c, "user", "settings.ini"), newOne = Path.Combine(c, "data", "settings.ini");
            File.WriteAllLines(old, new[] { "style=a", "tab_network=2", "notifications=no", "invented=C:\\bad.bat", "@echo off", "weather_reserve=broken", "skipped=" + new string('x', 100) });
            Settings.UseFile(newOne, old);
            Check(Settings.Read("style", "") == "a" && Settings.Read("tab_network", "") == "2" && Settings.Read("notifications", "") == "no", "migration: the known keys carry over");
            Check(File.Exists(newOne), "migration: the new file exists");
            string[] captions = File.ReadAllLines(newOne);
            Check(!captions.Any(x => x.Contains("bad") || x.Contains("echo")), "migration: the unknown keys are not rewritten");
            Check(Settings.Read("skipped", "empty") == "empty", "a value that is too long does not get in");
            Check(File.ReadAllLines(old).Length == 7, "the old file is left alone");
            File.AppendAllText(old, "style=b\r\n");
            Settings.Reread();
            Check(Settings.Read("style", "") == "a", "migration happens only once: a changed old file no longer counts");
            File.AppendAllText(newOne, "another=1\r\n");
            Settings.Reread();
            Check(Settings.Read("another", "empty") == "empty", "an unknown key added afterwards is not read");
            foreach (var k in new[] { "view", "detached", "hidden", "updates", "portable", "welcome", "position_x", "position_y", "tab_cpu", "tab_gpu", "tab_ram", "tab_disks", "tab_all" }) {
                Settings.Write(k, "1");
                Settings.Reread();
                Check(Settings.Read(k, "") == "1", "known key that stays: " + k);
            }
        } finally { Settings.UseFile(Path.Combine(c, "end", "settings.ini")); try { Directory.Delete(c, true); } catch { } }

        string clean = ErrorReport.Clean("error on 'D:\\Clients\\Smith Jones\\account.pdf' read\r\nin C:\\Jobs\\Smith Jones.txt end\r\nfrom \\\\server\\private\\file.txt\r\nhidden: covers the whole screen: window \"Secret class\" of process game\r\nC: 12%  D: 3%");
        Check(!new[] { "Clients", "Smith", "Jones", "account", "Jobs", "server", "private", "Secret", "game" }.Any(clean.Contains), "cleaned report: " + clean.Replace("\r\n", " / "));
        Check(clean.Contains(" read") && clean.Contains("C: 12%"), "report: the rest of the line and the disks stay");

        Check(Path.IsPathRooted(Installer.SystemPath("schtasks.exe")) && File.Exists(Installer.SystemPath("schtasks.exe")), "schtasks with the full path");
        Check(File.Exists(Installer.SystemPath("cmd.exe")) && File.Exists(Installer.SystemPath("timeout.exe")), "cmd and timeout with the full path");
        Check(File.Exists(Installer.Explorer()), "explorer with the full path");
        Check(Installer.Temporary.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)), "the installation's temporary files live under Program Files");
        var waits = new List<int>();
        int startups = 0, listens = 0;
        var results = new Queue<bool>(new[] { true, false, true });
        var durations = new Queue<double>(new[] { 1.0, 700.0 });
        Restart.Turn(() => { startups++; return results.Dequeue(); }, () => { listens++; return durations.Dequeue(); }, x => { waits.Add(x); return waits.Count < 3; });
        Check(startups == 3 && listens == 2, "read interrupted: it restarts, even after a failed start-up");
        Check(waits.SequenceEqual(new[] { 5, 10, 5 }), "read interrupted: it waits longer each time, and after a long read starts again from 5 seconds (" + string.Join(",", waits) + ")");
        Check(Restart.BackoffSeconds(0) == 5 && Restart.BackoffSeconds(50) == 300, "read interrupted: never beyond 5 minutes");
        var gd = new DiskGuard();
        Check(!gd.Changed(new[] { "0 C:", "1 D:" }), "disks: the first time it only learns");
        Check(!gd.Changed(new[] { "1 D:", "0 C:" }), "disks: same list in another order, nothing to redo");
        Check(gd.Changed(new[] { "0 C:", "1 E:" }), "disks: one letter changes, rebuild");
        Check(gd.Changed(new[] { "0 C:", "1 E:", "2 F:" }), "disks: one is plugged in, rebuild");
        Check(gd.Changed(new[] { "0 C:" }), "disks: one is unplugged, rebuild");
        using (var f = new PawnIOChoice()) {
            var buttons = f.Controls.OfType<Button>().ToList();
            Check(buttons.Count == 2 && buttons.Any(b => b.Text == Language.T("pawnio.yes")) && buttons.Any(b => b.Text == Language.T("pawnio.no")), "PawnIO: the two buttons with the decided words");
            Check(f.AcceptButton == null && f.CancelButton == null && buttons.All(b => b.DialogResult == DialogResult.None), "PawnIO: Enter and Esc do not choose");
            Check(buttons[0].BackColor == buttons[1].BackColor, "PawnIO: neither button is highlighted");
            Check(!f.Yes, "PawnIO: before choosing the answer is no");
        }
        using (var f = new Welcome(true)) {
            Check(!f.Controls.OfType<CheckBox>().Any(), "welcome: no box already ticked");
            Check(!f.PawnIO && !f.Install, "welcome: nothing decided before the click");
        }
        Check(Drawing.Logo() != null && GetDoubleClickTime() > 0, "calls to the Windows DLLs work with the search limited to System32");
        r.AddRange(OptionsTest());
        if (signature != null) r.Add("signature of " + Path.GetFileName(signature) + ": " + Installer.SignatureTest(signature));
        return r;
    }
    [DllImport("user32.dll")] static extern uint GetDoubleClickTime();

    static List<string> OptionsTest() {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        foreach (var row in new[] { "", "--open", "--style b", "--portable", "--test-capture", "--language de --color blue --appearance auto", "--preview", "--preview --sample --show cpu,ram --close-button", "--uninstall",
                                     @"--install --user D\n --pawnio --language it", @"--install --user D\n", "--inventory a.txt", "--test-shortcut a.lnk", "--test-notice", "--test-security --result a.txt --signature b.exe",
                                     "--test-notifications --result a.txt --live 0", "--test-updates --api http://x/y --result a.txt", "--test-flyouts --result a", "--test-themes --result a", "--test-close --result a", "--test-languages --result a", "--test-license --result a", "--test-etw --result a", "--test-disks --result a",
                                     "--screenshot a.png --sample --style a --view extended --flyout disks --tab 1 --height 48 --screen-height 400 --scroll 200 --background light --sentence yes --show cpu,ram --close-button --color pink --appearance light",
                                     "--screenshot-settings a --scale 1.5", "--screenshot-confirm a --windows 3 --details", "--screenshot-welcome a --without-pawnio", "--screenshot-report a", "--screenshot-update a", "--screenshot-menu a", "--screenshot-language a", "--screenshot-pawnio a", "--screenshot-license a --holder x --message other --scale 1.5", "--screenshot-settings a --holder x", "--screenshot a --sample --holder x" }) {
            string reason = WrongOption(row.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            Check(reason == null, "valid options: \"" + row + "\"" + (reason == null ? "" : " rejected: " + reason));
        }
        Func<string, string> OldOption = to => Legacy.Aliases.Concat(Legacy.InstallAliases).First(a => a.Value == to).Key;
        foreach (var row in new[] { new[] { OldOption("--open") }, new[] { OldOption("--style"), "b" }, new[] { OldOption("--uninstall") }, new[] { OldOption("--install") },
                                     new[] { OldOption("--install"), OldOption("--user"), @"D\n", "--pawnio", OldOption("--language"), "it" } }) {
            string reason = WrongOption(Legacy.Arguments(row));
            Check(reason == null, "old options still start: " + row.Length + " arguments" + (reason == null ? "" : " rejected: " + reason));
        }
        foreach (var kv in Legacy.Aliases)
            Check(WrongOption(new[] { kv.Key }) != null, "old option " + kv.Value + ": the program itself only knows the new name");
        Check(WrongOption(Legacy.Arguments(new[] { OldOption("--user"), "x" })) != null, "old install options without the old install command do not start");
        foreach (var row in new[] { "--invented", "--refresh", "--Install", "-install", "/install", "install", "screenshot.png", "--open --invented", "--screenshot a.png --invented", "--screenshot", "--screenshot --sample", "--result", "--style",
                                     "--sample", "--result a.txt", "--view extended", @"--user D\n", "--pawnio", "--close-button", "--live 0", "--open --api x", "--holder x", "--message x", "--open --holder x" })
            Check(WrongOption(row.Split(' ')) != null, "invalid options: \"" + row + "\" does not start");

        string result = Path.Combine(Path.GetTempPath(), "wm-options-" + Guid.NewGuid().ToString("N") + ".txt");
        var wrong = new[] { "--invented", "--result", result };
        if (WrongOption(wrong) == null) Check(false, "start with an invented option: the program would start");
        else {
            int previous = Environment.ExitCode;
            Main(wrong);
            Check(Environment.ExitCode == 2, "start with an invented option: error result");
            Check(File.Exists(result) && File.ReadAllText(result).StartsWith("ERROR options", StringComparison.Ordinal), "start with an invented option: the line in the result file");
            Environment.ExitCode = previous;
            try { File.Delete(result); } catch { }
        }
        return r;
    }

    public static SensorData TestSample() { return SampleData(); }
}
