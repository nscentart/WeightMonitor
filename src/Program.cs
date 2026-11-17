using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static partial class Program {
    public const string VERSION = "1.3";
    public static Sensors Sensors;
    public static Network Network;
    public static Dashboard Bar;
    public static NotifyIcon Icon;
    static volatile bool end, redo;
    static string LOG { get { return Path.Combine(Settings.Folder, "WeightMonitor.log"); } }
    static DateTime lastLog = DateTime.MinValue;

    public static bool Admin() {
        try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { return false; }
    }

    public static void Log(string row) {
        try {
            Directory.CreateDirectory(Settings.Folder);
            if (File.Exists(LOG) && new FileInfo(LOG).Length > 256 * 1024) { File.Copy(LOG, LOG + ".old", true); File.Delete(LOG); }
            File.AppendAllText(LOG, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " " + row + Environment.NewLine, Encoding.UTF8);
        } catch { }
    }

    public static bool Redoing { get { return redo; } }
    public static void RedoCheck() { redo = true; }

    public static SensorData DataTest;
    public static SensorData Current() { return Sensors == null ? (DataTest ?? new SensorData()) : Sensors.Current; }

    static void Work() {
        int turns = 0;
        while (!end) {
            var t0 = DateTime.UtcNow;
            try {
                if (redo) { try { Sensors.Inventory(); turns = 0; } finally { redo = false; } }
                Sensors.Read();
                var d = Sensors.Current;
                Network.Tick(d.Up, d.Down);
                Notification.Tick(d, Sensors.Details);
                turns++;
            } catch (Exception ex) {
                if ((DateTime.UtcNow - lastLog).TotalMinutes >= 10) { lastLog = DateTime.UtcNow; Log("reading: " + ex); }
            }
            int rest = 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds;
            if (rest > 20) Thread.Sleep(rest);
        }
    }

    public static ContextMenuStrip Menu() {
        var m = new ContextMenuStrip();
        m.Items.Add(new ToolStripMenuItem(Installer.Installed() ? "WeightMonitor " + VERSION : Language.T("menu.not_installed", "version", VERSION)) { Enabled = false });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(Language.T("menu.settings"), null, delegate { SettingsWindow.Open(); });
        var compact = new ToolStripMenuItem(Language.T("common.compact"));
        var detached = new ToolStripMenuItem(Language.T("common.detached"));
        compact.Click += delegate { Appearance(!Compact(), Detached()); };
        detached.Click += delegate { Appearance(Compact(), !Detached()); };
        var appearance = new ToolStripMenuItem(Language.T("common.appearance"));
        appearance.DropDownItems.Add(compact);
        appearance.DropDownItems.Add(new ToolStripSeparator());
        appearance.DropDownItems.Add(detached);
        appearance.DropDownOpening += delegate { compact.Checked = Compact(); detached.Checked = Detached(); };
        m.Items.Add(appearance);
        var notifications = new ToolStripMenuItem(Language.T("common.notifications"));
        notifications.Click += delegate { Notification.Set(!Notification.Active); };
        m.Items.Add(notifications);
        m.Opening += delegate { notifications.Checked = Notification.Active; };
        m.Items.Add(Language.T("menu.send_log"), null, delegate { ErrorReport.ShowReport(); });
        m.Items.Add(Language.T("menu.check_updates"), null, delegate { UpdateChecker.CheckNow(); });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(Language.T("common.close"), null, delegate { Application.Exit(); });
        return m;
    }

    public static bool Compact() { return Settings.Read("view", "automatic") == "compact"; }
    public static bool Detached() { return Settings.Read("detached", "no") == "yes"; }

    public static void Appearance(bool compact, bool detached) {
        if (compact != Compact()) Settings.Write("view", compact ? "compact" : "automatic");
        if (detached != Detached()) Settings.Write("detached", detached ? "yes" : "no");
        if (Bar != null) { Bar.Attach(); Bar.Invalidate(); Bar.Update(); }
        if (SettingsWindow.Current != null) SettingsWindow.Current.ChangedAppearance();
    }

    public static string HiddenWhere() { return Language.T("hidden.where", "settings", Language.T("menu.settings"), "show", Language.T("settings.show_on_taskbar")); }

    public static void ChangedLanguage() {
        if (Icon != null) {
            var old = Icon.ContextMenuStrip;
            Icon.ContextMenuStrip = Menu();
            if (old != null) old.Dispose();
        }
        if (Bar != null) Bar.Invalidate();
    }

    public static void ChangedTheme() {
        if (Bar != null) Bar.ChangedTheme();
        UpdateIcon();
        if (Flyout.Current != null) Flyout.Current.ChangedTheme();
        if (SettingsWindow.Current != null) SettingsWindow.Current.ColorsTest();
        if (LicenseWindow.Current != null) LicenseWindow.Current.ColorsTest();
    }

    static string iconColor = "blue";
    static void UpdateIcon() {
        if (Icon == null || iconColor == Theme.CurrentColor) return;
        try {
            var old = Icon.Icon;
            Icon.Icon = Drawing.LogoIcon(SystemInformation.SmallIconSize);
            iconColor = Theme.CurrentColor;
            if (old != null && old != SystemIcons.Application) old.Dispose();
        } catch (Exception ex) { Log("color icon: " + ex.Message); }
    }

    public static void Notice(string text) {
        using (var front = new Form { TopMost = true, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Size = new Size(1, 1) }) {
            front.Show();
            MessageBox.Show(front, text, "WeightMonitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    const string COPY_SINGLE = Legacy.MUTEX, OPEN_SETTINGS = Legacy.SETTINGS_EVENT;
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] static extern bool ChangeWindowMessageFilter(uint message, uint how);

    static bool RunningCopy() {
        try { Mutex m; if (!Mutex.TryOpenExisting(COPY_SINGLE, out m)) return false; m.Close(); return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    static void AskSettings() {
        try {
            AllowSetForegroundWindow(-1);
            using (var ev = EventWaitHandle.OpenExisting(OPEN_SETTINGS, EventWaitHandleRights.Modify | EventWaitHandleRights.Synchronize)) ev.Set();
        } catch (Exception ex) { Log("opening the settings of the other instance: " + ex.Message); }
    }
    static void ListenSettings() {
        try {
            var rules = new EventWaitHandleSecurity();
            rules.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                EventWaitHandleRights.Modify | EventWaitHandleRights.Synchronize, AccessControlType.Allow));
            bool created;
            var ev = new EventWaitHandle(false, EventResetMode.AutoReset, OPEN_SETTINGS, out created, rules);
            new Thread(delegate() {
                while (true) {
                    ev.WaitOne();
                    try { if (Bar.IsHandleCreated) Bar.BeginInvoke((MethodInvoker)SettingsWindow.Open); } catch { }
                }
            }) { IsBackground = true, Name = "settings" }.Start();
        } catch (Exception ex) { Log("settings signal: " + ex.Message); }
    }

    static string Arg(string[] args, string name, string defaultValue) {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return defaultValue;
    }

    static readonly string[] WITH_VALUE = { "--inventory", "--screenshot", "--screenshot-report", "--screenshot-update", "--screenshot-settings", "--screenshot-menu", "--screenshot-confirm", "--screenshot-language", "--screenshot-pawnio", "--screenshot-welcome", "--screenshot-license", "--test-shortcut",
        "--data", "--holder", "--message", "--style", "--language", "--color", "--appearance", "--result", "--api", "--live", "--signature", "--height", "--view", "--flyout", "--tab", "--screen-height", "--scroll", "--background", "--scale", "--windows", "--user", "--sentence", "--show", "--programs" };
    static readonly string[] WITHOUT_VALUE = { "--test-updates", "--test-notice", "--test-notifications", "--test-security", "--test-flyouts", "--test-themes", "--test-close", "--test-languages", "--test-license", "--test-etw", "--test-disks", "--test-paint", "--test-migration", "--install", "--uninstall", "--preview",
        "--open", "--portable", "--test-capture", "--sample", "--close-button", "--details", "--without-pawnio", "--pawnio" };
    static readonly string[] COMMANDS = { "--inventory", "--screenshot", "--screenshot-report", "--screenshot-update", "--screenshot-settings", "--screenshot-menu", "--screenshot-confirm", "--screenshot-language", "--screenshot-pawnio", "--screenshot-welcome", "--screenshot-license", "--test-shortcut",
        "--test-updates", "--test-notice", "--test-notifications", "--test-security", "--test-flyouts", "--test-themes", "--test-close", "--test-languages", "--test-license", "--test-etw", "--test-disks", "--test-paint", "--test-migration", "--install", "--uninstall", "--preview" };
    static readonly string[] STARTUP = { "--style", "--language", "--color", "--appearance", "--open", "--portable", "--test-capture" };

    static string WrongOption(string[] args) {
        bool command = false;
        string alone = null;
        for (int i = 0; i < args.Length; i++) {
            string a = args[i];
            if (WITH_VALUE.Contains(a)) {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) return a + " needs a value";
                i++;
            } else if (!WITHOUT_VALUE.Contains(a)) return "\"" + a + "\" is not an option";
            if (COMMANDS.Contains(a)) command = true;
            else if (!STARTUP.Contains(a) && alone == null) alone = a;
        }
        return command || alone == null ? null : alone + " alone does nothing";
    }

    static void Reject(string[] args, string reason) {
        Environment.ExitCode = 2;
        string file = Arg(args, "--result", null);
        if (file == null || file.StartsWith("--", StringComparison.Ordinal)) return;
        try { File.WriteAllLines(file, new[] { "ERROR options: " + reason + ". The program did not start." }, Encoding.UTF8); } catch { }
    }

    public static bool LightTheme() { return Theme.WindowsEntry(Theme.SYSTEM_ENTRY); }

    static readonly Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>();
    static Assembly Resolve(object o, ResolveEventArgs e) {
        string name = new AssemblyName(e.Name).Name;
        lock (loaded) {
            Assembly a;
            if (loaded.TryGetValue(name, out a)) return a;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib." + name + ".dll")) {
                if (s == null) return null;
                var b = new byte[s.Length]; int read = 0;
                while (read < b.Length) { int n = s.Read(b, read, b.Length - read); if (n <= 0) break; read += n; }
                a = Assembly.Load(b);
            }
            loaded[name] = a;
            return a;
        }
    }

    public static Stream Resource(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }

    [STAThread]
    static void Main(string[] args) {
        args = Legacy.Arguments(args);
        string reason = WrongOption(args);
        if (reason != null) { Reject(args, reason); return; }
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        Startup(args);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Startup(string[] args) {
        Language.Begin(Arg(args, "--language", null));
        Theme.Begin(Arg(args, "--color", null), Arg(args, "--appearance", null));
        if (args.Any(a => a.StartsWith("--screenshot", StringComparison.Ordinal))) LicenseLabel.SampleData(Arg(args, "--holder", null));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try {
            if (Arg(args, "--inventory", null) != null) {
                var s = new Sensors(); s.Read(); s.WriteInventory(Arg(args, "--inventory", null), new string[0]); s.Close(); return;
            }
            string style = Settings.Read("style", Arg(args, "--style", "b")) == "a" ? "a" : "b";
            if (Arg(args, "--sentence", null) != null) {
                var sa = new Health();
                var ca = new Sample { Cpu = 96, Gpu = 9, Ram = 42 };
                ca.Add("cpu", new App { Key = "chrome", Name = "Google Chrome", Path = @"C:\Program Files\Google\Chrome\Application\chrome.exe" }, 71);
                for (int t = 0; t < 40; t++) Notification.Current = sa.Tick(ca, 1000 + t);
                if (Arg(args, "--sentence", null) == "long") Notification.Current = new Sentence { Struggling = true, Flyout = "disks",
                    Text = Language.T("status.data_windows", "disk", "E:"), Subtext = Language.T("status.disk_busy", "disk", "E:", "percent", 99, "ending", Language.T("status.data_close_it")) };
            } else if (args.Contains("--sample")) {
                var sa = new Health();
                var ca = new Sample { Cpu = 12, Gpu = 9, Ram = 42 };
                ca.AddDisks(SampleData());
                for (int t = 0; t < 40; t++) Notification.Current = sa.Tick(ca, 1000 + t);
            }
            Drawing.ScreenshotTest = Arg(args, "--show", null);
            ProgramCloser.Screenshot = args.Contains("--close-button");
            string screenshot = Arg(args, "--screenshot", null);
            if (screenshot != null) {
                int height = int.Parse(Arg(args, "--height", "48"), CultureInfo.InvariantCulture);
                var network = new Network();
                SensorData d;
                if (args.Contains("--sample")) { d = SampleData(); network.SampleData(int.Parse(Arg(args, "--programs", "6"))); network.Activate = true; }
                else { var s = new Sensors(); s.Read(); Thread.Sleep(1100); s.Read(); d = s.Current; s.Close(); }
                Screenshot(screenshot, Arg(args, "--style", style), Arg(args, "--view", "extended"), Arg(args, "--flyout", null), int.Parse(Arg(args, "--tab", "1")), height, d, network,
                     int.Parse(Arg(args, "--screen-height", "0")), int.Parse(Arg(args, "--scroll", "0")), Arg(args, "--background", "") == "light");
                return;
            }

            if (Arg(args, "--screenshot-report", null) != null) {
                Settings.UseFile(Path.Combine(Path.GetTempPath(), "wm-shot-" + Guid.NewGuid().ToString("N"), "settings.ini"));
                using (var f = new ErrorReport(ErrorReport.BuildText(new[] { "CPU: AMD Ryzen 9 9950X3D", "GPU: NVIDIA GeForce RTX 5090 (dedicata)" }, new string[0]))) {
                    f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000); f.Show();
                    using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(Arg(args, "--screenshot-report", null), ImageFormat.Png); }
                }
                return;
            }
            if (Arg(args, "--screenshot-update", null) != null) {
                string h1 = string.Concat(Enumerable.Repeat("0123456789abcdef", 4)), h2 = string.Concat(Enumerable.Repeat("fedcba9876543210", 4)), testCase = Arg(args, "--sentence", "");
                string sample = "**Novità rispetto alla 1.2**\n<details>\n<summary>Dettagli</summary>\n\n- Un esempio di novità.\n- Un altro esempio.\n</details>";
                var n = new UpdateInfo { Label = "1.3", Note = sample + (testCase == "no" ? "" : "\n\nSHA-256: " + (testCase == "different" ? h2 : h1)), Digest = testCase == "no" ? null : "sha256:" + h1 };
                using (var f = new UpdateWindow(n)) {
                    f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000); f.Show();
                    using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(Arg(args, "--screenshot-update", null), ImageFormat.Png); }
                }
                return;
            }
            if (Arg(args, "--test-shortcut", null) != null) { Installer.Shortcut(Arg(args, "--test-shortcut", null)); return; }
            if (Arg(args, "--screenshot-settings", null) != null) {
                using (var f = new SettingsWindow(float.Parse(Arg(args, "--scale", "1"), CultureInfo.InvariantCulture))) {
                    f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000); f.Show();
                    RealScreenshot(f, Arg(args, "--screenshot-settings", null));
                }
                return;
            }
            if (Arg(args, "--screenshot-license", null) != null) {
                using (var f = new LicenseWindow(float.Parse(Arg(args, "--scale", "1"), CultureInfo.InvariantCulture))) {
                    if (Arg(args, "--message", null) != null) { bool problem; string sentence = LicenseLabel.ScreenshotSentence(Arg(args, "--message", null), out problem); f.Sentence(sentence, problem); }
                    f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000); f.Show();
                    RealScreenshot(f, Arg(args, "--screenshot-license", null));
                }
                return;
            }
            if (args.Contains("--test-updates")) { UpdateChecker.Test(Arg(args, "--api", null), Arg(args, "--result", "result.txt")); return; }
            if (args.Contains("--test-notice")) { Notice(HiddenWhere()); return; }
            if (Arg(args, "--screenshot-menu", null) != null) {
                using (var m = Menu()) {
                    foreach (ToolStripItem v in m.Items) if (v.Text == Language.T("common.notifications")) ((ToolStripMenuItem)v).Checked = true;
                    m.CreateControl();
                    m.PerformLayout();
                    using (var bmp = new Bitmap(m.Width, m.Height)) { m.DrawToBitmap(bmp, new Rectangle(0, 0, m.Width, m.Height)); bmp.Save(Arg(args, "--screenshot-menu", null), ImageFormat.Png); }
                }
                return;
            }
            if (args.Contains("--test-notifications")) { WriteTest(Arg(args, "--result", null), "notifications test", Notification.Test(int.Parse(Arg(args, "--live", "0")))); return; }
            if (args.Contains("--test-security")) { WriteTest(Arg(args, "--result", null), "security test", SecurityTest(Arg(args, "--signature", null))); return; }
            if (args.Contains("--test-flyouts")) { WriteTest(Arg(args, "--result", null), "flyouts test", Lane.Test().Concat(Flyout.NetworkTest(SampleData())).Concat(Flyout.ChangeTest()).Concat(Flyout.RealChangeTest()).Concat(Flyout.TabsTest()).Concat(SampleScrollTest()).Concat(Drawing.LightBarTest(SampleData())).Concat(Drawing.ChoiceTest(SampleData())).ToList()); return; }
            if (args.Contains("--test-themes")) { WriteTest(Arg(args, "--result", null), "themes test", Theme.Test()); return; }
            if (args.Contains("--test-close")) { WriteTest(Arg(args, "--result", null), "close test", ProgramCloser.Test()); return; }
            if (Arg(args, "--screenshot-confirm", null) != null) {
                using (var b = new ConfirmClose("Google Chrome", int.Parse(Arg(args, "--windows", "1")), args.Contains("--details"))) {
                    b.StartPosition = FormStartPosition.Manual; b.Location = new Point(-20000, -20000); b.Show();
                    using (var bmp = new Bitmap(b.Width, b.Height)) { b.DrawToBitmap(bmp, new Rectangle(0, 0, b.Width, b.Height)); bmp.Save(Arg(args, "--screenshot-confirm", null), ImageFormat.Png); }
                }
                return;
            }
            if (args.Contains("--test-languages")) { WriteTest(Arg(args, "--result", null), "languages test", Language.Test()); return; }
            if (args.Contains("--test-license")) { WriteTest(Arg(args, "--result", null), "license test", LicenseLabel.Test()); return; }
            if (Arg(args, "--screenshot-language", null) != null) {
                using (var b = new LanguageChoice(Language.Code)) {
                    b.StartPosition = FormStartPosition.Manual; b.Location = new Point(-20000, -20000); b.Show();
                    using (var bmp = new Bitmap(b.Width, b.Height)) { b.DrawToBitmap(bmp, new Rectangle(0, 0, b.Width, b.Height)); bmp.Save(Arg(args, "--screenshot-language", null), ImageFormat.Png); }
                }
                return;
            }
            if (args.Contains("--test-etw")) { WriteTest(Arg(args, "--result", null), "etw test", Network.RealTest()); return; }
            if (args.Contains("--test-disks")) { WriteTest(Arg(args, "--result", null), "disks test", Sensors.RealTest()); return; }
            if (args.Contains("--test-paint")) { WriteTest(Arg(args, "--result", null), "paint test", SafePaint.Test()); return; }
            if (args.Contains("--test-migration")) { WriteTest(Arg(args, "--result", null), "migration test", Legacy.Test(Arg(args, "--data", null))); return; }
            if (Arg(args, "--screenshot-pawnio", null) != null) {
                using (var b = new PawnIOChoice()) {
                    b.StartPosition = FormStartPosition.Manual; b.Location = new Point(-20000, -20000); b.Show();
                    using (var bmp = new Bitmap(b.Width, b.Height)) { b.DrawToBitmap(bmp, new Rectangle(0, 0, b.Width, b.Height)); bmp.Save(Arg(args, "--screenshot-pawnio", null), ImageFormat.Png); }
                }
                return;
            }
            if (Arg(args, "--screenshot-welcome", null) != null) {
                using (var b = new Welcome(args.Contains("--without-pawnio"))) {
                    b.StartPosition = FormStartPosition.Manual; b.Location = new Point(-20000, -20000); b.Show();
                    using (var bmp = new Bitmap(b.Width, b.Height)) { b.DrawToBitmap(bmp, new Rectangle(0, 0, b.Width, b.Height)); bmp.Save(Arg(args, "--screenshot-welcome", null), ImageFormat.Png); }
                }
                return;
            }
            if (args.Contains("--install")) { Environment.ExitCode = Installer.Install(Arg(args, "--user", Installer.User()), args.Contains("--pawnio")); return; }
            if (args.Contains("--uninstall")) {
                Environment.ExitCode = Admin() ? Installer.Uninstall() : Installer.AsAdministrator("--uninstall");
                return;
            }
            bool preview = args.Contains("--preview");
            if (!Installer.Installed() && !preview && !args.Contains("--portable") && (Settings.Read("portable", "no") != "yes" || Installer.InstalledVersion() != null)) {
                string inUse = Installer.InstalledVersion() != null ? Installer.InstalledLanguage() : null;
                if (inUse != null) Language.Use(inUse);
                else if (!Language.Valid(Arg(args, "--language", null)) && !Settings.FileExists()) {
                    using (var s = new LanguageChoice(Language.FromWindows())) {
                        if (s.ShowDialog() != DialogResult.OK) return;
                        Language.Use(s.Code);
                    }
                }
                bool install, pawnio;
                using (var b = new Welcome()) {
                    if (b.ShowDialog() != DialogResult.OK) return;
                    install = b.Install; pawnio = b.PawnIO;
                }
                if (install) {
                    string arg = "--install --user \"" + Installer.User() + "\"" + (pawnio ? " --pawnio" : "") + " --language " + Language.Code;
                    int r = Installer.AsAdministrator(arg);
                    if (r == -1) MessageBox.Show(Language.T("uac.denied"), "WeightMonitor");
                    return;
                }
                Language.Choose(Language.Code);
                Settings.Write("portable", "yes");
            }

            if (args.Contains("--open") && Installer.Installed() && !Admin() && !RunningCopy() && Installer.TurnOn()) return;

            bool newOne = false;
            Mutex single = null;
            try { single = new Mutex(true, COPY_SINGLE, out newOne); } catch (UnauthorizedAccessException) { }
            using (single) {
                if (!newOne) {
                    if (args.Length == 0 || args.Contains("--open")) AskSettings();
                    return;
                }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object o, ThreadExceptionEventArgs ev) { Log("interface: " + ev.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object o, UnhandledExceptionEventArgs ev) { Log("fatal: " + ev.ExceptionObject); };

                foreach (string name in new[] { "inventory.txt", Legacy.INVENTORY_FILE }) try { File.Delete(Path.Combine(Settings.Folder, name)); } catch { }
                Log("start " + VERSION + ": administrator " + (Admin() ? "yes" : "no") + ", PawnIO " + (Installer.PawnIOPresent() ? "yes" : "no"));
                Sensors = new Sensors();
                Sensors.Read();
                Network = new Network();
                Network.Start();
                if (!Network.Activate) Log("per-program network is off: " + Network.LogError);
                Notification.Start();
                var work = new Thread(Work) { IsBackground = true, Name = "sensors" };
                work.Start();

                Bar = new Dashboard(!preview, style) { CaptureTest = args.Contains("--test-capture") };
                ListenSettings();
                try { ChangeWindowMessageFilter(RegisterWindowMessage("TaskbarCreated"), 1); } catch { }
                Icon = new NotifyIcon { Text = "WeightMonitor " + VERSION, Visible = true, ContextMenuStrip = Menu() };
                try { using (var s = Resource("WeightMonitor.ico")) Icon.Icon = new Icon(s, SystemInformation.SmallIconSize); }
                catch { Icon.Icon = SystemIcons.Application; }
                UpdateIcon();
                Icon.BalloonTipClicked += delegate { Notification.Clicked(); };
                var wait = new System.Windows.Forms.Timer { Interval = Math.Min(SystemInformation.DoubleClickTime, 300) };
                var where = Point.Empty;
                wait.Tick += delegate { wait.Stop(); Flyout.Open("all", new Rectangle(where, new Size(1, 1)), Bar.Scale()); };
                Icon.MouseClick += delegate(object o, MouseEventArgs e) {
                    if (e.Button != MouseButtons.Left || Flyout.JustClosed("all")) return;
                    where = Cursor.Position; wait.Stop(); wait.Start();
                };
                Icon.MouseDoubleClick += delegate(object o, MouseEventArgs e) {
                    if (e.Button != MouseButtons.Left) return;
                    wait.Stop();
                    if (Flyout.Current != null) Flyout.Current.Close();
                    SettingsWindow.Open();
                };
                if (Installer.Installed() && Settings.Read("welcome", "no") != "done") {
                    var t = new System.Windows.Forms.Timer { Interval = 4000 };
                    t.Tick += delegate {
                        t.Stop(); Settings.Write("welcome", "done");
                        Flyout.Open("all", Bar.RectangleToScreen(Bar.ClientRectangle), Bar.Scale());
                    };
                    t.Start();
                }
                Bar.HandleCreated += delegate { UpdateChecker.AtStartup(Bar); };
                Application.Run(Bar);
                end = true;
                Notification.Stop();
                Icon.Visible = false;
                Icon.Dispose();
                Network.Stop();
                Sensors.Close();
            }
        } catch (Exception ex) {
            Log("start: " + ex);
        }
    }
}
