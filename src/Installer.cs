using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

public static partial class Installer {
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WeightMonitor");
    public static readonly string Exe = Path.Combine(Folder, "WeightMonitor.exe");
    public static readonly string Temporary = Path.Combine(Folder, "installation");
    static readonly string UpdateTemporary = Path.Combine(Folder, "update");
    public static void RemoveTemporary() {
        foreach (var c in new[] { Temporary, UpdateTemporary }.Concat(Legacy.TemporaryFolders.Select(n => Path.Combine(Folder, n)))) {
            try { if (Directory.Exists(c)) Directory.Delete(c, true); } catch { }
        }
    }
    const string TASK = "WeightMonitor";
    const string APP_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WeightMonitor";
    const string PAWNIO_URL = "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe";

    public static bool Installed() {
        return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase);
    }

    public static string InstalledVersion() {
        try {
            if (!File.Exists(Exe)) return null;
            string v = FileVersionInfo.GetVersionInfo(Exe).FileVersion ?? "";
            while (v.EndsWith(".0") && v.Count(c => c == '.') > 1) v = v.Substring(0, v.Length - 2);
            return v;
        } catch { return null; }
    }

    public static string InstalledLanguage() {
        try {
            foreach (var source in new[] { new[] { Path.Combine(Folder, "data", "settings.ini"), "language=" }, new[] { Legacy.SettingsFile(Folder), Legacy.LANGUAGE_KEY + "=" } }) {
                if (!File.Exists(source[0])) continue;
                foreach (var r in File.ReadAllLines(source[0]))
                    if (r.StartsWith(source[1]) && Language.Valid(r.Substring(source[1].Length))) return r.Substring(source[1].Length);
            }
        } catch { }
        return Language.INITIAL;
    }

    public static bool PawnIOPresent() {
        try { using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO")) return k != null; } catch { return false; }
    }

    public static int AsAdministrator(string arguments, bool waitFor = true) {
        try {
            var p = Process.Start(new ProcessStartInfo(Application.ExecutablePath, arguments) { UseShellExecute = true, Verb = "runas" });
            if (!waitFor) return 0;
            p.WaitForExit();
            return p.ExitCode;
        } catch (System.ComponentModel.Win32Exception) { return -1; }
    }

    public static string SystemPath(string exe) { return Path.Combine(Environment.SystemDirectory, exe); }
    public static string Explorer() { return Path.Combine(Path.GetDirectoryName(Environment.SystemDirectory), "explorer.exe"); }

    static int Command(string file, string arguments) {
        var p = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.SystemDirectory });
        p.WaitForExit();
        return p.ExitCode;
    }

    static void StopOthers() {
        try { Command(SystemPath("schtasks.exe"), "/End /TN " + TASK); } catch { }
        int io = Process.GetCurrentProcess().Id;
        foreach (var p in Process.GetProcessesByName("WeightMonitor")) {
            if (p.Id == io) continue;
            try { p.Kill(); p.WaitForExit(3000); } catch { }
        }
    }

    public static int Install(string user, bool pawnio) {
        try {
            StopOthers();
            Directory.CreateDirectory(Folder);
            foreach (var f in Directory.GetFiles(Folder).Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                                              || Path.GetFileName(f).StartsWith("WeightMonitor.", StringComparison.OrdinalIgnoreCase)))
                if (!string.Equals(f, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) { try { File.Delete(f); } catch { } }
            if (!Installed()) File.Copy(Application.ExecutablePath, Exe, true);
            Settings.Write("language", Language.Code);

            if (pawnio && !PawnIOPresent()) {
                string error = InstallPawnIO();
                if (error != null) MessageBox.Show(Language.T("install.pawnio_error", "error", error), "WeightMonitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            string xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n"
                + "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">"
                + "<RegistrationInfo><Description>" + SecurityElement.Escape(Language.T("install.task_description")) + "</Description></RegistrationInfo>"
                + "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + SecurityElement.Escape(user) + "</UserId><Delay>PT3S</Delay></LogonTrigger></Triggers>"
                + "<Principals><Principal id=\"Author\"><UserId>" + SecurityElement.Escape(user) + "</UserId><LogonType>InteractiveToken</LogonType>"
                + "<RunLevel>HighestAvailable</RunLevel></Principal></Principals>"
                + "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>"
                + "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority></Settings>"
                + "<Actions Context=\"Author\"><Exec><Command>\"" + SecurityElement.Escape(Exe) + "\"</Command><WorkingDirectory>"
                + SecurityElement.Escape(Folder) + "</WorkingDirectory></Exec></Actions></Task>";
            Directory.CreateDirectory(Temporary);
            string tmp = Path.Combine(Temporary, "WeightMonitor-task.xml");
            File.WriteAllText(tmp, xml, Encoding.Unicode);
            int r = Command(SystemPath("schtasks.exe"), "/Create /TN " + TASK + " /XML \"" + tmp + "\" /F");
            try { File.Delete(tmp); } catch { }
            RemoveTemporary();
            if (r != 0) throw new Exception(Language.T("install.startup_error", "code", r));

            using (var k = Registry.LocalMachine.CreateSubKey(APP_KEY)) {
                k.SetValue("DisplayName", "WeightMonitor");
                k.SetValue("DisplayVersion", Program.VERSION);
                k.SetValue("DisplayIcon", Exe);
                k.SetValue("InstallLocation", Folder);
                k.SetValue("UninstallString", "\"" + Exe + "\" --uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024), RegistryValueKind.DWord);
            }
            try { Shortcut(MenuStart); } catch (Exception ex) { Program.Log("Start menu shortcut: " + ex.Message); }
            Command(SystemPath("schtasks.exe"), "/Run /TN " + TASK);
            return 0;
        } catch (Exception ex) {
            Program.Log("installation: " + ex);
            MessageBox.Show(Language.T("install.failed", "message", ex.Message), "WeightMonitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static readonly string MenuStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "WeightMonitor.lnk");
    public static void Shortcut(string file) {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(type);
        object lnk = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { file });
        var t = lnk.GetType();
        foreach (var v in new[] { new[] { "TargetPath", Exe }, new[] { "Arguments", "--open" }, new[] { "WorkingDirectory", Folder },
                                  new[] { "IconLocation", Exe + ",0" }, new[] { "Description", "WeightMonitor" } })
            t.InvokeMember(v[0], System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { v[1] });
        t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, null);
    }
    public static bool TurnOn() {
        try { return Command(SystemPath("schtasks.exe"), "/Run /TN " + TASK) == 0; } catch { return false; }
    }

    static string InstallPawnIO() {
        string file = Path.Combine(Temporary, "PawnIO_setup.exe");
        try {
            Directory.CreateDirectory(Temporary);
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            using (var w = new WebClient()) w.DownloadFile(PAWNIO_URL, file);
            if (!ValidSignature(file)) return Language.T("install.signature_invalid");
            string who = Signer(file);
            if (who != "namazso.eu") return Language.T("install.signature_other", "signer", who);
            int r = Command(file, "-install -silent");
            return r == 0 || PawnIOPresent() ? null : Language.T("install.exit_code", "code", r);
        } catch (Exception ex) { return ex.Message; }
        finally { try { File.Delete(file); } catch { } }
    }

    static string Signer(string file) {
        using (var c = new X509Certificate2(X509Certificate.CreateFromSignedFile(file))) return c.GetNameInfo(X509NameType.SimpleName, false);
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)] static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, IntPtr data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct FILE_INFO { public int cbStruct; public IntPtr path; public IntPtr hFile; public IntPtr subject; }
    [StructLayout(LayoutKind.Sequential)]
    struct TRUST_DATA {
        public int cbStruct; public IntPtr policy, sip; public int ui, revocation, choice; public IntPtr file;
        public int state; public IntPtr hState, url; public int test, context; public IntPtr settings;
    }
    static bool ValidSignature(string file) {
        var info = new FILE_INFO { cbStruct = Marshal.SizeOf(typeof(FILE_INFO)), path = Marshal.StringToHGlobalUni(file) };
        IntPtr pInfo = Marshal.AllocHGlobal(info.cbStruct), pData = IntPtr.Zero;
        try {
            Marshal.StructureToPtr(info, pInfo, false);
            var d = new TRUST_DATA { cbStruct = Marshal.SizeOf(typeof(TRUST_DATA)), ui = 2, revocation = 1, choice = 1, file = pInfo };
            pData = Marshal.AllocHGlobal(d.cbStruct);
            Marshal.StructureToPtr(d, pData, false);
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            return WinVerifyTrust(new IntPtr(-1), ref action, pData) == 0;
        } finally {
            Marshal.FreeHGlobal(info.path); Marshal.FreeHGlobal(pInfo);
            if (pData != IntPtr.Zero) Marshal.FreeHGlobal(pData);
        }
    }

    public static int Uninstall() {
        var r = MessageBox.Show(Language.T("install.remove_ask"), "WeightMonitor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return 1;
        StopOthers();
        try { Command(SystemPath("schtasks.exe"), "/Delete /TN " + TASK + " /F"); } catch { }
        try { Registry.LocalMachine.DeleteSubKeyTree(APP_KEY, false); } catch { }
        try { File.Delete(MenuStart); } catch { }
        Settings.RemoveOld();
        if (Installed()) {
            Process.Start(new ProcessStartInfo(SystemPath("cmd.exe"), "/c " + SystemPath("timeout.exe") + " /t 2 /nobreak >nul & rmdir /s /q \"" + Folder + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.SystemDirectory });
        } else { try { Directory.Delete(Folder, true); } catch { } }
        MessageBox.Show(Language.T("install.removed"), "WeightMonitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }

    public static string User() { return WindowsIdentity.GetCurrent().Name; }
}
