using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public partial class Lane {
    public static List<string> Test() {
        var r = new List<string>();
        Rectangle large = new Rectangle(0, 0, 3840, 2088), left = new Rectangle(-2560, -200, 2560, 1392), small = new Rectangle(0, 0, 1280, 672);
        Action<string, Rectangle, Rectangle, Rectangle, int, int, bool> testCase = (name, area, anchor, bar, width, height, canCover) => {
            var c = Choose(area, anchor, bar, width, height, 15);
            var q = c.Reading(width, height);
            bool covered = (!bar.IsEmpty && q.IntersectsWith(bar)) || q.IntersectsWith(anchor);
            r.Add((covered && !canCover || !area.Contains(q) ? "ERROR " : "ok ") + name + ": x " + q.X + " y " + q.Y + " tall " + q.Height
                  + (q.Height < height ? " (shortened from " + height + ")" : "") + (c.Side < 0 ? ", above" : c.Side > 0 ? ", below" : ", beside"));
        };
        testCase("the defect of 01/11: bar in the middle of the screen, flyout 1069 tall", large, new Rectangle(1700, 976, 60, 48), new Rectangle(1250, 976, 697, 48), 705, 1069, false);
        testCase("bar in the middle of the screen, short flyout", large, new Rectangle(1700, 976, 60, 48), new Rectangle(1250, 976, 697, 48), 705, 500, false);
        testCase("bar in the middle of the screen, flyout 1500 tall", large, new Rectangle(1700, 976, 60, 48), new Rectangle(1250, 976, 697, 48), 705, 1500, false);
        testCase("bar at the top", large, new Rectangle(1700, 10, 60, 48), new Rectangle(1250, 10, 697, 48), 705, 1069, false);
        testCase("bar at the bottom", large, new Rectangle(1700, 2030, 60, 48), new Rectangle(1250, 2030, 697, 48), 705, 1069, false);
        testCase("bar attached to the left edge", large, new Rectangle(100, 976, 60, 48), new Rectangle(0, 976, 697, 48), 705, 1500, false);
        testCase("bar attached to the right edge", large, new Rectangle(3780, 976, 60, 48), new Rectangle(3143, 976, 697, 48), 705, 1500, false);
        testCase("monitor on the left, negative coordinates", left, new Rectangle(-1000, 400, 60, 48), new Rectangle(-1400, 400, 697, 48), 705, 1069, false);
        testCase("monitor on the left, bar at the top at negative coordinates", left, new Rectangle(-1000, -180, 60, 48), new Rectangle(-1400, -180, 697, 48), 705, 800, false);
        testCase("small screen, flyout taller than the screen", small, new Rectangle(600, 300, 60, 48), new Rectangle(300, 300, 697, 48), 470, 1069, false);
        testCase("bar as wide as the small screen", small, new Rectangle(600, 300, 60, 48), new Rectangle(0, 300, 1280, 48), 470, 1069, false);
        testCase("from the icon among the hidden ones, bar detached elsewhere", large, new Rectangle(3700, 2100, 1, 1), new Rectangle(3200, 1500, 697, 48), 705, 1069, false);
        testCase("bar attached, no obstacle on the screen", large, new Rectangle(1700, 2100, 60, 48), Rectangle.Empty, 705, 1069, false);
        testCase("tiny screen: the flyout is visible anyway", new Rectangle(0, 0, 800, 100), new Rectangle(300, 0, 60, 100), new Rectangle(0, 0, 800, 100), 470, 500, true);
        return r;
    }
}
