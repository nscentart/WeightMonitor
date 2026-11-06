using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public static partial class Notification {
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr HungWindowFromGhostWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder text, int max);
    [StructLayout(LayoutKind.Sequential)] struct LAST_TOUCH { public int cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LAST_TOUCH u);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(int msg, ref DATA d);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct DATA {
        public int cbSize; public IntPtr hWnd; public int uID, uFlags, uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }
    const int NIM_MODIFY = 1, NIF_INFO = 0x10, NIIF_INFO = 1, NIIF_WARNING = 2, NIIF_NOSOUND = 0x10, NIIF_RESPECT_QUIET_TIME = 0x80;

    public static volatile bool Active;
    public static volatile Sentence Current;
    static readonly Health health = new Health();
    static readonly Notifications logic = new Notifications();
    static readonly Guard guard = new Guard();
    static readonly Stopwatch time = Stopwatch.StartNew();
    static long toleranceUpTo;
    static string lastFlyout = "all";
    static volatile App lastProgram;
    static DateTime lastError = DateTime.MinValue;
    static PowerModeChangedEventHandler onWake;

    public static bool Choices() { return Settings.Read("notifications", "yes") != "no"; }

    public static void Set(bool started) {
        Active = started;
        Settings.Write("notifications", started ? "yes" : "no");
        if (SettingsWindow.Current != null) SettingsWindow.Current.ChangedNotifications();
    }

    public static void Start() {
        Active = Choices();
        Interlocked.Exchange(ref toleranceUpTo, time.ElapsedMilliseconds + 60000);
        guard.Start();
        onWake = delegate(object o, PowerModeChangedEventArgs e) {
            if (e.Mode == PowerModes.Resume) Interlocked.Exchange(ref toleranceUpTo, time.ElapsedMilliseconds + 60000);
        };
        try { SystemEvents.PowerModeChanged += onWake; } catch (Exception ex) { Program.Log("notifications, wake-up: " + ex.Message); }
    }
    public static void Stop() { try { if (onWake != null) SystemEvents.PowerModeChanged -= onWake; } catch { } }

    static Sample Collect(SensorData d, Details x) {
        int q;
        return Sample.From(d, x.Current, x.Cpu.Rows(0, 0, out q), x.Gpu.Rows(0, 0, out q), x.Ram.Rows(0, 0, out q));
    }

    public static void Tick(SensorData d, Details x) {
        try {
            long stall = guard.Consumes();
            if (x == null) return;
            var c = Collect(d, x);
            if (time.ElapsedMilliseconds >= Interlocked.Read(ref toleranceUpTo)) c.Stall = stall / 1000.0;
            Current = health.Tick(c, time.ElapsedMilliseconds / 1000.0);
            if (!Active) return;
            c.NotResponding = Front(x);
            logic.RememberNames(c);
            foreach (var a in logic.Tick(c, time.ElapsedMilliseconds / 1000.0, Silence)) Show(a);
        } catch (Exception ex) {
            if ((DateTime.UtcNow - lastError).TotalMinutes >= 10) { lastError = DateTime.UtcNow; Program.Log("notifications: " + ex); }
        }
    }

    static Entry Front(Details x) {
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero) return null;
        var cls = new StringBuilder(32);
        GetClassName(h, cls, cls.Capacity);
        if (cls.ToString() == "Ghost") {
            try { h = HungWindowFromGhostWindow(h); } catch { return null; }
            if (h == IntPtr.Zero) return null;
        } else if (!IsHungAppWindow(h)) return null;
        int pid;
        GetWindowThreadProcessId(h, out pid);
        var a = x.AppByPid(pid);
        return a == null || Notifications.IsSelf(a) ? null : Notifications.EntryOf("", a, 100);
    }

    static string Silence() {
        if (Dashboard.HiddenBar) return "bar hidden";
        int state;
        if (SHQueryUserNotificationState(out state) == 0 && state != 5) return "Windows says do not disturb (state " + state + ")";
        if (Dashboard.FrontFullScreen()) return "full-screen program";
        var u = new LAST_TOUCH(); u.cbSize = Marshal.SizeOf(typeof(LAST_TOUCH));
        if (GetLastInputInfo(ref u) && unchecked((uint)Environment.TickCount - u.dwTime) > 10 * 60 * 1000) return "nobody at the PC for 10 minutes";
        return null;
    }

    static void Show(Notice a) {
        Program.Log("notification of level " + a.Level + " on " + a.Flyout);
        var bar = Program.Bar;
        if (bar == null || !bar.IsHandleCreated) return;
        bar.BeginInvoke((MethodInvoker)delegate { Balloon(a); });
    }

    static void Balloon(Notice a) {
        var icon = Program.Icon;
        if (icon == null) return;
        lastFlyout = a.Flyout;
        lastProgram = a.App;
        string click = Language.T("notification.click_to_close");
        if (a.App != null && a.Text.Length + 1 + click.Length <= 255 && ProgramCloser.Targets(a.App, ProgramCloser.RealWorld()).Count > 0) a.Text += " " + click;
        bool done = false;
        try {
            var f = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
            var n = typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
            var window = f == null ? null : f.GetValue(icon) as NativeWindow;
            if (window != null && n != null) {
                var d = new DATA { hWnd = window.Handle, uID = (int)n.GetValue(icon), uFlags = NIF_INFO, szTip = "", szInfo = a.Text, szInfoTitle = a.Title, uVersion = 10000,
                                   dwInfoFlags = (a.Level == 2 ? NIIF_WARNING : NIIF_INFO) | NIIF_NOSOUND | NIIF_RESPECT_QUIET_TIME };
                d.cbSize = Marshal.SizeOf(typeof(DATA));
                done = Shell_NotifyIcon(NIM_MODIFY, ref d);
            }
        } catch (Exception ex) { Program.Log("notification, direct route: " + ex.Message); }
        if (!done) {
            Program.Log("notification: using the normal Windows route, without the guarantee of silence");
            icon.ShowBalloonTip(10000, a.Title, a.Text, a.Level == 2 ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
    }

    public static void Clicked() {
        var which = lastProgram;
        lastProgram = null;
        if (which != null && ProgramCloser.Ask(which, true, OpenFlyout)) return;
        OpenFlyout();
    }

    static void OpenFlyout() {
        var bar = Program.Bar;
        if (bar == null) return;
        var where = bar.ZoneOnScreen(lastFlyout == "all" ? "logo" : lastFlyout);
        if (where.IsEmpty) where = bar.ZoneOnScreen("logo");
        if (where.IsEmpty) { var area = Screen.PrimaryScreen.WorkingArea; where = new System.Drawing.Rectangle(area.Right - 1, area.Bottom - 1, 1, 1); }
        Flyout.Open(lastFlyout, where, bar.Scale());
    }
}
