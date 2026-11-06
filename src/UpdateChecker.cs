using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public static partial class UpdateChecker {
    static string API = "https://api.github.com/repos/nscentart/WeightMonitor/releases/latest";
    const string GOOD_ADDRESS = "https://github.com/nscentart/WeightMonitor/";
    const string LAST_PAGE = GOOD_ADDRESS + "releases/latest";

    public static bool GoodAddress(string url) {
        Uri u;
        if (!(Uri.TryCreate(url, UriKind.Absolute, out u) && Regex.IsMatch(url, @"\A[A-Za-z0-9:/._-]+\z")
            && u.AbsoluteUri == url && url.StartsWith(GOOD_ADDRESS, StringComparison.Ordinal))) return false;
        string rest = url.Substring(GOOD_ADDRESS.Length);
        return rest == "releases/latest" || (Regex.IsMatch(rest, @"\Areleases/tag/[A-Za-z0-9][A-Za-z0-9._-]*\z") && !rest.Contains(".."));
    }

    static readonly Regex HEXADECIMAL = new Regex(@"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])");

    public static List<string> FingerprintsIn(string notes) {
        var r = new List<string>();
        foreach (Match m in HEXADECIMAL.Matches(notes ?? "")) { string h = m.Value.ToLowerInvariant(); if (!r.Contains(h)) r.Add(h); }
        return r;
    }

    public static string FingerprintFrom(string notes) {
        var l = FingerprintsIn(notes);
        return l.Count == 1 ? l[0] : null;
    }

    public static string FromDigest(string digest) {
        if (digest == null) return null;
        var m = Regex.Match(digest.Trim(), @"\Asha256:([0-9A-Fa-f]{64})\z", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
    }

    public static FingerprintResult Fingerprint(string digest, string notes) {
        string d = FromDigest(digest);
        var l = FingerprintsIn(notes);
        if (l.Count > 1 || (d != null && l.Count == 1 && l[0] != d)) return new FingerprintResult { State = FingerprintState.Different };
        if (d != null) return new FingerprintResult { State = FingerprintState.Good, Value = d };
        if (l.Count == 1) return new FingerprintResult { State = FingerprintState.Good, Value = l[0] };
        return new FingerprintResult { State = FingerprintState.None };
    }

    public static string NotesText(string notes) {
        string t = (notes ?? "").Replace("\r\n", "\n");
        t = Regex.Replace(t, @"<!--.*?-->", "", RegexOptions.Singleline);
        t = Regex.Replace(t, @"</?[A-Za-z][^>]*>", "");
        t = t.Replace("**", "").Replace("`", "");
        t = Regex.Replace(t, @"\n{3,}", "\n\n").Trim('\n');
        return t.Replace("\n", "\r\n");
    }

    public static bool Active() { return Settings.Read("updates", "yes") != "no"; }

    static Version My() { return new Version(Program.VERSION.Split('.').Length == 2 ? Program.VERSION + ".0" : Program.VERSION); }

    public static UpdateInfo Find() {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        var req = (HttpWebRequest)WebRequest.Create(API);
        req.UserAgent = "WeightMonitor/" + Program.VERSION;
        req.Accept = "application/vnd.github+json";
        req.Timeout = 10000;
        string json;
        try {
            using (var r = req.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream())) json = sr.ReadToEnd();
        } catch (WebException ex) {
            var h = ex.Response as HttpWebResponse;
            if (h != null && h.StatusCode == HttpStatusCode.NotFound)
                throw new Exception(Language.T("update.not_public"));
            throw new Exception(Language.T("update.github_silent", "message", ex.Message));
        }
        return FromResponse(json, My());
    }

    public static UpdateInfo FromResponse(string json, Version my) {
        var d = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
        string tag = Convert.ToString(d["tag_name"]).TrimStart('v', 'V');
        Version v;
        if (!Version.TryParse(tag.Split('.').Length == 2 ? tag + ".0" : tag, out v) || v <= my) return null;
        bool exe = false;
        string digest = null;
        foreach (var o in (IEnumerable)d["assets"]) {
            var a = (Dictionary<string, object>)o;
            if (string.Equals(Convert.ToString(a["name"]), "WeightMonitor.exe", StringComparison.OrdinalIgnoreCase)) {
                exe = true;
                if (a.ContainsKey("digest") && a["digest"] != null) digest = Convert.ToString(a["digest"]);
            }
        }
        if (!exe) return null;
        string page = d.ContainsKey("html_url") ? Convert.ToString(d["html_url"]) : "";
        return new UpdateInfo { Version = v, Label = tag, Note = d.ContainsKey("body") ? Convert.ToString(d["body"]) : "", Digest = digest,
                            Page = GoodAddress(page) ? page : LAST_PAGE };
    }

    public static void AtStartup(Control ui) {
        Installer.RemoveTemporary();
        if (!Active()) return;
        var t = new Thread(() => {
            Thread.Sleep(20000);
            try {
                var n = Find();
                if (n == null || Settings.Read("skipped", "") == n.Label) return;
                ui.BeginInvoke((MethodInvoker)delegate { Notify(n); });
            } catch (Exception ex) { Program.Log("updates: " + ex.Message); }
        });
        t.IsBackground = true; t.Start();
    }

    public static void CheckNow() {
        try {
            var n = Find();
            if (n == null) Program.Notice(Language.T("update.latest", "version", Program.VERSION));
            else Notify(n);
        } catch (Exception ex) { Program.Notice(Language.T("update.check_failed", "error", ex.Message)); }
    }

    static void Notify(UpdateInfo n) {
        using (var f = new UpdateWindow(n)) {
            var r = f.ShowDialog();
            if (r == DialogResult.Ignore) Settings.Write("skipped", n.Label);
            if (r == DialogResult.OK) ErrorReport.OpenInBrowser(n.Page);
        }
    }
}
