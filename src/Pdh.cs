using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

class Pdh {
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhOpenQueryW(string source, IntPtr user, out IntPtr q);
    [DllImport("pdh.dll")] static extern uint PdhRemoveCounter(IntPtr c);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhAddEnglishCounterW(IntPtr q, string path, IntPtr user, out IntPtr c);
    [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr q);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhGetFormattedCounterArrayW(IntPtr c, uint format, ref uint dim, out uint amount, IntPtr buf);
    [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr q);
    const uint MORE_DATA = 0x800007D2, DOUBLE = 0x200, OVER_100 = 0x8000;

    IntPtr q;
    readonly Dictionary<string, IntPtr> counters = new Dictionary<string, IntPtr>();
    IntPtr buf = IntPtr.Zero; uint length;

    public Pdh() { if (PdhOpenQueryW(null, IntPtr.Zero, out q) != 0) q = IntPtr.Zero; }
    public void Add(string name, string path) {
        IntPtr c;
        if (q != IntPtr.Zero && PdhAddEnglishCounterW(q, path, IntPtr.Zero, out c) == 0) counters[name] = c;
    }
    public void Redo(string name, string path) {
        IntPtr c;
        if (counters.TryGetValue(name, out c)) { PdhRemoveCounter(c); counters.Remove(name); }
        Add(name, path);
    }
    public void Collect() { if (q != IntPtr.Zero) PdhCollectQueryData(q); }

    public List<KeyValuePair<string, double>> Values(string name) {
        var r = new List<KeyValuePair<string, double>>();
        IntPtr c;
        if (!counters.TryGetValue(name, out c)) return r;
        uint dim = 0, n;
        if (PdhGetFormattedCounterArrayW(c, DOUBLE | OVER_100, ref dim, out n, IntPtr.Zero) != MORE_DATA) return r;
        if (dim > length) { if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf); length = dim + 4096; buf = Marshal.AllocHGlobal((int)length); }
        dim = length;
        if (PdhGetFormattedCounterArrayW(c, DOUBLE | OVER_100, ref dim, out n, buf) != 0) return r;
        for (int i = 0; i < n; i++) {
            int state = Marshal.ReadInt32(buf, i * 24 + 8);
            if (state != 0 && state != 1) continue;
            string snap = Marshal.PtrToStringUni(Marshal.ReadIntPtr(buf, i * 24));
            r.Add(new KeyValuePair<string, double>(snap, BitConverter.Int64BitsToDouble(Marshal.ReadInt64(buf, i * 24 + 16))));
        }
        return r;
    }
    public double One(string name) { var v = Values(name); return v.Count == 0 ? -1 : v[0].Value; }
}
