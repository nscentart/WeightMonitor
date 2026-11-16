using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public static partial class SafePaint {
    public const int RESET_AFTER = 3;
    public const int RESET_AGAIN = 60;
    public const string MISSING = "n/d";
    static readonly Dictionary<string, int> streaks = new Dictionary<string, int>();
    public static int Resets;

    public static int Streak(string where) {
        int n;
        lock (streaks) return streaks.TryGetValue(where, out n) ? n : 0;
    }

    public static bool Run(string where, Graphics g, Rectangle r, Color back, Color fore, Action draw) {
        if (Guard(where, draw)) return true;
        Fallback(g, r, back, fore);
        return false;
    }

    public static bool Guard(string where, Action work) {
        try {
            work();
            Succeeded(where);
            return true;
        } catch (Exception ex) {
            Failed(where, ex);
            return false;
        }
    }

    static void Succeeded(string where) {
        int before;
        lock (streaks) {
            if (!streaks.TryGetValue(where, out before) || before == 0) return;
            streaks[where] = 0;
        }
        Program.Log("paint " + where + " recovered after " + before + " failures");
    }

    static void Failed(string where, Exception ex) {
        int n;
        lock (streaks) {
            streaks.TryGetValue(where, out n);
            n++;
            streaks[where] = n;
        }
        if (n == 1) Program.Log("paint " + where + " failed: " + ex.GetType().Name + ": " + ex.Message);
        if (n == RESET_AFTER || n % RESET_AGAIN == 0) {
            try { Drawing.ResetCaches(); } catch { }
            Resets++;
            if (n == RESET_AFTER) Program.Log("paint " + where + " cleared the image caches");
        }
    }

    public static void Fallback(Graphics g, Rectangle r, Color back, Color fore) {
        try {
            g.ResetTransform();
            g.ResetClip();
            g.Clear(back);
            TextRenderer.DrawText(g, MISSING, Control.DefaultFont, r, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        } catch { }
    }
}
