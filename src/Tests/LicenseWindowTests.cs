using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public partial class LicenseWindow {
    public void UseTest(string text) { tLicense.Text = text; Use(); }
    public void RemoveTest() { Remove(); }
    public string StateTest { get { return lState.Text; } }
    public string SentenceTest { get { return lSentence.Text; } }
    public string BoxTest { get { return tLicense.Text; } }
    public bool YesRemovesTest { get { return bRemove.Enabled; } }

    public void ColorsTest() { Colors(); }

    public List<string> SpacesTest(string of) {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "OK    " : "ERROR ") + of + what);
        var inside = new[] { lState, lDetails, lSentence };
        foreach (var l in inside) if (l.Text.Length > 0) {
            int needs = TextRenderer.MeasureText(l.Text, l.Font, new Size(l.Width, 0), ROWS).Height;
            if (l.Height < needs || l.Bottom > gState.Height) r.Add("ERROR " + of + "sentence cut off: " + l.Text);
        }
        Check(!lState.Bounds.IntersectsWith(lDetails.Bounds) && !(lSentence.Visible && lDetails.Bounds.IntersectsWith(lSentence.Bounds)), "the status sentences are not touched");
        var outside = new Control[] { gState, gPaste, lSale, bClose };
        bool fine = true;
        for (int i = 0; i < outside.Length; i++) {
            if (!ClientRectangle.Contains(outside[i].Bounds)) fine = false;
            for (int j = i + 1; j < outside.Length; j++) if (outside[i].Bounds.IntersectsWith(outside[j].Bounds)) fine = false;
        }
        foreach (var b in new[] { bUse, bRemove }) {
            if (b.Right > gPaste.Width - S1(8) || b.Bottom > gPaste.Height) fine = false;
            if (TextRenderer.MeasureText(b.Text, Font).Width > b.Width) fine = false;
        }
        Check(fine, "boxes and buttons stay in their place");
        return r;
    }
}
