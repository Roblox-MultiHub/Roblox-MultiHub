using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RobloxMultiHub
{
    // Per-process GPU utilization from the Windows Lang.T("GPU Engine") performance counters (the same
    // numbers Task Manager shows). Instance names look like pid_1234_luid_..._engtype_3D; every
    // engine of a process is summed.
    // IMPORTANT: NextValue() on these counters takes tens to hundreds of milliseconds EACH - with a
    // farm of 9 clients that is seconds of work. It MUST stay off the UI thread, so this sampler
    // runs on its own background thread and publishes an immutable snapshot; readers just grab it.
    public static class GpuMeter
    {
        static readonly object gate = new object();
        static Dictionary<string, PerformanceCounter> counters;
        static volatile Dictionary<int, double> latest = new Dictionary<int, double>();
        static bool unavailable;
        static bool started;

        public static bool Available { get { return !unavailable; } }

        // The latest snapshot; never blocks, never does counter work on the caller's thread.
        public static Dictionary<int, double> Latest { get { return latest; } }

        public static void Start()
        {
            if (started) return;
            started = true;
            System.Threading.Thread t = new System.Threading.Thread(Run);
            t.IsBackground = true;
            t.Name = "GpuMeter";
            t.Start();
        }

        static void Run()
        {
            int refreshAt = 0;
            while (true)
            {
                try
                {
                    if (counters == null || Environment.TickCount >= refreshAt)
                    {
                        // keep existing counters (a fresh counter reads 0 on its first sample);
                        // only ADD new instances and DROP vanished ones
                        PerformanceCounterCategory cat = new PerformanceCounterCategory("GPU Engine");
                        HashSet<string> live = new HashSet<string>();
                        Dictionary<string, PerformanceCounter> next = new Dictionary<string, PerformanceCounter>();
                        foreach (string inst in cat.GetInstanceNames())
                        {
                            if (PidOf(inst) <= 0) continue;
                            live.Add(inst);
                            PerformanceCounter old;
                            if (counters != null && counters.TryGetValue(inst, out old)) next[inst] = old;
                            else next[inst] = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true);
                        }
                        if (counters != null)
                            foreach (KeyValuePair<string, PerformanceCounter> kv in counters)
                                if (!live.Contains(kv.Key)) kv.Value.Dispose();
                        lock (gate) counters = next;
                        refreshAt = Environment.TickCount + 30000;
                    }
                    Dictionary<int, double> sum = new Dictionary<int, double>();
                    Dictionary<string, PerformanceCounter> snapshot;
                    lock (gate) snapshot = counters;
                    if (snapshot != null)
                        foreach (KeyValuePair<string, PerformanceCounter> kv in snapshot)
                        {
                            int pid = PidOf(kv.Key);
                            if (pid <= 0) continue;
                            float v = kv.Value.NextValue();
                            if (v <= 0f) continue;
                            double cur;
                            sum.TryGetValue(pid, out cur);
                            sum[pid] = cur + v;
                        }
                    latest = sum;                    // atomic swap: readers never see a half-built dict
                }
                catch (Exception)
                {
                    unavailable = true;              // no GPU counters on this Windows / localization
                    return;
                }
                System.Threading.Thread.Sleep(5000);
            }
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
