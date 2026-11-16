using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static partial class Program {
    static SensorData SampleData() {
        var d = new SensorData();
        d.CpuName = "AMD Ryzen 9 9950X3D"; d.GpuName = "NVIDIA GeForce RTX 5090";
        d.Cpu.Usage = 12; d.Cpu.Temp = 74; d.Gpu.Usage = 9; d.Gpu.Temp = 48; d.Ram.Usage = 42; d.Ram.Temp = 41;
        d.UsedRam = 53.1f; d.TotalRam = 126f; d.CommitUsed = 61.4f; d.CommitTotal = 146f;
        d.Disks.Add(new Disk { Number = 1, Model = "Samsung SSD 9100 PRO 1TB", Type = "SSD", Bus = "NVMe", Byte = 1000202273280, Letters = new List<char> { 'C' }, Space = new Dictionary<char, long[]> { { 'C', new[] { 412000000000L, 1000202273280L } } }, Usage = 3, Temp = 38 });
        d.Disks.Add(new Disk { Number = 2, Model = "Samsung SSD 9100 PRO 2TB", Type = "SSD", Bus = "NVMe", Byte = 2000396321280, Letters = new List<char> { 'D' }, Space = new Dictionary<char, long[]> { { 'D', new[] { 1240000000000L, 2000396321280L } } }, Usage = 1, Temp = 35 });
        d.Disks.Add(new Disk { Number = 0, Model = "ST8000NM000A-2KE101", Type = "HDD", Bus = "SATA", Byte = 8001560609280, Letters = new List<char> { 'E' }, Space = new Dictionary<char, long[]> { { 'E', new[] { 12300000000L, 8001560609280L } } }, Usage = 99, Temp = 41 });
        d.Up = 34406; d.Down = 15667;
        return d;
    }

    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int how);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint how);

    static void RealScreenshot(Form f, string file) {
        ShowWindow(f.Handle, 4);
        Application.DoEvents();
        using (var bmp = new Bitmap(f.Width, f.Height)) {
            using (var g = Graphics.FromImage(bmp)) {
                IntPtr hdc = g.GetHdc();
                PrintWindow(f.Handle, hdc, 2);
                g.ReleaseHdc(hdc);
            }
            bmp.Save(file, ImageFormat.Png);
        }
    }

    static void Screenshot(string file, string style, string shape, string flyout, int tab, int height, SensorData d, Network network, int low, int scroll, bool light) {
        Drawing.LightBar = light;
        Bitmap bmp;
        if (flyout == null) {
            int width;
            using (var b1 = new Bitmap(1, 1)) using (var g1 = Graphics.FromImage(b1)) width = Drawing.Width(g1, height, style, shape, d.Letters().Count);
            int barHeight = height * 3 / 2, margin = height;
            bmp = new Bitmap(width + margin * 2, barHeight, PixelFormat.Format32bppRgb);
            Drawing.WithoutHalo(bmp, g => {
                g.Clear(Theme.Key);
                var where = new Rectangle(margin, (barHeight - height) / 2, width, height);
                Drawing.Draw(g, where, d, style, shape, 0);
            });
            var below = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
            for (int y = 0; y < bmp.Height; y++) for (int x = 0; x < bmp.Width; x++) if (bmp.GetPixel(x, y).ToArgb() == Theme.Key.ToArgb()) bmp.SetPixel(x, y, below);
        } else {
            float s = height / 32f;
            int width = Drawing.FlyoutWidth(flyout, s), h;
            using (var b1 = new Bitmap(width, 4000)) using (var g1 = Graphics.FromImage(b1)) h = Drawing.Flyout(g1, flyout, width, s, d, network, tab, null);
            int visible = low > 0 ? Math.Min(low, h) : h;
            var list = flyout == "network" ? new ScrollList { Alt = visible, ScrollOffset = scroll } : null;
            if (list != null) { using (var b1 = new Bitmap(width, 4000)) using (var g1 = Graphics.FromImage(b1)) h = Drawing.Flyout(g1, flyout, width, s, d, network, tab, null, list); scroll = 0; }
            bmp = new Bitmap(width, visible, PixelFormat.Format32bppRgb);
            Drawing.WithoutHalo(bmp, g => Flyout.Draw(g, flyout, width, visible, h, Flyout.ScrollOffset(h, visible, 0, scroll), s, d, network, tab, null, list));
        }
        bmp.Save(file, ImageFormat.Png);
        bmp.Dispose();
    }
}
