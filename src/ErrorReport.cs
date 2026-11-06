using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public class ErrorReport : Form {
    public const string REPO = "https://github.com/nscentart/WeightMonitor";
    const int LOG_ROWS = 200, URL_MAX = 7000;
    readonly string text;

    public static void ShowReport() {
        var summary = Program.Sensors == null ? new List<string>() : Program.Sensors.Summary();
        var network = Program.Network == null ? new string[0] : Program.Network.Diagnosis();
        using (var f = new ErrorReport(BuildText(summary, network))) f.ShowDialog();
    }

    const string PATH_START = @"(?:\b[A-Za-z]:[\\/]|\\\\[^\s\\]+\\)";
    public static string Clean(string s) {
        s = System.Text.RegularExpressions.Regex.Replace(s, "['\"«]" + PATH_START + "[^'\"»\r\n]*['\"»]", "'" + Language.Fixed("fixed.report.path") + "'");
        s = System.Text.RegularExpressions.Regex.Replace(s, PATH_START + "[^\r\n]*", Language.Fixed("fixed.report.path"));
        s = Legacy.CleanLog(s, Language.Fixed("fixed.report.program"));
        s = System.Text.RegularExpressions.Regex.Replace(s, "window \"[^\"\r\n]*\" of process \\S+", Language.Fixed("fixed.report.program"));
        var replacements = new List<KeyValuePair<string, string>>();
        try { replacements.Add(new KeyValuePair<string, string>(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Language.Fixed("fixed.report.user"))); } catch { }
        try { replacements.Add(new KeyValuePair<string, string>(Environment.UserDomainName + "\\" + Environment.UserName, Language.Fixed("fixed.report.user"))); } catch { }
        try { replacements.Add(new KeyValuePair<string, string>(Environment.MachineName, Language.Fixed("fixed.report.computer"))); } catch { }
        try { replacements.Add(new KeyValuePair<string, string>(Environment.UserName, Language.Fixed("fixed.report.user"))); } catch { }
        foreach (var kv in replacements)
            if (!string.IsNullOrEmpty(kv.Key) && kv.Key.Length >= 3)
                s = System.Text.RegularExpressions.Regex.Replace(s, System.Text.RegularExpressions.Regex.Escape(kv.Key), kv.Value,
                                                                 System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return s;
    }

    static IEnumerable<string> LastRows(string file, int count) {
        try {
            if (!File.Exists(file)) return new string[0];
            var r = File.ReadAllLines(file, Encoding.UTF8);
            return r.Skip(Math.Max(0, r.Length - count));
        } catch (Exception ex) { return new[] { Language.Fixed("fixed.report.log_unreadable", "message", ex.Message) }; }
    }

    public static string BuildText(IEnumerable<string> summary, IEnumerable<string> network) {
        var sb = new StringBuilder();
        sb.AppendLine("WeightMonitor " + Program.VERSION + ", " + Language.Fixed(Installer.Installed() ? "fixed.report.installed" : "fixed.report.not_installed")
                      + ", " + Language.Fixed(Program.Admin() ? "fixed.report.admin" : "fixed.report.no_admin"));
        sb.AppendLine("Windows: " + Environment.OSVersion.VersionString + ", .NET " + Environment.Version);
        sb.AppendLine(Language.Fixed("fixed.report.date", "date", DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
        sb.AppendLine();
        sb.AppendLine(Language.Fixed("fixed.report.hardware"));
        foreach (var r in summary) sb.AppendLine(r);
        foreach (var r in network) sb.AppendLine(r);
        sb.AppendLine();
        sb.AppendLine(Language.Fixed("fixed.report.log", "count", LOG_ROWS));
        string log = Path.Combine(Settings.Folder, "WeightMonitor.log");
        var rows = Legacy.LogFiles(Settings.LegacyFolder, Settings.Folder).Concat(new[] { log + ".old", log })
            .SelectMany(f => LastRows(f, LOG_ROWS)).Where(r => !r.Contains(" capture test: ")).ToList();
        rows = rows.Skip(Math.Max(0, rows.Count - LOG_ROWS)).ToList();
        if (rows.Count == 0) sb.AppendLine(Language.Fixed("fixed.report.no_errors"));
        foreach (var r in rows) sb.AppendLine(r);
        return Clean(sb.ToString());
    }

    public ErrorReport(string text) {
        this.text = text;
        float k;
        using (var g = CreateGraphics()) k = g.DpiX / 96f;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Text = Language.T("menu.send_log");
        FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        Theme.Follow(this); BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT;
        Font = new Font("Segoe UI", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
        ClientSize = new Size(S(720), S(560));
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        string openText = Language.T("report.open"), explainText = Language.T("report.explanation", "open", openText);
        int explainHeight = Math.Max(S(56), TextRenderer.MeasureText(explainText, Font, new Size(S(688), 0), TextFormatFlags.WordBreak).Height + S(4));
        var explain = new Label {
            Text = explainText,
            Bounds = new Rectangle(S(16), S(12), S(688), explainHeight), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        var box = new TextBox {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = text.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"),
            Font = new Font("Consolas", 9f * 96f / 72f * k, GraphicsUnit.Pixel), BackColor = Drawing.FIELD, ForeColor = Drawing.TEXT,
            BorderStyle = BorderStyle.FixedSingle, Bounds = new Rectangle(S(16), S(14) + explainHeight, S(688), S(476) - explainHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        int yb = S(504), hb = S(40);
        string closeText = Language.T("common.close");
        int lCopy = Math.Max(S(110), TextRenderer.MeasureText(Language.T("report.copied"), Font).Width + S(20));
        int lClose = Math.Max(S(94), TextRenderer.MeasureText(closeText, Font).Width + S(20)), lOpen = Math.Max(S(130), TextRenderer.MeasureText(openText, Font).Width + S(20));
        var copy = Button(Language.T("report.copy"), new Rectangle(S(16), yb, lCopy, hb), false);
        var open = Button(openText, new Rectangle(S(704) - lClose - S(10) - lOpen, yb, lOpen, hb), true);
        var close = Button(closeText, new Rectangle(S(704) - lClose, yb, lClose, hb), false);
        foreach (var b in new[] { copy, open, close }) b.Anchor = AnchorStyles.Bottom | (b == copy ? AnchorStyles.Left : AnchorStyles.Right);
        close.DialogResult = DialogResult.Cancel;
        copy.Click += delegate { try { Clipboard.SetText(this.text); copy.Text = Language.T("report.copied"); } catch { } };
        open.Click += delegate { Open(); Close(); };
        Controls.AddRange(new Control[] { explain, box, copy, open, close });
        Shown += delegate { box.SelectionStart = 0; box.SelectionLength = 0; open.Focus(); };
        CancelButton = close;
    }

    static Button Button(string text, Rectangle r, bool primary) {
        var b = new Button { Text = text, Bounds = r, FlatStyle = FlatStyle.Flat,
                             BackColor = primary ? Drawing.ACCENT : Drawing.CAPSULE, ForeColor = primary ? Drawing.ON_ACCENT : Drawing.TEXT };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Drawing.BORDER;
        return b;
    }

    void Open() {
        string title = Language.Fixed("fixed.report.title", "version", Program.VERSION, "date", DateTime.Now.ToString("dd/MM/yyyy"));
        string body = "```\n" + text + "\n```";
        string url = REPO + "/issues/new?labels=log&title=" + Uri.EscapeDataString(title) + "&body=";
        if ((url + Uri.EscapeDataString(body)).Length > URL_MAX) {
            try { Clipboard.SetText(body); } catch { }
            body = Language.T("report.paste") + "\n\n";
        }
        url += Uri.EscapeDataString(body);
        OpenInBrowser(url);
    }

    public static void OpenInBrowser(string url) {
        try { Process.Start(Installer.Explorer(), "\"" + url + "\""); } catch (Exception ex) { Program.Log("browser: " + ex.Message); }
    }
}
