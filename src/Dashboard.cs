using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class Dashboard : Form {
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string c, string n);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    delegate bool OnWindow(IntPtr h, IntPtr given);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, OnWindow up, IntPtr given);
    [StructLayout(LayoutKind.Sequential)] struct CURSOR_INFO { public int cbSize, flags; public IntPtr cursor; public Point where; }
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSOR_INFO c);
    [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint f);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO mi);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
    delegate void WindowEvent(IntPtr hook, uint evt, IntPtr h, int obj, int child, uint thread, uint now);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WindowEvent f, uint pid, uint thread, uint flag);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT monitor, work; public int flags; }

    static readonly string[] SHELL = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow", "ApplicationFrameWindow",
        "ControlCenterWindow", "Xaml_WindowedPopupClass", "ForegroundStaging", "MultitaskingViewFrame", "TaskListThumbnailWnd" };

    readonly bool embedded;
    public string Style;
    string shape = "extended";
    readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
    readonly ToolTip tooltip = new ToolTip { ShowAlways = true, InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 15000 };
    List<Zone> zones = new List<Zone>();
    string belowZone = "", tipText = "";
    IntPtr attachedBar = IntPtr.Zero;
    Rectangle last = Rectangle.Empty;
    int barHeight = 48;
    Backdrop background;

    public Dashboard(bool embedded, string style) {
        this.embedded = embedded; Style = style;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
        Colors();
        Bounds = embedded ? new Rectangle(0, 0, 10, 10) : new Rectangle(200, 1300, 600, 48);
        ContextMenuStrip = Program.Menu();
        IntPtr front = IntPtr.Zero;
        ContextMenuStrip.Opening += delegate { front = GetForegroundWindow(); if (front != Handle) SetForegroundWindow(Handle); };
        ContextMenuStrip.Closed += delegate {
            BeginInvoke((MethodInvoker)delegate {
                if (GetForegroundWindow() == Handle && front != Handle && front != IntPtr.Zero && IsWindow(front)) SetForegroundWindow(front);
            });
        };
        clock.Interval = 1000;
        clock.Tick += delegate {
            Drawing.LightBar = embedded && !free && Program.LightTheme();
            if (Theme.FollowWindows()) Program.ChangedTheme();
            if (embedded) Attach(); else Width = Reading(Height, 4000);
            Invalidate();
            UpdateTooltip();
            if (Program.Icon != null)
                Program.Icon.Text = Settings.Read("hidden", "no") == "yes"
                    ? Reduced(Language.T("tip.off_taskbar", "settings", Language.T("menu.settings"))) : Reduced(Summary());
        };
        clock.Start();
    }

    void Colors() {
        BackColor = embedded ? Theme.Key : Theme.Bar.Backdrop;
        if (embedded) TransparencyKey = Theme.Key;
    }
    public void ChangedTheme() { Colors(); Invalidate(); Update(); }

    static string Reduced(string s) { return s.Length <= 63 ? s : s.Substring(0, 63); }
    static string Summary() {
        var d = Program.Current();
        return "CPU " + Drawing.FormatPercent(d.Cpu.Usage) + " " + Drawing.FormatTemp(d.Cpu.Temp, false) + " · GPU " + Drawing.FormatPercent(d.Gpu.Usage) + " "
             + Drawing.FormatTemp(d.Gpu.Temp, false) + " · RAM " + Drawing.FormatPercent(d.Ram.Usage);
    }

    public float Scale() { return Math.Max(1f, barHeight / 48f); }

    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000 | 0x8; return cp; }
    }

    WindowEvent onForeground;
    IntPtr hook = IntPtr.Zero;
    readonly System.Windows.Forms.Timer above = new System.Windows.Forms.Timer();
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        if (!embedded) return;
        background = new Backdrop();
        background.MouseMove += delegate(object o, MouseEventArgs ev) { OnMouseMove(ev); };
        background.MouseLeave += delegate { OnMouseLeave(EventArgs.Empty); };
        background.MouseClick += delegate(object o, MouseEventArgs ev) { OnMouseClick(ev); };
        background.MouseDown += delegate(object o, MouseEventArgs ev) { OnMouseDown(ev); };
        background.MouseUp += delegate(object o, MouseEventArgs ev) { OnMouseUp(ev); };
        background.ContextMenuStrip = ContextMenuStrip;
        Attach();
        onForeground = delegate { try { Attach(); CheckTooltip(); } catch { } };
        hook = SetWinEventHook(3, 3, IntPtr.Zero, onForeground, 0, 0, 0);
        above.Interval = 100;
        above.Tick += delegate { if (IsWindowVisible(Handle)) Level(); CheckTooltip(); if (CaptureTest) WriteCaptureTest(); };
        above.Start();
    }

    void Level() {
        if (attachedBar == IntPtr.Zero) return;
        IntPtr up = GetWindow(attachedBar, 3);
        if (up == background.Handle) {
            IntPtr h = GetWindow(up, 3);
            while (h != IntPtr.Zero && h != Handle && Our(h)) h = GetWindow(h, 3);
            if (h == Handle) return;
        }
        while (up == Handle || up == background.Handle) up = GetWindow(up, 3);
        const uint STOPPED = 0x0001 | 0x0002 | 0x0010;
        SetWindowPos(Handle, up == IntPtr.Zero ? new IntPtr(-1) : up, 0, 0, 0, 0, STOPPED);
        SetWindowPos(background.Handle, Handle, 0, 0, 0, 0, STOPPED);
    }

    static readonly int myPid = Process.GetCurrentProcess().Id;
    static bool Our(IntPtr h) { int pid; GetWindowThreadProcessId(h, out pid); return pid == myPid; }

    static string Class(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, sb.Capacity); return sb.ToString(); }

    string FullScreen(IntPtr bar) {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == bar || fg == Handle) return null;
        int pid; GetWindowThreadProcessId(fg, out pid);
        if (pid == myPid) return null;
        string cls = Class(fg);
        if (SHELL.Contains(cls)) return null;
        string process = ProcessName(pid);
        if (CAPTURE.Contains(process, StringComparer.OrdinalIgnoreCase)) return null;
        string who = CaptureTest ? ": window \"" + cls + "\" of process " + process : "";
        int state;
        if (SHQueryUserNotificationState(out state) == 0 && (state == 3 || state == 4))
            return "Windows says full screen (state " + state + ")" + who;
        RECT rf; if (!GetWindowRect(fg, out rf)) return null;
        var mi = new MONITORINFO(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        if (!GetMonitorInfo(MonitorFromWindow(fg, 2), ref mi)) return null;
        if (MonitorFromWindow(fg, 2) != MonitorFromWindow(bar == IntPtr.Zero ? Handle : bar, 2)) return null;
        bool covers = rf.L <= mi.monitor.L && rf.T <= mi.monitor.T && rf.R >= mi.monitor.R && rf.B >= mi.monitor.B;
        return covers ? "a window covers the whole screen" + who : null;
    }
    public static bool FrontFullScreen() {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || Our(fg) || SHELL.Contains(Class(fg))) return false;
        RECT rf; if (!GetWindowRect(fg, out rf)) return false;
        var mi = new MONITORINFO(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        if (!GetMonitorInfo(MonitorFromWindow(fg, 2), ref mi)) return false;
        return rf.L <= mi.monitor.L && rf.T <= mi.monitor.T && rf.R >= mi.monitor.R && rf.B >= mi.monitor.B;
    }

    static string ProcessName(int pid) { try { using (var p = Process.GetProcessById(pid)) return p.ProcessName; } catch { return pid.ToString(); } }
    string lastReason = "";
    DateTime lastReasonAt = DateTime.MinValue;
    static readonly string[] CAPTURE = { "SnippingTool", "ScreenSketch", "ScreenClippingHost", "ShareX", "Greenshot", "Lightshot" };

    int Reading(int height, int space) {
        int n = Program.Current().Letters().Count;
        using (var bmp = new Bitmap(1, 1)) using (var g = Graphics.FromImage(bmp)) {
            var tests = Program.Compact() ? new[] { "compact" } : new[] { "extended", "collected", "compact" };
            foreach (var f in tests) {
                int w = Drawing.Width(g, height, Style, f, n);
                shape = f;
                if (w <= space) return w;
            }
            return Drawing.Width(g, height, Style, shape, n);
        }
    }

    public void Attach() {
        if (!embedded) return;
        if (Program.Detached()) { PlaceDetached(); return; }
        IntPtr bar = FindWindow("Shell_TrayWnd", null);
        if (bar == IntPtr.Zero) return;
        free = false;
        if (bar != attachedBar) {
            SetWindowLongPtr(Handle, -8, bar); SetWindowLongPtr(background.Handle, -8, bar);
            attachedBar = bar; last = Rectangle.Empty;
        }
        RECT rb;
        if (!GetWindowRect(bar, out rb) || rb.B - rb.T <= 0) return;
        bool visibleBar = IsWindowVisible(bar) && rb.T < Screen.FromHandle(bar).Bounds.Bottom - 2;
        string reason = Settings.Read("hidden", "no") == "yes" ? "hidden by the settings"
                      : !visibleBar ? "Windows taskbar hidden" : FullScreen(bar);
        if (reason != null) { Hide(reason); return; }
        barHeight = rb.B - rb.T;
        int height = barHeight * 2 / 3;
        float share;
        if (!float.TryParse(Settings.Read("weather_reserve", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out share) || share < 0 || share > 50) share = 3.2f;
        int reserve = (int)(barHeight * share);
        int margin = Math.Max(4, barHeight / 10), right, space;
        IntPtr start = FindWindowEx(bar, IntPtr.Zero, "Start", null);
        RECT rs = new RECT();
        bool withStart = start != IntPtr.Zero && GetWindowRect(start, out rs) && rs.L - rb.L > reserve + barHeight * 2;
        if (withStart) { right = rs.L - margin; space = right - (rb.L + reserve); }
        else {
            IntPtr notification = FindWindowEx(bar, IntPtr.Zero, "TrayNotifyWnd", null);
            RECT rn; right = notification != IntPtr.Zero && GetWindowRect(notification, out rn) ? rn.L - margin : rb.R - barHeight * 6;
            space = barHeight * 12;
        }
        int width = Reading(height, space);
        var r = new Rectangle(right - width, rb.T + (barHeight - height) / 2, width, height);
        const uint SHOW = 0x0004 | 0x0010 | 0x0040;
        SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, SHOW);
        SetWindowPos(background.Handle, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, SHOW);
        HiddenBar = false;
        Level();
        if (r != last) { last = r; Invalidate(); }
    }

    public static volatile bool HiddenBar;

    void Hide(string reason) {
        HiddenBar = reason != "Windows taskbar hidden";
        if (IsWindowVisible(Handle)) { ShowWindow(Handle, 0); ShowWindow(background.Handle, 0); }
        if (reason != lastReason || (DateTime.Now - lastReasonAt).TotalMinutes >= 10) {
            lastReason = reason; lastReasonAt = DateTime.Now; Program.Log("hidden: " + reason);
        }
    }

    bool free, drag, dragged;
    Point MouseStart, barStart;

    void PlaceDetached() {
        if (drag) {
            if ((Control.MouseButtons & MouseButtons.Left) != 0) return;
            DragEnd();
        }
        const uint STOPPED = 0x0001 | 0x0002 | 0x0010;
        if (!free) {
            free = true;
            SetWindowLongPtr(Handle, -8, IntPtr.Zero); SetWindowLongPtr(background.Handle, -8, IntPtr.Zero);
            attachedBar = IntPtr.Zero; last = Rectangle.Empty;
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, STOPPED);
            SetWindowPos(background.Handle, Handle, 0, 0, 0, 0, STOPPED);
        }
        string reason = Settings.Read("hidden", "no") == "yes" ? "hidden by the settings" : FullScreen(IntPtr.Zero);
        if (reason != null) { Hide(reason); return; }
        IntPtr bar = FindWindow("Shell_TrayWnd", null);
        RECT rb;
        if (bar != IntPtr.Zero && GetWindowRect(bar, out rb) && rb.B - rb.T > 0) barHeight = rb.B - rb.T;
        int height = barHeight * 2 / 3;
        int width = Reading(height, Screen.FromHandle(Handle).Bounds.Width);
        var r = new Rectangle(FreePosition(width, height), new Size(width, height));
        const uint SHOW = 0x0004 | 0x0010 | 0x0040;
        SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, SHOW);
        SetWindowPos(background.Handle, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, SHOW);
        HiddenBar = false;
        if (r != last) { last = r; Invalidate(); }
    }

    public bool Above(Point p) { return IsHandleCreated && IsWindowVisible(Handle) && Bounds.Contains(p); }

    public Rectangle Footprint() { return free && IsHandleCreated && IsWindowVisible(Handle) ? Bounds : Rectangle.Empty; }

    Point FreePosition(int width, int height) {
        int x, y;
        if (!int.TryParse(Settings.Read("position_x", ""), out x) || !int.TryParse(Settings.Read("position_y", ""), out y)) {
            var area = Screen.FromHandle(Handle).WorkingArea;
            x = Left; y = area.Bottom - height - barHeight / 4;
            RememberPosition(x, y);
        }
        var screen = Screen.FromRectangle(new Rectangle(x, y, width, height)).Bounds;
        return new Point(Math.Max(screen.Left, Math.Min(x, screen.Right - width)), Math.Max(screen.Top, Math.Min(y, screen.Bottom - height)));
    }
    static void RememberPosition(int x, int y) {
        Settings.Write("position_x", x.ToString(CultureInfo.InvariantCulture));
        Settings.Write("position_y", y.ToString(CultureInfo.InvariantCulture));
    }

    void DragEnd() {
        drag = false;
        if (dragged) RememberPosition(Left, Top);
    }

    protected override void WndProc(ref Message m) {
        if (Flyout.DoNotActivate(ref m)) return;
        base.WndProc(ref m);
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        dragged = false;
        if (e.Button == MouseButtons.Right && Flyout.Current != null) Flyout.Current.Close();
        if (e.Button != MouseButtons.Left || !free) return;
        drag = true; MouseStart = Cursor.Position; barStart = Location;
    }
    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (drag && e.Button == MouseButtons.Left) DragEnd();
    }

    protected override void OnPaint(PaintEventArgs e) {
        var r = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        if (free && Style == "a") e.Graphics.Clear(Theme.Bar.Backdrop);
        zones = Drawing.Draw(e.Graphics, r, Program.Current(), Style, shape, 0);
        if (!zonesLogged && Environment.GetEnvironmentVariable("WM_TRACE") == "1") {
            zonesLogged = true;
            Program.Log("bar " + Bounds + " shape " + shape + " zones " + string.Join(" ", zones.Select(z => z.Name + "=" + RectangleToScreen(z.R))));
        }
    }
    bool zonesLogged;

    public Rectangle ZoneOnScreen(string name) {
        if (!IsHandleCreated || !IsWindowVisible(Handle)) return Rectangle.Empty;
        var z = zones.FirstOrDefault(x => x.Name == name);
        return z == null ? Rectangle.Empty : RectangleToScreen(z.R);
    }

    string ZoneIn(Point p) { var z = zones.FirstOrDefault(x => x.R.Contains(p)); return z == null ? "" : z.Name; }

    void UpdateTooltip() {
        if (belowZone.Length == 0 || Flyout.Current != null || CaptureInProgress()) return;
        string t = Drawing.Tooltip(belowZone, Program.Current(), Program.Network);
        if (t != tipText) { tipText = t; tooltip.SetToolTip(this, t); if (background != null) tooltip.SetToolTip(background, t); }
    }

    void CheckTooltip() {
        if (belowZone.Length == 0) return;
        if (!CaptureInProgress() && Our(WindowFromPoint(Cursor.Position))) return;
        StopTooltip();
    }
    void StopTooltip() {
        belowZone = ""; tipText = "";
        tooltip.SetToolTip(this, ""); tooltip.Hide(this);
        if (background != null) { tooltip.SetToolTip(background, ""); tooltip.Hide(background); }
    }
    bool CaptureInProgress() {
        int pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        return pid != 0 && pid != myPid && CAPTURE.Contains(ProcessName(pid), StringComparer.OrdinalIgnoreCase);
    }

    public bool CaptureTest;
    string lastTest = "";
    void WriteCaptureTest() {
        int our = 0; bool capture = CaptureInProgress();
        var who = new List<string>();
        foreach (int tenths in new[] { 0, 2, 5, 8, 10 }) {
            var p = new Point(Left + Math.Min(Width - 1, Width * tenths / 10), Top + Height / 2);
            IntPtr h = WindowFromPoint(p);
            int pid; GetWindowThreadProcessId(h, out pid);
            string name = ProcessName(pid);
            if (pid == myPid) our++;
            if (CAPTURE.Contains(name, StringComparer.OrdinalIgnoreCase)) capture = true;
            who.Add(p.X + "," + p.Y + "=" + name + "/" + Class(h));
        }
        if (!capture) { if (lastTest != "") { lastTest = ""; Program.Log("capture test: closed"); } return; }
        bool visible = false;
        EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr h, IntPtr d) {
            if (Class(h) == "tooltips_class32" && IsWindowVisible(h)) visible = true;
            return true;
        }, IntPtr.Zero);
        var c = new CURSOR_INFO(); c.cbSize = Marshal.SizeOf(typeof(CURSOR_INFO));
        string cursor = !GetCursorInfo(ref c) ? "?" : c.cursor == LoadCursor(IntPtr.Zero, new IntPtr(32512)) ? "arrow"
                       : c.cursor == LoadCursor(IntPtr.Zero, new IntPtr(32515)) ? "cross" : "other";
        string row = (free ? "detached" : "attached") + (IsWindowVisible(Handle) ? "" : ", bar hidden") + ", ours " + our + " points out of 5, tooltip "
                    + (visible ? "VISIBLE" : "hidden") + ", cursor " + cursor + ": " + string.Join(" ", who);
        if (row != lastTest) { lastTest = row; Program.Log("capture test: " + row); }
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (drag) {
            if ((Control.MouseButtons & MouseButtons.Left) == 0) DragEnd();
            else {
                var c = Cursor.Position;
                int dx = c.X - MouseStart.X, dy = c.Y - MouseStart.Y;
                if (!dragged && (Math.Abs(dx) >= SystemInformation.DragSize.Width || Math.Abs(dy) >= SystemInformation.DragSize.Height)) {
                    dragged = true; StopTooltip();
                    if (Flyout.Current != null) Flyout.Current.Close();
                }
                if (dragged) {
                    const uint MOVE = 0x0001 | 0x0004 | 0x0010;
                    SetWindowPos(Handle, IntPtr.Zero, barStart.X + dx, barStart.Y + dy, 0, 0, MOVE);
                    SetWindowPos(background.Handle, IntPtr.Zero, barStart.X + dx, barStart.Y + dy, 0, 0, MOVE);
                    return;
                }
            }
        }
        string z = ZoneIn(e.Location);
        if (z != belowZone) { belowZone = z; tipText = ""; tooltip.SetToolTip(this, ""); UpdateTooltip(); }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (!Bounds.Contains(Cursor.Position)) belowZone = ""; }

    protected override void OnMouseClick(MouseEventArgs e) {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || dragged) return;
        var z = zones.FirstOrDefault(x => x.R.Contains(e.Location));
        if (z == null) { if (Flyout.Current != null) Flyout.Current.Close(); return; }
        tooltip.Hide(this); if (background != null) tooltip.Hide(background);
        var screen = RectangleToScreen(z.R);
        Flyout.Open(Flyout.TypeOf(z.Name), screen, Scale());
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        clock.Stop(); above.Stop();
        if (hook != IntPtr.Zero) UnhookWinEvent(hook);
        if (background != null) background.Close();
        base.OnFormClosed(e);
    }
}
