using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        Label lblBetaLive;
        bool loadingBeta;
        CheckBox chkDetectSecrets;
        RoundTextBox tbSecretImage;
        NumericUpDown numSecretHold, numSecretOffX, numSecretOffY, numSecretTries;
        CheckBox chkSecretAllowClick;
        PictureBox picSecretPreview;
        Label lblSecretLive;

        void BuildBetaPage()
        {
            loadingBeta = true;
            Panel page = NewPage("Beta");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Beta", "New features that are still being tested. When one has proven itself it moves to the page it belongs on.");

            lblBetaLive = Ui.Lbl("", "good");
            p.Row(lblBetaLive);

            p.Section("Diagnostics");
            p.Opt("Quick numbers without a probe.",
                "Reads cores, RAM, GPU counters, disks, pagefile and the clients currently running, and estimates how many windows this PC can run. The 'Max RAM per window' cap and its probe now live on the Performance page.",
                Ui.Btn("Run diagnostics", 150, delegate { BetaDiagnostics(); }, ""));

            p.Section("Detect Secrets");
            chkDetectSecrets = Ui.Chk("Detect Secrets", Store.Settings.DetectSecrets, null);
            p.Opt("Clicks once when the chosen image appears.",
                "Watches every Roblox window (foreground or background) every 5 seconds. When the selected image is found, the cursor moves to its center (plus the offsets below) and the left button is held for the chosen time. One Discord message is sent for that account and its lifetime secret counter goes up by 1. It does not fire again for that account until the image has been off-screen for 10 seconds.\n\nRequires 'Rename windows to the account name' (Layout), the Discord webhook (Settings) and 'Log status' (Settings) to all be ON.",
                chkDetectSecrets);
            tbSecretImage = Ui.Txt(300, Store.Settings.SecretImagePath);
            p.Opt("Which image to detect.",
                "Pick a PNG of the UI element to detect (for the secret text strip, pick that). The click lands at the center of the detected image plus the X/Y offsets below.",
                Ui.Fld("Image", 60), tbSecretImage,
                Ui.Btn("Browse...", 90, delegate { BrowseSecretImage(); }, ""));
            numSecretHold = Ui.Num(20, 5000, Math.Max(20, Store.Settings.SecretHoldMs), 70);
            p.Opt("How long the button stays held.",
                "Time in milliseconds the left mouse button is held on the detected spot.",
                Ui.Fld("Hold (ms)", 90), numSecretHold);
            numSecretOffX = Ui.Num(-500, 500, Store.Settings.SecretOffsetX, 70);
            numSecretOffY = Ui.Num(-500, 500, Store.Settings.SecretOffsetY, 70);
            p.Opt("Click point relative to the image center.",
                "For the secret icon above the text strip, leave X at 0 and Y at -18.",
                Ui.Fld("X offset", 70), numSecretOffX, Ui.Fld("Y offset", 70), numSecretOffY);
            numSecretTries = Ui.Num(1, 20, Math.Max(1, Store.Settings.SecretMaxTries), 70);
            p.Opt("How many times to retry a failed click.",
                "If the image is still on screen after a click (the click did not work), the detector clicks again on the next scan, up to this many tries in total. It stops once the image disappears.",
                Ui.Fld("Max click tries", 120), numSecretTries);
            numSecretTries.ValueChanged += delegate { if (!loadingBeta) { Store.Settings.SecretMaxTries = (int)numSecretTries.Value; Store.SaveSettings(); } };

            p.Opt("Reset the lifetime secret counter.",
                "Sets every account's secret counter back to 0. This cannot be undone.",
                Ui.Btn("Reset secret counters", 190, delegate { ResetSecretCounters(); }, "danger"));

            chkSecretAllowClick = Ui.Chk("Allow click", Store.Settings.SecretAllowClick, null);
            p.Opt("Whether the detector may touch the mouse.",
                "ON: when the image is found the detector moves the cursor to it (plus the offsets) and holds the left button. OFF: it still detects, logs and counts every hit but never moves or clicks the mouse.",
                chkSecretAllowClick);
            chkSecretAllowClick.CheckedChanged += delegate { if (!loadingBeta) { Store.Settings.SecretAllowClick = chkSecretAllowClick.Checked; Store.SaveSettings(); } };

            picSecretPreview = new PictureBox();
            picSecretPreview.SizeMode = PictureBoxSizeMode.Zoom;
            picSecretPreview.BackColor = Theme.Panel2;
            picSecretPreview.Width = p.Width - PageBuilder.PadX * 2;
            picSecretPreview.Height = 210;
            p.Row(picSecretPreview);
            lblSecretLive = Ui.Lbl("Detect Secrets preview: not running yet.", "muted");
            p.Row(lblSecretLive);

            chkDetectSecrets.CheckedChanged += delegate
            {
                if (loadingBeta) return;
                Store.Settings.DetectSecrets = chkDetectSecrets.Checked;
                Store.SaveSettings();
                if (chkDetectSecrets.Checked)
                {
                    if (SecretDetector.RequirementsMet()) { SecretDetector.Start(); AddLog("Detect Secrets ON."); }
                    else
                    {
                        loadingBeta = true;
                        chkDetectSecrets.Checked = false;
                        loadingBeta = false;
                        Store.Settings.DetectSecrets = false;
                        Store.SaveSettings();
                    }
                }
                else { SecretDetector.Stop(); if (lblSecretLive != null) lblSecretLive.Text = "Detect Secrets is off."; }
            };
            tbSecretImage.Leave += delegate
            {
                if (loadingBeta) return;
                Store.Settings.SecretImagePath = tbSecretImage.Text.Trim();
                Store.SaveSettings();
            };
            numSecretHold.ValueChanged += delegate { if (!loadingBeta) { Store.Settings.SecretHoldMs = (int)numSecretHold.Value; Store.SaveSettings(); } };
            numSecretOffX.ValueChanged += delegate { if (!loadingBeta) { Store.Settings.SecretOffsetX = (int)numSecretOffX.Value; Store.SaveSettings(); } };
            numSecretOffY.ValueChanged += delegate { if (!loadingBeta) { Store.Settings.SecretOffsetY = (int)numSecretOffY.Value; Store.SaveSettings(); } };

            SecretDetector.Detected -= OnSecretDetected;
            SecretDetector.Detected += OnSecretDetected;
            SecretDetector.Preview -= OnSecretPreview;
            SecretDetector.Preview += OnSecretPreview;

            loadingBeta = false;
            UpdateBetaLive();
        }

        void BrowseSecretImage()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Choose the image to detect";
                d.Filter = "PNG images|*.png|All files|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                tbSecretImage.Text = d.FileName;
                Store.Settings.SecretImagePath = d.FileName;
                Store.SaveSettings();
            }
        }

        void UpdateSecretDetectorGating()
        {
            if (chkDetectSecrets == null) return;
            bool ok = SecretDetector.RequirementsMet();
            chkDetectSecrets.Enabled = ok;
            if (!ok && chkDetectSecrets.Checked)
            {
                loadingBeta = true;
                chkDetectSecrets.Checked = false;
                loadingBeta = false;
                Store.Settings.DetectSecrets = false;
                Store.SaveSettings();
                SecretDetector.Stop();
                if (lblSecretLive != null) lblSecretLive.Text = "Detect Secrets is off (a requirement is no longer met).";
                AddLog("Detect Secrets turned OFF: a requirement is no longer met.");
            }
        }

        void OnSecretDetected(int pid, string name, int sx, int sy)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<int, string, int, int>(OnSecretDetected), pid, name, sx, sy); } catch (Exception) { }
                return;
            }
            Instance inst = Launcher.FindInstance(pid);
            string who = name;
            if (inst != null && inst.Account != null)
            {
                inst.Account.SecretsFound++;
                Store.SaveAccounts();
                who = AppState.Name(inst.Account);
            }
            Webhook.Send("Secret detected", "Secret detected for account " + who, Webhook.Yellow);
            AddLog("Detect Secrets: secret found for account " + who + " (clicked at " + sx + "," + sy + ").");
        }

        void OnSecretPreview(Bitmap bmp, string text)
        {
            if (IsDisposed) { if (bmp != null) bmp.Dispose(); return; }
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<Bitmap, string>(OnSecretPreview), bmp, text); }
                catch (Exception) { if (bmp != null) bmp.Dispose(); }
                return;
            }
            if (lblSecretLive != null) lblSecretLive.Text = text;
            if (picSecretPreview == null) { if (bmp != null) bmp.Dispose(); return; }
            Image old = picSecretPreview.Image;
            picSecretPreview.Image = bmp;
            if (old != null) old.Dispose();
        }

        void ResetSecretCounters()
        {
            if (MessageBox.Show(this, "Reset the secret counter for ALL accounts to 0?", "Detect Secrets", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            int n = 0;
            foreach (Account a in Store.Accounts)
                if (a.SecretsFound != 0) { a.SecretsFound = 0; n++; }
            Store.SaveAccounts();
            AddLog("Detect Secrets: reset the secret counter on " + n + " account(s).");
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
            UpdateSecretDetectorGating();
            int count = UsageMonitor.Count;
            if (count == 0) { lblBetaLive.Text = Lang.T("Right now: no Roblox window is running."); return; }
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
