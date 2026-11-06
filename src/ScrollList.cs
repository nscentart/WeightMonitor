using System;
using System.Drawing;
using System.Linq;

public class ScrollList {
    public const int ROWS = 14;
    public int Alt;
    public int ScrollOffset;
    public bool Started;
    public int Total, Visible, Step;
    public Rectangle Frame, Track, Knob;
    public bool Scrolls { get { return Total > Visible; } }
    public void MoveBy(int delta) { ScrollOffset = Math.Max(0, Math.Min(Math.Max(0, Total - Visible), ScrollOffset + delta)); }
    public void MoveKnob(int y) {
        int travel = Track.Height - Knob.Height;
        if (travel > 0) { ScrollOffset = 0; MoveBy((int)Math.Round((double)(y - Track.Y) * (Total - Visible) / travel)); }
    }
}
