using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public partial class Lane {
    public Rectangle Area;
    public int X, Up, Down, Side, Center;

    public Rectangle Reading(int width, int height) {
        int h = Math.Min(height, Down - Up);
        int y = Side < 0 ? Down - h : Side > 0 ? Up : Math.Max(Up, Math.Min(Center - h / 2, Down - h));
        return new Rectangle(X, y, width, h);
    }

    const int SHORTEN = 90;

    public static Lane Choose(Rectangle area, Rectangle anchor, Rectangle bar, int width, int height, int d) {
        var center = new Point(anchor.X + anchor.Width / 2, anchor.Y + anchor.Height / 2);
        var obstacles = new List<Rectangle> { anchor };
        if (!bar.IsEmpty) obstacles.Add(bar);
        var footprint = !bar.IsEmpty && bar.Contains(center) ? bar : anchor;
        int left = area.Left + d, right = area.Right - width - d;
        int xCenter = Math.Max(left, Math.Min(center.X - width / 2, right));

        var near = FreeLanes(area, obstacles, xCenter, width, d, center.Y, anchor);
        var onSide = new List<Lane>();
        if (footprint.Left - d - width >= left) onSide.AddRange(FreeLanes(area, obstacles, footprint.Left - d - width, width, d, center.Y, anchor));
        if (footprint.Right + d <= right) onSide.AddRange(FreeLanes(area, obstacles, footprint.Right + d, width, d, center.Y, anchor));

        foreach (var c in near) if (c.Down - c.Up >= height) return c;
        var best = near.OrderByDescending(c => c.Down - c.Up).FirstOrDefault();
        if (best != null && (best.Down - best.Up) * 100 >= height * SHORTEN) return best;
        foreach (var c in onSide) if (c.Down - c.Up >= height) return c;
        foreach (var c in onSide) if (best == null || c.Down - c.Up > best.Down - best.Up) best = c;
        return best ?? new Lane { Area = area, X = xCenter, Up = area.Top + d, Down = Math.Max(area.Top + d + 1, area.Bottom - d), Center = center.Y };
    }

    static List<Lane> FreeLanes(Rectangle area, List<Rectangle> obstacles, int x, int width, int d, int center, Rectangle anchor) {
        var r = new List<Lane>();
        int up = area.Top + d, end = area.Bottom - d;
        foreach (var o in obstacles.Where(o => o.Left < x + width + d && o.Right > x - d).OrderBy(o => o.Top)) {
            if (o.Top - d > up) r.Add(Of(area, x, up, Math.Min(o.Top - d, end), center, anchor));
            up = Math.Max(up, o.Bottom + d);
        }
        if (end > up) r.Add(Of(area, x, up, end, center, anchor));
        return r.Where(c => c.Down > c.Up).ToList();
    }
    static Lane Of(Rectangle area, int x, int up, int down, int center, Rectangle anchor) {
        return new Lane { Area = area, X = x, Up = up, Down = down, Center = center, Side = down <= anchor.Top ? -1 : up >= anchor.Bottom ? 1 : 0 };
    }
}
