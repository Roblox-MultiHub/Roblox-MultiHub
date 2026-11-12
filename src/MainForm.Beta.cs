using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        Label lblBetaLive;
        bool loadingBeta;

        void BuildBetaPage()
        {
            loadingBeta = true;
            Panel page = NewPage("Beta");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Beta", "New features that are still being tested. When one has proven itself it moves to the page it belongs on.");

            lblBetaLive = Ui.Lbl("", "good");
            p.Row(lblBetaLive);

            p.Opt(Lang.T("Quick numbers without a probe."),
                "Reads cores, RAM, GPU counters, disks, pagefile and the clients currently running, and estimates how many windows this PC can run. The real per-window cap comes from the probe on the Performance page.",
                Ui.Btn(Lang.T("Run diagnostics"), 150, delegate { BetaDiagnostics(); }, ""));

            loadingBeta = false;
            UpdateBetaLive();
        }

        int betaSuggestMb;

        // one line per physical disk + where the pagefile lives (fault cost = pagefile disk speed)
        static System.Collections.Generic.List<string> DiskLines()
        {
            System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
            try
            {
                using (System.Management.ManagementObjectSearcher d = new System.Management.ManagementObjectSearcher(
                    "SELECT Model, Size, InterfaceType, MediaType FROM Win32_DiskDrive"))
                using (System.Management.ManagementObjectCollection r = d.Get())
                    foreach (System.Management.ManagementObject mo in r)
                    {
                        try
                        {
                            lines.Add("Disk: " + (mo["Model"] ?? "?") + "  " + Util.FormatBytes(Convert.ToInt64(mo["Size"]))
                                + "  " + (mo["InterfaceType"] ?? "?") + "  " + (mo["MediaType"] ?? "?"));
                        }
                        finally { mo.Dispose(); }
                    }
                using (System.Management.ManagementObjectSearcher p = new System.Management.ManagementObjectSearcher(
                    "SELECT Name, AllocatedBaseSize FROM Win32_PageFileUsage"))
                using (System.Management.ManagementObjectCollection r = p.Get())
                    foreach (System.Management.ManagementObject mo in r)
                    {
                        try { lines.Add("Pagefile: " + mo["Name"] + "  " + mo["AllocatedBaseSize"] + " MB (put it on your fastest SSD - a low RAM cap trades disk reads for RAM)"); }
                        finally { mo.Dispose(); }
                    }
            }
            catch (Exception) { }
            return lines;
        }

        void BetaDiagnostics()
        {
            int load; long total, avail;
            MemClean.Status(out load, out total, out avail);
            double totalGb = total / 1073741824.0;
            double freeGb = avail / 1073741824.0;
            int cores = Environment.ProcessorCount;
            int running = UsageMonitor.Count;

            // budget: keep 4 GB for Windows and everything else that runs next to the farm
            double budgetGb = Math.Max(2.0, totalGb - 4.0);
            double perClientGb = Math.Max(0.4, running > 0 ? UsageMonitor.TotalRam / 1073741824.0 / running : 0.75);
            int byRam = (int)(budgetGb / perClientGb);
            int byCpu = (int)(cores * 1.5);                 // idle farms use well under a core each
            int n = Math.Max(1, Math.Min(byRam, byCpu));

            // the cap should be the LOWEST reliable number, not a budget share: idle AFK clients
            // settle around 450 MB under a cap, so that is the baseline. If clients are running
            // with a cap already holding them, the measured average IS the floor - suggest one
            // step below it, so every run of the diagnostics ratchets the number down until the
            // user sees a stutter and steps back up.
            int suggest = Store.Settings.RamCapFloorMb;

            AddLog("Diagnostics: " + cores + " CPU cores, " + (GpuMeter.Available ? "GPU counters available" : "no GPU counters")
                + ", RAM " + totalGb.ToString("0.0") + " GB total / " + freeGb.ToString("0.0") + " GB free.");
            foreach (string d in DiskLines()) AddLog(d);
            if (running > 0)
                AddLog("Running now: " + running + " client(s), avg " + (UsageMonitor.TotalCpu / running).ToString("0.0") + "% CPU"
                    + (GpuMeter.Available ? ", " + (UsageMonitor.TotalGpu / running).ToString("0.0") + "% GPU" : "")
                    + ", " + (UsageMonitor.TotalRam / running / 1048576) + " MB RAM each.");
            if (suggest > 0)
                AddLog("Measured cap floor on this PC: " + suggest + " MB per window (from the probe).");
            else
                AddLog("Cap floor not measured yet - press 'Find my real cap' while your accounts idle; it measures your actual clients.");
            betaSuggestMb = suggest;
        }

        void UpdateBetaLive()
        {
            if (lblBetaLive == null) return;
            int count = UsageMonitor.Count;
            if (count == 0) { lblBetaLive.Text = Lang.T("Right now: no Roblox window is running."); return; }
            lblBetaLive.Text = "Right now: " + count + " window(s), " + Util.FormatBytes(UsageMonitor.TotalRam) + " RAM in total, "
                + Util.FormatBytes(UsageMonitor.TotalRam / count) + " average.";
        }
    }

    // Hard working-set cap per Roblox window ("Max RAM per window", Beta page). The cap is a Windows
    // working-set quota: pages beyond it are pushed to the pagefile instead of staying in RAM. It is
    // not a memory limit - nothing crashes, the game only pays a page fault when it touches a paged-out
    // page again. Re-applied by the main timer, removed when the hub closes or the value goes to 0.
    public static class RamCap
    {
        const uint PROCESS_SET_QUOTA = 0x0100;
        const uint HARDWS_MAX_ENABLE = 0x4;    // QUOTA_LIMITS_HARDWS_MAX_ENABLE
        const uint HARDWS_MAX_DISABLE = 0x8;   // QUOTA_LIMITS_HARDWS_MAX_DISABLE

        static readonly Dictionary<int, int> capped = new Dictionary<int, int>();   // pid -> MB

        public static int CappedCount { get { return capped.Count; } }

        // Called from the main timer (every couple of seconds).
        public static void Tick()
        {
            int want = Store.Settings.MaxRamMb;
            if (want <= 0)
            {
                if (capped.Count > 0) Reset();
                return;
            }
            List<int> pids = Launcher.RobloxPids();
            foreach (int pid in pids)
            {
                int prev;
                if (capped.TryGetValue(pid, out prev) && prev == want) continue;
                if (SetCap(pid, want)) capped[pid] = want;
            }
            foreach (int pid in new List<int>(capped.Keys))
                if (!pids.Contains(pid)) capped.Remove(pid);
        }

        // Gives every capped window its normal working set back.
        public static void Reset()
        {
            foreach (int pid in new List<int>(capped.Keys))
            {
                SetCapOff(pid);
                capped.Remove(pid);
            }
        }

        static bool SetCap(int pid, int mb)
        {
            IntPtr h = Native.OpenProcess(PROCESS_SET_QUOTA, false, pid);
            if (h == IntPtr.Zero) return false;
            try
            {
                // a real minimum must be passed (-1 is rejected together with a maximum)
                return SetProcessWorkingSetSizeEx(h, (IntPtr)1048576, (IntPtr)((long)mb * 1048576L), HARDWS_MAX_ENABLE);
            }
            finally { Native.CloseHandle(h); }
        }

        static void SetCapOff(int pid)
        {
            IntPtr h = Native.OpenProcess(PROCESS_SET_QUOTA, false, pid);
            if (h == IntPtr.Zero) return;
            try { SetProcessWorkingSetSizeEx(h, (IntPtr)1048576, (IntPtr)(8192L * 1048576L), HARDWS_MAX_DISABLE); }
            finally { Native.CloseHandle(h); }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetProcessWorkingSetSizeEx(IntPtr hProcess, IntPtr min, IntPtr max, uint flags);
    }
}
