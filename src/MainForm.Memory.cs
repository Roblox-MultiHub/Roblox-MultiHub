using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // Performance page > Memory (RAM): live RAM use, Lang.T("Clean memory now") and automatic cleaning over a limit.
    public partial class MainForm
    {
        Label lblRam, lblMemState;
        CheckBox chkAutoClean, chkMemWs, chkMemCache, chkMemModified, chkMemStandby, chkMemStandbyFull, chkMemCombine;
        NumericUpDown numMemLimit, numMemInterval;
        RoundButton btnCleanNow;
        long memSeen;                       // LastTicks of the last clean already written to the log
        DateTime memHelperStarted = DateTime.MinValue;
        bool memBusy;
        bool loadingMem;

        void BuildMemorySection(PageBuilder p)
        {
            loadingMem = true;
            MemSettings M = Store.Settings.Mem;
            p.Section("Memory (RAM)");
            lblRam = Ui.Lbl("", "");
            p.Row(lblRam);

            btnCleanNow = Ui.Btn("Clean memory now", 150, delegate { CleanMemoryNow(); }, "primary");
            p.Opt("Frees RAM right away.",
                "Empties the memory areas ticked below, like Mem Reduct does. Windows needs administrator rights for that, so the first time you are asked for permission.\n\nWhat it does: programs (and Roblox windows) give back memory they are not using right now, and Windows' caches are emptied. The RAM counter drops at once. Programs take back what they really need, so a window can hitch for a moment right after a clean - that is normal.\n\nIf you say No to the permission, only the Roblox windows started from this app are trimmed (that needs no admin).",
                btnCleanNow);

            chkAutoClean = Ui.Chk("Clean automatically when RAM use goes over", M.AutoClean, null);
            numMemLimit = Ui.Num(30, 98, M.Threshold, 60);
            p.Opt("Cleans at most once a minute.",
                "When ON, a small helper runs as administrator in the background (you are asked for permission when you turn this on and every time MultiHub starts). As soon as the RAM in use goes over this percentage, it cleans. It waits at least a minute before the next clean, so it never keeps cleaning in a loop.\n\n80% is a good limit. The helper stops when you close MultiHub.",
                chkAutoClean, numMemLimit, Ui.Lbl("%", "muted"));
            numMemInterval = Ui.Num(0, 1440, M.IntervalMin, 60);
            p.Opt("Extra clean on a timer (0 = off).",
                "Besides the limit above, also clean every this many minutes while auto-clean is ON, even when the RAM use is below the limit. 0 = only clean when the limit is reached.",
                Ui.Fld("Also every (minutes)", 160), numMemInterval);
            lblMemState = Ui.Lbl("", "muted");
            p.Row(lblMemState);

            chkMemWs = Ui.Chk("Trim running programs (working sets)", M.WorkingSets, null);
            p.Opt("The biggest effect: programs give memory back.",
                "Every program, including each Roblox window, hands back the memory it is not actively using. Recommended.", chkMemWs);
            chkMemCache = Ui.Chk("Empty the system file cache", M.FileCache, null);
            p.Opt("Empties the file cache.",
                "Windows keeps recently read files in RAM to open them faster. This empties that cache. Recommended.", chkMemCache);
            chkMemModified = Ui.Chk("Flush modified pages", M.ModifiedList, null);
            p.Opt("Writes changed memory to disk.",
                "Memory that was changed and is waiting to be written to the page file is written now, so it can be freed. Recommended.", chkMemModified);
            chkMemStandby = Ui.Chk("Empty the low-priority standby list", M.StandbyLow, null);
            p.Opt("Frees cached memory Windows rarely needs.",
                "The standby list is memory Windows keeps 'just in case'. This empties only its least important part. Recommended.", chkMemStandby);
            chkMemStandbyFull = Ui.Chk("Empty the whole standby list", M.StandbyFull, null);
            p.Opt("Frees the most; slower reloads.",
                "Empties the complete standby list. Frees the most memory, but files and programs you use often have to be read from the disk again. OFF by default.", chkMemStandbyFull);
            chkMemCombine = Ui.Chk("Combine identical memory pages", M.Combine, null);
            p.Opt("Merges duplicate memory across windows.",
                "Windows looks for memory pages with exactly the same content and keeps only one copy. With many Roblox windows there is a lot of duplicate memory, but the search itself costs some CPU for a moment. OFF by default.", chkMemCombine);

            EventHandler changed = delegate { OnMemChanged(); };
            numMemLimit.ValueChanged += changed;
            numMemInterval.ValueChanged += changed;
            foreach (CheckBox c in new CheckBox[] { chkMemWs, chkMemCache, chkMemModified, chkMemStandby, chkMemStandbyFull, chkMemCombine })
                c.CheckedChanged += changed;
            chkAutoClean.CheckedChanged += delegate { OnAutoCleanToggled(); };

            MemStatus st = MemClean.ReadStatus();
            memSeen = st == null ? 0 : st.LastTicks;
            loadingMem = false;
            UpdateMemUi();
        }

        void OnMemChanged()
        {
            if (loadingMem) return;
            MemSettings M = Store.Settings.Mem;
            M.Threshold = (int)numMemLimit.Value;
            M.IntervalMin = (int)numMemInterval.Value;
            M.WorkingSets = chkMemWs.Checked;
            M.FileCache = chkMemCache.Checked;
            M.ModifiedList = chkMemModified.Checked;
            M.StandbyLow = chkMemStandby.Checked;
            M.StandbyFull = chkMemStandbyFull.Checked;
            M.Combine = chkMemCombine.Checked;
            Store.SaveSettings();                           // the helper re-reads the settings file by itself
            UpdateMemUi();
        }

        void OnAutoCleanToggled()
        {
            if (loadingMem) return;
            Store.Settings.Mem.AutoClean = chkAutoClean.Checked;
            Store.SaveSettings();
            if (chkAutoClean.Checked)
            {
                DialogResult ok = MessageBox.Show(this,
                    "Auto-clean starts a small helper that runs as administrator and automatically frees Windows' memory whenever RAM use goes over your limit. Windows will ask you for permission.\r\n\r\nNothing is cleaned until you turn this on. Turn automatic RAM cleaning ON now?",
                    "Automatic RAM cleaning", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ok != DialogResult.Yes)
                {
                    loadingMem = true; chkAutoClean.Checked = false; loadingMem = false;
                    Store.Settings.Mem.AutoClean = false; Store.SaveSettings();
                    UpdateMemUi();
                    return;
                }
                if (StartMemHelper()) AddLog("Auto-clean ON: memory is cleaned when more than " + Store.Settings.Mem.Threshold + "% of the RAM is in use.");
                else SetAutoCleanOff("Auto-clean needs administrator permission - it was turned off again.");
            }
            else AddLog("Auto-clean OFF.");
            UpdateMemUi();
        }

        void SetAutoCleanOff(string why)
        {
            Store.Settings.Mem.AutoClean = false;
            Store.SaveSettings();
            loadingMem = true;
            chkAutoClean.Checked = false;
            loadingMem = false;
            AddLog(why);
        }

        // Starts the admin helper (asks for permission unless this app already runs as administrator).
        bool StartMemHelper()
        {
            if (MemClean.HelperAlive()) return true;
            try
            {
                Process.Start(MemStartInfo("--memhelper " + Process.GetCurrentProcess().Id));
                memHelperStarted = DateTime.UtcNow;
                return true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1223) Log.Error("StartMemHelper", ex);   // 1223 = you clicked No
                return false;
            }
            catch (Exception ex) { Log.Error("StartMemHelper", ex); return false; }
        }

        // A hidden administrator copy of this exe. The data folder is passed along, so it is also right when
        // Windows asks for another administrator's password; the permission popup belongs to this window.
        ProcessStartInfo MemStartInfo(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, args + " --datadir \"" + Store.Dir + "\"");
            psi.UseShellExecute = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.ErrorDialog = false;
            if (IsHandleCreated) psi.ErrorDialogParentHandle = Handle;
            if (!MemClean.IsElevated()) psi.Verb = "runas";
            return psi;
        }

        // Called once after start-up: auto-clean was left ON last time.
        void StartMemOnLaunch()
        {
            if (!Store.Settings.Mem.AutoClean) return;
            if (!StartMemHelper()) SetAutoCleanOff("Auto-clean needs administrator permission and you said No - it is OFF for now (turn it on again on the Performance page).");
        }

        async void CleanMemoryNow()
        {
            if (memBusy) return;
            memBusy = true;
            btnCleanNow.Enabled = false;
            try
            {
                if (MemClean.HelperAlive())
                {
                    File.WriteAllText(MemClean.RequestPath, "clean");           // the running helper picks this up within a second
                    for (int i = 0; i < 40 && !NewMemResult(); i++) await Task.Delay(250);
                }
                else
                {
                    ProcessStartInfo psi = MemStartInfo("--memclean");
                    bool ok = await Task.Run(delegate
                    {
                        try
                        {
                            using (Process pr = Process.Start(psi)) pr.WaitForExit(30000);
                            return true;
                        }
                        catch (Exception) { return false; }
                    });
                    if (!ok)
                    {
                        long freed = MemClean.TrimProcesses(Launcher.RobloxPids());
                        AddLog("No administrator permission - only the Roblox windows were trimmed: " + Util.FormatBytes(freed) + " freed.");
                    }
                }
                CheckMemResult();
            }
            catch (Exception ex) { Log.Error("CleanMemoryNow", ex); }
            btnCleanNow.Enabled = true;
            memBusy = false;
        }

        bool NewMemResult()
        {
            MemStatus st = MemClean.ReadStatus();
            return st != null && st.LastTicks != memSeen;
        }

        // Writes new clean results (from the helper or a one-time clean) to the log.
        void CheckMemResult()
        {
            MemStatus st = MemClean.ReadStatus();
            if (st == null || st.LastTicks == memSeen) return;
            memSeen = st.LastTicks;
            string msg = "Memory cleaned (" + st.Reason + "): " + Util.FormatBytes(st.LastFreed) + " freed, RAM in use " + st.LoadBefore + "% -> " + st.LoadAfter + "%.";
            if (st.Errors.Length > 0) msg += "  Not possible: " + st.Errors + ".";
            AddLog(msg);
        }

        // Every 2 seconds from the timer.
        void UpdateMemUi()
        {
            if (lblRam == null) return;
            int load; long total; long avail;
            if (MemClean.Status(out load, out total, out avail))
            {
                lblRam.Text = "RAM in use right now: " + load + "%  (" + Util.FormatBytes(total - avail) + " of " + Util.FormatBytes(total) + ")"
                    + (UsageMonitor.Count > 0 ? "  -  Roblox: " + Util.FormatBytes(UsageMonitor.TotalRam) : "");
                lblRam.ForeColor = load >= Store.Settings.Mem.Threshold ? Theme.Warn : Theme.Text;
            }
            CheckMemResult();
            MemSettings M = Store.Settings.Mem;
            bool alive = MemClean.HelperAlive();
            MemStatus st = MemClean.ReadStatus();
            string last = st != null && st.LastTicks > 0 ? "  Last clean " + Util.Ago(st.LastTicks) + ": " + Util.FormatBytes(st.LastFreed) + " freed." : "";
            if (!M.AutoClean) lblMemState.Text = "Auto-clean is off." + last;
            else if (alive) lblMemState.Text = "Auto-clean is running (as administrator) - cleans above " + M.Threshold + "%." + last;
            else if ((DateTime.UtcNow - memHelperStarted).TotalSeconds < 10) lblMemState.Text = "Auto-clean is starting...";
            else lblMemState.Text = "Auto-clean is on but the helper is not running - turn it off and on to start it again." + last;
            lblMemState.ForeColor = M.AutoClean && !alive ? Theme.Warn : Theme.Muted;
            numMemInterval.Enabled = M.AutoClean;
            chkMemStandby.Enabled = !M.StandbyFull;
        }
    }
}
