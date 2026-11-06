using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public partial class UpdateWindow : Form {
    readonly float k;
    readonly Label title, below, lFingerprint, lNeeds, how;
    readonly TextBox tFingerprint, notes;
    readonly Button[] buttons3;
    readonly PictureBox logo;

    public UpdateWindow(UpdateInfo n) : this(n, 0f) { }

    public UpdateWindow(UpdateInfo n, float scale) {
        if (scale > 0) k = scale;
        else using (var g = CreateGraphics()) k = g.DpiX / 96f;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Text = Language.T("update.title");
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; TopMost = true;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        Theme.Follow(this); BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT;
        Font = new Font("Segoe UI", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
        ClientSize = new Size(S(560), S(400));
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        const TextFormatFlags ROWS = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        Func<string, Font, int, int> Alt = (t, f, w) => TextRenderer.MeasureText(t, f, new Size(w, 0), ROWS).Height + S(4);
        int x = S(24), W = S(512);

        logo = new PictureBox { Image = Drawing.Logo(), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(S(24), S(22), S(48), S(48)) };
        title = new Label { Text = Language.T("update.released", "version", n.Label), Font = new Font("Segoe UI", 15f * 96f / 72f * k, FontStyle.Bold, GraphicsUnit.Pixel),
                             AutoSize = true, UseMnemonic = false, Location = new Point(S(84), S(20)) };
        string belowText = Language.T("update.you_have", "version", Program.VERSION);
        below = new Label { Text = belowText, ForeColor = Drawing.MUTED, UseMnemonic = false, Bounds = new Rectangle(S(86), S(54), S(450), Alt(belowText, Font, S(450))) };
        int y = Math.Max(S(90), below.Bottom + S(8));

        var fingerprint = UpdateChecker.Fingerprint(n.Digest, n.Note);
        Font fingerprintFont = new Font("Consolas", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
        string fingerprintText = fingerprint.State == FingerprintState.Good ? Language.T("update.fingerprint") : fingerprint.State == FingerprintState.Different ? Language.T("update.fingerprint_mismatch") : Language.T("update.fingerprint_missing");
        Font fEt = fingerprint.State == FingerprintState.Different ? new Font(Font, FontStyle.Bold) : Font;
        lFingerprint = new Label { Text = fingerprintText, UseMnemonic = false, Font = fEt, ForeColor = fingerprint.State == FingerprintState.Different ? Drawing.RED : (fingerprint.State == FingerprintState.None ? Drawing.AMBER : Drawing.MUTED) };
        lFingerprint.Bounds = new Rectangle(x, y, W, Alt(fingerprintText, fEt, W));
        y = lFingerprint.Bottom;
        if (fingerprint.State == FingerprintState.Good) {
            tFingerprint = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Font = fingerprintFont, Text = fingerprint.Value, WordWrap = true,
                                      BackColor = Drawing.BACKGROUND, ForeColor = Drawing.TEXT };
            tFingerprint.Bounds = new Rectangle(x, y, W, Alt(fingerprint.Value, fingerprintFont, W - S(6)));
            tFingerprint.Click += delegate { tFingerprint.SelectAll(); };
            y = tFingerprint.Bottom;
            string needs = Language.T("update.fingerprint_purpose");
            lNeeds = new Label { Text = needs, UseMnemonic = false, ForeColor = Drawing.MUTED, Bounds = new Rectangle(x, y, W, Alt(needs, Font, W)) };
            y = lNeeds.Bottom;
        }
        y += S(8);

        notes = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
                             Text = UpdateChecker.NotesText(n.Note),
                             BackColor = Drawing.FIELD, ForeColor = Drawing.TEXT, Bounds = new Rectangle(x, y, W, S(188)) };
        string howText = Language.T("update.how");
        int howHeight = Math.Max(S(44), Alt(howText, Font, W));
        how = new Label { Text = howText, ForeColor = Drawing.MUTED, UseMnemonic = false, Bounds = new Rectangle(x, notes.Bottom + S(8), W, howHeight) };
        int yb = how.Bottom + S(8), hb = S(40);
        string[] buttons = { Language.T("update.skip"), Language.T("update.later"), Language.T("update.open_page") };
        int[] width = { Math.Max(S(150), Wide(buttons[0], S(20))), Math.Max(S(100), Wide(buttons[1], S(20))), Math.Max(S(190), Wide(buttons[2], S(20))) };
        int space = S(10), rest = S(512) - 2 * space - width[0] - width[1] - width[2];
        if (rest >= 0) width[2] += rest;
        else { hb = S(58); width[1] = Math.Max(S(100), Wide(buttons[1], S(20))); width[0] = (S(512) - 2 * space - width[1]) / 2; width[2] = S(512) - 2 * space - width[0] - width[1]; }
        ClientSize = new Size(S(560), yb + hb + S(22));
        var skip = Button(buttons[0], new Rectangle(S(24), yb, width[0], hb), false, DialogResult.Ignore);
        var after = Button(buttons[1], new Rectangle(S(24) + width[0] + space, yb, width[1], hb), false, DialogResult.Cancel);
        var page = Button(buttons[2], new Rectangle(S(24) + width[0] + width[1] + 2 * space, yb, width[2], hb), true, DialogResult.OK);
        buttons3 = new[] { skip, after, page };
        Controls.AddRange(new Control[] { logo, title, below, lFingerprint, notes, how, skip, after, page });
        if (tFingerprint != null) Controls.Add(tFingerprint);
        if (lNeeds != null) Controls.Add(lNeeds);
        AcceptButton = page; CancelButton = after;
        Shown += delegate { notes.SelectionLength = 0; page.Focus(); };
    }

    int Wide(string text, int margin) { return TextRenderer.MeasureText(text, Font).Width + margin; }

    static Button Button(string text, Rectangle r, bool primary, DialogResult result) {
        var b = new Button { Text = text, Bounds = r, FlatStyle = FlatStyle.Flat, DialogResult = result,
                             BackColor = primary ? Drawing.ACCENT : Drawing.CAPSULE, ForeColor = primary ? Drawing.ON_ACCENT : Drawing.TEXT };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Drawing.BORDER;
        return b;
    }
}
