using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LibreHardwareMonitor.Hardware;

public class Disk {
    public int Number = -1;
    public string Model = "", Type = "", Bus = "";
    public long Byte;
    public List<char> Letters = new List<char>();
    public float Usage = -1f, Temp = -1f;
    public Dictionary<char, long[]> Space = new Dictionary<char, long[]>();

    public static bool AlmostFull(long free, long total) {
        if (free < 0 || total <= 0) return false;
        return free < 2000000000L || (free < total / 20 && free < 20000000000L);
    }
    public bool AlmostFull() { return Space.Values.Any(v => AlmostFull(v[0], v[1])); }

    public string Free() {
        if (Space.Count == 0) return null;
        if (Space.Count == 1) { var v = Space.Values.First(); return Language.T("disks.free_of_total", "free", Trim(v[0]), "total", Trim(v[1])); }
        return string.Join(" · ", Space.OrderBy(kv => kv.Key).Select(kv => Language.T("disks.free_with_letter", "letter", kv.Key, "free", Trim(kv.Value[0]))));
    }

    public Disk Copy() { var d = (Disk)MemberwiseClone(); d.Letters = new List<char>(Letters); return d; }
    public string Codes(bool log = false) { return Letters.Count == 0 ? (log ? Language.Fixed("sensors.no_letter") : Language.T("sensors.no_letter")) : string.Join(" ", Letters.Select(l => l + ":")); }
    public string Description(bool log = false) {
        var parts = new List<string>();
        if (Type.Length > 0) parts.Add(Type);
        if (Bus.Length > 0) parts.Add(Bus == "Virtual" ? (log ? Language.Fixed("sensors.virtual") : Language.T("sensors.virtual")) : Bus);
        if (Byte > 0) parts.Add(Trim(Byte));
        return string.Join(" · ", parts);
    }
    public static string Trim(long b) {
        if (b >= 1000000000000L) return (b / 1e12).ToString("0.#", CultureInfo.InvariantCulture) + " TB";
        if (b < 10000000000L) return (b / 1e9).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        return (b / 1e9).ToString("0", CultureInfo.InvariantCulture) + " GB";
    }
}
