using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class ConfirmClose : Form {
    public ProgramCloser.Choice Chosen = ProgramCloser.Choice.Cancel;

    public ConfirmClose(string name, int windows, bool withDetails) {
        float k;
        using (var g = CreateGraphics()) k = g.DpiX / 96f;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Text = "WeightMonitor";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        Theme.Follow(this); BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT;
        Font = new Font("Segoe UI", 10f * 96f / 72f * k, GraphicsUnit.Pixel);
                try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var title = new Label { Text = Language.T("close.title", "name", name), Font = new Font("Segoe UI", 15f * 96f / 72f * k, FontStyle.Bold, GraphicsUnit.Pixel),
                                 AutoEllipsis = true, Bounds = new Rectangle(S(22), S(16), S(436), S(34)) };
        var work = new Label { Text = Language.T("close.work"), ForeColor = Drawing.AMBER, Font = new Font("Segoe UI Semibold", 10f * 96f / 72f * k, GraphicsUnit.Pixel), Bounds = new Rectangle(S(24), S(56), S(432), S(24)) };
        string howText = Language.T("close.how") + (windows > 1 ? "\n" + Language.T("close.windows", "count", windows) : "");
        int howHeight = Math.Max(S(windows > 1 ? 74 : 48), TextRenderer.MeasureText(howText, Font, new Size(S(432), 0), TextFormatFlags.WordBreak).Height + S(4));
        var how = new Label { Text = howText, Bounds = new Rectangle(S(24), S(82), S(432), howHeight) };
        int yT = S(82) + howHeight + S(16), hT = S(38);
        ClientSize = new Size(S(480), yT + hT + S(16));
        string tClose = Language.T("close.button"), tDetails = Language.T("close.details"), tCancel = Language.T("common.cancel");
        int lClose = Math.Max(S(170), TextRenderer.MeasureText(tClose, Font).Width + S(16)), lDetail = withDetails ? Math.Max(S(130), TextRenderer.MeasureText(tDetails, Font).Width + S(16)) : 0,
            lCancel = Math.Max(S(100), TextRenderer.MeasureText(tCancel, Font).Width + S(16)), gap = S(8), total = lClose + lDetail + lCancel + (withDetails ? 2 : 1) * gap;
        if (total > S(432)) { hT = S(54); ClientSize = new Size(S(480), yT + hT + S(16)); int sta = S(432) - (withDetails ? 2 : 1) * gap; lClose = lClose * sta / (lClose + lDetail + lCancel); lDetail = withDetails ? lDetail * sta / (lDetail + lCancel + lClose) : 0; lCancel = sta - lClose - lDetail; }
        else lCancel += S(432) - total;
        var close = Button(tClose, new Rectangle(S(24), yT, lClose, hT));
        var details = Button(tDetails, new Rectangle(S(24) + lClose + gap, yT, lDetail, hT));
        var cancel = Button(tCancel, new Rectangle(S(24) + lClose + gap + (withDetails ? lDetail + gap : 0), yT, lCancel, hT));
        close.Click += delegate { Chosen = ProgramCloser.Choice.Close; DialogResult = DialogResult.OK; };
        details.Click += delegate { Chosen = ProgramCloser.Choice.Details; DialogResult = DialogResult.OK; };
        cancel.Click += delegate { Chosen = ProgramCloser.Choice.Cancel; DialogResult = DialogResult.Cancel; };
        CancelButton = cancel;
        Controls.AddRange(new Control[] { title, work, how, close, cancel });
        if (withDetails) Controls.Add(details);
        Shown += delegate { ActiveControl = null; };
    }

    static Button Button(string text, Rectangle r) {
        var b = new Button { Text = text, Bounds = r, FlatStyle = FlatStyle.Flat, BackColor = Drawing.CAPSULE, ForeColor = Drawing.TEXT, TabStop = false };
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = Drawing.BORDER;
        return b;
    }
}
