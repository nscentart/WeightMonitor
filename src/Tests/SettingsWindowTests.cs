using System;
using System.Drawing;
using System.Windows.Forms;

public partial class SettingsWindow {
    public void ColorsTest() { Colors(); }

    public bool LicenseClickTest() {
        StartPosition = FormStartPosition.Manual; Location = new Point(-20000, -20000); Show();
        bool open = false;
        using (var t = new System.Windows.Forms.Timer { Interval = 300 }) {
            t.Tick += delegate { t.Stop(); if (LicenseWindow.Current != null) { open = true; LicenseWindow.Current.Close(); } };
            t.Start();
            lLicense.PerformClick();
        }
        Close();
        return open && LicenseWindow.Current == null;
    }

    public System.Collections.Generic.List<string> LicenseCaptionTest(string of, bool whole) {
        var r = new System.Collections.Generic.List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "OK    " : "ERROR ") + of + what);
        Check(lLicense.Right <= ClientSize.Width && lLicense.Bottom <= ClientSize.Height, "Settings: the caption fits in the window");
        Check(!lLicense.Bounds.IntersectsWith(bOk.Bounds) && !lLicense.Bounds.IntersectsWith(bCancel.Bounds) && !lLicense.Bounds.IntersectsWith(bApply.Bounds) && !lLicense.Bounds.IntersectsWith(lVersion.Bounds), "Settings: the caption does not touch the buttons and the name");
        Check(TextRenderer.MeasureText(lVersion.Text, Font).Width <= lVersion.Width, "Settings: the name fits whole");
        if (whole) Check(TextRenderer.MeasureText(lLicense.Text, Font, new Size(9999, 999), TextFormatFlags.NoPrefix).Width <= lLicense.Width, "Settings: the caption fits whole");
        return r;
    }
}
