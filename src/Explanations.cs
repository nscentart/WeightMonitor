using System;
using System.Collections.Generic;
using System.IO;

public static class Explanations {
    static readonly Dictionary<string, string> LIST = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        { "svchost", "svchost" },
        { "MsMpEng", "MsMpEng" },
        { "NisSrv", "NisSrv" },
        { "SecurityHealthService", "SecurityHealthService" },
        { "SearchIndexer", "SearchIndexer" },
        { "SearchHost", "SearchHost" },
        { "dwm", "dwm" },
        { "csrss", "csrss" },
        { "lsass", "lsass" },
        { "services", "services" },
        { "smss", "smss" },
        { "wininit", "smss" },
        { "winlogon", "winlogon" },
        { "fontdrvhost", "fontdrvhost" },
        { "spoolsv", "spoolsv" },
        { "audiodg", "audiodg" },
        { "ntoskrnl", "ntoskrnl" },
        { "Registry", "Registry" },
        { "Memory Compression", "Memory_Compression" },
        { "explorer", "explorer" },
        { "TiWorker", "TiWorker" },
        { "TrustedInstaller", "TiWorker" },
        { "MoUsoCoreWorker", "MoUsoCoreWorker" },
        { "wuauclt", "MoUsoCoreWorker" },
        { "RuntimeBroker", "RuntimeBroker" },
        { "sihost", "sihost" },
        { "ShellExperienceHost", "sihost" },
        { "StartMenuExperienceHost", "StartMenuExperienceHost" },
        { "taskhostw", "taskhostw" },
        { "WmiPrvSE", "WmiPrvSE" },
        { "dllhost", "dllhost" },
        { "conhost", "conhost" },
        { "ctfmon", "ctfmon" },
    };

    public static IEnumerable<string> All { get { return LIST.Keys; } }

    static string Find(App a) {
        if (a == null) return null;
        if (a.Key == "system") return LIST["ntoskrnl"];
        string file;
        if (a.Path != null) {
            if (!Notifications.InWindowsFiles(a.Path)) return null;
            file = Path.GetFileNameWithoutExtension(a.Path);
        } else if (a.Key != null && a.Key.StartsWith("name:")) file = Path.GetFileNameWithoutExtension(a.Key.Substring(5));
        else return null;
        string v;
        return LIST.TryGetValue(file, out v) ? v : null;
    }

    public static string Name(App a) {
        var v = Find(a);
        if (v != null) return Language.T("explain." + v + ".name");
        return a.PidClosed > 0 ? Language.T("name.closed", "pid", a.PidClosed) : a.Name;
    }
    public static string Row(App a) { var v = Find(a); return v != null ? Language.T("explain." + v + ".summary") : null; }
}
