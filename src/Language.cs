using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static partial class Language {
    public static readonly string[] CODES = { "it", "en", "fr", "es", "de" };
    static readonly string[] NAMES = { "Italiano", "English", "Français", "Español", "Deutsch" };
    static readonly string[] CULTURE = { "it-IT", "en-US", "fr-FR", "es-ES", "de-DE" };
    public const string INITIAL = "it", FALLBACK = "en";

    static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
    static volatile string current = INITIAL;
    static volatile CultureInfo culture = new CultureInfo("it-IT");

    public static string Code { get { return current; } }
    public static bool Valid(string code) { return code != null && Array.IndexOf(CODES, code) >= 0; }
    public static string NameOf(string code) { int i = Array.IndexOf(CODES, code); return i < 0 ? code : NAMES[i]; }

    public static string FromWindows() { return FromCulture(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName); }
    public static string FromCulture(string twoLetter) { return Valid(twoLetter) ? twoLetter : FALLBACK; }

    public static void Begin(string fromCommand) {
        if (Valid(fromCommand)) { Use(fromCommand); return; }
        string saved = Settings.Read("language", "");
        Use(Valid(saved) ? saved : Settings.FileExists() ? INITIAL : FromWindows());
    }

    public static void Use(string code) {
        if (!Valid(code)) return;
        culture = new CultureInfo(CULTURE[Array.IndexOf(CODES, code)]);
        current = code;
    }

    public static void Choose(string code) {
        if (!Valid(code)) return;
        Use(code);
        Settings.Write("language", code);
    }

    static Dictionary<string, string> Table(string code) {
        lock (tables) {
            Dictionary<string, string> t;
            if (tables.TryGetValue(code, out t)) return t;
            t = new Dictionary<string, string>();
            try {
                using (var s = Program.Resource("locales." + code + ".txt"))
                    if (s != null) using (var r = new StreamReader(s, new UTF8Encoding(false))) t = Read(r.ReadToEnd());
            } catch (Exception ex) { Program.Log("language " + code + ": " + ex.Message); }
            tables[code] = t;
            return t;
        }
    }

    public static Dictionary<string, string> Read(string text) {
        var t = new Dictionary<string, string>();
        foreach (var row in text.Split('\n')) {
            string r = row.TrimEnd('\r');
            if (r.Length == 0 || r[0] == '#') continue;
            int i = r.IndexOf('=');
            if (i <= 0) continue;
            t[r.Substring(0, i)] = r.Substring(i + 1).Replace("\\n", "\n");
        }
        return t;
    }

    static string Find(string key) {
        string v;
        if (Table(current).TryGetValue(key, out v)) return v;
        if (Table(FALLBACK).TryGetValue(key, out v)) return v;
        if (Table(INITIAL).TryGetValue(key, out v)) return v;
        return key;
    }

    public static string T(string key, params object[] pairs) { return Fill(Find(key), pairs); }

    public static string N(string key, long n, params object[] pairs) {
        bool one = current == "fr" ? n == 0 || n == 1 : n == 1;
        var all = new object[pairs.Length + 2];
        all[0] = "count"; all[1] = Number(n);
        Array.Copy(pairs, 0, all, 2, pairs.Length);
        return T(key + (one ? ".one" : ".many"), all);
    }

    public static string Fixed(string key, params object[] pairs) { return Fill(Raw(INITIAL, key) ?? key, pairs); }

    public static string Number(long n) { return n.ToString("N0", culture); }

    public static string Fill(string text, object[] pairs) {
        if (pairs == null || pairs.Length < 2 || text.IndexOf('{') < 0) return text;
        var sb = new StringBuilder(text.Length + 16);
        int i = 0;
        while (i < text.Length) {
            char c = text[i];
            int end = c == '{' ? text.IndexOf('}', i + 1) : -1;
            if (end < 0) { sb.Append(c); i++; continue; }
            string name = text.Substring(i + 1, end - i - 1);
            bool put = false;
            for (int k = 0; k + 1 < pairs.Length; k += 2)
                if (string.Equals(pairs[k] as string, name)) { sb.Append(Convert.ToString(pairs[k + 1], CultureInfo.InvariantCulture)); put = true; break; }
            if (put) i = end + 1; else { sb.Append(c); i++; }
        }
        return sb.ToString();
    }
    static int Letters(string v) {
        int n = 0; bool inside = false;
        foreach (char c in v) { if (c == '{') inside = true; else if (c == '}') inside = false; else if (!inside && char.IsLetter(c)) n++; }
        return n;
    }
    public static IEnumerable<string> Keys(string code) { return Table(code).Keys; }
    public static string Raw(string code, string key) { string v; return Table(code).TryGetValue(key, out v) ? v : null; }
}
