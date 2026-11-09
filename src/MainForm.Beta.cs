using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        Label lblBetaLive;
        NumericUpDown numMaxRam;
        bool loadingBeta;

        void BuildBetaPage()
        {
            loadingBeta = true;
            Panel page = NewPage("Beta");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Beta", "New features that are still being tested. When one has proven itself it moves to the page it belongs on.");

            lblBetaLive = Ui.Lbl("", "good");
            p.Row(lblBetaLive);

            p.Section("Max RAM per window");
            numMaxRam = Ui.Num(0, 32768, Math.Max(0, Store.Settings.MaxRamMb), 80);
            p.Opt("Each Roblox window may keep at most this much RAM (0 = off).",
                "Windows stops every window from keeping more than this many MB of RAM: pages the game is not using right now are pushed out to the pagefile and stay there until the game really needs them. The RAM counter of each window stays near this number instead of growing all day.\n\n"
                + "Start high (800 MB is a good first try) and lower it step by step; when a window starts to stutter you went too low, go a little back up. 0 turns it off.\n\n"
                + "The cap is applied to every Roblox window within about 2 seconds (also right after it starts) and is removed when MultiHub closes.",
                Ui.Fld("Max RAM (MB)", 110), numMaxRam);

            numMaxRam.ValueChanged += delegate
            {
                if (loadingBeta) return;
                Store.Settings.MaxRamMb = (int)numMaxRam.Value;
                Store.SaveSettings();
                RamCap.Tick();
                UpdateBetaLive();
            };

            loadingBeta = false;
            UpdateBetaLive();
        }

        void UpdateBetaLive()
        {
            if (lblBetaLive == null) return;
            int count = UsageMonitor.Count;
            if (count == 0) { lblBetaLive.Text = "Right now: no Roblox window is running."; return; }
            string s = "Right now: " + count + " window(s), " + Util.FormatBytes(UsageMonitor.TotalRam) + " RAM in total, "
                + Util.FormatBytes(UsageMonitor.TotalRam / count) + " average.";
            if (Store.Settings.MaxRamMb > 0)
                s += "  " + RamCap.CappedCount + " of " + count + " window(s) hold the cap of " + Store.Settings.MaxRamMb + " MB.";
            lblBetaLive.Text = s;
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
