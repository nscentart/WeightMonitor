using System;
using System.Drawing;
using System.Windows.Forms;

public class LanguageChoice : Form {
    public string Code;
    readonly Label question;
    readonly Button forward;

    public LanguageChoice(string proposal) {
        float k;
        using (var g = CreateGraphics()) k = g.DpiX / 96f;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Code = Language.Valid(proposal) ? proposal : Language.FALLBACK;
        Language.Use(Code);
        Text = "WeightMonitor";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        Theme.Follow(this); BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT;
        Font = new Font("Segoe UI", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
        ClientSize = new Size(S(340), S(310));
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        question = new Label { Font = new Font("Segoe UI", 13f * 96f / 72f * k, FontStyle.Bold, GraphicsUnit.Pixel), Bounds = new Rectangle(S(24), S(18), S(292), S(34)) };
        var list = new ListBox { BorderStyle = BorderStyle.FixedSingle, BackColor = Drawing.CAPSULE, ForeColor = Drawing.TEXT, IntegralHeight = false, ItemHeight = S(32),
                                   DrawMode = DrawMode.OwnerDrawFixed, Bounds = new Rectangle(S(24), S(58), S(292), 5 * S(32) + S(2)) };
        foreach (var c in Language.CODES) list.Items.Add(Language.NameOf(c));
        list.DrawItem += (o, e) => {
            if (e.Index < 0) return;
            bool choice = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(choice ? Drawing.ACCENT : Drawing.CAPSULE)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, (string)list.Items[e.Index], Font, e.Bounds, choice ? Drawing.ON_ACCENT : Drawing.TEXT, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.LeftAndRightPadding);
        };
        forward = new Button { Bounds = new Rectangle(S(24), S(58) + 5 * S(32) + S(14), S(292), S(40)), FlatStyle = FlatStyle.Flat, BackColor = Drawing.CAPSULE, ForeColor = Drawing.TEXT, DialogResult = DialogResult.OK };
        forward.FlatAppearance.BorderColor = Drawing.BORDER;
        AcceptButton = forward;
        list.SelectedIndex = Array.IndexOf(Language.CODES, Code);
        list.SelectedIndexChanged += delegate {
            if (list.SelectedIndex < 0) return;
            Code = Language.CODES[list.SelectedIndex];
            Language.Use(Code);
            Texts();
        };
        Controls.AddRange(new Control[] { question, list, forward });
        Texts();
    }

    void Texts() { question.Text = Language.T("language.choose"); forward.Text = Language.T("language.continue"); }
}
