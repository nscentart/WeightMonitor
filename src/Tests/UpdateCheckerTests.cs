using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

public static partial class UpdateChecker {
    public static void Test(string api, string file) {
        if (api != null) API = api;
        string r;
        try { var n = Find(); r = n == null ? "no newer version" : "new " + n.Label + " " + n.Page; }
        catch (Exception ex) { r = "error: " + ex.Message; }
        File.WriteAllText(file, r);
    }

    public static List<string> WindowTest() {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        string h1 = string.Concat(Enumerable.Repeat("0123456789abcdef", 4)), h2 = string.Concat(Enumerable.Repeat("fedcba9876543210", 4));
        Check(FingerprintFrom("SHA-256: " + h1) == h1, "FingerprintFrom: right notes, the fingerprint is read");
        Check(FingerprintFrom("SHA-256: " + h1.ToUpperInvariant()) == h1, "FingerprintFrom: uppercase, lowercase is returned");
        Check(FingerprintFrom("nothing to see here") == null && FingerprintFrom("") == null && FingerprintFrom(null) == null, "FingerprintFrom: no fingerprint, no value");
        Check(FingerprintFrom(h1 + " and then " + h2) == null, "FingerprintFrom: two different fingerprints, no value");
        Check(FingerprintFrom(h1 + "\n" + h1.ToUpperInvariant()) == h1, "FingerprintFrom: two equal fingerprints, that one counts");
        Check(FingerprintFrom(h1.Substring(0, 63)) == null, "FingerprintFrom: 63 characters, not a fingerprint");
        Check(FingerprintFrom(h1 + "a") == null && FingerprintFrom("a" + h1) == null, "FingerprintFrom: 65 characters, not a fingerprint");
        Check(FingerprintFrom(h1.Substring(0, 63) + "g") == null, "FingerprintFrom: a non-hexadecimal character, not a fingerprint");
        Check(FromDigest("sha256:" + h1) == h1 && FromDigest("SHA256:" + h1.ToUpperInvariant()) == h1, "digest: form sha256:<64 hexadecimal digits>");
        Check(FromDigest(null) == null && FromDigest("") == null && FromDigest("sha1:" + h1) == null && FromDigest("sha256:" + h1.Substring(1)) == null && FromDigest("sha256:" + h1 + "0") == null && FromDigest(h1) == null, "digest: missing or wrong form, no value");
        Func<string, string, FingerprintResult> GetFingerprint = (d, n) => Fingerprint(d, n);
        Check(GetFingerprint("sha256:" + h1, "note").State == FingerprintState.Good && GetFingerprint("sha256:" + h1, "note").Value == h1, "fingerprint: digest only, the digest is shown");
        Check(GetFingerprint(null, "x " + h1).State == FingerprintState.Good && GetFingerprint(null, "x " + h1).Value == h1, "fingerprint: no digest, it is read from the notes");
        Check(GetFingerprint("sha256:" + h1, h1.ToUpperInvariant()).State == FingerprintState.Good, "fingerprint: digest and notes equal, fine");
        Check(GetFingerprint("sha256:" + h1, h2).State == FingerprintState.Different && GetFingerprint("sha256:" + h1, h2).Value == null, "fingerprint: digest and notes different, it is different and none is chosen");
        Check(GetFingerprint(null, h1 + " " + h2).State == FingerprintState.Different, "fingerprint: notes with two different fingerprints, it is different");
        Check(GetFingerprint("sha256:" + h1, h1 + " " + h2).State == FingerprintState.Different, "fingerprint: digest and notes with two fingerprints, it is different");
        Check(GetFingerprint(null, "nothing").State == FingerprintState.None && GetFingerprint("broken", "nothing").State == FingerprintState.None && GetFingerprint(null, h1.Substring(1)).State == FingerprintState.None, "fingerprint: neither of the two, not available");
        const string PAGE = "https://github.com/nscentart/WeightMonitor/releases/tag/v1.2";
        string json = "{\"tag_name\":\"v1.2\",\"body\":\"n\",\"html_url\":\"" + PAGE + "\",\"assets\":[{\"name\":\"other.zip\",\"digest\":\"sha256:" + h2 + "\"},{\"name\":\"WeightMonitor.exe\",\"digest\":\"sha256:" + h1 + "\"}]}";
        var nv = FromResponse(json, new Version("1.1.0"));
        Check(nv != null && nv.Digest == "sha256:" + h1, "GitHub response: the digest of the WeightMonitor.exe asset is taken");
        nv = FromResponse(json.Replace(",\"digest\":\"sha256:" + h1 + "\"", ""), new Version("1.1.0"));
        Check(nv != null && nv.Digest == null, "GitHub response: no digest, no value");
        nv = FromResponse(json.Replace("\"sha256:" + h1 + "\"", "null"), new Version("1.1.0"));
        Check(nv != null && nv.Digest == null, "GitHub response: null digest, no value");
        string t = NotesText("**News**\r\n<details>\r\n<summary>Details</summary>\r\n\r\n- one `two`\r\n</details>\r\n<b>strong</b> <br/> a < b <!-- hidden --> end");
        Check(!t.Contains("<details") && !t.Contains("</details") && !t.Contains("<summary") && !t.Contains("</summary") && !t.Contains("<b>") && !t.Contains("<br") && !t.Contains("hidden") && !t.Contains("**") && !t.Contains("`"), "notes: details, summary, every other tag, comments, ** and backticks are removed");
        Check(t.Contains("Details") && t.Contains("one two") && t.Contains("strong") && t.Contains("a < b") && t.Contains("News"), "notes: the content stays, a lone \"<\" stays");
        Check(NotesText(null) == "" && !NotesText("a\n\n\n\n\nb").Contains("\r\n\r\n\r\n"), "notes: without text, and the extra blank lines are removed");
        const string B = "https://github.com/nscentart/WeightMonitor/";
        foreach (var u in new[] { B + "releases/latest", B + "releases/tag/v1.2", B + "releases/tag/1.2" })
            Check(GoodAddress(u), "page accepted: " + u);
        foreach (var u in new[] { B, B + "releases", B + "releases/", B + "releases/latest/x", B + "releases/tag/", B + "releases/tag/..", B + "releases/tag/v1/../x", B + "releases/download/v1.2/WeightMonitor.exe", B + "issues/1", B + "archive/main.zip", B + "WeightMonitor.exe" })
            Check(!GoodAddress(u), "page rejected: " + u);
        nv = FromResponse(json.Replace(PAGE, B + "releases/download/v1.2/WeightMonitor.exe"), new Version("1.1.0"));
        Check(nv != null && nv.Page == B + "releases/latest", "response with a file to download instead of the page: releases/latest opens");

        string previousLanguage = Language.Code;
        var longTexts = new System.Text.StringBuilder("**News**\n<details>\n<summary>What changes</summary>\n\n");
        for (int i = 0; i < 40; i++) longTexts.Append("- A long example line number " + i + " saying what has changed for people who use the program every day.\n");
        longTexts.Append("</details>\n");
        string longNotes = longTexts.ToString();
        var cases = new[] {
            new { name = "fingerprint in the notes", d = (string)null, n = longNotes + "SHA-256: " + h1, state = FingerprintState.Good, val = h1 },
            new { name = "digest and notes equal", d = "sha256:" + h1, n = longNotes + "SHA-256: " + h1, state = FingerprintState.Good, val = h1 },
            new { name = "digest only", d = "sha256:" + h2, n = longNotes, state = FingerprintState.Good, val = h2 },
            new { name = "digest and notes different", d = "sha256:" + h2, n = longNotes + "SHA-256: " + h1, state = FingerprintState.Different, val = "" },
            new { name = "no fingerprint", d = (string)null, n = longNotes, state = FingerprintState.None, val = "" } };
        try {
            foreach (var c in Language.CODES) {
                Language.Use(c);
                foreach (var scale in new[] { 1f, 1.5f })
                    foreach (var cs in cases) {
                        string of = c + ", scale " + scale + ", " + cs.name + ": ";
                        using (var f = new UpdateWindow(new UpdateInfo { Label = "1.2", Note = cs.n, Digest = cs.d }, scale)) {
                            r.AddRange(f.SpacesTest(of));
                            Check(f.NotesTest.IndexOf("<details", StringComparison.OrdinalIgnoreCase) < 0 && f.NotesTest.IndexOf("<summary", StringComparison.OrdinalIgnoreCase) < 0 && f.NotesTest.Contains("What changes"), of + "the notes do not show the tags and keep the content");
                            if (cs.state == FingerprintState.Good)
                                Check(f.FingerprintTest == cs.val && f.FingerprintTest.Length == 64 && f.SelectableFingerprintTest && f.NoticeTest == Language.T("update.fingerprint"), of + "fixed line with the fingerprint, selectable and copyable");
                            else if (cs.state == FingerprintState.Different)
                                Check(f.FingerprintTest == "" && f.NoticeTest == Language.T("update.fingerprint_mismatch") && f.NoticeTest.Length > 20, of + "\"different fingerprint\" notice, no fingerprint to trust");
                            else
                                Check(f.FingerprintTest == "" && f.NoticeTest == Language.T("update.fingerprint_missing"), of + "\"fingerprint not available\", no random value");
                        }
                    }
                string needs = Language.T("update.fingerprint_purpose");
                Check(Language.T("update.fingerprint").Length > 5 && needs.Length > 20 && !needs.ToLowerInvariant().Contains("firm") && !needs.ToLowerInvariant().Contains("sign") && !needs.ToLowerInvariant().Contains("signat"), c + ": fingerprint sentence present and without signature words");
            }
        } finally { Language.Use(previousLanguage); }
        return r;
    }
}
