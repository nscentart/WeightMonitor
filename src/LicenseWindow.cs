using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public partial class LicenseWindow : Form {
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    public static LicenseWindow Current;

    readonly SettingsWindow.Group gState = new SettingsWindow.Group(), gPaste = new SettingsWindow.Group();
    readonly Label lState = new Label { UseMnemonic = false }, lDetails = new Label { UseMnemonic = false }, lSentence = new Label { UseMnemonic = false }, lSale = new Label { UseMnemonic = false };
    readonly TextBox tLicense = new TextBox { Multiline = true, BorderStyle = BorderStyle.FixedSingle, WordWrap = true };
    readonly Button bUse = SettingsWindow.Button(true), bRemove = SettingsWindow.Button(false), bClose = SettingsWindow.Button(false);
    float scale;
    string sentence = "";
    bool problemSentence;

    public static void Open(Form above, float scale) {
        using (var f = new LicenseWindow(scale)) {
            f.StartPosition = FormStartPosition.CenterParent;
            Current = f;
            try { f.ShowDialog(above); } finally { Current = null; }
        }
    }

    public LicenseWindow(float scale) {
        this.scale = scale;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        gState.Controls.AddRange(new Control[] { lState, lDetails, lSentence });
        gPaste.Controls.AddRange(new Control[] { tLicense, bUse, bRemove });
        Controls.AddRange(new Control[] { gState, gPaste, lSale, bClose });
        AcceptButton = bUse; CancelButton = bClose;
        bClose.Click += delegate { Close(); };
        bUse.Click += delegate { Use(); };
        bRemove.Click += delegate { Remove(); };
        if (LicenseLabel.Problem(LicenseLabel.Current) != "") { sentence = LicenseLabel.Problem(LicenseLabel.Current) + " " + LicenseLabel.Stays(); problemSentence = true; }
        Colors();
        Texts();
    }

    void Use() {
        bool ok;
        string m = LicenseLabel.Use(tLicense.Text, out ok);
        if (ok) tLicense.Clear();
        Sentence(m, !ok);
    }
    void Remove() { bool ok; string m = LicenseLabel.Remove(out ok); Sentence(m, !ok); }

    public void Sentence(string text, bool problem) { sentence = text; problemSentence = problem; Texts(); Colors(); }

    void Texts() {
        Text = Language.T("license.title");
        gState.Title = Language.T("license.group.status"); gPaste.Title = Language.T("license.group.paste");
        var e = LicenseLabel.Current;
        lState.Text = LicenseLabel.Caption();
        lDetails.Text = e.Valid ? Language.T("license.details", "number", e.Number, "date", e.Data) : Language.T("license.personal_explain");
        lSentence.Text = sentence;
        bUse.Text = Language.T("license.use"); bRemove.Text = Language.T("license.remove"); bClose.Text = Language.T("common.close");
        bRemove.Enabled = LicenseLabel.HasFile();
        lSale.Text = Language.T("license.sale");
        Arrange(scale);
    }
    void Colors() {
        BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT;
        gState.BackColor = gPaste.BackColor = Drawing.BACKGROUND;
        foreach (var l in new[] { lState, lDetails, lSentence }) l.BackColor = SettingsWindow.PANEL;
        lState.ForeColor = Drawing.TEXT; lDetails.ForeColor = Drawing.MUTED; lSentence.ForeColor = problemSentence ? Drawing.AMBER : Drawing.TEXT;
        lSale.BackColor = Drawing.BACKGROUND; lSale.ForeColor = Drawing.MUTED;
        tLicense.BackColor = Drawing.CAPSULE; tLicense.ForeColor = Drawing.TEXT;
        SettingsWindow.Colorize(bUse, true);
        foreach (var b in new[] { bRemove, bClose }) SettingsWindow.Colorize(b, false);
        if (IsHandleCreated) Theme.Title(Handle);
        Invalidate(true);
    }

    const TextFormatFlags ROWS = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;

    void Arrange(float k) {
        scale = k;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Font = new Font("Segoe UI", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
        lState.Font = new Font("Segoe UI Semibold", 11.5f * 96f / 72f * k, GraphicsUnit.Pixel);
        foreach (var g in new[] { gState, gPaste }) g.ApplyScale(k);

        int margin = S(16), x = S(14), start = S(38), end = S(10), space = S(8), buttonHeight = S(32);
        Func<string, int> Wide = t => TextRenderer.MeasureText(t, Font).Width + S(20);
        int buttonsWidth = Math.Max(Wide(bUse.Text), S(120)) + space + Math.Max(Wide(bRemove.Text), S(120));
        int width = Math.Max(S(480), buttonsWidth + 2 * margin + 2 * x), inner = width - 2 * margin - 2 * x;
        Func<string, Font, int> High = (t, f) => string.IsNullOrEmpty(t) ? 0 : TextRenderer.MeasureText(t, f, new Size(inner, 0), ROWS).Height + S(2);

        int y = margin, yi = start, a = High(lState.Text, lState.Font);
        lState.Bounds = new Rectangle(x, yi, inner, a); yi += a + S(2);
        a = High(lDetails.Text, Font);
        lDetails.Bounds = new Rectangle(x, yi, inner, a); yi += a;
        a = High(lSentence.Text, Font);
        lSentence.Visible = a > 0;
        lSentence.Bounds = new Rectangle(x, yi + S(6), inner, a); if (a > 0) yi += S(6) + a;
        gState.Bounds = new Rectangle(margin, y, width - 2 * margin, yi + end);
        y += gState.Height + S(10);

        tLicense.Font = Font;
        int boxHeight = S(66);
        tLicense.Bounds = new Rectangle(x, start, inner, boxHeight);
        int lUse = Math.Max(Wide(bUse.Text), S(120)), yt = start + boxHeight + space;
        bUse.Bounds = new Rectangle(x, yt, lUse, buttonHeight);
        bRemove.Bounds = new Rectangle(x + lUse + space, yt, Math.Max(Wide(bRemove.Text), S(120)), buttonHeight);
        gPaste.Bounds = new Rectangle(margin, y, width - 2 * margin, yt + buttonHeight + end + S(4));
        y += gPaste.Height + S(10);

        int saleHeight = TextRenderer.MeasureText(lSale.Text, Font, new Size(width - 2 * margin, 0), ROWS).Height + S(2);
        lSale.Bounds = new Rectangle(margin, y, width - 2 * margin, saleHeight);
        y += saleHeight + S(10);

        int lClose = Math.Max(S(92), Wide(bClose.Text));
        bClose.Bounds = new Rectangle(width - margin - lClose, y, lClose, buttonHeight);
        ClientSize = new Size(width, y + buttonHeight + margin);
        Invalidate(true);
    }
    int S1(int v) { return (int)Math.Round(v * scale); }

    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x02E0) {
            var r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            Location = new Point(r.L, r.T);
            Arrange((m.WParam.ToInt64() & 0xFFFF) / 96f);
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        Theme.Title(Handle);
    }
}
