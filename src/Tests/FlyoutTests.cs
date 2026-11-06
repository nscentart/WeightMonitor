using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public partial class Flyout {
    public static List<string> TabsTest() {
        var r = new List<string>();
        string f = Path.GetTempFileName();
        try {
            Settings.UseFile(f);
            var types = new[] { "cpu", "gpu", "ram", "disks", "network" };
            foreach (var t in types) r.Add((TabOf(t) == 1 ? "ok " : "ERROR ") + "first time " + t + ": last minute");
            for (int i = 0; i < types.Length; i++) RememberTab(types[i], i % 3);
            Settings.Reread();
            for (int i = 0; i < types.Length; i++) r.Add((TabOf(types[i]) == i % 3 ? "ok " : "ERROR ") + "after the restart " + types[i] + ": tab " + TabOf(types[i]));
            Settings.Write("tab_cpu", "nove");
            Settings.Write("tab_gpu", "7");
            r.Add((TabOf("cpu") == 1 && TabOf("gpu") == 1 ? "ok " : "ERROR ") + "broken values in the file: last minute");
        } finally { try { File.Delete(f); } catch { } }
        return r;
    }

    public static List<string> ChangeTest() {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        Check(Rule("cpu", "gpu", "", 9999) == "switch", "cpu open, click on gpu: it switches");
        Check(Rule("cpu", "cpu", "", 9999) == "close", "cpu open, click on cpu: it closes");
        Check(Rule(null, "cpu", "", 9999) == "open", "nothing open, click on cpu: it opens");
        Check(Rule(null, "cpu", "cpu", 100) == "none" && Rule(null, "cpu", "cpu", 500) == "open", "the click that has just closed it does not reopen it, the next one does");
        Check(Rule(null, "gpu", "cpu", 100) == "open", "cpu just closed, click on gpu: it opens at once");
        Check(TypeOf("logo") == "all" && TypeOf("disks") == "disks", "the logo opens the overview");

        Check(!ClosesOnMouse(true, false, false, true), "left on the bar: the flyout waits for the click");
        Check(ClosesOnMouse(true, false, false, false), "left outside: it closes");
        Check(ClosesOnMouse(false, true, false, true), "right on the bar: it closes, the menu opens");
        Check(!ClosesOnMouse(true, false, true, false) && !ClosesOnMouse(false, false, false, false), "inside, or without buttons: it stays");
        Check(!ClosesOnFocusLoss(true, true, 9999), "loses focus when pressing on the bar: it waits for the click");
        Check(!ClosesOnFocusLoss(false, false, 100), "just opened, loses focus: it stays");
        Check(ClosesOnFocusLoss(false, true, 9999) && ClosesOnFocusLoss(true, false, 9999) && ClosesOnFocusLoss(false, false, 9999), "loses focus in every other case: it closes");

        var d = Program.TestSample();
        foreach (string style in new[] { "a", "b" }) foreach (string shape in new[] { "extended", "collected", "compact" })
            using (var b = new Bitmap(1400, 48)) using (var g = Graphics.FromImage(b)) {
                var types = Drawing.Draw(g, new Rectangle(0, 0, 1400, 48), d, style, shape, 0).Select(z => TypeOf(z.Name)).Distinct().ToList();
                int wrong = 0;
                foreach (var from in types) foreach (var a in types) {
                    string open = !ClosesOnMouse(true, false, false, true) && !ClosesOnFocusLoss(true, true, 9999) ? from : null;
                    if (Rule(open, a, "", 9999) != (from == a ? "close" : "switch")) wrong++;
                }
                Check(types.Count > 0 && wrong == 0, "style " + style + ", view " + shape + ": " + string.Join(" ", types) + ", a single click from each to each");
            }
        return r;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateDesktop(string name, IntPtr dev, IntPtr mode, uint flag, uint access, IntPtr security);
    [DllImport("user32.dll")] static extern bool SetThreadDesktop(IntPtr h);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

    public static List<string> RealChangeTest() {
        var r = new List<string>();
        var strand = new System.Threading.Thread(() => {
            IntPtr screen = CreateDesktop("WM-test-" + Guid.NewGuid().ToString("N"), IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
            if (screen == IntPtr.Zero || !SetThreadDesktop(screen)) { r.Add("ERROR the fake screen cannot be created"); return; }
            var buttons = Buttons; var where = Where; var bar = Program.Bar; var data = Program.DataTest;
            string file = Path.GetTempFileName();
            try { Settings.UseFile(file); foreach (string style in new[] { "a", "b" }) OnBarTest(r, style); }
            catch (Exception e) { r.Add("ERROR the real test stopped: " + e.GetType().Name + " " + e.Message); }
            finally {
                Buttons = buttons; Where = where; Program.Bar = bar; Program.DataTest = data;
                try { File.Delete(file); } catch { }
                CloseDesktop(screen);
            }
        });
        strand.SetApartmentState(System.Threading.ApartmentState.STA);
        strand.Start(); strand.Join();
        return r;
    }

    static void OnBarTest(List<string> r, string style) {
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + "style " + style + ", real windows: " + what);
        var pressed = MouseButtons.None; var mouse = new Point(0, 0);
        Buttons = () => pressed; Where = () => mouse;
        Program.DataTest = Program.TestSample();
        var area = Screen.PrimaryScreen.WorkingArea;
        using (var b = new Dashboard(false, style)) {
            Program.Bar = b;
            b.StartPosition = FormStartPosition.Manual;
            b.Show();
            b.Bounds = new Rectangle(area.Left + 10, area.Bottom - 48, Math.Min(area.Width - 20, 1400), 48);
            b.Refresh();
            Action<int> Runs = ms => { var end = DateTime.Now.AddMilliseconds(ms); do { Application.DoEvents(); System.Threading.Thread.Sleep(5); } while (DateTime.Now < end); };
            Func<string> Which = () => Current == null ? "none" : Current.type;
            Action Reset = () => { if (Current != null) Current.Close(); Runs(10); closedAt = DateTime.MinValue; lastType = ""; };
            Action<int, Point> Send = (msg, p) => { var c = b.PointToClient(p); SendMessage(b.Handle, msg, msg == 0x0201 || msg == 0x0204 ? new IntPtr(1) : IntPtr.Zero, new IntPtr((c.Y << 16) | (c.X & 0xFFFF))); };
            Action<Rectangle, Action> Click = (dest, meanwhile) => {
                mouse = new Point(dest.X + dest.Width / 2, dest.Y + dest.Height / 2);
                pressed = MouseButtons.Left; Send(0x0201, mouse);
                if (meanwhile != null) meanwhile();
                Runs(130);
                pressed = MouseButtons.None; Send(0x0202, mouse);
                Runs(30);
            };
            var types = new[] { "disks", "network", "cpu", "gpu", "ram" };
            var zones = types.ToDictionary(t => t, t => b.ZoneOnScreen(t));
            Check(zones.Values.All(z => !z.IsEmpty), "all the pieces are on the bar");
            if (zones.Values.Any(z => z.IsEmpty)) return;

            Click(zones["cpu"], null);
            Check(Which() == "cpu", "nothing open, click on CPU: it opens (" + Which() + ")");
            int wrong = 0, tried = 0;
            foreach (var from in types) foreach (var a in types) {
                if (from == a) continue;
                Reset(); Click(zones[from], null);
                var old = Current;
                Click(zones[a], null);
                tried++;
                if (Which() != a || old == null || !old.IsDisposed) { wrong++; r.Add("ERROR style " + style + ": from " + from + " a " + a + " stays " + Which()); }
            }
            Check(wrong == 0 && tried == 20, "from every piece to every other a single click, the new one open and the old one closed (" + tried + " cases)");
            Reset(); Click(zones["cpu"], null);
            Runs(520);
            Click(zones["gpu"], () => { if (Current != null) SendMessage(Current.Handle, 0x0006, IntPtr.Zero, IntPtr.Zero); });
            Check(Which() == "gpu", "the flyout loses focus while pressing on the bar: it switches anyway (" + Which() + ")");
            var newOne = Current;
            SendMessage(newOne.Handle, 0x0006, new IntPtr(1), IntPtr.Zero);
            SendMessage(newOne.Handle, 0x0006, IntPtr.Zero, IntPtr.Zero);
            Runs(10);
            Check(Current == newOne, "the new one loses focus just after opening: it stays open (" + Which() + ")");
            Click(zones["gpu"], null);
            Check(Which() == "none", "click on the same piece: it closes (" + Which() + ")");
            Click(zones["gpu"], null);
            Check(Which() == "none", "the same piece right after: the second of a double click does not reopen it (" + Which() + ")");
            Reset(); Click(zones["ram"], null);
            mouse = new Point(area.Left + 2, area.Top + 2); pressed = MouseButtons.Left; Runs(130); pressed = MouseButtons.None; Runs(20);
            Check(Which() == "none", "click outside: it closes (" + Which() + ")");
            Reset(); Click(zones["ram"], null);
            mouse = new Point(zones["cpu"].X + 2, zones["cpu"].Y + 2); pressed = MouseButtons.Right; Runs(130); pressed = MouseButtons.None; Runs(20);
            Check(Which() == "none", "right click on the bar: it closes (" + Which() + ")");
            Reset(); Click(zones["ram"], null);
            mouse = new Point(zones["cpu"].X + 2, zones["cpu"].Y + 2); pressed = MouseButtons.Left; Runs(130);
            mouse = new Point(area.Left + 2, area.Top + 2); Runs(130); pressed = MouseButtons.None; Runs(20);
            Check(Which() == "none", "pressed on the bar and dragged away: it closes (" + Which() + ")");
            Func<IntPtr, int, int> Question = (h, button) => SendMessage(h, 0x0021, h, new IntPtr((button << 16) | 1)).ToInt32();
            Action<Rectangle> WindowsClick = dest => {
                mouse = new Point(dest.X + dest.Width / 2, dest.Y + dest.Height / 2);
                var previous = Current;
                pressed = MouseButtons.None;
                if (Question(b.Handle, 0x0201) != 3 && previous != null) SendMessage(previous.Handle, 0x0006, IntPtr.Zero, IntPtr.Zero);
                if (previous == null || !previous.IsDisposed) { pressed = MouseButtons.Left; Send(0x0201, mouse); Runs(130); }
                pressed = MouseButtons.None; Send(0x0202, mouse); Runs(30);
            };
            var background = new Backdrop();
            Reset(); WindowsClick(zones["cpu"]);
            Check(Which() == "cpu", "Windows click, nothing open: it opens (" + Which() + ")");
            Runs(520);
            WindowsClick(zones["gpu"]);
            Check(Which() == "gpu", "Windows click, CPU open for a while, click on GPU: GPU opens (" + Which() + ")");
            Runs(520);
            WindowsClick(zones["gpu"]);
            Check(Which() == "none", "Windows click on the same piece: it closes (" + Which() + ")");
            Reset(); WindowsClick(zones["ram"]); Runs(520);
            Check(Question(background.Handle, 0x0201) == 3, "flyout open: not even the bottom of the bar steals its focus, so the clicks on its transparent points count");
            mouse = new Point(zones["cpu"].X + 2, zones["cpu"].Y + 2);
            Question(b.Handle, 0x0204); pressed = MouseButtons.Right; Send(0x0204, mouse); pressed = MouseButtons.None;
            Check(Which() == "none", "Windows right click on the bar: it closes at once, even if short (" + Which() + ")");
            Reset();
            Check(Question(b.Handle, 0x0201) != 3 && Question(background.Handle, 0x0201) != 3, "nothing open: the bar activates as usual, so the new flyout can take the foreground");
            Click(zones["ram"], null); Runs(520);
            mouse = new Point(area.Left + 2, area.Top + 2);
            SendMessage(Current.Handle, 0x0006, IntPtr.Zero, IntPtr.Zero); Runs(10);
            Check(Which() == "none", "loses focus without keys, like with Alt+Tab: it closes (" + Which() + ")");
            background.Dispose();
            Reset(); Click(zones["cpu"], null);
            int wide = b.Width; b.Width = wide + 40;
            var limit = DateTime.Now.AddMilliseconds(1500);
            while (b.Width != wide && DateTime.Now < limit) Runs(5);
            b.Width = wide + 40;
            b.Refresh();
            Click(new Rectangle(b.Right - 2, b.Top + 2, 1, 1), null);
            Check(Which() == "none", "click on the bar outside the pieces: it closes (" + Which() + ")");
            Reset();
            RealNetworkTest(Check, b, Runs, v => { pressed = v; }, v => { mouse = v; }, Click, Reset);
            b.Close();
        }
    }

    static void RealNetworkTest(Action<bool, string> Check, Dashboard b, Action<int> Runs, Action<MouseButtons> Pressed, Action<Point> Mouse, Action<Rectangle, Action> Click, Action Reset) {
        var realNetwork = Program.Network;
        var fake = new Network(); fake.SampleData(40); fake.Activate = true;
        Program.Network = fake;
        try {
            Click(b.ZoneOnScreen("network"), null);
            var t = Current;
            Check(t != null && t.type == "network", "Network flyout with 40 programs: it opens");
            if (t == null) return;
            t.Refresh();
            Action<int, Point, int> Send = (msg, p, w) => { var c = t.PointToClient(p); SendMessage(t.Handle, msg, new IntPtr(w), new IntPtr(((c.Y & 0xFFFF) << 16) | (c.X & 0xFFFF))); };
            var stopped = t.Bounds; bool move = false; var places = new List<Rectangle>();
            for (int i = 0; i < 3; i++) {
                var zs = t.zones.First(z => z.Name == "tab:" + i);
                var p = t.PointToScreen(new Point(zs.R.X + zs.R.Width / 2, zs.R.Y + zs.R.Height / 2));
                places.Add(zs.R);
                Send(0x0201, p, 1); Send(0x0202, p, 0);
                Runs(20); t.Recalculate(); t.Refresh();
                if (t.Bounds != stopped || t.tab != i) move = true;
            }
            Check(!move && places.Distinct().Count() == 3, "the three tabs can be clicked and the window does not move (" + stopped.Size + ")");
            Check(t.list.Scrolls && t.content <= t.ClientSize.Height, "only the list scrolls, not the flyout");
            var where = t.PointToScreen(new Point(t.ClientSize.Width / 2, t.list.Frame.Y + 10));
            SendMessage(t.Handle, 0x020A, new IntPtr(-120 << 16), new IntPtr(((where.Y & 0xFFFF) << 16) | (where.X & 0xFFFF)));
            Runs(10); t.Refresh();
            Check(t.list.ScrollOffset == t.list.Step * 3, "the wheel scrolls three rows (" + t.list.ScrollOffset + ")");
            var knob = t.list.Knob;
            var taken = t.PointToScreen(new Point(knob.X + knob.Width / 2, knob.Y + knob.Height / 2));
            int before = t.list.ScrollOffset;
            Mouse(taken); Pressed(MouseButtons.Left); Send(0x0201, taken, 1);
            var outside = new Point(t.Bounds.Right + 200, taken.Y + 60);
            Mouse(outside); Send(0x0200, outside, 1); Runs(130); t.Refresh();
            Check(Current == t && t.list.ScrollOffset > before, "the knob dragged with the mouse out of the flyout: it scrolls and the flyout stays open (" + t.list.ScrollOffset + ")");
            Pressed(MouseButtons.None); Send(0x0202, outside, 0); Runs(80);
            Check(Current == t, "released outside the flyout: it stays open");
            int now = t.list.ScrollOffset;
            t.Recalculate(); t.Refresh();
            Check(t.list.ScrollOffset == now && t.Bounds == stopped, "the update every second moves neither position nor window");
            int previousTab = t.tab;
            var other = t.zones.First(z => z.Name == "tab:" + (previousTab + 1) % 3);
            knob = t.list.Knob;
            taken = t.PointToScreen(new Point(knob.X + knob.Width / 2, knob.Y + knob.Height / 2));
            var onButton = t.PointToScreen(new Point(other.R.X + other.R.Width / 2, other.R.Y + other.R.Height / 2));
            Mouse(taken); Pressed(MouseButtons.Left); Send(0x0201, taken, 1);
            Mouse(onButton); Send(0x0200, onButton, 1); Pressed(MouseButtons.None); Send(0x0202, onButton, 0); Runs(20);
            Check(t.tab == previousTab, "the knob left over a tab does not click on it");
        } finally { Program.Network = realNetwork; Reset(); }
    }

    public static List<string> NetworkTest(SensorData d) {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + "network: " + what);
        Func<int, Network> With = n => { var x = new Network(); x.SampleData(n); x.Activate = true; return x; };
        var amount = new[] { 0, 1, 3, 6, 14, 15, 40 };
        foreach (float sc in new[] { 1f, 1.25f, 1.5f, 2f }) {
            int width = Drawing.FlyoutWidth("network", sc);
            var high = new List<int>();
            using (var b = new Bitmap(width, 4000)) using (var g = Graphics.FromImage(b)) {
                foreach (int n in amount) for (int sch = 0; sch < 3; sch++) high.Add(Drawing.Flyout(g, "network", width, sc, d, With(n), sch, null));
                high.Add(Drawing.Flyout(g, "network", width, sc, d, new Network(), 1, null));
            }
            var different = high.Distinct().ToList();
            Check(different.Count == 1, "scale " + sc + ", three tabs, from 0 to 40 programs and reading off: height always " + string.Join(", ", different));
        }
        using (var b = new Bitmap(470, 4000)) using (var g = Graphics.FromImage(b)) {
            Func<Network, ScrollList, List<Zone>> Turn = (x, el) => { var z = new List<Zone>(); Drawing.Flyout(g, "network", 470, 1f, d, x, 1, z, el); return z; };
            Func<List<Zone>, List<Zone>> Close = z => z.Where(q => q.Name.StartsWith("close:")).ToList();
            var el14 = new ScrollList(); Turn(With(14), el14);
            var el15 = new ScrollList(); Turn(With(15), el15);
            Check(!el14.Scrolls && el14.Track.IsEmpty && el15.Scrolls && !el15.Track.IsEmpty, "14 programs fit without a bar, 15 have the bar");
            Check(el15.Track.X >= 470 - 16, "the bar sits outside the number columns");

            bool previousScreenshot = ProgramCloser.Screenshot; ProgramCloser.Screenshot = true;
            var el16 = new ScrollList(); var many = With(40); var seen = new HashSet<string>(); int steps = 0; bool inside = true;
            do {
                foreach (var q in Close(Turn(many, el16))) { seen.Add(q.Name); if (!el16.Frame.Contains(q.R)) inside = false; }
                int previous = el16.ScrollOffset; el16.MoveBy(el16.Visible - el16.Step);
                if (el16.ScrollOffset == previous) break;
            } while (++steps < 50);
            Check(seen.Count(v => v.Contains("Programma ")) == 34, "40 programs: scrolling reaches the last one too, each with its Close (" + seen.Count(v => v.Contains("Programma ")) + " of 34 fake)");
            Check(inside, "no Close button leaves the frame of the list");
            int background = Drawing.Flyout(g, "network", 470, 1f, d, many, 1, null, el16);
            Check(el16.ScrollOffset == el16.Total - el16.Visible && el16.Frame.Bottom + 16 == background, "at the bottom the last row ends with the frame, and the margin remains below");
            Check(el16.Knob.Bottom == el16.Track.Bottom, "at the bottom of the list the knob touches the bottom");

            var up = new ScrollList(); var first = Close(Turn(many, up)); up.MoveBy(up.Step); var after = Close(Turn(many, up));
            var a = first[1]; var same = after.FirstOrDefault(q => q.Name == a.Name);
            Check(same != null && same.R.Y == a.R.Y - up.Step, "scrolled one row, the Close of that program moved up one row: the click takes the right one");
            Check(Close(Turn(many, up)).Select(q => q.R).SequenceEqual(after.Select(q => q.R)) && up.ScrollOffset == up.Step, "redrawing, as every second, does not move the position");

            ProgramCloser.Screenshot = previousScreenshot;
            var drop = new ScrollList(); Turn(many, drop); drop.MoveBy(1 << 24); Turn(With(6), drop);
            Check(drop.ScrollOffset == 0 && !drop.Scrolls, "the programs decrease: the position adjusts and the bar disappears");
            var half = new ScrollList(); Turn(many, half); half.MoveKnob(half.Track.Y + (half.Track.Height - half.Knob.Height) / 2);
            Check(Math.Abs(half.ScrollOffset - (half.Total - half.Visible) / 2) <= half.Step, "the knob halfway leads to the middle of the list");

            int whole = Drawing.Flyout(g, "network", 470, 1f, d, many, 1, null);
            var low = new ScrollList { Alt = whole - 100 }; int hb = Drawing.Flyout(g, "network", 470, 1f, d, many, 1, null, low);
            Check(hb == whole - 100 && low.Visible == low.Step * ScrollList.ROWS - 100, "with less space the list shortens, and the flyout does not scroll twice");
            var high = new ScrollList { Alt = whole + 500 };
            Check(Drawing.Flyout(g, "network", 470, 1f, d, many, 1, null, high) == whole, "with more space it does not grow");
            var little = new ScrollList { Alt = 100 };
            Check(Drawing.Flyout(g, "network", 470, 1f, d, many, 1, null, little) > 100 && little.Visible == little.Step * 3, "with very little space the list keeps three rows, and the flyout scrolls");
        }
        return r;
    }

    public static List<string> ScrollTest(SensorData d, Network network) {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        Check(ScrollOffset(500, 600, 0, 100) == 0, "it all fits: it does not scroll");
        Check(ScrollOffset(1000, 600, 0, 100) == 100 && ScrollOffset(1000, 600, 100, -500) == 0, "it scrolls and stops at the top");
        Check(ScrollOffset(1000, 600, 0, 9999) == 400, "it stops at the bottom");
        Check(ScrollOffset(1000, 900, 400, 0) == 100, "if the flyout gets longer it does not stay beyond the end");
        foreach (string type in new[] { "cpu", "gpu", "ram", "disks", "network", "all" }) {
            int width = Drawing.FlyoutWidth(type, 1f), h;
            using (var b = new Bitmap(width, 4000)) using (var g = Graphics.FromImage(b)) h = Drawing.Flyout(g, type, width, 1f, d, network, 1, null);
            int height = Math.Max(120, h - 150), max = ScrollOffset(h, height, 0, 99999), different = 0;
            using (var whole = new Bitmap(width, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb)) using (var low = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb)) {
                Drawing.WithoutHalo(whole, g => Draw(g, type, width, h, h, 0, 1f, d, network, 1, null));
                Drawing.WithoutHalo(low, g => Draw(g, type, width, height, h, max, 1f, d, network, 1, null));
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width - 12; x++)
                        if (whole.GetPixel(x, y + max) != low.GetPixel(x, y)) different++;
            }
            Check(max == h - height && different == 0, "flyout " + type + " tall " + h + ", short " + height + ": at the bottom the last row is visible (" + different + " different points)");
        }
        return r;
    }
}
