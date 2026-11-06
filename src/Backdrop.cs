using System.Drawing;
using System.Windows.Forms;

class Backdrop : Form {
    public Backdrop() {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black; Opacity = 0.01;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000 | 0x8; return cp; }
    }
    protected override void WndProc(ref Message m) {
        if (Flyout.DoNotActivate(ref m)) return;
        base.WndProc(ref m);
    }
}
