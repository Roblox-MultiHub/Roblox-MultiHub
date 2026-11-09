using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RobloxMultiHub
{
    public class UsageSample
    {
        public double Cpu;      // percent of the WHOLE PC's CPU (like Task Manager)
        public double Gpu;      // percent of the whole GPU (sum of the process's engine counters)
        public long Ram;        // working set in bytes
    }

    // Measures CPU and memory of every Roblox client. Called every couple of seconds.
    public static class UsageMonitor
    {
        static Dictionary<int, long> lastCpu = new Dictionary<int, long>();
        static DateTime lastAt = DateTime.MinValue;
        public static Dictionary<int, UsageSample> Samples = new Dictionary<int, UsageSample>();
        public static double TotalCpu;
        public static double TotalGpu;
        public static long TotalRam;
        public static int Count;
        public static bool CpuAvailable = true;

        // Short rolling history per process, for the sparklines on the Monitor page.
        public const int HistLen = 40;
        public static Dictionary<int, List<double>> CpuHist = new Dictionary<int, List<double>>();
        public static Dictionary<int, List<double>> RamHist = new Dictionary<int, List<double>>();

        static void Push(Dictionary<int, List<double>> hist, int pid, double v)
        {
            List<double> l;
            if (!hist.TryGetValue(pid, out l)) { l = new List<double>(); hist[pid] = l; }
            l.Add(v);
            if (l.Count > HistLen) l.RemoveRange(0, l.Count - HistLen);
        }

        public static void Update()
        {
            DateTime now = DateTime.UtcNow;
            double elapsed = lastAt == DateTime.MinValue ? 0 : (now - lastAt).TotalSeconds;
            int cores = Math.Max(1, Environment.ProcessorCount);
            Dictionary<int, long> cpuNow = new Dictionary<int, long>();
            Dictionary<int, UsageSample> fresh = new Dictionary<int, UsageSample>();
            double total = 0;
            long ram = 0;
            int count = 0;
            int cpuFailures = 0;

            foreach (Process p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                try
                {
                    int pid = p.Id;
                    UsageSample s = new UsageSample();
                    s.Ram = p.WorkingSet64;
                    long cpu = Native.CpuTime100ns(pid);
                    if (cpu < 0) cpuFailures++;
                    else
                    {
                        cpuNow[pid] = cpu;
                        long prev;
                        if (elapsed > 0.2 && lastCpu.TryGetValue(pid, out prev))
                        {
                            s.Cpu = ((cpu - prev) / 10000000.0) / elapsed / cores * 100.0;
                            if (s.Cpu < 0) s.Cpu = 0;
                            if (s.Cpu > 100) s.Cpu = 100;
                        }
                    }
                    fresh[pid] = s;
                    total += s.Cpu;
                    ram += s.Ram;
                    count++;
                }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
            CpuAvailable = !(count > 0 && cpuFailures == count);
            // GPU: one snapshot for all pids (the engine counters are per process instance)
            Dictionary<int, double> gpu = GpuMeter.Sample();
            double gpuTotal = 0;
            foreach (KeyValuePair<int, UsageSample> kv in fresh)
            {
                double g;
                if (gpu.TryGetValue(kv.Key, out g)) { kv.Value.Gpu = g; gpuTotal += g; }
            }
            lastCpu = cpuNow;
            lastAt = now;
            Samples = fresh;
            TotalCpu = total;
            TotalGpu = gpuTotal;
            TotalRam = ram;
            Count = count;

            // roll the per-process history and forget processes that are gone
            foreach (KeyValuePair<int, UsageSample> kv in fresh)
            {
                Push(CpuHist, kv.Key, kv.Value.Cpu);
                Push(RamHist, kv.Key, kv.Value.Ram / 1048576.0);   // MB
            }
            foreach (int pid in new List<int>(CpuHist.Keys)) if (!fresh.ContainsKey(pid)) CpuHist.Remove(pid);
            foreach (int pid in new List<int>(RamHist.Keys)) if (!fresh.ContainsKey(pid)) RamHist.Remove(pid);
        }
    }
}
