using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public partial class Flyout : Form {
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int dim);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

    public static Flyout Current;
    static string lastType = "";
    static DateTime closedAt = DateTime.MinValue;

    readonly string type;
    readonly Rectangle anchor;
    readonly float s;
    int tab, content, scroll;
    readonly ScrollList list;
    int taken = -1;
    bool fromBar;
    Lane lane;
    readonly DateTime openedAt = DateTime.Now;
    List<Zone> zones = new List<Zone>();
    readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer(), guard = new System.Windows.Forms.Timer();

    public static int TabOf(string type) {
        int n;
        return int.TryParse(Settings.Read("tab_" + type, ""), out n) && n >= 0 && n <= 2 ? n : 1;
    }
    public static void RememberTab(string type, int n) { Settings.Write("tab_" + type, n.ToString(CultureInfo.InvariantCulture)); }

    public static bool JustClosed(string type) { return Current == null && type == lastType && (DateTime.Now - closedAt).TotalMilliseconds < 400; }

    public static bool DoNotActivate(ref Message m) {
        if (m.Msg != 0x0021 || Current == null) return false;
        m.Result = new IntPtr(3);
        return true;
    }

    public static string TypeOf(string zone) { return zone == "logo" ? "all" : zone == "commit" ? "ram" : zone; }

    public static string Rule(string open, string clicked, string last, double msFromClosing) {
        if (open == clicked) return "close";
        if (open != null) return "switch";
        return clicked == last && msFromClosing < 400 ? "none" : "open";
    }
    public static bool ClosesOnMouse(bool left, bool right, bool inside, bool onBar) { return !inside && (right || (left && !onBar)); }
    public static bool ClosesOnFocusLoss(bool left, bool onBar, double msFromOpening) { return !(left && onBar) && msFromOpening >= 500; }

    static Func<MouseButtons> Buttons = () => Control.MouseButtons;
    static Func<Point> Where = () => Cursor.Position;
    static bool OnBar() { return Program.Bar != null && Program.Bar.Above(Where()); }

    public static void Open(string type, Rectangle anchor, float s) {
        var old = Current;
        string what = Rule(old != null ? old.type : null, type, lastType, (DateTime.Now - closedAt).TotalMilliseconds);
        if (what == "none") return;
        if (what == "close") { old.Close(); return; }
        var t = new Flyout(type, anchor, s);
        Current = t;
        t.Show();
        if (Environment.GetEnvironmentVariable("WM_TRACE") == "1") Program.Log("flyout " + type + " opened in " + t.Bounds);
        t.Activate();
        SetForegroundWindow(t.Handle);
        if (old != null && !old.IsDisposed) old.Close();
    }

    Flyout(string type, Rectangle anchor, float s) {
        this.type = type; this.anchor = anchor; this.s = s; tab = TabOf(type);
        if (type == "network") list = new ScrollList();
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; KeyPreview = true;
        StartPosition = FormStartPosition.Manual; DoubleBuffered = true; BackColor = Drawing.BACKGROUND;
        Recalculate();
        clock.Interval = 1000;
        clock.Tick += delegate { SafePaint.Guard("flyout tick", Recalculate); Invalidate(); };
        clock.Start();
        guard.Interval = 60;
        guard.Tick += delegate {
            if (taken >= 0) return;
            var t = Buttons();
            if (ClosesOnMouse((t & MouseButtons.Left) != 0, (t & MouseButtons.Right) != 0, Bounds.Contains(Where()), OnBar())) Close();
        };
        guard.Start();
    }

    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x8; cp.ClassStyle |= 0x20000; return cp; }
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        int round = 2;
        try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        Theme.Title(Handle);
    }

    public void ChangedTheme() { BackColor = Drawing.BACKGROUND; Theme.Title(Handle); Invalidate(); }

    void Recalculate() {
        int width = Drawing.FlyoutWidth(type, s), h;
        Func<ScrollList, int> Reading = el => { using (var b = new Bitmap(width, 4000)) using (var g = Graphics.FromImage(b)) return Drawing.Flyout(g, type, width, s, Program.Current(), Program.Network, tab, null, el); };
        h = Reading(null);
        var area = Screen.FromRectangle(anchor).WorkingArea;
        if (lane == null || lane.Area != area)
            lane = Lane.Choose(area, anchor, Program.Bar != null ? Program.Bar.Footprint() : Rectangle.Empty, width, h, (int)(10 * s));
        var newOne = lane.Reading(width, h);
        if (newOne != Bounds) Bounds = newOne;
        if (list != null) { list.Alt = newOne.Height; h = Reading(list); }
        content = h;
        scroll = ScrollOffset(h, newOne.Height, scroll, 0);
    }

    public static int ScrollOffset(int content, int visible, int current, int delta) {
        return Math.Max(0, Math.Min(Math.Max(0, content - visible), current + delta));
    }

    public static void Draw(Graphics g, string type, int width, int height, int content, int scroll, float s, SensorData d, Network network, int tab, List<Zone> zones, ScrollList list = null) {
        g.Clear(Drawing.BACKGROUND);
        if (content <= height) { Drawing.Flyout(g, type, width, s, d, network, tab, zones, list); return; }
        using (var whole = new Bitmap(width, content, System.Drawing.Imaging.PixelFormat.Format32bppRgb)) {
            Drawing.WithoutHalo(whole, gt => { gt.Clear(Drawing.BACKGROUND); Drawing.Flyout(gt, type, width, s, d, network, tab, zones, list); });
            g.DrawImageUnscaled(whole, 0, -scroll);
        }
        int max = content - height, band = (int)(28 * s);
        if (scroll < max)
            using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, height - band - 1, width, band + 1), Color.FromArgb(0, Drawing.BACKGROUND), Drawing.BACKGROUND, 90f))
                g.FillRectangle(b, 0, height - band, width, band);
        int lb = Math.Max((int)(24 * s), height * height / content), yb = (height - lb) * scroll / max;
        using (var b = new SolidBrush(Drawing.MUTED)) g.FillRectangle(b, width - (int)(6 * s), yb, Math.Max(2, (int)(3 * s)), lb);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var z = new List<Zone>();
        if (SafePaint.Run("flyout", e.Graphics, ClientRectangle, Drawing.BACKGROUND, Drawing.MUTED,
            () => Draw(e.Graphics, type, ClientSize.Width, ClientSize.Height, content, scroll, s, Program.Current(), Program.Network, tab, z, list)))
            zones = z;
    }

    protected override void OnMouseClick(MouseEventArgs e) {
        base.OnMouseClick(e);
        if (fromBar) return;
        var where = new Point(e.X, e.Y + scroll);
        var z = zones.FirstOrDefault(x => x.R.Contains(where));
        if (z == null) return;
        if (z.Name.StartsWith("tab:")) { tab = int.Parse(z.Name.Substring(4)); RememberTab(type, tab); lane = null; if (list != null) list.ScrollOffset = 0; Recalculate(); Invalidate(); }
        else if (z.Name.StartsWith("close:")) {
            var which = ProgramCloser.View(z.Name.Substring(6));
            Close();
            if (which != null) ProgramCloser.Ask(which, false, null);
        }
        else if (z.Name.StartsWith("open:")) {
            Open(z.Name.Substring(5), anchor, s);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        var where = new Point(e.X, e.Y + scroll);
        if (taken >= 0) { list.MoveKnob(where.Y - taken); Invalidate(); return; }
        Cursor = zones.Any(x => x.R.Contains(where)) ? Cursors.Hand : Cursors.Default;
        if (list != null && list.Started != list.Knob.Contains(where)) { list.Started = !list.Started; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        var where = new Point(e.X, e.Y + scroll);
        if (e.Button != MouseButtons.Left || !WithList || !list.Track.Contains(where)) return;
        fromBar = true;
        if (list.Knob.Contains(where)) taken = where.Y - list.Knob.Y;
        else { list.MoveBy(Math.Sign(where.Y - list.Knob.Y) * (list.Visible - list.Step)); Invalidate(); }
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); taken = -1; fromBar = false; }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); taken = -1; }

    bool WithList { get { return list != null && list.Scrolls; } }
    void MoveBy(int delta) {
        if (WithList) {
            int previous = list.ScrollOffset;
            list.MoveBy(delta);
            if (list.ScrollOffset != previous) { Invalidate(); return; }
        }
        int n = ScrollOffset(content, ClientSize.Height, scroll, delta);
        if (n != scroll) { scroll = n; Invalidate(); }
    }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); MoveBy(-Math.Sign(e.Delta) * (WithList ? list.Step * 3 : (int)(60 * s))); }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        int page = WithList ? list.Visible - list.Step : ClientSize.Height * 9 / 10, row = WithList ? list.Step : (int)(40 * s), all = 1 << 24;
        if (e.KeyCode == Keys.Escape) Close();
        else if (e.KeyCode == Keys.Down) MoveBy(row);
        else if (e.KeyCode == Keys.Up) MoveBy(-row);
        else if (e.KeyCode == Keys.PageDown) MoveBy(page);
        else if (e.KeyCode == Keys.PageUp) MoveBy(-page);
        else if (e.KeyCode == Keys.End) MoveBy(all);
        else if (e.KeyCode == Keys.Home) MoveBy(-all);
    }
    protected override void OnDeactivate(EventArgs e) {
        base.OnDeactivate(e);
        if (ClosesOnFocusLoss((Buttons() & MouseButtons.Left) != 0, OnBar(), (DateTime.Now - openedAt).TotalMilliseconds)) Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        clock.Stop(); guard.Stop();
        if (Current == this) Current = null;
        if (Environment.GetEnvironmentVariable("WM_TRACE") == "1") Program.Log("flyout " + type + " closed:" + Environment.StackTrace.Split('\n').Skip(4).Take(6).Aggregate("", (a, b) => a + " | " + b.Trim()));
        lastType = type; closedAt = DateTime.Now;
        base.OnFormClosed(e);
    }
}
