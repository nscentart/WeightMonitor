using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class PawnIOChoice : Form {
    public bool Yes;

    public PawnIOChoice() {
        float k;
        using (var g = CreateGraphics()) k = g.DpiX / 96f;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Text = Language.T("pawnio.window_title");
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.None;
        Theme.Follow(this); BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT;
        Font = new Font("Segoe UI", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
        ClientSize = new Size(S(560), S(404));
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var title = new Label { Text = Language.T("pawnio.title"), Font = new Font("Segoe UI", 15f * 96f / 72f * k, FontStyle.Bold, GraphicsUnit.Pixel),
                                 AutoSize = true, Location = new Point(S(22), S(18)) };
        string explain = Language.T("pawnio.explanation");
        int textHeight = Math.Max(S(226), TextRenderer.MeasureText(explain, Font, new Size(S(512), 0), TextFormatFlags.WordBreak).Height + S(6));
        var text = new Label { Text = explain, Bounds = new Rectangle(S(24), S(58), S(512), textHeight) };
        int ys = S(66) + textHeight;
        ClientSize = new Size(S(560), ys + 2 * S(46) + S(8) + S(12));
        var yes = Button(Language.T("pawnio.yes"), new Rectangle(S(24), ys, S(512), S(46)));
        var no = Button(Language.T("pawnio.no"), new Rectangle(S(24), ys + S(54), S(512), S(46)));
        yes.Click += delegate { Yes = true; DialogResult = DialogResult.OK; };
        no.Click += delegate { Yes = false; DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { title, text, yes, no });
        Shown += delegate { ActiveControl = null; };
    }

    static Button Button(string text, Rectangle r) {
        var b = new Button { Text = text, Bounds = r, FlatStyle = FlatStyle.Flat, BackColor = Drawing.CAPSULE, ForeColor = Drawing.TEXT };
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = Drawing.BORDER;
        return b;
    }
}
