using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

public partial class UpdateWindow {
    public string FingerprintTest { get { return tFingerprint == null ? "" : tFingerprint.Text; } }
    public bool SelectableFingerprintTest { get { return tFingerprint != null && tFingerprint.ReadOnly && tFingerprint.BorderStyle == BorderStyle.None && tFingerprint.Enabled; } }
    public string NoticeTest { get { return lFingerprint.Text; } }
    public string NotesTest { get { return notes.Text; } }

    public List<string> SpacesTest(string of) {
        var r = new List<string>();
        var labels = new List<Control> { title, below, lFingerprint, how };
        if (lNeeds != null) labels.Add(lNeeds);
        if (tFingerprint != null) labels.Add(tFingerprint);
        foreach (var l in labels) {
            if (l == title) { if (l.Right > ClientSize.Width) r.Add("ERROR " + of + "title outside the window"); continue; }
            int wide = l == tFingerprint ? l.Width - (int)Math.Round(6 * k) : l.Width;
            int needs = TextRenderer.MeasureText(l.Text, l.Font, new Size(wide, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            if (l.Height < needs) r.Add("ERROR " + of + "sentence cut off: " + l.Text);
        }
        var all = new List<Control>(labels) { logo, notes };
        all.AddRange(buttons3);
        bool fine = true;
        for (int i = 0; i < all.Count; i++) {
            if (!ClientRectangle.Contains(all[i].Bounds)) { fine = false; r.Add("ERROR " + of + "outside the window: " + all[i].GetType().Name + " " + all[i].Bounds); }
            for (int j = i + 1; j < all.Count; j++)
                if (all[i].Bounds.IntersectsWith(all[j].Bounds)) { fine = false; r.Add("ERROR " + of + "overlapping: " + all[i].GetType().Name + " " + all[i].Bounds + " e " + all[j].GetType().Name + " " + all[j].Bounds); }
        }
        foreach (var b in buttons3)
            if (TextRenderer.MeasureText(b.Text, Font, new Size(b.Width - (int)Math.Round(8 * k), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height > b.Height) { fine = false; r.Add("ERROR " + of + "button cut off: " + b.Text); }
        r.Add((fine ? "OK    " : "ERROR ") + of + "no piece of the window goes out or overlaps");
        return r;
    }
}
