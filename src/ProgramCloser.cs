using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

public static partial class ProgramCloser {
    public class Done {
        public int Pid, Session;
        public long Born;
        public string Path, User;
        public bool Critical;
    }
    public class Window { public IntPtr H; public int Pid; }
    public class Target { public int Pid; public long Born; public string Path; public List<IntPtr> Windows = new List<IntPtr>(); }

    public class World {
        public int MyPid, MySession;
        public string MyUser, MyPath;
        public Func<List<Window>> Windows;
        public Func<int, Done> Read;
        public Func<IntPtr, int> WindowPid;
        public Func<IntPtr, bool> AskClosing;
    }

    static readonly string[] NEVER = { "WeightMonitor", "ApplicationFrameHost", "ShellHost", "TextInputHost", "LockApp", "LogonUI", "consent", "sppsvc", "wlanext", "dasHost",
        "SgrmBroker", "SearchApp", "smartscreen", "userinit", "CompPkgSrv", "PhoneExperienceHost", "SystemSettings", "SecHealthUI", "SecurityHealthSystray", "MpCmdRun", "MsSense" };

    public static string PathReason(string path, string myPath) {
        if (string.IsNullOrWhiteSpace(path)) return "unknown path";
        string p = path.Trim();
        if (p.StartsWith(@"\\?\")) p = p.Substring(4);
        if (p.Contains("~")) return "shortened path: it is unclear where it leads";
        if (p.StartsWith(@"\\") || p.Length < 3 || !char.IsLetter(p[0]) || p[1] != ':' || p[2] != '\\') return "unusual path";
        try { p = Path.GetFullPath(p); } catch { return "invalid path"; }
        string lowercase = p.ToLowerInvariant();
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\').ToLowerInvariant() + "\\";
        if (lowercase.StartsWith(windows) || lowercase.Substring(1).StartsWith(@":\windows\")) return "Windows folder";
        if (lowercase.Contains(@"\windows defender\") || lowercase.Contains(@"\windows security\") || lowercase.Contains(@"\systemapps\")) return "Windows protection components";
        if (lowercase.Contains(@"\windowsapps\microsoft.windows") || lowercase.Contains(@"\windowsapps\microsoftwindows.")) return "Windows app";
        foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            if (pf.Length > 0 && lowercase.StartsWith(pf.ToLowerInvariant().TrimEnd('\\') + @"\windows ")) return "Windows programs";
        string file = Path.GetFileNameWithoutExtension(p);
        if (NEVER.Contains(file, StringComparer.OrdinalIgnoreCase) || Notifications.IsWindowsName(file) || Explanations.All.Contains(file, StringComparer.OrdinalIgnoreCase))
            return "has the name of a Windows program";
        if (myPath != null && string.Equals(p, myPath, StringComparison.OrdinalIgnoreCase)) return "it is me";
        return null;
    }

    public static string AppReason(App a, string myPath) {
        if (a == null) return "no program";
        if (a.Key == null || a.Key == "system" || a.Key.StartsWith("name:") || a.Key.StartsWith("pid:")) return "program without a path";
        if (Notifications.OfWindows(a)) return "Windows program";
        return PathReason(a.Path, myPath);
    }

    public static string ProcessReason(Done f, World m) {
        if (f == null) return "it no longer exists";
        if (f.Pid <= 4) return "system process";
        if (f.Pid == m.MyPid) return "it is me";
        if (f.Born <= 0) return "start time unknown";
        if (f.Critical) return "Windows cannot run without it";
        if (f.Session != m.MySession) return "another session";
        if (f.User == null || m.MyUser == null || !string.Equals(f.User, m.MyUser, StringComparison.OrdinalIgnoreCase)) return "another user";
        return PathReason(f.Path, m.MyPath);
    }

    public static List<Target> Targets(App a, World m) {
        var r = new List<Target>();
        if (AppReason(a, m.MyPath) != null) return r;
        foreach (var group in m.Windows().GroupBy(x => x.Pid)) {
            var f = m.Read(group.Key);
            if (f == null || !string.Equals(f.Path, a.Path, StringComparison.OrdinalIgnoreCase) || ProcessReason(f, m) != null) continue;
            r.Add(new Target { Pid = f.Pid, Born = f.Born, Path = f.Path, Windows = group.Select(x => x.H).ToList() });
        }
        return r;
    }

    public static int Run(List<Target> targets, World m) {
        int sent = 0;
        foreach (var b in targets) {
            if (!StillThere(b, m)) continue;
            foreach (var h in b.Windows)
                if (m.WindowPid(h) == b.Pid && m.AskClosing(h)) sent++;
        }
        return sent;
    }

    public static bool StillThere(Target b, World m) {
        var f = m.Read(b.Pid);
        return f != null && f.Born == b.Born && string.Equals(f.Path, b.Path, StringComparison.OrdinalIgnoreCase) && ProcessReason(f, m) == null;
    }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] static extern IntPtr GetWindowLong64(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern IntPtr GetWindowLong32(IntPtr h, int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int dim);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherits, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder s, ref int n);
    [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h, out long born, out long end, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool ProcessIdToSessionId(int pid, out int session);
    [DllImport("kernel32.dll")] static extern bool IsProcessCritical(IntPtr h, out bool critical);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int cls, IntPtr data, int dim, out int needs);
    const uint WM_CLOSE = 0x0010;

    static List<Window> RealWindows() {
        var r = new List<Window>();
        EnumProc f = (h, l) => {
            if (!IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero || GetWindowTextLength(h) == 0) return true;
            long es = (IntPtr.Size == 8 ? GetWindowLong64(h, -20) : GetWindowLong32(h, -20)).ToInt64();
            if ((es & 0x80) != 0) return true;
            int hidden = 0;
            if (DwmGetWindowAttribute(h, 14, out hidden, 4) == 0 && hidden != 0) return true;
            int pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid > 4) r.Add(new Window { H = h, Pid = pid });
            return true;
        };
        EnumWindows(f, IntPtr.Zero);
        GC.KeepAlive(f);
        return r;
    }

    static string UserOf(IntPtr process) {
        IntPtr token;
        if (!OpenProcessToken(process, 0x0008, out token)) return null;
        IntPtr data = IntPtr.Zero;
        try {
            int needs;
            GetTokenInformation(token, 1, IntPtr.Zero, 0, out needs);
            if (needs <= 0) return null;
            data = Marshal.AllocHGlobal(needs);
            if (!GetTokenInformation(token, 1, data, needs, out needs)) return null;
            return new SecurityIdentifier(Marshal.ReadIntPtr(data)).Value;
        } catch { return null; }
        finally { if (data != IntPtr.Zero) Marshal.FreeHGlobal(data); CloseHandle(token); }
    }

    static Done ReadReal(int pid) {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return null;
        try {
            var f = new Done { Pid = pid };
            long end, kernel, user;
            if (GetProcessTimes(h, out f.Born, out end, out kernel, out user) && end != 0 && HasExited(h)) return null;
            var sb = new StringBuilder(1024); int n = sb.Capacity;
            if (QueryFullProcessImageName(h, 0, sb, ref n)) f.Path = sb.ToString();
            if (!ProcessIdToSessionId(pid, out f.Session)) f.Session = -1;
            bool critical;
            f.Critical = !IsProcessCritical(h, out critical) || critical;
            f.User = UserOf(h);
            return f;
        } finally { CloseHandle(h); }
    }
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    static bool HasExited(IntPtr h) { return WaitForSingleObject(h, 0) == 0; }

    static World real;
    public static World RealWorld() {
        if (real == null) {
            int session;
            ProcessIdToSessionId(Process.GetCurrentProcess().Id, out session);
            string user = null;
            try { using (var w = WindowsIdentity.GetCurrent()) user = w.User.Value; } catch { }
            real = new World {
                MyPid = Process.GetCurrentProcess().Id, MySession = session, MyUser = user, MyPath = Application.ExecutablePath,
                Windows = RealWindows, Read = ReadReal,
                WindowPid = h => { int pid; if (!IsWindow(h)) return 0; GetWindowThreadProcessId(h, out pid); return pid; },
                AskClosing = h => PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
            };
        }
        return real;
    }

    public static bool Screenshot;
    static readonly string[] SCREENSHOT = { "Google Chrome", "Thunderbird", "Claude", "Spotify" };
    static readonly Dictionary<string, App> seen = new Dictionary<string, App>();
    static HashSet<string> paths;
    static int read;

    public static bool Yes(App a) {
        if (Screenshot) return SCREENSHOT.Contains(Explanations.Name(a)) || a.Key.StartsWith("Programma ");
        if (a.Path == null) return false;
        if (paths == null || unchecked(Environment.TickCount - read) > 2000) {
            var m = RealWorld();
            var newOnes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try {
                foreach (var g in m.Windows().GroupBy(x => x.Pid)) {
                    var f = m.Read(g.Key);
                    if (f != null && ProcessReason(f, m) == null) newOnes.Add(f.Path);
                }
            } catch (Exception ex) { Program.Log("Close button: " + ex.Message); }
            paths = newOnes;
            read = Environment.TickCount;
        }
        return paths.Contains(a.Path) && AppReason(a, RealWorld().MyPath) == null;
    }
    public static void Remember(App a) { seen[a.Key] = a; }
    public static App View(string key) { App a; return seen.TryGetValue(key, out a) ? a : null; }

    public enum Choice { Cancel, Close, Details }
    public const int WAIT_MS = 15000;
    static bool inProgress;

    public static bool Ask(App a, bool withDetails, Action details) {
        if (inProgress) return true;
        var m = RealWorld();
        var previous = Targets(a, m);
        if (previous.Count == 0) return false;
        inProgress = true;
        try {
            string name = Explanations.Name(a);
            var s = Confirm(name, previous.Sum(b => b.Windows.Count), withDetails);
            if (s == Choice.Details && details != null) details();
            if (s != Choice.Close) return true;
            int sent = Run(previous, m);
            Program.Log("Close button: close request to " + sent + " windows");
            if (sent == 0) { Program.Notice(Language.T("close.already_closed", "name", name)); return true; }
            var after = new System.Windows.Forms.Timer { Interval = WAIT_MS };
            after.Tick += delegate {
                after.Stop(); after.Dispose();
                if (previous.Any(b => StillThere(b, m))) Program.Notice(Language.T("close.not_closed", "name", name));
            };
            after.Start();
            return true;
        } finally { inProgress = false; }
    }

    static Choice Confirm(string name, int windows, bool withDetails) {
        using (var front = new Form { TopMost = true, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Size = new Size(1, 1) })
        using (var c = new ConfirmClose(name, windows, withDetails)) {
            front.Show();
            c.TopMost = true;
            c.ShowDialog(front);
            return c.Chosen;
        }
    }
}
