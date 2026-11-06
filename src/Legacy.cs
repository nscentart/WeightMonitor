using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class Legacy {
    public const string MUTEX = "WeightMonitor-una-sola-copia";
    public const string SETTINGS_EVENT = "WeightMonitor-apri-impostazioni";
    public const string ETW_SESSION = "WeightMonitor-Rete";
    public const string ETW_TEST_SESSION = "WeightMonitor-Prova";
    public const string DATA_FOLDER = "dati";
    public const string SETTINGS_FILE = "impostazioni.ini";
    public const string LICENSE_FILE = "licenza.txt";
    public const string INVENTORY_FILE = "inventario.txt";
    public const string LANGUAGE_KEY = "lingua";
    public const string LOG_OLD_SUFFIX = ".vecchio";
    public const string TEMPORARY_SUFFIX = ".nuovo";
    public static readonly string[] TemporaryFolders = { "installazione", "aggiornamento" };
    public static readonly string[] UserFiles = { SETTINGS_FILE, SETTINGS_FILE + TEMPORARY_SUFFIX, "WeightMonitor.log" + LOG_OLD_SUFFIX, INVENTORY_FILE };

    public static readonly Dictionary<string, string> Aliases = new Dictionary<string, string> {
        { "--disinstalla", "--uninstall" }, { "--apri", "--open" }, { "--stile", "--style" }, { "--installa", "--install" } };
    public static readonly Dictionary<string, string> InstallAliases = new Dictionary<string, string> {
        { "--utente", "--user" }, { "--lingua", "--language" } };

    public static string[] Arguments(string[] args) {
        bool install = args.Contains("--installa");
        return args.Select(a => {
            string to;
            if (Aliases.TryGetValue(a, out to)) return to;
            if (install && InstallAliases.TryGetValue(a, out to)) return to;
            return a;
        }).ToArray();
    }

    static readonly Dictionary<string, string> KEYS = new Dictionary<string, string> {
        { "vista", "view" }, { "sganciata", "detached" }, { "stile", "style" }, { "nascosto", "hidden" }, { "aggiornamenti", "updates" },
        { "saltata", "skipped" }, { "portatile", "portable" }, { "benvenuto", "welcome" }, { "riserva_meteo", "weather_reserve" },
        { "pos_x", "position_x" }, { "pos_y", "position_y" }, { "notifiche", "notifications" },
        { "scheda_cpu", "tab_cpu" }, { "scheda_gpu", "tab_gpu" }, { "scheda_ram", "tab_ram" },
        { "scheda_dischi", "tab_disks" }, { "scheda_rete", "tab_network" }, { "scheda_tutto", "tab_all" },
        { "barra_mostra", "bar_show" }, { "lingua", "language" }, { "tema", "theme" }, { "colore", "color" }, { "aspetto", "appearance" } };

    static readonly Dictionary<string, string> YES = new Dictionary<string, string> { { "sì", "yes" } };
    static readonly Dictionary<string, Dictionary<string, string>> VALUES = new Dictionary<string, Dictionary<string, string>> {
        { "view", new Dictionary<string, string> { { "automatica", "automatic" }, { "compatta", "compact" } } },
        { "detached", YES }, { "hidden", YES }, { "updates", YES }, { "portable", YES }, { "notifications", YES },
        { "welcome", new Dictionary<string, string> { { "fatto", "done" } } },
        { "color", new Dictionary<string, string> { { "blu", "blue" }, { "verde", "green" }, { "limone", "lemon" }, { "rosa", "pink" }, { "viola", "purple" } } },
        { "appearance", new Dictionary<string, string> { { "scuro", "dark" }, { "chiaro", "light" } } },
        { "theme", new Dictionary<string, string> { { "acido", "acid" }, { "chiaro", "light" }, { "verde", "green" }, { "rosa", "pink" }, { "viola", "purple" } } } };
    static readonly Dictionary<string, string> PIECES = new Dictionary<string, string> { { "dischi", "disks" }, { "rete", "network" } };
    const int MAX_VALUE = 64;

    static string Mapped(Dictionary<string, string> map, string value) {
        string to;
        return map.TryGetValue(value, out to) ? to : value;
    }

    public static bool Setting(string key, string value, out string newKey, out string newValue) {
        newKey = null; newValue = null;
        if (value.Length > MAX_VALUE || !KEYS.TryGetValue(key, out newKey)) return false;
        Dictionary<string, string> map;
        if (newKey == "bar_show") newValue = string.Join(",", value.Split(',').Select(p => Mapped(PIECES, p)));
        else newValue = VALUES.TryGetValue(newKey, out map) ? Mapped(map, value) : value;
        return true;
    }

    public static string SettingsFile(string installFolder) { return Path.Combine(installFolder, DATA_FOLDER, SETTINGS_FILE); }

    public static string CleanLog(string text, string replacement) {
        text = Regex.Replace(text, "finestra «[^»\r\n]*» del processo \\S+", replacement);
        return Regex.Replace(text, "(notifica «[^»\r\n]*»):[^\r\n]*", "$1");
    }

    public static IEnumerable<string> LogFiles(string legacyFolder, string folder) {
        yield return Path.Combine(legacyFolder, "WeightMonitor.log" + LOG_OLD_SUFFIX);
        if (!string.Equals(Path.GetFullPath(legacyFolder), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine(legacyFolder, "WeightMonitor.log");
    }

    public static bool MigrateLicense(string folder, string legacyFolder) {
        try {
            string current = Path.Combine(folder, License.FILE), old = Path.Combine(legacyFolder, LICENSE_FILE);
            if (File.Exists(current) || Directory.Exists(current) || !File.Exists(old)) return false;
            Directory.CreateDirectory(folder);
            string temporary = current + ".new";
            File.Copy(old, temporary, true);
            File.SetAttributes(temporary, FileAttributes.Normal);
            File.Move(temporary, current);
            return true;
        } catch { return false; }
    }

    public static bool RemoveLicense(string legacyFolder) {
        string file = Path.Combine(legacyFolder, LICENSE_FILE);
        try { File.Delete(file); } catch { }
        return !File.Exists(file) && !Directory.Exists(file);
    }
}
