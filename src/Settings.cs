using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class Settings {
    sealed class Source {
        public readonly string File;
        public readonly bool Old;
        public Source(string file, bool old) { File = file; Old = old; }
    }

    static readonly string UserFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeightMonitor");
    static readonly bool ProtectedInstall = Program.Admin() && (Installer.Installed() || Legacy.Arguments(Environment.GetCommandLineArgs()).Contains("--install"));
    public static string Folder = ProtectedInstall ? Path.Combine(Installer.Folder, "data") : UserFolder;
    public static string LegacyFolder = ProtectedInstall ? Path.Combine(Installer.Folder, Legacy.DATA_FOLDER) : UserFolder;
    static string SettingsFile = Path.Combine(Folder, "settings.ini");
    static Source[] sources = ProtectedInstall
        ? new[] { new Source(Path.Combine(LegacyFolder, Legacy.SETTINGS_FILE), true), new Source(Path.Combine(UserFolder, "settings.ini"), false), new Source(Path.Combine(UserFolder, Legacy.SETTINGS_FILE), true) }
        : new[] { new Source(Path.Combine(UserFolder, Legacy.SETTINGS_FILE), true) };
    static Dictionary<string, string> values;

    static readonly HashSet<string> KNOWN = new HashSet<string> { "view", "detached", "style", "hidden", "updates", "skipped", "portable", "welcome",
        "weather_reserve", "position_x", "position_y", "notifications", "tab_cpu", "tab_gpu", "tab_ram", "tab_disks", "tab_network", "tab_all", "bar_show", "bar_commit", "commit_reserve", "language", "theme", "color", "appearance" };
    const int MAX_VALUE = 64;

    public static void UseFile(string file, string old = null, string legacy = null, string legacyOld = null) {
        lock (freeze) {
            SettingsFile = file; Folder = Path.GetDirectoryName(file);
            LegacyFolder = legacy != null ? Path.GetDirectoryName(legacy) : Folder;
            var list = new List<Source>();
            if (legacy != null) list.Add(new Source(legacy, true));
            if (old != null) list.Add(new Source(old, false));
            if (legacyOld != null) list.Add(new Source(legacyOld, true));
            sources = list.ToArray(); values = null;
        }
    }
    public static void Reread() { lock (freeze) values = null; }
    public static bool FileExists() { lock (freeze) return File.Exists(SettingsFile) || sources.Any(s => File.Exists(s.File)); }

    static bool badRead;
    static void Load() {
        if (values != null && !badRead) return;
        var newOnes = new Dictionary<string, string>();
        Source found = File.Exists(SettingsFile) ? null : sources.FirstOrDefault(s => File.Exists(s.File));
        bool relocation = found != null;
        string from = relocation ? found.File : SettingsFile;
        try {
            if (File.Exists(from))
                foreach (var r in File.ReadAllLines(from)) {
                    int i = r.IndexOf('=');
                    if (i <= 0) continue;
                    string k = r.Substring(0, i).Trim(), v = r.Substring(i + 1).Trim();
                    if (relocation && found.Old && !Legacy.Setting(k, v, out k, out v)) continue;
                    if (KNOWN.Contains(k) && v.Length <= MAX_VALUE) newOnes[k] = v;
                }
            badRead = false;
        } catch { badRead = true; if (values != null) return; }
        if (values != null) foreach (var kv in values) if (!newOnes.ContainsKey(kv.Key)) newOnes[kv.Key] = kv.Value;
        values = newOnes;
        if (relocation && !badRead) Save();
    }
    public static string Read(string key, string defaultValue) { lock (freeze) { Load(); string v; return values.TryGetValue(key, out v) ? v : defaultValue; } }
    public static void Write(string key, string value) {
        lock (freeze) {
            Load(); values[key] = value;
            if (!badRead) Save();
        }
    }
    static void Save() {
        try {
            Directory.CreateDirectory(Folder);
            string temporary = SettingsFile + ".new";
            File.WriteAllLines(temporary, values.Select(kv => kv.Key + "=" + kv.Value));
            if (File.Exists(SettingsFile)) File.Replace(temporary, SettingsFile, null); else File.Move(temporary, SettingsFile);
        } catch { }
    }
    public static void RemoveOld(string folder = null) {
        folder = folder ?? UserFolder;
        foreach (var f in new[] { "settings.ini", "settings.ini.new", "WeightMonitor.log", "WeightMonitor.log.old", "inventory.txt" }.Concat(Legacy.UserFiles))
            try { File.Delete(Path.Combine(folder, f)); } catch { }
        try { Directory.Delete(folder); } catch { }
    }
    static readonly object freeze = new object();
}
