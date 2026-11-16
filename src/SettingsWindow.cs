using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public partial class SettingsWindow : Form {
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint f);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr m, int type, out uint x, out uint y);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    public static Color PANEL { get { return Theme.Current.Panel; } }
    public static SettingsWindow Current;

    readonly Group gAppearance = new Group(), gStyle = new Group(), gShow = new Group(), gBar = new Group(), gTools = new Group(), gLanguage = new Group(), gColor = new Group(), gMode = new Group();
    readonly Box cCompact = new Box();
    readonly Box cDetached = new Box();
    readonly Radio rCapsules = new Radio(), rClassic = new Radio();
    readonly Box pDisks = new Box(), pNetwork = new Box(), pCpu = new Box(),
                     pGpu = new Box(), pRam = new Box(), pCommit = new Box();
    readonly Label lLogo = new Label { AutoEllipsis = true };
    readonly Box cShow = new Box();
    readonly Box cNotifications = new Box();
    readonly Box cStartup = new Box();
    readonly Button bRedo = Button(false), bFolder = Button(false);
    readonly Button bOk = Button(true), bCancel = Button(false), bApply = Button(false);
    readonly DropDown cLanguage = new DropDown(), cColor = new DropDown(), cMode = new DropDown();
    readonly Label lState = new Label { AutoEllipsis = true };
    readonly Label lVersion = new Label { TextAlign = ContentAlignment.MiddleLeft };
    readonly Caption lLicense = new Caption();
    readonly System.Windows.Forms.Timer wait = new System.Windows.Forms.Timer { Interval = 250 };
    float scale = 1f;
    bool checkedState;
    string checkTime = "";
    string style, pieces, language, color, mode;
    bool compact, detached, show, notifications, startup, commit;

    public static void Open() {
        if (Current == null) {
            var where = Cursor.Position;
            var f = new SettingsWindow(ScaleIn(where));
            var area = Screen.FromPoint(where).WorkingArea;
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(area.Left + (area.Width - f.Width) / 2, area.Top + (area.Height - f.Height) / 2);
            Current = f;
            f.Show();
        }
        Current.Activate();
        SetForegroundWindow(Current.Handle);
    }

    static float ScaleIn(Point p) {
        try {
            uint x, y;
            if (GetDpiForMonitor(MonitorFromPoint(new POINT { X = p.X, Y = p.Y }, 2), 0, out x, out y) == 0) return x / 96f;
        } catch { }
        return 1f;
    }

    public SettingsWindow(float scale) {
        this.scale = scale;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.None; KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        lVersion.Text = "WeightMonitor " + Program.VERSION;

        gAppearance.Controls.AddRange(new Control[] { cCompact, cDetached });
        gStyle.Controls.AddRange(new Control[] { rCapsules, rClassic });
        gShow.Controls.AddRange(new Control[] { pDisks, pNetwork, pCpu, pGpu, pRam, pCommit, lLogo });
        gBar.Controls.AddRange(new Control[] { cShow, cNotifications, cStartup });
        gTools.Controls.AddRange(new Control[] { bRedo, bFolder, lState });
        gLanguage.Controls.Add(cLanguage);
        foreach (var c in Language.CODES) cLanguage.Items.Add(Language.NameOf(c));
        cLanguage.SelectedIndex = Math.Max(0, Array.IndexOf(Language.CODES, Language.Code));
        gColor.Controls.Add(cColor);
        foreach (var c in Theme.COLORS) cColor.Items.Add(c);
        cColor.SelectedIndex = Math.Max(0, Array.IndexOf(Theme.COLORS, Theme.CurrentColor));
        cColor.DisplayName = i => Language.T("color." + Theme.COLORS[i]);
        cColor.Mark = i => Theme.Of(Theme.COLORS[i], Theme.Current.Light).AccentBar;
        gMode.Controls.Add(cMode);
        foreach (var c in Theme.LOOKS) cMode.Items.Add(c);
        cMode.SelectedIndex = Math.Max(0, Array.IndexOf(Theme.LOOKS, Theme.Appearance));
        cMode.DisplayName = i => Language.T("appearance." + Theme.LOOKS[i]);
        Controls.AddRange(new Control[] { gLanguage, gColor, gMode, gAppearance, gStyle, gShow, gBar, gTools, lVersion, lLicense, bOk, bCancel, bApply });
        AcceptButton = bOk; CancelButton = bCancel;

        cCompact.Checked = Program.Compact();
        cDetached.Checked = Program.Detached();
        style = Program.Bar != null ? Program.Bar.Style : Settings.Read("style", "b");
        (style == "a" ? rClassic : rCapsules).Checked = true;
        pDisks.Checked = Drawing.Show("disks"); pNetwork.Checked = Drawing.Show("network"); pCpu.Checked = Drawing.Show("cpu");
        pGpu.Checked = Drawing.Show("gpu"); pRam.Checked = Drawing.Show("ram"); pCommit.Checked = Drawing.Show("commit");
        cShow.Checked = Settings.Read("hidden", "no") != "yes";
        cNotifications.Checked = Notification.Choices();
        cStartup.Checked = UpdateChecker.Active();
        Remember();
        Colors();

        EventHandler changed = delegate { bApply.Enabled = Different(); };
        foreach (var r in new[] { rCapsules, rClassic }) r.CheckedChanged += changed;
        foreach (var c in new[] { cCompact, cDetached, pDisks, pNetwork, pCpu, pGpu, pRam, pCommit, cShow, cNotifications, cStartup }) c.CheckedChanged += changed;
        cLanguage.SelectedIndexChanged += changed;
        cColor.SelectedIndexChanged += changed;
        cMode.SelectedIndexChanged += changed;

        bOk.Click += delegate { Close(); Apply(); };
        bCancel.Click += delegate { Close(); };
        bApply.Click += delegate { Apply(); };
        bFolder.Click += delegate { try { Process.Start(Installer.Explorer(), "\"" + Settings.Folder + "\""); } catch { } };
        bRedo.Click += delegate { Program.RedoCheck(); Check(); };
        lLicense.Click += delegate { LicenseWindow.Open(this, scale); lLicense.Text = LicenseLabel.Caption(); };
        wait.Tick += delegate { Check(); };
        if (Program.Redoing) Check();

        Texts();
    }

    void Texts() {
        Text = Language.T("settings.title");
        gAppearance.Title = Language.T("common.appearance"); gStyle.Title = Language.T("settings.group.style"); gShow.Title = Language.T("settings.group.show");
        gBar.Title = Language.T("settings.group.taskbar"); gTools.Title = Language.T("settings.group.tools"); gLanguage.Title = Language.T("settings.group.language"); gColor.Title = Language.T("settings.group.color"); gMode.Title = Language.T("settings.group.appearance"); cColor.Invalidate(); cMode.Invalidate();
        cCompact.Texts(Language.T("common.compact"), Language.T("settings.note.compact"));
        cDetached.Texts(Language.T("common.detached"), Language.T("settings.note.detached"));
        rCapsules.Texts(Language.T("settings.capsules"), null); rClassic.Texts(Language.T("settings.classic"), null);
        pDisks.Texts(Language.T("common.disks"), null); pNetwork.Texts(Language.T("common.network"), null); pCpu.Texts(Language.T("overview.processor_title"), null);
        pGpu.Texts(Language.T("settings.graphics_card"), null); pRam.Texts(Language.T("settings.memory"), null); pCommit.Texts(Language.T("settings.commit"), null);
        lLogo.Text = Language.T("settings.logo");
        cShow.Texts(Language.T("settings.show_on_taskbar"), Language.T("settings.note.show_on_taskbar"));
        cNotifications.Texts(Language.T("common.notifications"), Language.T("settings.note.notifications"));
        cStartup.Texts(Language.T("settings.check_at_startup"), null);
        bRedo.Text = Language.T("settings.recheck_hardware"); bFolder.Text = Language.T("settings.open_data_folder");
        lLicense.Text = LicenseLabel.Caption();
        bOk.Text = Language.T("settings.ok"); bCancel.Text = Language.T("common.cancel"); bApply.Text = Language.T("settings.apply");
        if (!Program.Redoing) lState.Text = checkedState ? Language.T("settings.hardware_done", "time", checkTime) : Language.T("settings.hardware_automatic");
        else lState.Text = Language.T("settings.hardware_checking");
        Arrange(scale);
        foreach (Control c in Controls) c.Invalidate(true);
    }

    void Colors() {
        BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT;
        foreach (var g in new[] { gAppearance, gStyle, gShow, gBar, gTools, gLanguage, gColor, gMode }) {
            g.BackColor = Drawing.BACKGROUND;
            foreach (Control c in g.Controls) if (!(c is Button)) { c.BackColor = PANEL; c.ForeColor = c is Label ? Drawing.MUTED : Drawing.TEXT; }
        }
        lVersion.ForeColor = Drawing.MUTED;
        lLicense.BackColor = Drawing.BACKGROUND;
        cLanguage.Colors(); cColor.Colors(); cMode.Colors();
        foreach (var b in new[] { bRedo, bFolder, bCancel, bApply }) Colorize(b, false);
        Colorize(bOk, true);
        if (IsHandleCreated) Theme.Title(Handle);
        Invalidate(true);
    }

    string ChosenColor() { return Theme.COLORS[Math.Max(0, cColor.SelectedIndex)]; }
    string ChosenMode() { return Theme.LOOKS[Math.Max(0, cMode.SelectedIndex)]; }
    string ChoiceLanguage() { return Language.CODES[Math.Max(0, cLanguage.SelectedIndex)]; }
    string ChosenStyle() { return rClassic.Checked ? "a" : "b"; }
    string ChosenPieces() {
        var l = new System.Collections.Generic.List<string>();
        if (pDisks.Checked) l.Add("disks");
        if (pNetwork.Checked) l.Add("network");
        if (pCpu.Checked) l.Add("cpu");
        if (pGpu.Checked) l.Add("gpu");
        if (pRam.Checked) l.Add("ram");
        return l.Count == 0 ? "logo" : string.Join(",", l.ToArray());
    }
    void Remember() {
        pieces = ChosenPieces(); commit = pCommit.Checked; compact = cCompact.Checked; detached = cDetached.Checked; style = ChosenStyle(); show = cShow.Checked; notifications = cNotifications.Checked; startup = cStartup.Checked; language = ChoiceLanguage(); color = ChosenColor(); mode = ChosenMode();
        bApply.Enabled = false;
    }
    bool Different() { return ChosenPieces() != pieces || pCommit.Checked != commit || cCompact.Checked != compact || cDetached.Checked != detached || ChosenStyle() != style || cShow.Checked != show || cNotifications.Checked != notifications || cStartup.Checked != startup || ChoiceLanguage() != language || ChosenColor() != color || ChosenMode() != mode; }

    public void ChangedAppearance() {
        compact = Program.Compact(); detached = Program.Detached();
        cCompact.Checked = compact; cDetached.Checked = detached;
        bApply.Enabled = Different();
    }

    public void ChangedNotifications() {
        notifications = Notification.Active;
        cNotifications.Checked = notifications;
        bApply.Enabled = Different();
    }

    void Apply() {
        if (!Different()) return;
        bool hide = show && !cShow.Checked;
        if (ChosenStyle() != style) Settings.Write("style", ChosenStyle());
        if (ChosenPieces() != pieces) Settings.Write("bar_show", ChosenPieces());
        if (pCommit.Checked != commit) Settings.Write("bar_commit", pCommit.Checked ? "yes" : "no");
        if (cShow.Checked != show) Settings.Write("hidden", cShow.Checked ? "no" : "yes");
        if (cStartup.Checked != startup) Settings.Write("updates", cStartup.Checked ? "yes" : "no");
        bool newNotifications = cNotifications.Checked, changed = newNotifications != notifications;
        bool newCompact = cCompact.Checked, newDetached = cDetached.Checked, newLanguage = ChoiceLanguage() != language, newTheme = ChosenColor() != color || ChosenMode() != mode;
        if (newLanguage) Language.Choose(ChoiceLanguage());
        if (newTheme) Theme.Choose(ChosenColor(), ChosenMode());
        Remember();
        if (newLanguage) { Program.ChangedLanguage(); if (!IsDisposed) Texts(); }
        if (newTheme) { Program.ChangedTheme(); if (!IsDisposed) Colors(); }
        if (Program.Bar != null) Program.Bar.Style = style;
        Program.Appearance(newCompact, newDetached);
        if (changed) Notification.Set(newNotifications);
        if (hide) Program.Notice(Program.HiddenWhere());
    }

    void Check() {
        bool inProgress = Program.Redoing;
        bRedo.Enabled = !inProgress;
        if (inProgress) { lState.Text = Language.T("settings.hardware_checking"); wait.Start(); }
        else { wait.Stop(); checkedState = true; checkTime = DateTime.Now.ToString("HH:mm"); lState.Text = Language.T("settings.hardware_done", "time", checkTime); }
    }

    void Arrange(float k) {
        scale = k;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Font = new Font("Segoe UI", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
        foreach (var g in new[] { gAppearance, gStyle, gShow, gBar, gTools, gLanguage, gColor, gMode }) g.ApplyScale(k);

        Func<string, int> Wide = t => TextRenderer.MeasureText(t, Font).Width;
        int reserved = 2 * S(16) + 2 * S(14), bare = Font.Height + S(8), needs = S(480) - reserved;
        foreach (var c in new[] { cCompact, cDetached, cShow, cNotifications, cStartup }) needs = Math.Max(needs, bare + Wide(c.Text) + (c.Note != null ? Font.Height * 8 / 10 + Wide(c.Note) : 0));
        needs = Math.Max(needs, Wide(lLogo.Text) + S(4));
        Func<DropDown, int> Entry = e => { int m = 0; for (int i = 0; i < e.Items.Count; i++) m = Math.Max(m, Wide(e.DisplayName != null ? e.DisplayName(i) : (string)e.Items[i])); return m; };
        foreach (var t in new[] { Language.T("settings.group.language"), Language.T("settings.group.color"), Language.T("settings.group.appearance") }) needs = Math.Max(needs, 3 * (Wide(t) + S(32)));
        foreach (var e in new[] { cLanguage, cColor, cMode }) needs = Math.Max(needs, 3 * (Entry(e) + S(68)));
        needs = Math.Max(needs, 2 * Math.Max(Wide(bRedo.Text), Wide(bFolder.Text)) + S(48));
        needs = Math.Max(needs, Wide(Language.T("settings.hardware_done", "time", "00:00")) + S(4));
        foreach (var c in new[] { pDisks, pNetwork, pCpu, pGpu, pRam, pCommit }) needs = Math.Max(needs, 3 * (bare + Wide(c.Text)));
        int width = Math.Min(needs + reserved, S(760)), margin = S(16), inner = width - 2 * margin - 2 * S(14), x = S(14), row = S(26), start = S(38), end = S(10);
        int y = margin;
        Func<Group, int, int> Put = (g, h) => { g.Bounds = new Rectangle(margin, y, width - 2 * margin, h); y += h + S(10); return h; };

        int listsHeight = start + S(30) + end, thirdWidth = (width - 2 * margin - 2 * S(10)) / 3;
        gLanguage.Bounds = new Rectangle(margin, y, thirdWidth, listsHeight);
        gColor.Bounds = new Rectangle(margin + thirdWidth + S(10), y, thirdWidth, listsHeight);
        gMode.Bounds = new Rectangle(width - margin - thirdWidth, y, thirdWidth, listsHeight);
        y += listsHeight + S(10);
        foreach (var e in new[] { cLanguage, cColor, cMode }) { e.Font = Font; e.ItemHeight = S(24); e.Bounds = new Rectangle(x, start, thirdWidth - 2 * x, S(30)); }

        Put(gAppearance, start + 2 * row + end);
        cCompact.Bounds = new Rectangle(x, start, inner, row);
        cDetached.Bounds = new Rectangle(x, start + row, inner, row);

        Put(gStyle, start + row + end);
        int lStyle = Math.Max(S(120), bare + Math.Max(Wide(rCapsules.Text), Wide(rClassic.Text)));
        rCapsules.Bounds = new Rectangle(x, start, lStyle, row);
        rClassic.Bounds = new Rectangle(x + lStyle + S(10), start, lStyle, row);

        Put(gShow, start + 3 * row + end);
        int third = inner / 3;
        pDisks.Bounds = new Rectangle(x, start, third, row);
        pNetwork.Bounds = new Rectangle(x + third, start, third, row);
        pCommit.Bounds = new Rectangle(x + 2 * third, start, third, row);
        pCpu.Bounds = new Rectangle(x, start + row, third, row);
        pGpu.Bounds = new Rectangle(x + third, start + row, third, row);
        pRam.Bounds = new Rectangle(x + 2 * third, start + row, third, row);
        lLogo.Bounds = new Rectangle(x, start + 2 * row, inner, row);

        Put(gBar, start + 3 * row + end);
        cShow.Bounds = new Rectangle(x, start, inner, row);
        cNotifications.Bounds = new Rectangle(x, start + row, inner, row);
        cStartup.Bounds = new Rectangle(x, start + 2 * row, inner, row);

        int buttonHeight = S(32), stateHeight = S(22), space = S(8), half = (inner - space) / 2;
        Put(gTools, start + buttonHeight + S(4) + stateHeight + end);
        bRedo.Bounds = new Rectangle(x, start, half, buttonHeight);
        bFolder.Bounds = new Rectangle(x + half + space, start, half, buttonHeight);
        lState.Bounds = new Rectangle(x, start + buttonHeight + S(4), inner, stateHeight);

        y += S(4);
        int buttonWidth = Math.Max(S(92), S(20) + Math.Max(Wide(bApply.Text), Math.Max(Wide(bCancel.Text), Wide(bOk.Text)))), right = width - margin;
        bApply.Bounds = new Rectangle(right - buttonWidth, y, buttonWidth, buttonHeight);
        bCancel.Bounds = new Rectangle(right - 2 * buttonWidth - space, y, buttonWidth, buttonHeight);
        bOk.Bounds = new Rectangle(right - 3 * buttonWidth - 2 * space, y, buttonWidth, buttonHeight);
        lVersion.Bounds = new Rectangle(margin, y, bOk.Left - margin - space, buttonHeight);
        int licenseHeight = S(22);
        lLicense.Font = Font;
        lLicense.Bounds = new Rectangle(margin, y + buttonHeight + S(2), width - 2 * margin, licenseHeight);
        ClientSize = new Size(width, y + buttonHeight + S(2) + licenseHeight + margin);
    }

    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x02E0) {
            var r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            Location = new Point(r.L, r.T);
            Arrange((m.WParam.ToInt64() & 0xFFFF) / 96f);
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        Theme.Title(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        wait.Stop();
        if (Current == this) Current = null;
        base.OnFormClosed(e);
    }

    public static Button Button(bool primary) {
        var b = new Button { FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        Colorize(b, primary);
        return b;
    }
    public static void Colorize(Button b, bool primary) {
        b.BackColor = primary ? Drawing.ACCENT : Drawing.CAPSULE; b.ForeColor = primary ? Drawing.ON_ACCENT : Drawing.TEXT;
        b.FlatAppearance.BorderColor = Drawing.BORDER;
    }

    class DropDown : ComboBox {
        public Func<int, string> DisplayName;
        public Func<int, Color> Mark;
        public DropDown() { DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; DrawMode = DrawMode.OwnerDrawFixed; }
        public void Colors() { BackColor = Drawing.CAPSULE; ForeColor = Drawing.TEXT; Invalidate(); }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            if (e.Index < 0) return;
            bool choice = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var b = new SolidBrush(choice ? Drawing.ACCENT : Drawing.CAPSULE)) e.Graphics.FillRectangle(b, e.Bounds);
            var r = e.Bounds;
            if (Mark != null) {
                int side = r.Height * 5 / 12, dx = r.Height / 4;
                var q = e.Graphics.SmoothingMode; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Mark(e.Index))) e.Graphics.FillEllipse(b, r.X + dx, r.Y + (r.Height - side) / 2, side, side);
                using (var p = new Pen(choice ? Drawing.ON_ACCENT : Drawing.BORDER)) e.Graphics.DrawEllipse(p, r.X + dx, r.Y + (r.Height - side) / 2, side, side);
                e.Graphics.SmoothingMode = q;
                r = new Rectangle(r.X + side + dx + dx / 2, r.Y, r.Width - side - dx - dx / 2, r.Height);
            }
            TextRenderer.DrawText(e.Graphics, DisplayName != null ? DisplayName(e.Index) : (string)Items[e.Index], Font, r, choice ? Drawing.ON_ACCENT : Drawing.TEXT, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || !IsHandleCreated) return;
            using (var g = Graphics.FromHwnd(Handle)) {
                int w = SystemInformation.HorizontalScrollBarArrowWidth + 3;
                var r = new Rectangle(Width - w, 0, w, Height);
                using (var b = new SolidBrush(Drawing.CAPSULE)) g.FillRectangle(b, r);
                using (var p = new Pen(Drawing.BORDER)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                int half = Math.Max(3, w / 4), cx = r.X + w / 2, cy = Height / 2;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Drawing.MUTED)) g.FillPolygon(b, new[] { new Point(cx - half, cy - half / 2), new Point(cx + half, cy - half / 2), new Point(cx, cy + half - half / 2) });
            }
        }
    }

    class Caption : Button {
        public Caption() { DoubleBuffered = true; UseMnemonic = false; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e) {
            SafePaint.Run("settings", e.Graphics, ClientRectangle, BackColor, Drawing.MUTED, () => {
                e.Graphics.Clear(BackColor);
                bool above = ClientRectangle.Contains(PointToClient(Cursor.Position));
                using (var f = new Font(Font, FontStyle.Underline))
                    TextRenderer.DrawText(e.Graphics, Text, f, ClientRectangle, above || Focused ? Drawing.TEXT : Drawing.MUTED, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            });
        }
    }

    public class Group : Panel {
        string title = "";
        float k = 1f;
        Font bold;
        public string Title { set { title = value; Invalidate(); } }
        public Group() { DoubleBuffered = true; ResizeRedraw = true; }
        public void ApplyScale(float k) {
            this.k = k;
            if (bold != null) bold.Dispose();
            bold = new Font("Segoe UI Semibold", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e) { SafePaint.Run("settings", e.Graphics, ClientRectangle, PANEL, Drawing.MUTED, () => Outline(e.Graphics)); }
        void Outline(Graphics g) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = (int)(12 * k);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = new GraphicsPath()) using (var b = new SolidBrush(PANEL)) using (var pen = new Pen(Drawing.ROW)) {
                p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
                g.FillPath(b, p); g.DrawPath(pen, p);
            }
            if (bold != null) TextRenderer.DrawText(g, title, bold, new Point((int)(14 * k), (int)(10 * k)), Drawing.MUTED, TextFormatFlags.NoPadding);
        }
    }

    class Radio : RadioButton {
        string note;
        public Radio() { DoubleBuffered = true; }
        public void Texts(string text, string note) { Text = text; this.note = note; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) { SafePaint.Run("settings", e.Graphics, ClientRectangle, BackColor, Drawing.MUTED, () => Tick(e.Graphics, this, Checked, true, note, Focused && ShowFocusCues)); }
    }
    class Box : CheckBox {
        string note;
        public string Note { get { return note; } }
        public Box() { DoubleBuffered = true; }
        public void Texts(string text, string note) { Text = text; this.note = note; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) { SafePaint.Run("settings", e.Graphics, ClientRectangle, BackColor, Drawing.MUTED, () => Tick(e.Graphics, this, Checked, false, note, Focused && ShowFocusCues)); }
    }

    static void Tick(Graphics g, ButtonBase c, bool started, bool round, string note, bool focus) {
        const TextFormatFlags F = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        g.Clear(c.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int side = c.Font.Height * 8 / 10, y = (c.Height - side) / 2, x = 1;
        var q = new Rectangle(x, y, side, side);
        bool above = c.ClientRectangle.Contains(c.PointToClient(Cursor.Position));
        using (var pen = new Pen(started ? Drawing.ACCENT : above ? Drawing.TEXT : Drawing.MUTED, Math.Max(1f, side / 14f)))
        using (var full = new SolidBrush(Drawing.ACCENT))
        using (var white = new SolidBrush(Drawing.ON_ACCENT)) {
            if (round) {
                if (started) { g.FillEllipse(full, q); int m = side * 3 / 10; g.FillEllipse(white, q.X + m, q.Y + m, side - 2 * m, side - 2 * m); }
                else g.DrawEllipse(pen, q);
            } else {
                int d = Math.Max(2, side / 3);
                using (var p = new GraphicsPath()) {
                    p.AddArc(q.X, q.Y, d, d, 180, 90); p.AddArc(q.Right - d, q.Y, d, d, 270, 90);
                    p.AddArc(q.Right - d, q.Bottom - d, d, d, 0, 90); p.AddArc(q.X, q.Bottom - d, d, d, 90, 90); p.CloseFigure();
                    if (started) {
                        g.FillPath(full, p);
                        using (var v = new Pen(Drawing.ON_ACCENT, Math.Max(1.5f, side / 8f)))
                            g.DrawLines(v, new[] { new PointF(q.X + side * .24f, q.Y + side * .52f), new PointF(q.X + side * .43f, q.Y + side * .71f), new PointF(q.X + side * .77f, q.Y + side * .30f) });
                    } else g.DrawPath(pen, p);
                }
            }
        }
        int xt = x + side + side / 2, wt = TextRenderer.MeasureText(g, c.Text, c.Font, new Size(9999, 999), F).Width;
        TextRenderer.DrawText(g, c.Text, c.Font, new Rectangle(xt, 0, c.Width - xt, c.Height), c.Enabled ? c.ForeColor : Drawing.MUTED, F);
        if (note != null) TextRenderer.DrawText(g, note, c.Font, new Rectangle(xt + wt + side, 0, Math.Max(1, c.Width - xt - wt - side), c.Height), Drawing.MUTED, F | TextFormatFlags.EndEllipsis);
        if (focus) using (var f = new Pen(Drawing.MUTED) { DashStyle = DashStyle.Dot }) { g.SmoothingMode = SmoothingMode.None; g.DrawRectangle(f, xt - 2, (c.Height - c.Font.Height) / 2 - 1, wt + 4, c.Font.Height + 1); }
    }
}
