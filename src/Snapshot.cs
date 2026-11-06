using System.Collections.Generic;

public class Snapshot {
    public int Thread, Core, Processes, ThreadTotal, Handle;
    public double Uptime;
    public float[] CoreGhz = new float[0];
    public float GhzAverage = -1, CpuWatt = -1;
    public List<KeyValuePair<string, float>> CpuTemp = new List<KeyValuePair<string, float>>();
    public List<KeyValuePair<string, float>> GpuTemp = new List<KeyValuePair<string, float>>();
    public List<KeyValuePair<string, float>> Engines = new List<KeyValuePair<string, float>>();
    public float UsedVram = -1, TotalVram = -1, Shared = -1, GpuMhz = -1, GpuWatt = -1, Fan = -1;
    public double TotalRam, UsedRam, Committed, Limit, Cache, Modified, PlaceDetached, Paging, NonPaging, Compressed;
    public bool DiskEtw;
    public Dictionary<int, Dictionary<App, double>> AppDisk = new Dictionary<int, Dictionary<App, double>>();
}
