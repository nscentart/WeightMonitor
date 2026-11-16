using System;
using System.Collections.Generic;
using System.Drawing;

public static partial class SafePaint {
    static void Check(List<string> r, bool good, string what) { r.Add((good ? "ok " : "ERROR ") + what); }

    public static List<string> Test() {
        var r = new List<string>();
        var d = Program.TestSample();
        using (var b = new Bitmap(800, 48)) using (var g = Graphics.FromImage(b)) {
            var area = new Rectangle(0, 0, 800, 48);
            Action paint = () => Drawing.Draw(g, area, d, "b", "extended", 0);
            Drawing.FailTest = 1;
            bool first = true, thrown = false;
            try { first = Run("test", g, area, Color.Black, Color.White, paint); } catch { thrown = true; }
            Check(r, !thrown && !first, "a failed paint does not throw and draws the fallback");
            Check(r, Streak("test") == 1, "the failure streak counts one failure");
            bool second = Run("test", g, area, Color.Black, Color.White, paint);
            Check(r, second && Streak("test") == 0, "the next paint works again and the streak goes back to zero");
            Check(r, Drawing.Cached > 0, "a working paint fills the image caches");
            int before = Resets;
            Drawing.FailTest = RESET_AFTER;
            for (int i = 0; i < RESET_AFTER; i++) Run("test", g, area, Color.Black, Color.White, paint);
            Check(r, Resets == before + 1 && Drawing.Cached == 0, "after " + RESET_AFTER + " failures in a row the image caches are cleared");
            Check(r, Run("test", g, area, Color.Black, Color.White, paint), "after clearing the caches the paint works again");
            Drawing.FailTest = 0;
            bool fallback = true;
            try { Fallback(g, area, Color.Black, Color.White); } catch { fallback = false; }
            Check(r, fallback, "the fallback draws without throwing");
        }
        Check(r, Drawing.FormatPercent(float.NaN) == MISSING && Drawing.FormatPercent(float.PositiveInfinity) == MISSING, "an odd percent shows " + MISSING);
        Check(r, Drawing.FormatPercent(42f) == "42%", "a normal percent stays as it was");
        var odd = Program.TestSample();
        odd.Cpu.Usage = float.NaN; odd.Gpu.Temp = float.PositiveInfinity; odd.Ram.Usage = 1e12f;
        string summary = null;
        try { summary = Dashboard.Summary(odd); } catch { }
        Check(r, summary != null && summary.Contains(MISSING), "the summary with odd readings works: " + summary);
        try { summary = Dashboard.Summary(null); } catch { summary = null; }
        Check(r, summary != null, "the summary without readings works: " + summary);
        var c = new SensorData();
        Check(r, c.CommitFree() == -1f && Drawing.FreeGb(c.CommitFree(), true) == "--", "committed memory without readings shows --");
        c.CommitUsed = 61.4f; c.CommitTotal = 146f;
        Check(r, Math.Abs(c.CommitFree() - 84.6f) < 0.01f && !Drawing.CommitLow(c), "committed memory 61.4 of 146 GB leaves 84.6 GB free, no warning");
        c.CommitUsed = 140f;
        Check(r, Drawing.CommitLow(c) && Drawing.FreeGb(c.CommitFree(), false) == "6.0 GB", "committed memory 140 of 146 GB leaves 6.0 GB, warning on");
        c.CommitUsed = float.NaN;
        Check(r, c.CommitFree() == -1f && !Drawing.CommitLow(c), "committed memory with an odd reading gives no warning");
        Check(r, Drawing.FreeGb(float.NaN, true) == MISSING, "free memory with an odd value shows " + MISSING);
        return r;
    }
}
