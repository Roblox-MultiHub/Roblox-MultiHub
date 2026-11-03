using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // Presets: save the accounts that are running (in launch order), the game each one is in, their window
    // positions and all launch / layout / performance settings - and bring all of it back with one click.
    public partial class MainForm
    {
        RoundTextBox tbPresetName;
        ListView lvPresets;
        CheckBox chkExactServers;
        ComboBox cbStartupPreset;
        bool loadingPresets;

        void BuildPresetsPage()
        {
            loadingPresets = true;
            Panel page = NewPage("Presets");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Presets", "Save your whole setup - accounts, games, window layout and settings - and start it again with one click.");

            p.Section("Save what is running now");
            tbPresetName = Ui.Txt(220, "");
            tbPresetName.Tip("A name for this setup, for example 'Farm 6 accounts'.");
            p.Opt("Saves the running accounts, their games, window positions and settings.",
                "Saves:\n- every account this app launched that is still running, in the order you launched them\n- the game / server / private server / followed player each one joined\n- where every Roblox window is right now (position, size, border, minimized, in grid or not)\n- the settings of the Launch, Layout and Performance pages and the launch options on the Settings page (FPS cap, graphics, CPU priority, delay, grid, auto-relaunch ...)\n\nArrange the windows the way you like first (Layout page or by hand), then save.\n\nIf nothing launched by this app is running, the SELECTED accounts are saved with their own game (or the Launch page) and the windows are arranged with the grid when the preset runs.\n\nUsing an existing name replaces that preset.",
                Ui.Fld("Preset name", 100), tbPresetName, Ui.Btn("Save as preset", 130, delegate { SavePresetFromUi(); }, "primary"));

            p.Section("Your presets");
            lvPresets = new BufferedListView();
            lvPresets.Size = new Size(PageW - PageBuilder.PadX * 2, 180);
            lvPresets.Columns.Add("Name", 170);
            lvPresets.Columns.Add("Accounts", 70);
            lvPresets.Columns.Add("Saved", 110);
            lvPresets.Columns.Add("Accounts in launch order", 150);
            lvPresets.MultiSelect = false;
            ListStyle.Attach(lvPresets);
            lvPresets.DoubleClick += delegate { RunSelectedPreset(); };
            lvPresets.SelectedIndexChanged += delegate
            {
                Preset sp = SelectedPreset();
                if (sp != null) tbPresetName.Text = sp.Name;
            };
            p.Add(lvPresets);
            p.Opt("Run starts everything exactly like it was saved (or double-click a preset).",
                "'Run' puts the saved settings back, launches the accounts one after another in the saved order into their saved games, waits for the windows and moves every window to its saved position and size. Accounts that are already running are not launched again, only moved.\n\n'Stop' on the Launch page cancels a running preset. 'Update' replaces the selected preset with what is running now. 'Details' shows exactly what is inside.",
                Ui.Btn("Run", 80, delegate { RunSelectedPreset(); }, "primary"),
                Ui.Btn("Update", 80, delegate { UpdateSelectedPreset(); }, ""),
                Ui.Btn("Rename...", 90, delegate { RenameSelectedPreset(); }, ""),
                Ui.Btn("Details", 80, delegate { ShowPresetDetails(); }, ""),
                Ui.Btn("Delete", 76, delegate { DeleteSelectedPreset(); }, "danger"));

            p.Section("Options");
            chkExactServers = Ui.Chk("Rejoin the exact same servers (Job ID)", Store.Settings.PresetExactServers, null);
            chkExactServers.CheckedChanged += delegate
            {
                if (loadingPresets) return;
                Store.Settings.PresetExactServers = chkExactServers.Checked;
                Store.SaveSettings();
            };
            p.Opt("Off = join the same game, on any server (servers close after a while).",
                "A preset remembers the exact server every account was in. Roblox servers shut down when they are empty or old, so after a restart that server usually no longer exists and the join fails.\n\nOFF (default): accounts join the same game on any server; private servers and followed players are always kept.\nON: accounts try the exact saved server - handy to bring accounts back into the same server shortly after saving.",
                chkExactServers);
            cbStartupPreset = Ui.Cmb(260, "(none)");
            cbStartupPreset.SelectedIndexChanged += delegate
            {
                if (loadingPresets) return;
                Store.Settings.StartupPreset = cbStartupPreset.SelectedIndex <= 0 ? "" : cbStartupPreset.SelectedItem.ToString();
                Store.SaveSettings();
                if (Store.Settings.StartupPreset.Length > 0) AddLog("Preset '" + Store.Settings.StartupPreset + "' will run every time MultiHub starts.");
            };
            p.Opt("Runs a preset by itself every time MultiHub starts.",
                "Together with 'Start MultiHub with Windows' (Settings page) your whole setup comes back after a restart of the PC without clicking anything. The preset starts a few seconds after the app opened.",
                Ui.Fld("Run at startup", 100), cbStartupPreset);

            loadingPresets = false;
            RefreshPresetList();
        }

        void RefreshPresetList()
        {
            if (lvPresets == null) return;
            string keep = SelectedPreset() == null ? null : SelectedPreset().Name;
            lvPresets.BeginUpdate();
            lvPresets.Items.Clear();
            foreach (Preset pr in Store.Settings.Presets)
            {
                string who = string.Join(", ", pr.Entries.Select(e => EntryName(e)).ToArray());
                ListViewItem it = new ListViewItem(new string[] { pr.Name, pr.Entries.Count.ToString(), pr.SavedTicks > 0 ? Util.Ago(pr.SavedTicks) : "-", who });
                it.Tag = pr;
                lvPresets.Items.Add(it);
                if (pr.Name == keep) it.Selected = true;
            }
            lvPresets.EndUpdate();

            loadingPresets = true;
            cbStartupPreset.Items.Clear();
            cbStartupPreset.Items.Add("(none)");
            foreach (Preset pr in Store.Settings.Presets) cbStartupPreset.Items.Add(pr.Name);
            int idx = cbStartupPreset.Items.IndexOf(Store.Settings.StartupPreset);
            cbStartupPreset.SelectedIndex = idx > 0 ? idx : 0;
            loadingPresets = false;
        }

        Preset SelectedPreset()
        {
            if (lvPresets == null || lvPresets.SelectedItems.Count == 0) return null;
            return lvPresets.SelectedItems[0].Tag as Preset;
        }

        Preset FindPreset(string name)
        {
            return Store.Settings.Presets.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        string EntryName(PresetEntry e)
        {
            Account a = Store.Accounts.FirstOrDefault(x => x.Id == e.AccountId);
            return a != null ? AppState.Name(a) : e.Username + " (removed)";
        }

        // ---------------- saving ----------------
        void SavePresetFromUi()
        {
            string name = tbPresetName.Text.Trim();
            if (name.Length == 0) { AddLog("Type a name for the preset first."); tbPresetName.Focus(); return; }
            Preset old = FindPreset(name);
            if (old != null && MessageBox.Show(this, "Replace the preset '" + old.Name + "' with what is running now?", "Save preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Preset pr = CapturePreset(name);
            if (pr == null) return;
            if (old != null) Store.Settings.Presets[Store.Settings.Presets.IndexOf(old)] = pr;
            else Store.Settings.Presets.Add(pr);
            Store.SaveSettings();
            RefreshPresetList();
            int withWin = pr.Entries.Count(e => e.HasRect || e.Minimized);
            AddLog("Preset '" + name + "' saved: " + pr.Entries.Count + " account(s), " + withWin + " window position(s) and all settings.");
        }

        void UpdateSelectedPreset()
        {
            Preset sel = SelectedPreset();
            if (sel == null) { AddLog("Select a preset in the list first."); return; }
            tbPresetName.Text = sel.Name;
            SavePresetFromUi();
        }

        Preset CapturePreset(string name)
        {
            RefreshWindowList();
            Preset pr = new Preset();
            pr.Name = name;
            pr.SavedTicks = DateTime.UtcNow.Ticks;
            pr.Settings = PresetSettings.Capture(Store.Settings);

            List<Instance> live = Launcher.Instances.Where(i => Launcher.IsAlive(i.Pid)).OrderBy(i => i.Started).ToList();
            if (live.Count == 0)
            {
                List<Account> sel = SelectedAccounts();
                if (sel.Count == 0)
                {
                    MessageBox.Show(this, "Nothing launched by this app is running, and no accounts are selected.\r\n\r\nLaunch your accounts and arrange the windows first (or select the accounts you want in the preset), then save again.", "Save preset");
                    return null;
                }
                if (MessageBox.Show(this, "Nothing launched by this app is running right now.\r\n\r\nSave the " + sel.Count + " selected account(s) with their own game (or the Launch page)? Their windows will be arranged with the grid when the preset runs.", "Save preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return null;
                foreach (Account a in sel)
                {
                    PresetEntry e = new PresetEntry();
                    e.AccountId = a.Id;
                    e.Username = a.Username;
                    pr.Entries.Add(e);
                }
                return pr;
            }

            using (new DpiScope())
            {
                foreach (Instance inst in live)
                {
                    if (pr.Entries.Any(x => x.AccountId == inst.Account.Id)) continue;     // one entry per account
                    PresetEntry e = new PresetEntry();
                    e.AccountId = inst.Account.Id;
                    e.Username = inst.Account.Username;
                    e.Spec = inst.Spec == null ? null : inst.Spec.Copy();
                    WinRow row = winRows.FirstOrDefault(r => r.Info.Pid == inst.Pid);
                    if (row != null)
                    {
                        e.GridIndex = winRows.IndexOf(row);
                        e.Include = row.Include || row.Pin.HasValue;
                        if (row.Info.HasWindow)
                        {
                            e.Borderless = WindowLayout.IsBorderless(row.Info.Hwnd);
                            if (Native.IsIconic(row.Info.Hwnd)) e.Minimized = true;
                            else
                            {
                                Rectangle r = WindowLayout.VisibleRect(row.Info.Hwnd);
                                if (r.Width > 0 && r.Height > 0)
                                {
                                    e.HasRect = true;
                                    e.X = r.X; e.Y = r.Y; e.W = r.Width; e.H = r.Height;
                                }
                            }
                        }
                    }
                    pr.Entries.Add(e);
                }
            }
            return pr;
        }

        // ---------------- managing ----------------
        void RenameSelectedPreset()
        {
            Preset sel = SelectedPreset();
            if (sel == null) { AddLog("Select a preset in the list first."); return; }
            string n = Dialogs.Prompt(this, "Rename preset", "New name for '" + sel.Name + "':", sel.Name);
            if (string.IsNullOrEmpty(n) || n == sel.Name) return;
            Preset other = FindPreset(n);
            if (other != null && other != sel) { AddLog("There is already a preset called '" + n + "'."); return; }
            if (Store.Settings.StartupPreset == sel.Name) Store.Settings.StartupPreset = n;
            sel.Name = n;
            Store.SaveSettings();
            tbPresetName.Text = n;
            RefreshPresetList();
        }

        void DeleteSelectedPreset()
        {
            Preset sel = SelectedPreset();
            if (sel == null) { AddLog("Select a preset in the list first."); return; }
            if (MessageBox.Show(this, "Delete the preset '" + sel.Name + "'?\r\n(Accounts and windows are not touched.)", "Delete preset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            Store.Settings.Presets.Remove(sel);
            if (Store.Settings.StartupPreset == sel.Name) Store.Settings.StartupPreset = "";
            Store.SaveSettings();
            RefreshPresetList();
            AddLog("Preset '" + sel.Name + "' deleted.");
        }

        void ShowPresetDetails()
        {
            Preset pr = SelectedPreset();
            if (pr == null) { AddLog("Select a preset in the list first."); return; }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Preset:  " + pr.Name + "   (saved " + (pr.SavedTicks > 0 ? new DateTime(pr.SavedTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "?") + ")");
            sb.AppendLine();
            sb.AppendLine("ACCOUNTS (launch order)");
            for (int i = 0; i < pr.Entries.Count; i++)
            {
                PresetEntry e = pr.Entries[i];
                string where = e.Spec != null ? e.Spec.Describe() : "its own game / the Launch page";
                string win = e.Minimized ? "minimized" : (e.HasRect ? e.X + ", " + e.Y + "  " + e.W + " x " + e.H + (e.Borderless ? "  borderless" : "") : "arranged by the grid");
                if (!e.Include) win += "  (not in grid)";
                sb.AppendLine((i + 1) + ". " + EntryName(e) + "  -  " + where);
                sb.AppendLine("     window: " + win);
            }
            PresetSettings s = pr.Settings ?? new PresetSettings();
            LayoutSettings L = s.Layout ?? new LayoutSettings();
            sb.AppendLine();
            sb.AppendLine("SETTINGS");
            sb.AppendLine("FPS cap:            " + (s.FpsCap > 0 ? s.FpsCap.ToString() : "Roblox decides"));
            sb.AppendLine("Graphics preset:    " + (s.Gfx == null ? "Default" : s.Gfx.Preset));
            sb.AppendLine("CPU priority:       " + s.Priority);
            sb.AppendLine("Launch method:      " + s.LaunchMethod + ",  " + s.LaunchDelaySec + " s between accounts");
            sb.AppendLine("Layout:             " + L.Mode + (L.Mode == "Grid" ? " " + L.Rows + " x " + L.Cols : "") + ",  scale " + L.Scale + "%,  gap " + L.Gap + ",  margin " + L.Margin
                + (L.Borderless ? ",  borderless" : "") + (L.AlwaysOnTop ? ",  always on top" : "") + (L.AutoCapture ? ",  auto-capture" : ""));
            sb.AppendLine("Auto-relaunch:      " + (s.AutoRelaunch ? "on" : "off"));
            Dialogs.ShowText(this, "Preset - " + pr.Name, sb.ToString());
        }

        // ---------------- running ----------------
        void RunSelectedPreset()
        {
            Preset sel = SelectedPreset();
            if (sel == null) { AddLog("Select a preset in the list first."); return; }
            RunPreset(sel);
        }

        async void RunStartupPreset()
        {
            Preset pr = FindPreset(Store.Settings.StartupPreset ?? "");
            if (pr == null) return;
            AddLog("Starting preset '" + pr.Name + "' (set to run at startup on the Presets page)...");
            await Task.Delay(3000);                         // let multi-instance grab its lock first
            if (!IsDisposed) RunPreset(pr);
        }

        // Pushes the settings that a preset may have changed into the controls of every page.
        void ReloadSettingControls()
        {
            AppSettings S = Store.Settings;
            SyncSettingsControls();
            bool was = loadingSettings;
            loadingSettings = true;
            SetNum(numWatcher, S.WatcherSec);
            loadingSettings = was;

            loadingPerf = true;
            SetNum(numFps, S.FpsCap);
            tbCustomFlags.Text = S.CustomClientSettings;
            loadingPerf = false;
            LoadGfxControls();
            UpdatePreview();

            SetNum(numDelay, S.LaunchDelaySec);
            chkUseSaved.Checked = S.UseSavedPerAccount;
            cbPlace.Text = S.LastPlaceId;
            tbJob.Text = S.LastJobId;
            tbPrivate.Text = S.LastPrivate;
            tbFollow.Text = S.LastFollow;
            if (S.LastMode == "Job") rbJob.Checked = true;
            else if (S.LastMode == "Private") rbPrivate.Checked = true;
            else if (S.LastMode == "Follow") rbFollow.Checked = true;
            else rbGame.Checked = true;

            SyncLayoutControls();
        }

        static void SetNum(NumericUpDown n, int v)
        {
            n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, v));
        }

        async void RunPreset(Preset pr)
        {
            if (launching) { AddLog("A launch is already running. Press Stop to cancel it."); return; }
            AddLog("Running preset '" + pr.Name + "'...");

            // Remember your own MultiHub settings, so the preset's settings apply only for this run and
            // are handed back afterwards. Running a preset must never permanently change your settings.
            PresetSettings mySettings = PresetSettings.Capture(Store.Settings);
            if (pr.Settings != null)
            {
                pr.Settings.ApplyTo(Store.Settings);
                Store.SaveSettings();
                ReloadSettingControls();
                AddLog("Preset settings applied for this run (FPS cap, graphics, priority, layout, launch options) - your own settings come back when it finishes.");
            }
            if (pr.Entries.Count > 1 && !Store.Settings.MultiInstance)
            {
                Store.Settings.MultiInstance = true;
                Store.SaveSettings();
                SyncSettingsControls();
                AddLog("Multi-instance turned ON - the preset runs several accounts.");
            }

            SetLaunching(true);
            launchCts = new CancellationTokenSource();
            CancellationToken ct = launchCts.Token;
            try
            {
                RebuildRunningIds();
                int delay = Math.Max(0, Store.Settings.LaunchDelaySec);
                bool launchedAny = false;
                for (int i = 0; i < pr.Entries.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    PresetEntry e = pr.Entries[i];
                    Account acc = Store.Accounts.FirstOrDefault(x => x.Id == e.AccountId);
                    if (acc == null) { AddLog("Preset: account " + e.Username + " is no longer in the list - skipped."); continue; }
                    if (runningIds.Contains(acc.Id)) { AddLog("Preset: " + AppState.Name(acc) + " is already running - only its window is moved."); continue; }
                    LaunchSpec spec = await PresetSpec(e, acc);
                    if (spec == null) { AddLog("Preset: no game known for " + AppState.Name(acc) + " - skipped."); continue; }
                    if (launchedAny && delay > 0) await Task.Delay(delay * 1000, ct);
                    if (spec.PlaceId > 0) AddRecent(spec.PlaceId);
                    await Launcher.LaunchAsync(acc, spec, AddLog);
                    launchedAny = true;
                    UpdateStatusCells();
                }
                if (!ct.IsCancellationRequested)
                {
                    await RestorePresetWindows(pr, ct);
                    AddLog("Preset '" + pr.Name + "' is ready.");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Error("RunPreset", ex); AddLog("Preset failed: " + ex.Message); }
            if (ct.IsCancellationRequested) AddLog("Preset cancelled.");
            launchCts = null;
            // Hand your own MultiHub settings back - a preset keeps its settings to itself and never changes yours.
            if (pr.Settings != null)
            {
                mySettings.ApplyTo(Store.Settings);
                Store.SaveSettings();
                ReloadSettingControls();
                AddLog("Your own MultiHub settings are back.");
            }
            SetLaunching(false);
            UpdateStatusCells();
        }

        // Where an account of a preset goes: the saved game (any server unless 'exact servers' is on),
        // otherwise its own game, otherwise the Launch page that the preset restored.
        async Task<LaunchSpec> PresetSpec(PresetEntry e, Account acc)
        {
            if (e.Spec != null)
            {
                LaunchSpec s = e.Spec.Copy();
                if (s.Kind == "Job" && !Store.Settings.PresetExactServers) { s.Kind = "Game"; s.JobId = ""; }
                return s;
            }
            LaunchSpec own = SavedSpec(acc);
            if (own != null) return own;
            return await BuildSpec(acc);
        }

        // Waits until the windows of the preset's accounts are open, then puts each one back where it was saved.
        async Task RestorePresetWindows(Preset pr, CancellationToken ct)
        {
            List<PresetEntry> want = pr.Entries.Where(e => Store.Accounts.Any(a => a.Id == e.AccountId)).ToList();
            if (want.Count == 0) return;
            AddLog("Waiting for the Roblox windows to open...");
            DateTime until = DateTime.Now.AddSeconds(90);
            while (DateTime.Now < until)
            {
                RefreshWindowList();
                int open = want.Count(e => RowFor(e) != null);
                int running = want.Count(e => Launcher.Instances.Any(i => i.Account.Id == e.AccountId && Launcher.IsAlive(i.Pid)));
                if (open >= running) break;
                await Task.Delay(1000, ct);
            }
            PlacePresetWindows(pr);
            // Roblox can resize its window once the game has loaded - put everything back once more.
            await Task.Delay(6000, ct);
            PlacePresetWindows(pr);
        }

        WinRow RowFor(PresetEntry e)
        {
            foreach (WinRow r in winRows)
            {
                if (!r.Info.HasWindow) continue;
                Instance inst = Launcher.FindInstance(r.Info.Pid);
                if (inst != null && inst.Account.Id == e.AccountId) return r;
            }
            return null;
        }

        void PlacePresetWindows(Preset pr)
        {
            RefreshWindowList();
            // the window list follows the saved grid order, so 'Arrange now' later gives the same cells
            Dictionary<WinRow, int> rank = new Dictionary<WinRow, int>();
            foreach (PresetEntry e in pr.Entries)
            {
                WinRow r = RowFor(e);
                if (r == null) continue;
                rank[r] = e.GridIndex >= 0 ? e.GridIndex : 1000 + pr.Entries.IndexOf(e);
                r.Include = e.Include;
            }
            winRows = winRows.OrderBy(r => rank.ContainsKey(r) ? rank[r] : 100000 + winRows.IndexOf(r)).ToList();

            List<MonitorDesc> mons = WindowLayout.GetMonitors();
            List<KeyValuePair<WinRow, PresetEntry>> exact = new List<KeyValuePair<WinRow, PresetEntry>>();
            bool needGrid = false;
            foreach (PresetEntry e in pr.Entries)
            {
                WinRow r = RowFor(e);
                if (r == null) continue;
                Rectangle rect = new Rectangle(e.X, e.Y, e.W, e.H);
                if (e.HasRect && mons.Any(m => m.Bounds.IntersectsWith(rect))) exact.Add(new KeyValuePair<WinRow, PresetEntry>(r, e));
                else if (!e.Minimized && e.Include) needGrid = true;
            }
            if (needGrid) ArrangeNow();                     // accounts without a saved position use the grid

            LayoutSettings L = Store.Settings.Layout;
            using (new DpiScope())
            {
                foreach (KeyValuePair<WinRow, PresetEntry> kv in exact)
                {
                    LayoutSettings one = Json.From<LayoutSettings>(Json.To(L));
                    one.Borderless = kv.Value.Borderless;
                    WindowLayout.Place(kv.Key.Info, new Rectangle(kv.Value.X, kv.Value.Y, kv.Value.W, kv.Value.H), one, true);
                }
                foreach (PresetEntry e in pr.Entries)
                {
                    WinRow r = RowFor(e);
                    if (r != null && e.Minimized) WindowLayout.Minimize(r.Info.Hwnd);
                }
                if (L.LabelTitles)
                    foreach (WinRow r in winRows)
                        if (r.Info.HasWindow && !string.IsNullOrEmpty(r.Info.Account)) WindowLayout.SetTitle(r.Info.Hwnd, "Roblox - " + r.Info.Account);
            }
            lastSet = new HashSet<IntPtr>(winRows.Where(r => r.Info.HasWindow).Select(r => r.Info.Hwnd));   // auto-capture: nothing new
            lastWinSig = "";
            RefreshWindowList();
            AddLog("Preset windows placed: " + exact.Count + " at their saved position" + (needGrid ? ", the others with the grid." : "."));
        }
    }
}
