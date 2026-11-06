using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static partial class Language {
    static readonly HashSet<string> EQUAL = new HashSet<string> { "color.green","color.pink","time.hours_minutes","time.minutes","settings.memory","sensors.memory","sensors.copy","disks.count.one","network.up_average","network.down_average","temps.average","cpu.col_average","gpu.col_average","ram.col_average","disks.col_down_average","disks.col_up_average","settings.group.theme","theme.lime","theme.green","theme.pink" };

    public static List<string> Test() {
        var r = new List<string>();
        Action<bool, string> Verify = (ok, what) => r.Add((ok ? "OK    " : "ERROR ") + what);
        var reference = new List<string>();
        foreach (var key in Keys(INITIAL)) if (!key.StartsWith("fixed.", StringComparison.Ordinal)) reference.Add(key);
        Verify(reference.Count > 300, "Italian has its texts: " + reference.Count);
        foreach (var c in CODES) {
            int missing = 0, placeholder = 0, longValue = 0, italian = 0, empty = 0;
            foreach (var k in reference) {
                string v = Raw(c, k);
                if (v == null) { missing++; r.Add("ERROR " + c + ": missing " + k); continue; }
                if (v.Length == 0 && k != "notification.percent") { empty++; r.Add("ERROR " + c + ": empty " + k); }
                var a = new List<string>(Placeholder(v)); a.Sort(StringComparer.Ordinal);
                var b = new List<string>(Placeholder(Raw(INITIAL, k))); b.Sort(StringComparer.Ordinal);
                if (string.Join(",", a) != string.Join(",", b)) { placeholder++; r.Add("ERROR " + c + ": different placeholders in " + k); }
                if (v.IndexOf('\u2014') >= 0) { longValue++; r.Add("ERROR " + c + ": long dash in " + k); }
                if (c != INITIAL && v == Raw(INITIAL, k) && !EQUAL.Contains(k) && Letters(v) >= 3) { italian++; r.Add("ERROR " + c + ": still in Italian " + k); }
            }
            foreach (var k in Keys(c)) if (Raw(INITIAL, k) == null) { missing++; r.Add("ERROR " + c + ": extra key " + k); }
            Verify(missing + placeholder + longValue + italian + empty == 0, c + ": " + reference.Count + " texts, none missing, same placeholders, no long dash, no Italian left");
        }

        int fixedKeys = 0, strayFixed = 0;
        foreach (var key in Keys(INITIAL)) if (key.StartsWith("fixed.", StringComparison.Ordinal)) fixedKeys++;
        foreach (var c in CODES) if (c != INITIAL) foreach (var key in Keys(c)) if (key.StartsWith("fixed.", StringComparison.Ordinal)) strayFixed++;
        Verify(fixedKeys > 0 && strayFixed == 0, "the fixed Italian texts live only in the Italian file: " + fixedKeys);

        string previous = current;
        Use("fr");
        string en = Raw("en", "common.close"), it = Raw("it", "common.close"), fr = Raw("fr", "common.close");
        Verify(T("common.close") == fr, "fallback: French exists, French is used");
        tables["fr"].Remove("common.close");
        Verify(T("common.close") == en, "fallback: French missing, English is used");
        tables["en"].Remove("common.close");
        Verify(T("common.close") == it, "fallback: English missing too, Italian is used");
        tables["it"].Remove("common.close");
        Verify(T("common.close") == "common.close", "fallback: nowhere, the key is shown and nothing breaks");
        tables["fr"]["common.close"] = fr; tables["en"]["common.close"] = en; tables["it"]["common.close"] = it;

        Verify(Fill("a {b", new object[] { "b", 1 }) == "a {b" && Fill("}{", new object[] { "b", 1 }) == "}{" && Fill("{0} {x}", new object[] { "x", "{0}" }) == "{0} {0}", "wrong braces and values with braces do no harm");
        Verify(Fill("{a}{b}", new object[] { "a", "{b}", "b", "z" }) == "{b}z", "a substitution is not read again");
        Verify(Read("a=1\r\n# note\r\n\r\nno equals\r\nb=x=y\r\nc=two\\nlines").Count == 3 && Read("b=x=y")["b"] == "x=y" && Read("c=two\\nlines")["c"] == "two\nlines", "the language file also reads with strange lines");

        Use("fr"); Verify(N("disks.count", 0) == T("disks.count.one", "count", "0") && N("disks.count", 1) == T("disks.count.one", "count", "1") && N("disks.count", 2) == T("disks.count.many", "count", "2"), "French plural");
        Use("en"); Verify(N("disks.count", 0) == T("disks.count.many", "count", "0") && N("disks.count", 1) == T("disks.count.one", "count", "1"), "English plural");

        Verify(FromCulture("de") == "de" && FromCulture("nl") == "en" && FromCulture("ru") == "en" && FromCulture("") == "en" && FromCulture(null) == "en", "Windows language: among the five that one, otherwise it starts from English");
        Verify(CODES.Length == 5, "there are five languages");

        string folder = Path.Combine(Path.GetTempPath(), "WM-test-languages-" + Guid.NewGuid().ToString("N")), file = Path.Combine(folder, "settings.ini");
        var ThreadCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try {
            Directory.CreateDirectory(folder);
            Settings.UseFile(file);
            Begin(null);
            Verify(current == FromWindows(), "without settings it starts from the Windows language");
            Choose("de");
            Settings.Reread(); Begin(null);
            Verify(current == "de", "the choice is remembered on reopening");
            Begin("es");
            Verify(current == "es", "--language applies for this time only");
            Settings.Reread(); Begin(null);
            Verify(current == "de", "--language is not written: the choice stays");
            File.WriteAllText(file, "language=klingon\r\nstyle=b\r\n", new UTF8Encoding(false));
            Settings.Reread(); Begin(null);
            Verify(current == INITIAL, "a nonsense value in the file breaks nothing");
            File.WriteAllText(file, "style=b\r\n", new UTF8Encoding(false));
            Settings.Reread(); Begin(null);
            Verify(current == INITIAL, "update from an Italian-only version: Italian stays");
            foreach (var n in new[] { "de-DE", "tr-TR", "ar-SA", "fr-FR" }) {
                System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo(n);
                File.WriteAllText(file, "language=fr\r\nposition_x=1234\r\n", new UTF8Encoding(false));
                Settings.Reread(); Begin(null);
                Verify(current == "fr" && Settings.Read("position_x", "") == "1234" && Language.Fill("{n}", new object[] { "n", 1.5 }) == "1.5", "with the system in " + n + " the settings read the same and the numbers in the texts do not change");
            }
        } finally {
            System.Threading.Thread.CurrentThread.CurrentCulture = ThreadCulture;
            Settings.UseFile(Path.Combine(folder, "x.ini"));
            try { Directory.Delete(folder, true); } catch { }
            Use(previous);
        }
        return r;
    }

    public static IEnumerable<string> Placeholder(string text) {
        int i = 0;
        while ((i = text.IndexOf('{', i)) >= 0) {
            int end = text.IndexOf('}', i);
            if (end < 0) yield break;
            yield return text.Substring(i + 1, end - i - 1);
            i = end + 1;
        }
    }
}
