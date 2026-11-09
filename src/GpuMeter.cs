using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RobloxMultiHub
{
    // Per-process GPU utilization from the Windows "GPU Engine" performance counters (the same
    // numbers Task Manager shows). Instance names look like pid_1234_luid_..._engtype_3D; every
    // engine of a process is summed. Counters are cached because a fresh counter returns 0 on its
    // first sample - values become meaningful from the second tick on (we tick every ~2 s).
    public static class GpuMeter
    {
        static Dictionary<string, PerformanceCounter> counters;
        static Dictionary<int, double> last = new Dictionary<int, double>();
        static int refreshAt;
        static bool unavailable;

        public static bool Available { get { return !unavailable; } }

        public static Dictionary<int, double> Sample()
        {
            if (unavailable) return last;
            try
            {
                if (counters == null || Environment.TickCount >= refreshAt)
                {
                    PerformanceCounterCategory cat = new PerformanceCounterCategory("GPU Engine");
                    counters = new Dictionary<string, PerformanceCounter>();
                    foreach (string inst in cat.GetInstanceNames())
                    {
                        if (PidOf(inst) <= 0) continue;
                        if (!counters.ContainsKey(inst))
                            counters[inst] = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true);
                    }
                    refreshAt = Environment.TickCount + 30000;      // engines come and go; refresh the list
                }
                Dictionary<int, double> sum = new Dictionary<int, double>();
                foreach (KeyValuePair<string, PerformanceCounter> kv in counters)
                {
                    int pid = PidOf(kv.Key);
                    if (pid <= 0) continue;
                    float v = kv.Value.NextValue();
                    if (v <= 0f) continue;
                    double cur;
                    sum.TryGetValue(pid, out cur);
                    sum[pid] = cur + v;
                }
                last = sum;
            }
            catch (Exception)
            {
                unavailable = true;         // no GPU counters on this Windows / localization
                counters = null;
            }
            return last;
        }

        static int PidOf(string instance)
        {
            if (string.IsNullOrEmpty(instance) || !instance.StartsWith("pid_", StringComparison.Ordinal)) return 0;
            int us = instance.IndexOf('_', 4);
            if (us <= 4) return 0;
            int v;
            return int.TryParse(instance.Substring(4, us - 4), out v) ? v : 0;
        }
    }
}
