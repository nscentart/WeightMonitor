using System;
using System.Runtime.InteropServices;
using System.Threading;

class Guard {
    [DllImport("kernel32.dll")] static extern bool QueryUnbiasedInterruptTime(out ulong t);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] static extern bool SetThreadPriority(IntPtr h, int priority);
    const int STEP = 250;
    long worstDelay;

    public void Start() {
        new Thread(delegate() {
            SetThreadPriority(GetCurrentThread(), 15);
            while (true) {
                ulong t0, t1;
                QueryUnbiasedInterruptTime(out t0);
                Thread.Sleep(STEP);
                QueryUnbiasedInterruptTime(out t1);
                long delay = (long)((t1 - t0) / 10000) - STEP;
                if (delay >= 1000 && delay > Interlocked.Read(ref worstDelay)) Interlocked.Exchange(ref worstDelay, delay);
            }
        }) { IsBackground = true, Name = "guard" }.Start();
    }
    public long Consumes() { return Interlocked.Exchange(ref worstDelay, 0); }
}
