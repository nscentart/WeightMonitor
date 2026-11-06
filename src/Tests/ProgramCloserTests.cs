using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

public static partial class ProgramCloser {
    public static List<string> Test() {
        var r = new List<string>();
        Action<bool, string> Verify = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        string my = @"D:\Test\WeightMonitor.exe", win = Environment.GetFolderPath(Environment.SpecialFolder.Windows), pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        Action<string, string> No = (path, what) => { string reason = PathReason(path, my); Verify(reason != null, "excluded, " + what + ": " + (reason ?? "NOT EXCLUDED")); };
        No(null, "missing path");
        No("", "empty path");
        No("chrome.exe", "relative path");
        No(@"\\server\shared\program.exe", "network program");
        No(win + @"\notepad.exe", "Notepad");
        No(win.ToUpperInvariant() + @"\SYSTEM32\CALC.EXE", "uppercase");
        No(win + @"\System32\something.exe", "anything in System32");
        No(@"C:\Programs\..\Windows\System32\cmd.exe", "round trip with ..");
        No(@"\\?\" + win + @"\System32\cmd.exe", "round trip with \\\\?\\");
        No(@"C:\WINDOW~1\System32\cmd.exe", "shortened name");
        No(@"D:\Windows\System32\cmd.exe", "another Windows on another disk");
        No(pf + @"\Windows Defender\MsMpEng.exe", "Windows antivirus");
        No(pf + @"\Windows Media Player\wmplayer.exe", "Windows program in Program Files");
        No(pf + @"\WindowsApps\Microsoft.WindowsNotepad_11.0_x64\Notepad.exe", "new Notepad");
        No(@"D:\Games\svchost.exe", "pretends to be svchost");
        No(@"D:\Games\explorer.exe", "pretends to be File Explorer");
        No(@"D:\Games\lsass.exe", "pretends to be lsass");
        No(@"E:\Other copy\WeightMonitor.exe", "another copy of WeightMonitor");
        No(my, "WeightMonitor itself");
        No(my.ToUpperInvariant(), "WeightMonitor itself, in uppercase");
        foreach (string good in new[] { @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"D:\Games\game.exe", pf + @"\WindowsApps\SpotifyAB.SpotifyMusic_1.2_x64\Spotify.exe" })
            Verify(PathReason(good, my) == null, "allowed: " + Path.GetFileName(good));

        string chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        var a = new App { Key = chrome.ToLowerInvariant(), Name = "Google Chrome", Path = chrome };
        Verify(AppReason(a, my) == null, "allowed: Google Chrome");
        Verify(AppReason(null, my) != null, "excluded, no program");
        Verify(AppReason(new App { Key = "system", Name = "system" }, my) != null, "excluded, the system");
        Verify(AppReason(new App { Key = "name:something.exe", Name = "something" }, my) != null, "excluded, without a path");
        Verify(AppReason(new App { Key = "pid:1234", Name = "closed", PidClosed = 1234 }, my) != null, "excluded, already closed");
        Verify(AppReason(new App { Key = "x", Name = "Windows tool", Windows = true, Path = @"D:\x.exe" }, my) != null, "excluded, it declares itself Windows");

        Func<World> Fake = () => new World { MyPid = 500, MySession = 1, MyUser = "S-1-5-21-1", MyPath = my };
        Func<Done> Healthy = () => new Done { Pid = 777, Session = 1, Born = 1000, Path = chrome, User = "S-1-5-21-1" };
        var mf = Fake();
        Verify(ProcessReason(Healthy(), mf) == null, "allowed: a normal process of the user");
        Action<Func<Done, Done>, string> ExcludedProcess = (change, what) => { string reason = ProcessReason(change(Healthy()), mf); Verify(reason != null, "excluded, " + what + ": " + (reason ?? "NOT EXCLUDED")); };
        ExcludedProcess(f => null, "process gone");
        ExcludedProcess(f => { f.Pid = 0; return f; }, "number 0");
        ExcludedProcess(f => { f.Pid = 4; return f; }, "number 4");
        ExcludedProcess(f => { f.Pid = 500; return f; }, "it is me, with my number");
        ExcludedProcess(f => { f.Session = 0; return f; }, "services session");
        ExcludedProcess(f => { f.Session = 2; return f; }, "session of another user");
        ExcludedProcess(f => { f.User = "S-1-5-18"; return f; }, "SYSTEM user");
        ExcludedProcess(f => { f.User = "S-1-5-21-2"; return f; }, "another user");
        ExcludedProcess(f => { f.User = null; return f; }, "user that cannot be read");
        ExcludedProcess(f => { f.Born = 0; return f; }, "start time that cannot be read");
        ExcludedProcess(f => { f.Critical = true; return f; }, "critical process");
        ExcludedProcess(f => { f.Path = null; return f; }, "path that cannot be read");
        ExcludedProcess(f => { f.Path = win + @"\explorer.exe"; return f; }, "process in the Windows folder");

        Func<Done, List<IntPtr>, Func<IntPtr, int>, World> Scene = (now, closed, pidOf) => {
            var m = Fake();
            m.Windows = () => new List<Window> { new Window { H = (IntPtr)11, Pid = 777 }, new Window { H = (IntPtr)12, Pid = 777 } };
            m.Read = pid => pid == 777 ? now : null;
            m.WindowPid = pidOf;
            m.AskClosing = h => { closed.Add(h); return true; };
            return m;
        };
        Func<IntPtr, int> Its = h => 777;
        var closed1 = new List<IntPtr>();
        var scene = Scene(Healthy(), closed1, Its);
        var previous = Targets(a, scene);
        Verify(previous.Count == 1 && previous[0].Windows.Count == 2 && closed1.Count == 0, "looking for the targets closes nothing");
        Verify(Run(previous, scene) == 2 && closed1.Count == 2, "same program: the request goes to its two windows, only once");
        Action<Func<Done, Done>, Func<IntPtr, int>, string> Changed = (change, pidOf, what) => {
            var closed = new List<IntPtr>();
            var firstTargets = Targets(a, Scene(Healthy(), closed, Its));
            int n = Run(firstTargets, Scene(change(Healthy()), closed, pidOf));
            Verify(n == 0 && closed.Count == 0, what + ": no window closed");
        };
        Changed(f => { f.Born = 2000; return f; }, Its, "number reused with another start time");
        Changed(f => { f.Born = 2000; f.Path = @"D:\Work\important.exe"; return f; }, Its, "number reused by another program");
        Changed(f => { f.Path = @"D:\Work\important.exe"; return f; }, Its, "same time but another path");
        Changed(f => null, Its, "program already closed");
        Changed(f => { f.User = "S-1-5-18"; return f; }, Its, "now runs as system");
        Changed(f => f, h => 0, "window gone");
        Changed(f => f, h => 999, "window passed to another program");
        {
            var closed = new List<IntPtr>();
            var m = Scene(Healthy(), closed, Its);
            m.Read = pid => { var f = Healthy(); f.Path = win + @"\notepad.exe"; return f; };
            var w = new App { Key = (win + @"\notepad.exe").ToLowerInvariant(), Name = "Notepad", Path = win + @"\notepad.exe" };
            Verify(Targets(w, m).Count == 0, "Notepad is Windows: no button");
            m.Read = pid => { var f = Healthy(); f.Path = @"D:\Other\other.exe"; return f; };
            Verify(Targets(a, m).Count == 0, "a window of another program does not count");
            m.Windows = () => new List<Window>();
            m.Read = pid => Healthy();
            Verify(Targets(a, m).Count == 0, "no window: no button");
        }

        var names = new[] { "TerminateProcess", "NtTerminateProcess", "ZwTerminateProcess", "TerminateThread", "EndTask", "NtSuspendProcess", "DebugActiveProcess" };
        var all = Assembly.GetExecutingAssembly().GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)).ToList();
        var force = all.Where(x => x.GetCustomAttributes(typeof(DllImportAttribute), false).Cast<DllImportAttribute>().Any(d => names.Contains(d.EntryPoint ?? x.Name, StringComparer.OrdinalIgnoreCase)) || names.Contains(x.Name)).ToList();
        Verify(force.Count == 0, "no forced close declared in the program");
        int kill = 0, callCount = 0;
        foreach (var half in typeof(ProgramCloser).Assembly.GetTypes().Where(t => t == typeof(ProgramCloser) || t == typeof(ConfirmClose) || (t.DeclaringType != null && t.DeclaringType == typeof(ProgramCloser)))
                     .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))) {
            var body = half.GetMethodBody();
            if (body == null) continue;
            var the = body.GetILAsByteArray();
            for (int i = 0; i + 4 < the.Length; i++) {
                if (the[i] != 0x28 && the[i] != 0x6F) continue;
                try { var calledMethod = half.Module.ResolveMethod(BitConverter.ToInt32(the, i + 1)); callCount++; if (calledMethod.DeclaringType == typeof(Process) && calledMethod.Name == "Kill") kill++; } catch { }
            }
        }
        Verify(kill == 0 && callCount > 100, "Closing never calls Process.Kill, out of " + callCount + " calls checked");

        var mv = RealWorld();
        Verify(mv.MyUser != null && mv.MySession >= 0, "I know who I am: user and session read");
        Verify(ProcessReason(mv.Read(mv.MyPid), mv) != null, "the real world: WeightMonitor cannot close itself");
        var windows = mv.Windows();
        int allowed = 0, wrong = 0;
        foreach (var g in windows.GroupBy(x => x.Pid)) {
            var f = mv.Read(g.Key);
            if (f == null || ProcessReason(f, mv) != null) continue;
            allowed++;
            if (f.Path == null || f.Path.StartsWith(win, StringComparison.OrdinalIgnoreCase)) wrong++;
        }
        Verify(wrong == 0, "the real world: among " + windows.Count + " windows, " + allowed + " programs would have the button, none is Windows. None was touched");
        return r;
    }
}
