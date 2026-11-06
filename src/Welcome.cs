using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public class Welcome : Form {
    public bool Install, PawnIO;

    public Welcome(bool fakeWithoutPawnIO = false) {
        float k;
        using (var g = CreateGraphics()) k = g.DpiX / 96f;
        Func<int, int> S = v => (int)Math.Round(v * k);
        Func<float, FontStyle, Font> F = (pt, st) => new Font("Segoe UI", pt * 96f / 72f * k, st, GraphicsUnit.Pixel);
        Text = "WeightMonitor " + Program.VERSION;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
        Theme.Follow(this); BackColor = Drawing.BACKGROUND; ForeColor = Drawing.TEXT; Font = F(10f, FontStyle.Regular);
        ClientSize = new Size(S(560), S(360));
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var logo = new PictureBox { Image = Drawing.Logo(), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(S(24), S(24), S(56), S(56)) };
        var title = new Label { Text = "WeightMonitor", Font = F(16f, FontStyle.Bold), AutoSize = true, Location = new Point(S(92), S(22)) };
        var below = new Label { Text = Language.T("welcome.subtitle"), ForeColor = Drawing.MUTED, AutoSize = true, Location = new Point(S(94), S(58)) };
        bool needs = fakeWithoutPawnIO || !Installer.PawnIOPresent();
        string already = Installer.InstalledVersion();
        string installText = Language.T("common.install"), withoutText = Language.T("welcome.without_install"), cancelText = Language.T("common.cancel");
        string explain = (already != null ? Language.T("welcome.already_installed", "installed", already, "install", installText, "version", Program.VERSION) + "\n\n" : "")
                      + Language.T("welcome.explain_install", "install", installText) + " " + Language.T(needs ? "welcome.asks_driver" : "welcome.driver_present") + "\n\n"
                      + Language.T("welcome.explain_without", "without", withoutText);
        int textHeight = Math.Max(S(190), TextRenderer.MeasureText(explain, Font, new Size(S(512), 0), TextFormatFlags.WordBreak).Height + S(6));
        var text = new Label { Text = explain, Bounds = new Rectangle(S(24), S(96), S(512), textHeight) };
        int yb = S(106) + textHeight, hb = S(40);
        ClientSize = new Size(S(560), yb + hb + S(24));
        int lWithout = Math.Max(S(190), TextRenderer.MeasureText(withoutText, Font).Width + S(20)), lCancel = Math.Max(S(100), TextRenderer.MeasureText(cancelText, Font).Width + S(20)),
            lInstall = Math.Max(S(120), TextRenderer.MeasureText(installText, Font).Width + S(20));
        var portable = Button(withoutText, new Rectangle(S(24), yb, lWithout, hb), false);
        var install = Button(installText, new Rectangle(S(536) - lCancel - S(10) - lInstall, yb, lInstall, hb), true);
        var cancel = Button(cancelText, new Rectangle(S(536) - lCancel, yb, lCancel, hb), false);
        cancel.DialogResult = DialogResult.Cancel;
        install.Click += delegate {
            if (needs) using (var p = new PawnIOChoice()) {
                if (p.ShowDialog(this) != DialogResult.OK) return;
                PawnIO = p.Yes;
            }
            Install = true; DialogResult = DialogResult.OK;
        };
        portable.Click += delegate { Install = false; DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { logo, title, below, text, portable, install, cancel });
        AcceptButton = install; CancelButton = cancel;
    }

    static Button Button(string text, Rectangle r, bool primary) {
        var b = new Button { Text = text, Bounds = r, FlatStyle = FlatStyle.Flat,
                             BackColor = primary ? Drawing.ACCENT : Drawing.CAPSULE, ForeColor = primary ? Drawing.ON_ACCENT : Drawing.TEXT };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Drawing.BORDER;
        return b;
    }
}
