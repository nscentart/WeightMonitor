using System;
using System.Linq;

public static class Restart {
    public static int BackoffSeconds(int attempt) { return Math.Min(300, 5 << Math.Min(attempt, 6)); }

    public static void Turn(Func<bool> turnOn, Func<double> listen, Func<int, bool> waitFor) {
        int n = 0;
        while (true) {
            if (turnOn() && listen() >= 600) n = 0;
            if (!waitFor(BackoffSeconds(n++))) return;
        }
    }
}
