using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

public partial class Network {
    public static List<string> RealTest() {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "ok " : "ERROR ") + what);
        Func<Func<bool>, int, bool> WaitUntil = (cond, seconds) => {
            for (int i = 0; i < seconds * 10 && !cond(); i++) Thread.Sleep(100);
            return cond();
        };
        string f = Path.GetTempFileName();
        Settings.UseFile(f);
        try {
            var x = new Network(Legacy.ETW_TEST_SESSION);
            x.Start();
            if (!Program.Admin()) {
                Check(!x.Activate && x.Error.Length > 0, "without administrator it stays off and says why: " + x.Error);
                r.Add("real interruption: not tried, it needs the administrator");
                x.Stop();
                return r;
            }
            Check(x.Activate, "it turns on");
            x.Prepare();
            ControlTrace(0, x.sessionName, x.properties, 1);
            Check(WaitUntil(() => !x.Activate, 10), "it notices the interruption");
            Check(WaitUntil(() => x.Activate, 15), "it restarts by itself");
            x.Stop();
            Check(!x.Activate, "it stops");
            Thread.Sleep(7000);
            Check(!x.Activate, "after closing it does not restart");
        } finally { try { File.Delete(f); } catch { } }
        return r;
    }
}
