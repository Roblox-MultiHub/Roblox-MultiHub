using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        RadioButton rbGame, rbJob, rbPrivate, rbFollow;
        ComboBox cbPlace;
        RoundTextBox tbJob, tbPrivate, tbFollow;
        Button btnRandom;
        NumericUpDown numDelay;
        CheckBox chkUseSaved;
        Button btnLaunch, btnLaunchAll, btnStop;
        Label lblLaunchSel;
        CancellationTokenSource launchCts;
        bool launching;
        readonly Random rng = new Random();

        const int PageW = 600;

        void BuildLaunchPage()
        {
            Panel page = NewPage("Launch");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Launch / Join", "Choose where the selected accounts go, then press Launch.");

            lblLaunchSel = Ui.Lbl("Selected accounts: none - click accounts on the left (Ctrl / Shift for several).", "warn");
            p.Row(lblLaunchSel);

            p.Section("Where should they go?");

            rbGame = Ui.Rad("Join a game", Store.Settings.LastMode == "Game" || string.IsNullOrEmpty(Store.Settings.LastMode), null);
            p.Opt("Roblox picks any public server, like the normal Play button.",
                "Starts the game exactly like pressing Play on the website: Roblox chooses a public server for every account.\n\nEnter the game's Place ID in the box below, or paste its roblox.com/games/... link - the app finds the Place ID itself. Games you launched before are remembered in the drop-down list.",
                rbGame);
            cbPlace = new DarkCombo();
            cbPlace.DropDownStyle = ComboBoxStyle.DropDown;
            cbPlace.Width = 250;
            cbPlace.Text = Store.Settings.LastPlaceId;
            p.Opt("Type the Place ID, or search a game by name.",
                "Paste a number such as 920587237 or a full roblox.com/games/... link - the app finds the Place ID itself. Games you launched before are in the drop-down list.\n\nOr press 'Search game...' to look a game up by name: a popup lists the matches with their players online, and the one you pick is filled in here.",
                Ui.Fld("Place ID or link", 120), cbPlace, Ui.Btn("Search game...", 120, delegate { SearchGameForLaunch(); }, ""));
            RefreshRecentCombo();

            rbJob = Ui.Rad("Join a specific server", Store.Settings.LastMode == "Job", null);
            p.Opt("Put accounts into one exact server (Job ID).",
                "Joins one specific server, identified by its Job ID. This is how you get several accounts into the SAME server.\n\nPick a server on the Servers page, paste a Job ID yourself, or press 'Random server' to let the app choose an open public server of the Place ID above.",
                rbJob);
            tbJob = Ui.Txt(170, Store.Settings.LastJobId);
            btnRandom = Ui.Btn("Random", 80, delegate { PickRandomServer(); }, "");
            p.Opt("Pick a server from a list, or let the app choose one.",
                "'Pick server...' opens the running servers of the game above in a popup. Sort them by fewest players, most players, lowest ping or highest FPS, then pick one. 'Random' picks any open public server. You can also paste a Job ID yourself.",
                Ui.Fld("Job ID", 120), tbJob, btnRandom, Ui.Btn("Pick server...", 110, delegate { PickServerForLaunch(); }, ""));

            rbPrivate = Ui.Rad("Join a private server", Store.Settings.LastMode == "Private", null);
            p.Opt("Join a VIP / private server.",
                "Needs a private server link that the account is allowed to use (as owner or invited).\n\nBoth old links (...?privateServerLinkCode=...) and new share links (roblox.com/share?code=...&type=Server) work. The first selected account is used to resolve a share link.",
                rbPrivate);
            tbPrivate = Ui.Txt(340, Store.Settings.LastPrivate);
            p.Row(Ui.Fld("Private server link", 120), tbPrivate);

            rbFollow = Ui.Rad("Follow a player", Store.Settings.LastMode == "Follow", null);
            p.Opt("Join the game a player is currently in.",
                "Type a Roblox username. The accounts join the game that player is playing right now. This only works if the player is in a game and their privacy settings allow joining them. The app warns you when the player does not look joinable.",
                rbFollow);
            tbFollow = Ui.Txt(230, Store.Settings.LastFollow);
            p.Row(Ui.Fld("Roblox username", 120), tbFollow);

            EventHandler modeChanged = delegate { UpdateLaunchModeUi(); };
            rbGame.CheckedChanged += modeChanged;
            rbJob.CheckedChanged += modeChanged;
            rbPrivate.CheckedChanged += modeChanged;
            rbFollow.CheckedChanged += modeChanged;
            if (!rbGame.Checked && !rbJob.Checked && !rbPrivate.Checked && !rbFollow.Checked) rbGame.Checked = true;

            p.Section("Launch options");
            numDelay = Ui.Num(0, 600, Store.Settings.LaunchDelaySec, 70);
            numDelay.ValueChanged += delegate { Store.Settings.LaunchDelaySec = (int)numDelay.Value; Store.SaveSettings(); };
            p.Opt("Pause between accounts so your PC is not overloaded.",
                "Starting many clients at the same instant often makes some of them fail to load. 6-10 seconds is a good default; lower it on a fast PC, raise it if windows fail to start.",
                Ui.Fld("Seconds between accounts", 190), numDelay);

            chkUseSaved = Ui.Chk("Use each account's own game when it has one", Store.Settings.UseSavedPerAccount, null);
            chkUseSaved.CheckedChanged += delegate { Store.Settings.UseSavedPerAccount = chkUseSaved.Checked; Store.SaveSettings(); };
            p.Opt("Accounts with their own game ignore the choice above.",
                "Every account can have its own game (game field on the account card, or Edit account). With this ticked, those accounts join their own game while the others use the choice above. If every selected account has its own game, the form above is not needed at all.",
                chkUseSaved);

            p.Opt("Remember this Place / Job on the selected accounts.",
                "Copies the Place ID (and Job ID, if 'specific server' is chosen) above into the game field of every selected account, so 'Launch selected' can send every account to its own favourite game later.",
                Ui.Btn("Save to selected accounts", 200, delegate { SaveToSelected(); }, ""));

            p.Section("Go");
            btnLaunch = Ui.Btn("Launch selected", 150, delegate { LaunchSelected(); }, "primary");
            btnLaunchAll = Ui.Btn("Launch all listed", 150, delegate { LaunchAllListed(); }, "");
            btnStop = Ui.Btn("Stop", 80, delegate { StopLaunch(); }, "danger");
            btnStop.Enabled = false;
            p.Opt("Starts the accounts one after another.",
                "'Launch selected' starts the accounts you clicked, 'Launch all listed' starts every account that is visible in the list (respects search and group filter), 'Stop' cancels a running sequence after the current account.\n\nTurn multi-instance on first (top right). Windows are NOT moved unless you use the Layout page - auto-capture is off by default.",
                btnLaunch, btnLaunchAll, btnStop);

            UpdateLaunchModeUi();
        }

        void UpdateLaunchModeUi()
        {
            if (cbPlace == null) return;
            cbPlace.Enabled = rbGame.Checked || rbJob.Checked;
            tbJob.Enabled = rbJob.Checked;
            btnRandom.Enabled = rbJob.Checked;
            tbPrivate.Enabled = rbPrivate.Checked;
            tbFollow.Enabled = rbFollow.Checked;
        }

        // Called whenever the selection in the account list changes.
        void OnAccountSelectionChanged()
        {
            if (lblLaunchSel == null) return;
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0)
            {
                lblLaunchSel.Text = "Selected accounts: none - click accounts in the list on the left (Ctrl / Shift for several).";
                lblLaunchSel.Tag = "warn";
                lblLaunchSel.ForeColor = Theme.Warn;
            }
            else
            {
                string names = string.Join(", ", sel.Take(4).Select(a => AppState.Name(a)).ToArray());
                if (sel.Count > 4) names += " ... (+" + (sel.Count - 4) + ")";
                lblLaunchSel.Text = "Selected accounts (" + sel.Count + "): " + names;
                lblLaunchSel.Tag = "good";
                lblLaunchSel.ForeColor = Theme.Good;
            }
        }

        void RefreshRecentCombo()
        {
            string keep = cbPlace.Text;
            cbPlace.Items.Clear();
            foreach (GameEntry g in Store.Settings.Recent)
                cbPlace.Items.Add(g.PlaceId + "  -  " + (string.IsNullOrEmpty(g.Name) ? "(unknown name)" : g.Name));
            cbPlace.Text = keep;
        }

        async void AddRecent(long placeId)
        {
            try
            {
                GameEntry entry = Store.Settings.Recent.FirstOrDefault(x => x.PlaceId == placeId);
                if (entry == null)
                {
                    entry = new GameEntry();
                    entry.PlaceId = placeId;
                    Store.Settings.Recent.Insert(0, entry);
                }
                entry.Ticks = DateTime.UtcNow.Ticks;
                if (string.IsNullOrEmpty(entry.Name)) entry.Name = await Api.PlaceName(placeId);
                Store.Settings.Recent = Store.Settings.Recent.OrderByDescending(x => x.Ticks).Take(25).ToList();
                Store.SaveSettings();
                RefreshRecentCombo();
            }
            catch (Exception ex) { Log.Error("AddRecent", ex); }
        }

        void SaveLaunchFields()
        {
            Store.Settings.LastMode = rbJob.Checked ? "Job" : (rbPrivate.Checked ? "Private" : (rbFollow.Checked ? "Follow" : "Game"));
            Store.Settings.LastPlaceId = cbPlace.Text.Trim();
            Store.Settings.LastJobId = tbJob.Text.Trim();
            Store.Settings.LastPrivate = tbPrivate.Text.Trim();
            Store.Settings.LastFollow = tbFollow.Text.Trim();
            Store.SaveSettings();
        }

        void SaveToSelected()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select one or more accounts first."); return; }
            long pid = Util.ParsePlaceId(cbPlace.Text);
            foreach (Account a in sel)
            {
                a.SavedPlaceId = pid > 0 ? pid.ToString() : "";
                a.GameLink = pid > 0 ? "https://www.roblox.com/games/" + pid : "";
                a.SavedJobId = rbJob.Checked ? tbJob.Text.Trim() : "";
            }
            Store.SaveAccounts();
            UpdateStatusCells();
            AddLog("Saved Place " + (pid > 0 ? pid.ToString() : "(none)") + " to " + sel.Count + " account(s).");
        }

        string AnyCookie()
        {
            Account a = FirstSelected();
            if (a == null) a = Store.Accounts.FirstOrDefault(x => x.Cookie.Length > 0 && !AppState.Live(x).CookieBad);
            return a == null ? null : a.Cookie;
        }

        async void PickRandomServer()
        {
            long pid = Util.ParsePlaceId(cbPlace.Text);
            if (pid <= 0) { AddLog("Enter a Place ID first."); return; }
            AddLog("Looking up servers of place " + pid + "...");
            ServerPage sp = await Api.Servers(pid, false, "", AnyCookie());
            if (sp.Error.Length > 0) { AddLog("Could not load servers: " + sp.Error); return; }
            List<ServerInfo> open = sp.Servers.Where(x => x.Playing < x.Max).ToList();
            if (open.Count == 0) { AddLog("No joinable servers found for that place."); return; }
            ServerInfo pick = open[rng.Next(open.Count)];
            tbJob.Text = pick.Id;
            AddLog("Picked server " + pick.Id + " (" + pick.Playing + "/" + pick.Max + " players).");
        }

        void SearchGameForLaunch()
        {
            GameInfo g = Pickers.PickGame(this, AnyCookie());
            if (g == null) return;
            cbPlace.Text = g.PlaceId.ToString();
            if (rbPrivate.Checked || rbFollow.Checked) rbGame.Checked = true;
            AddLog("Game selected: " + g.Name + " (" + g.PlaceId + ").");
            AddRecent(g.PlaceId);
        }

        void PickServerForLaunch()
        {
            long pid = Util.ParsePlaceId(cbPlace.Text);
            if (pid <= 0) { AddLog("Choose a game first: type its Place ID or press 'Search game...'."); return; }
            string job = Pickers.PickServer(this, pid, AnyCookie());
            if (job == null) return;
            tbJob.Text = job;
            rbJob.Checked = true;
            AddLog("Server selected: " + job);
        }

        // ---------------- building the join request ----------------
        async Task<LaunchSpec> BuildSpec(Account first)
        {
            LaunchSpec s = new LaunchSpec();
            if (rbGame.Checked || rbJob.Checked)
            {
                long pid = Util.ParsePlaceId(cbPlace.Text);
                if (pid <= 0) { AddLog("Enter a valid Place ID (or paste a game link)."); return null; }
                s.Kind = rbJob.Checked ? "Job" : "Game";
                s.PlaceId = pid;
                if (rbJob.Checked)
                {
                    string j = tbJob.Text.Trim();
                    if (!Util.LooksLikeGuid(j)) { AddLog("The Job ID must look like xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx."); return null; }
                    s.JobId = j;
                }
                return s;
            }
            if (rbPrivate.Checked)
            {
                string link = tbPrivate.Text.Trim();
                if (link.Length == 0) { AddLog("Paste a private server link first."); return null; }
                Match game = Regex.Match(link, @"/games/(\d+)");
                Match code = Regex.Match(link, @"privateServerLinkCode=([^&#\s]+)");
                Match share = Regex.Match(link, @"[?&]code=([A-Za-z0-9]+)");
                if (game.Success && code.Success)
                {
                    s.Kind = "Private";
                    s.PlaceId = long.Parse(game.Groups[1].Value);
                    s.LinkCode = code.Groups[1].Value;
                    return s;
                }
                if (share.Success && link.IndexOf("share", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddLog("Resolving the share link...");
                    LaunchSpec r = await Api.ResolveShare(share.Groups[1].Value, first.Cookie);
                    if (r == null)
                    {
                        AddLog("Could not resolve that link. The first selected account must be allowed to use this private server.");
                        return null;
                    }
                    return r;
                }
                AddLog("That does not look like a private server link.");
                return null;
            }
            string name = tbFollow.Text.Trim();
            if (name.Length == 0) { AddLog("Type the username of the player to follow."); return null; }
            UserInfo u = await Api.UserByName(name);
            if (!u.Valid) { AddLog("Could not find user '" + name + "' (" + u.Error + ")."); return null; }
            s.Kind = "Follow";
            s.FollowUserId = u.Id;
            s.FollowName = u.Name;
            List<long> ids = new List<long>();
            ids.Add(u.Id);
            Dictionary<long, PresenceInfo> pr = await Api.Presence(first.Cookie, ids);
            PresenceInfo pi;
            if (pr.TryGetValue(u.Id, out pi))
            {
                if (pi.Type == 2) AddLog(u.Name + " is in a game: " + pi.Location);
                else AddLog("Heads-up: " + u.Name + " does not look like they are in a game right now (or hide it). Roblox may refuse the follow. Launching anyway.");
            }
            return s;
        }

        // The game stored on one account (card field / Edit dialog / "save to selected"), or null if none.
        LaunchSpec SavedSpec(Account acc)
        {
            string link = acc.GameLink ?? "";
            long pid = Util.ParsePlaceId(link);
            if (pid <= 0) pid = Util.ParsePlaceId(acc.SavedPlaceId);
            if (pid <= 0) return null;
            LaunchSpec s = new LaunchSpec();
            s.PlaceId = pid;
            Match code = Regex.Match(link, @"privateServerLinkCode=([^&#\s]+)");
            if (code.Success) { s.Kind = "Private"; s.LinkCode = code.Groups[1].Value; }
            else if (Util.LooksLikeGuid(acc.SavedJobId)) { s.Kind = "Job"; s.JobId = acc.SavedJobId.Trim(); }
            else s.Kind = "Game";
            return s;
        }

        // A saved game replaces the Launch page choice, but only if that choice is a normal game / server.
        LaunchSpec ApplySaved(Account acc, LaunchSpec spec)
        {
            if (spec.Kind != "Game" && spec.Kind != "Job") return spec;
            LaunchSpec s = SavedSpec(acc);
            return s != null ? s : spec;
        }

        // ---------------- running a launch ----------------
        void LaunchSelected()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0)
            {
                AddLog("Select one or more accounts in the list first.");
                ShowPage("Launch");
                return;
            }
            StartLaunch(sel);
        }

        void LaunchAllListed()
        {
            List<Account> all = FilteredAccounts();
            if (all.Count == 0) { AddLog("There are no accounts in the list."); return; }
            if (all.Count > 3 && MessageBox.Show(this, "Launch all " + all.Count + " listed accounts?", "Launch all", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            StartLaunch(all);
        }

        void SetLaunching(bool on)
        {
            launching = on;
            if (btnLaunch != null) btnLaunch.Enabled = !on;
            if (btnLaunchAll != null) btnLaunchAll.Enabled = !on;
            if (btnStop != null) btnStop.Enabled = on;
        }

        void StopLaunch()
        {
            if (launchCts != null) { launchCts.Cancel(); AddLog("Stopping after the current account..."); }
        }

        async void StartLaunch(List<Account> accs)
        {
            if (launching) { AddLog("A launch is already running. Press Stop to cancel it."); return; }
            SetLaunching(true);
            try
            {
                LaunchSpec spec;
                bool allSaved = Store.Settings.UseSavedPerAccount && accs.All(x => SavedSpec(x) != null);
                if (allSaved) spec = SavedSpec(accs[0]);
                else
                {
                    SaveLaunchFields();
                    spec = await BuildSpec(accs[0]);
                }
                if (spec != null) await RunLaunch(accs, spec, true);
            }
            catch (Exception ex) { Log.Error("StartLaunch", ex); AddLog("Launch failed: " + ex.Message); }
            SetLaunching(false);
        }

        // Used by the Servers page: put the selected accounts into one exact server.
        async void JoinServerWithSelected(long placeId, string jobId)
        {
            List<Account> accs = SelectedAccounts();
            if (accs.Count == 0) { AddLog("Select one or more accounts in the list first."); return; }
            if (launching) { AddLog("A launch is already running. Press Stop to cancel it."); return; }
            SetLaunching(true);
            try
            {
                LaunchSpec spec = new LaunchSpec();
                spec.Kind = "Job";
                spec.PlaceId = placeId;
                spec.JobId = jobId;
                await RunLaunch(accs, spec, false);
            }
            catch (Exception ex) { Log.Error("JoinServer", ex); AddLog("Launch failed: " + ex.Message); }
            SetLaunching(false);
        }

        // Launch button on an account card: uses that account's own game, otherwise the Launch page.
        async void LaunchOne(Account acc)
        {
            if (launching) { AddLog("A launch is already running. Press Stop to cancel it."); return; }
            SetLaunching(true);
            try
            {
                LaunchSpec spec = SavedSpec(acc);
                if (spec == null)
                {
                    SaveLaunchFields();
                    spec = await BuildSpec(acc);
                }
                if (spec != null)
                {
                    List<Account> one = new List<Account>();
                    one.Add(acc);
                    await RunLaunch(one, spec, true);
                }
            }
            catch (Exception ex) { Log.Error("LaunchOne", ex); AddLog("Launch failed: " + ex.Message); }
            SetLaunching(false);
        }

        void StopAccount(Account acc)
        {
            Launcher.CloseInstancesOf(acc, AddLog);
            UpdateStatusCells();
        }

        // Closes every Roblox window that THIS app started (windows you opened yourself are left alone).
        void StopAll()
        {
            StopLaunch();
            int n = 0;
            foreach (Instance i in Launcher.Instances.ToList())
            {
                i.UserClosed = true;
                if (Launcher.IsAlive(i.Pid)) { Launcher.KillPid(i.Pid); n++; }
                Launcher.Instances.Remove(i);
            }
            AddLog(n > 0 ? "Stopped " + n + " Roblox window(s) started by this app." : "No Roblox windows started by this app are running.");
            UpdateStatusCells();
        }

        async Task RunLaunch(List<Account> accs, LaunchSpec spec, bool allowSaved)
        {
            launchCts = new CancellationTokenSource();
            CancellationToken ct = launchCts.Token;
            int delay = Math.Max(0, Store.Settings.LaunchDelaySec);
            AddLog("Launching " + accs.Count + " account(s): " + spec.Describe());
            if (spec.PlaceId > 0) AddRecent(spec.PlaceId);
            try
            {
                for (int i = 0; i < accs.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    Account acc = accs[i];
                    Launcher.BreakerClear(acc);     // a launch you started yourself resets its crash breaker
                    LaunchSpec per = (allowSaved && Store.Settings.UseSavedPerAccount) ? ApplySaved(acc, spec) : spec;
                    await Launcher.LaunchAsync(acc, per, AddLog);
                    UpdateStatusCells();
                    if (i < accs.Count - 1 && !ct.IsCancellationRequested) await Task.Delay(delay * 1000, ct);
                }
            }
            catch (OperationCanceledException) { }
            bool cancelled = ct.IsCancellationRequested;
            launchCts = null;
            AddLog(cancelled ? "Launch cancelled." : "Launch sequence finished.");
            if (!cancelled && Store.Settings.WebhookBatch)
                Webhook.Send("Launch finished", "Started " + accs.Count + " account(s): " + spec.Describe(), Webhook.Green);
        }
    }
}
