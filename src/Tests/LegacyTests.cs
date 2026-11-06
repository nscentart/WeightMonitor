using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public static partial class Legacy {
    static string Hash(string text) { return Hex(Encoding.UTF8.GetBytes(text)); }
    static string Hex(byte[] bytes) {
        using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
    }
    static string FileHash(string file) { return Hex(File.ReadAllBytes(file)); }

    public static List<string> Test(string dataFolder) {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        if (dataFolder == null || !Directory.Exists(dataFolder)) { Check(false, "migration data folder missing"); return r; }
        string root = Path.Combine(Path.GetTempPath(), "wm-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            NamesTest(Check);
            AliasesTest(Check);
            SettingsTest(Path.Combine(dataFolder, "settings"), root, Check);
            ChainTest(root, Check);
            LicenseTest(Path.Combine(dataFolder, "license"), root, Check);
            LogsTest(Path.Combine(dataFolder, "logs"), root, Check);
            RemovalTest(root, Check);
        } catch (Exception ex) {
            Check(false, "migration test stopped: " + ex);
        } finally {
            Settings.UseFile(Path.Combine(root, "end", "settings.ini"));
            try { foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(root, true); } catch { }
        }
        return r;
    }

    static void NamesTest(Action<bool, string> Check) {
        var expected = new Dictionary<string, string> {
            { MUTEX, "1d883382c75a65ffb3cda1ac540a46aca23e2038746da4ae1ca0e5fb20a159de" },
            { SETTINGS_EVENT, "f7cffda6dd43af384adb90e3775af348a13955b248f8b648612f3509827e336e" },
            { ETW_SESSION, "31cad3cacc7aa6ffaba7c96c5a27c2f804ab79035809954decfa3d41ece18842" },
            { ETW_TEST_SESSION, "02c2ad244afd25165436ba99a0a8ef960ee64a46bbc8052f7cd243973f8eed10" } };
        foreach (var kv in expected) Check(Hash(kv.Key) == kv.Value, "name shared with version 1.1 unchanged: " + kv.Key.Length + " characters, SHA-256 " + kv.Value.Substring(0, 12));
    }

    static void AliasesTest(Action<bool, string> Check) {
        foreach (var kv in Aliases) {
            string[] mapped = Arguments(new[] { "first", kv.Key, "last" });
            Check(mapped.SequenceEqual(new[] { "first", kv.Value, "last" }), "old option " + kv.Key.Length + " characters long becomes " + kv.Value);
        }
        string[] keys = InstallAliases.Keys.ToArray();
        string installOld = Aliases.First(a => a.Value == "--install").Key;
        Check(Arguments(new[] { installOld }.Concat(keys).ToArray()).SequenceEqual(new[] { "--install" }.Concat(keys.Select(k => InstallAliases[k]))), "old install options follow the old install command");
        Check(Arguments(keys).SequenceEqual(keys), "old install options alone stay as they are");
        Check(Arguments(new[] { "--install", "--user", "x" }).SequenceEqual(new[] { "--install", "--user", "x" }), "new options are never touched");
    }

    static string[] Lines(string file) { return File.ReadAllLines(file); }

    static void SettingsTest(string folder, string root, Action<bool, string> Check) {
        if (!Directory.Exists(folder)) return;
        foreach (var ini in Directory.GetFiles(folder, "*.ini").OrderBy(f => f, StringComparer.Ordinal)) {
            string name = Path.GetFileNameWithoutExtension(ini), expected = Path.ChangeExtension(ini, ".expected");
            if (!File.Exists(expected)) { Check(false, name + ": expected result missing"); continue; }
            string dir = Path.Combine(root, "case-" + name);
            Directory.CreateDirectory(dir);
            string old = Path.Combine(dir, SETTINGS_FILE), current = Path.Combine(dir, "settings.ini");
            File.Copy(ini, old);
            File.SetAttributes(old, FileAttributes.ReadOnly);
            string before = FileHash(old);
            Settings.UseFile(current, null, old);
            Check(Settings.FileExists(), name + ": the old file counts as the settings file");
            Settings.Read("language", "");
            Check(File.Exists(current), name + ": the new file is written");
            Check(File.Exists(current) && Lines(current).SequenceEqual(Lines(expected)), name + ": every setting carried over as expected (" + (File.Exists(current) ? Lines(current).Length : -1) + " lines)");
            Check(FileHash(old) == before && (File.GetAttributes(old) & FileAttributes.ReadOnly) != 0, name + ": the old file is untouched");
            Check(!File.Exists(current + ".new") && !File.Exists(old + TEMPORARY_SUFFIX), name + ": no temporary file left");
            File.SetAttributes(old, FileAttributes.Normal);
            File.AppendAllText(old, LANGUAGE_KEY + "=de\r\n");
            Settings.Reread();
            Settings.Read("language", "");
            Check(Lines(current).SequenceEqual(Lines(expected)), name + ": a change made later to the old file no longer counts");
        }
    }

    static void ChainTest(string root, Action<bool, string> Check) {
        string dir = Path.Combine(root, "chain");
        string install = Path.Combine(dir, "install", SETTINGS_FILE), userNew = Path.Combine(dir, "user", "settings.ini"), userOld = Path.Combine(dir, "user", SETTINGS_FILE);
        string current = Path.Combine(dir, "install", "data", "settings.ini");
        Action<string, string, string, string, string, string> Case = (what, newOne, atInstall, atUserNew, atUserOld, expected) => {
            foreach (var f in new[] { install, userNew, userOld, current }) { try { File.Delete(f); } catch { } }
            Directory.CreateDirectory(Path.GetDirectoryName(install));
            Directory.CreateDirectory(Path.GetDirectoryName(userNew));
            Directory.CreateDirectory(Path.GetDirectoryName(current));
            if (newOne != null) File.WriteAllText(current, "language=" + newOne + "\r\n");
            if (atInstall != null) File.WriteAllText(install, LANGUAGE_KEY + "=" + atInstall + "\r\n");
            if (atUserNew != null) File.WriteAllText(userNew, "language=" + atUserNew + "\r\n");
            if (atUserOld != null) File.WriteAllText(userOld, LANGUAGE_KEY + "=" + atUserOld + "\r\n");
            Settings.UseFile(current, userNew, install, userOld);
            Check(Settings.Read("language", "none") == expected, "read order: " + what + " (" + Settings.Read("language", "none") + ")");
        };
        Case("the current file wins over everything", "it", "fr", "de", "es", "it");
        Case("the old file beside it comes first", null, "fr", "de", "es", "fr");
        Case("then the new file of the user folder", null, null, "de", "es", "de");
        Case("then the old file of the user folder", null, null, null, "es", "es");
        Case("nothing found: the default", null, null, null, null, "none");
        Settings.UseFile(current, userNew, install, userOld);
        Check(!Settings.FileExists(), "read order: no file at all, no settings file");
    }

    static void LicenseTest(string folder, string root, Action<bool, string> Check) {
        string source = Path.Combine(folder, LICENSE_FILE);
        if (!File.Exists(source)) return;
        string legacyFolder = Path.Combine(root, "license-old"), folderNew = Path.Combine(root, "license-new");
        Directory.CreateDirectory(legacyFolder);
        string old = Path.Combine(legacyFolder, LICENSE_FILE), current = Path.Combine(folderNew, License.FILE);
        File.Copy(source, old);
        File.SetAttributes(old, FileAttributes.ReadOnly);
        string before = FileHash(old);
        Check(MigrateLicense(folderNew, legacyFolder), "license: the old file is copied to the new name");
        Check(File.Exists(current) && FileHash(current) == before, "license: the copy is identical byte for byte");
        Check(FileHash(old) == before && (File.GetAttributes(old) & FileAttributes.ReadOnly) != 0, "license: the old file is untouched");
        Check(!File.Exists(current + ".new"), "license: no temporary file left");
        File.WriteAllText(current, "changed");
        Check(!MigrateLicense(folderNew, legacyFolder) && File.ReadAllText(current) == "changed", "license: an existing new file is never overwritten");
        Check(!MigrateLicense(Path.Combine(root, "license-none"), Path.Combine(root, "license-nothing")) && !Directory.Exists(Path.Combine(root, "license-none")), "license: nothing to copy, nothing created");
        File.SetAttributes(old, FileAttributes.Normal);
        Check(RemoveLicense(legacyFolder) && !File.Exists(old), "license: removing the license removes the old file too");
        Check(RemoveLicense(legacyFolder), "license: removing again is fine");
    }

    static void LogsTest(string folder, string root, Action<bool, string> Check) {
        string input = Path.Combine(folder, "lines.txt"), expected = Path.Combine(folder, "lines.expected");
        if (!File.Exists(input) || !File.Exists(expected)) return;
        Check(CleanLog(File.ReadAllText(input, Encoding.UTF8), "<program>") == File.ReadAllText(expected, Encoding.UTF8), "old log lines: the window line and the notice line are cleaned as before");
        string a = Path.Combine(root, "logs-a"), b = Path.Combine(root, "logs-b");
        var apart = LogFiles(a, b).ToArray();
        Check(apart.Length == 2 && apart[0] == Path.Combine(a, "WeightMonitor.log" + LOG_OLD_SUFFIX) && apart[1] == Path.Combine(a, "WeightMonitor.log"), "old logs: the old folder gives both files, oldest first");
        var same = LogFiles(a, a).ToArray();
        Check(same.Length == 1 && same[0] == Path.Combine(a, "WeightMonitor.log" + LOG_OLD_SUFFIX), "old logs: in the same folder only the rotated file is extra");
    }

    static void RemovalTest(string root, Action<bool, string> Check) {
        string all = Path.Combine(root, "remove-all");
        Directory.CreateDirectory(all);
        var names = new[] { "settings.ini", "settings.ini.new", "WeightMonitor.log", "WeightMonitor.log.old", "inventory.txt" }.Concat(UserFiles).Distinct().ToArray();
        foreach (var f in names) File.WriteAllText(Path.Combine(all, f), "x");
        Settings.RemoveOld(all);
        Check(!Directory.Exists(all), "removal: the files of both generations and then the folder are gone (" + names.Length + " files)");
        string kept = Path.Combine(root, "remove-kept");
        Directory.CreateDirectory(kept);
        foreach (var f in names) File.WriteAllText(Path.Combine(kept, f), "x");
        File.WriteAllText(Path.Combine(kept, "other.txt"), "x");
        Settings.RemoveOld(kept);
        Check(Directory.GetFiles(kept).Length == 1 && File.Exists(Path.Combine(kept, "other.txt")), "removal: a file that is not ours stays, and so does its folder");
        Check(TemporaryFolders.Length == 2 && TemporaryFolders.Distinct().Count() == 2, "removal: the two old temporary folders are known");
    }
}
