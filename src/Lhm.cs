using System.Collections.Generic;

public class Lhm {
    public float[] CoreUsage = new float[0];
    public float CpuWatt = -1, UsedVram = -1, TotalVram = -1, Shared = -1, GpuMhz = -1, GpuWatt = -1, Fan = -1;
    public List<KeyValuePair<string, float>> CpuTemp = new List<KeyValuePair<string, float>>();
    public List<KeyValuePair<string, float>> GpuTemp = new List<KeyValuePair<string, float>>();
    public List<KeyValuePair<string, float>> Engines = new List<KeyValuePair<string, float>>();
}
