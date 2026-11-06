using System.Windows.Forms;

public static partial class LicenseLabel {
    public const string PROGRAM = "WeightMonitor";
    static LicenseResult now;
    static string[] keys = License.PUBLIC_KEYS;
    static int[] revoked = License.REVOKED;

    public static LicenseResult Current { get { return now ?? (now = Open()); } }

    static LicenseResult Open() {
        Legacy.MigrateLicense(Settings.Folder, Settings.LegacyFolder);
        return License.Load(Settings.Folder, PROGRAM, keys, revoked);
    }

    public static void SampleData(string holder) {
        now = holder == null ? new LicenseResult { State = LicenseState.Absent }
            : new LicenseResult { State = LicenseState.Valid, Program = PROGRAM, Number = 1042, Holder = holder, Data = "2026-11-03" };
    }

    public static string Caption() { return Current.Valid ? Language.T("license.commercial_of", "name", Current.Holder) : Language.T("license.personal"); }

    public static string FlyoutCaption() {
        string license = Current.Valid ? Language.T("overview.license_commercial", "name", Current.Holder) : Language.T("overview.license_personal");
        return Language.T("overview.version", "version", Program.VERSION) + " · " + license;
    }

    public static string Problem(LicenseResult e) {
        switch (e.State) {
            case LicenseState.Invalid: return Language.T("license.msg.invalid");
            case LicenseState.OtherProgram: return Language.T("license.msg.other_program", "program", e.Program);
            case LicenseState.Revoked: return Language.T("license.msg.revoked");
            case LicenseState.Unreadable: return Language.T("license.msg.unreadable");
            default: return "";
        }
    }

    public static string Stays() { return Language.T(Current.Valid ? "license.msg.keeps_commercial" : "license.msg.keeps_personal"); }

    public static string Use(string text, out bool ok) {
        ok = false;
        if (text == null || License.Clean(text) == "") return Language.T("license.msg.empty");
        var e = License.Check(text, PROGRAM, keys, revoked);
        if (!e.Valid) return Problem(e) + " " + Stays();
        if (!License.Write(Settings.Folder, text)) return Language.T("license.msg.not_saved") + " " + Stays();
        now = e; ok = true;
        return Language.T("license.msg.saved");
    }

    public static string Remove(out bool ok) {
        ok = Legacy.RemoveLicense(Settings.LegacyFolder) & License.Remove(Settings.Folder);
        now = null;
        return ok ? Language.T("license.msg.removed") + " " + Stays() : Language.T("license.msg.not_removed") + " " + Stays();
    }

    public static string ScreenshotSentence(string which, out bool problem) {
        problem = which != "active" && which != "removed";
        if (which == "active") return Language.T("license.msg.saved");
        if (which == "removed") return Language.T("license.msg.removed") + " " + Stays();
        var st = which == "other" ? LicenseState.OtherProgram : which == "revoked" ? LicenseState.Revoked : which == "unreadable" ? LicenseState.Unreadable : LicenseState.Invalid;
        return Problem(new LicenseResult { State = st, Program = "ProjectManager" }) + " " + Stays();
    }

    public static bool HasFile() { return Current.State != LicenseState.Absent; }
}
