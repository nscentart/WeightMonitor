using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

public class DiskGuard {
    string seen;
    public bool Changed(IEnumerable<string> names) {
        string f = string.Join("|", names.OrderBy(x => x, StringComparer.Ordinal));
        bool c = seen != null && f != seen;
        seen = f;
        return c;
    }
}
