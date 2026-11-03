using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm : Form
    {
        readonly bool startMinimized;

        // shell
        TableLayoutPanel root;
        SplitContainer split;
        Panel leftHost;
        Label lblMulti;
        StatBlock statRun, statCpu, statRam;
        TextBox logBox;
        Panel pageHost;
        string currentPage = "";
        readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        readonly Dictionary<string, RoundButton> navButtons = new Dictionary<string, RoundButton>();
        readonly List<string> logLines = new List<string>();
        System.Windows.Forms.Timer tick;
        int tickN;
        bool presenceBusy;
        bool watcherBusy;

        // accounts
        BufferedListView lv;
        FlowLayoutPanel cardHost;
        readonly Dictionary<string, AccountCard> cards = new Dictionary<string, AccountCard>();
        RoundButton btnViewList, btnViewCards;
        RoundTextBox tbSearch;
        ComboBox cbGroupFilter;
        Label lblCount;
        ContextMenuStrip accMenu;
        ContextMenuStrip addMenu;
        ToolStripMenuItem miGroup;
        int sortCol = -1;
        bool sortAsc = true;
        bool suspendSel;
        bool cardsView;
        HashSet<string> selected = new HashSet<string>();
        HashSet<string> runningIds = new HashSet<string>();
        System.Threading.CancellationTokenSource browserCts;

        public MainForm(bool minimized)
        {
            startMinimized = minimized;
            Theme.Set(Store.Settings.Theme != "Light");
            Text = "Roblox MultiHub";
            Font = Theme.Base;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Math.Max(1320, Store.Settings.WinW), Math.Max(660, Store.Settings.WinH));
            MinimumSize = new Size(1320, 660);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            BuildShell();
            BuildAccounts();
            BuildLaunchPage();
            BuildServersPage();
            BuildLayoutPage();
            BuildMonitorPage();
            BuildPresetsPage();
            BuildPerfPage();
            BuildSettingsPage();
            BuildHelpPage();
            ApplyTheme();
            ShowPage("Launch");
            RefreshAccountList();
            SetView(Store.Settings.View == "Cards");

            Log.Line += OnLogLine;
            Avatars.Updated += OnAvatarsUpdated;
            tick = new System.Windows.Forms.Timer();
            tick.Interval = 1000;
            tick.Tick += OnTick;
            tick.Start();
        }

        // =====================================================================
        //  Shell
        // =====================================================================
        void BuildShell()
        {
            root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Margin = Padding.Empty;
            root.Padding = Padding.Empty;
            Controls.Add(root);

            // ---- top bar: title, live usage, multi-instance status ----
            RoundPanel topBar = new RoundPanel();
            topBar.Dock = DockStyle.Fill;
            topBar.Tag = "card";
            topBar.Margin = Padding.Empty;
            Label title = Ui.Lbl("Roblox MultiHub", "title");
            title.Location = new Point(18, 7);
            topBar.Controls.Add(title);
            Label sub = Ui.Lbl(Lang.T("Multi-account launcher"), "muted");
            sub.Location = new Point(21, 43);
            topBar.Controls.Add(sub);
            DiscordIcon disc = new DiscordIcon();
            disc.Location = new Point(sub.Right + 10, 41);
            disc.Click += delegate { OpenLink(DiscordUrl, "Discord"); };
            Ui.Tips.SetToolTip(disc, "Join our Discord - news, help and support tickets.");
            topBar.Controls.Add(disc);

            statRun = new StatBlock(Lang.T("Running"), 140);
            statCpu = new StatBlock(Lang.T("Roblox CPU"), 160);
            statRam = new StatBlock(Lang.T("Roblox memory"), 170);
            statRun.SetTip("Accounts running that this app started / accounts saved.");
            statCpu.SetTip("CPU used by all Roblox windows together, as a percentage of your whole PC (same scale as Task Manager). The small line is the average per account.");
            statRam.SetTip("Memory used by all Roblox windows together. The small line is the average per account.");
            topBar.Controls.Add(statRun);
            topBar.Controls.Add(statCpu);
            topBar.Controls.Add(statRam);

            lblMulti = Ui.Lbl("", "muted");
            lblMulti.AutoSize = false;
            lblMulti.Size = new Size(310, 44);
            lblMulti.TextAlign = ContentAlignment.MiddleRight;
            lblMulti.Cursor = Cursors.Hand;
            lblMulti.Click += delegate { ShowPage("Settings"); };
            Ui.Tips.SetToolTip(lblMulti, "Click to open Settings. Multi-instance is what lets several Roblox windows run at the same time.");
            topBar.Controls.Add(lblMulti);
            root.RowStyles[0].Height = Math.Max(68, statRun.NeededHeight() + 14);
            topBar.Resize += delegate
            {
                // placed after the real (DPI scaled) title and sizes, so the blocks never overlap or get cut off
                int h = statRun.NeededHeight();
                int top = Math.Max(0, (topBar.Height - h) / 2);
                int x = Math.Max(270, Math.Max(title.Right, sub.Right) + 50);
                foreach (StatBlock sb in new StatBlock[] { statRun, statCpu, statRam })
                {
                    sb.Height = h;
                    sb.Location = new Point(x, top);
                    x = sb.Right + 10;
                }
                lblMulti.Location = new Point(topBar.Width - lblMulti.Width - 18, 12);
            };
            root.Controls.Add(topBar, 0, 0);

            // ---- body: accounts | pages, with a draggable splitter ----
            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(10);
            body.Margin = Padding.Empty;
            root.Controls.Add(body, 0, 1);

            split = new SplitContainer();
            split.Size = new Size(1100, 600);               // give it a size first, so the minimum sizes below are valid
            try
            {
                split.Orientation = Orientation.Vertical;
                split.SplitterWidth = 10;
                split.FixedPanel = FixedPanel.Panel1;
                split.Panel1MinSize = 380;
                split.Panel2MinSize = 900;                  // room for all page buttons (with icons) in one row
            }
            catch (Exception ex) { Log.Error("split", ex); }
            split.BorderStyle = BorderStyle.None;
            split.TabStop = false;
            split.Dock = DockStyle.Fill;
            split.SplitterMoved += delegate { LayoutCards(); };
            body.Controls.Add(split);

            leftHost = new RoundPanel();
            leftHost.Dock = DockStyle.Fill;
            leftHost.Tag = "card";
            leftHost.Padding = new Padding(8);
            split.Panel1.Controls.Add(leftHost);

            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 3;
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            right.Margin = Padding.Empty;
            split.Panel2.Controls.Add(right);

            FlowLayoutPanel nav = new FlowLayoutPanel();
            nav.Dock = DockStyle.Fill;
            nav.WrapContents = false;
            nav.Margin = new Padding(0, 0, 0, 6);
            string[][] items = new string[][]
            {
                new string[] { "Launch", "\U0001F680  Launch", "94" },
                new string[] { "Servers", "\U0001F310  Servers", "102" },
                new string[] { "Layout", "\U0001FA9F  Layout", "94" },
                new string[] { "Monitor", "\U0001F4CA  Monitor", "104" },
                new string[] { "Presets", "\U0001F4BE  Presets", "98" },
                new string[] { "Perf", "⚡  Perf", "78" },
                new string[] { "Settings", "⚙  Settings", "100" },
                new string[] { "Help", "❓  Help", "78" },
                new string[] { "Macro", "\U0001F3AC  Macro", "90" }      // opens its own window instead of a page
            };
            foreach (string[] it in items)
            {
                string key = it[0];
                string label = Lang.T(it[1]);
                int w = int.Parse(it[2]);
                // grow the button if the (translated) label needs more room, so nothing clips
                int measured = TextRenderer.MeasureText(label, Theme.Base).Width + 34;
                RoundButton b = Ui.Btn(label, Math.Max(w, measured), null, "nav");
                b.Height = 34;
                b.Margin = new Padding(0, 2, 6, 0);
                b.Click += delegate { if (key == "Macro") OpenMacro(); else ShowPage(key); };
                if (key == "Macro") Ui.Tips.SetToolTip(b, "Opens the macro recorder (record and replay mouse + keyboard, like TinyTask) in its own window.");
                navButtons[key] = b;
                nav.Controls.Add(b);
            }
            right.Controls.Add(nav, 0, 0);

            pageHost = new RoundPanel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.Tag = "card";
            pageHost.Padding = new Padding(6);
            pageHost.Margin = Padding.Empty;
            right.Controls.Add(pageHost, 0, 1);

            RoundPanel logHost = new RoundPanel();
            logHost.Dock = DockStyle.Fill;
            logHost.Tag = "card";
            logHost.Padding = new Padding(8, 4, 8, 8);
            logHost.Margin = new Padding(0, 10, 0, 0);
            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill;
            logBox.Font = Theme.Mono;
            logBox.Tag = "log";
            logHost.Controls.Add(logBox);
            Label logTitle = Ui.Lbl("Activity log  (everything the app does is written here)", "muted");
            logTitle.AutoSize = false;
            logTitle.Dock = DockStyle.Top;
            logTitle.Height = 20;
            logHost.Controls.Add(logTitle);
            right.Controls.Add(logHost, 0, 2);
        }

        Panel NewPage(string key)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.Tag = "card";
            p.Visible = false;
            pages[key] = p;
            pageHost.Controls.Add(p);
            return p;
        }

        void ShowPage(string key)
        {
            currentPage = key;
            foreach (KeyValuePair<string, Panel> kv in pages) kv.Value.Visible = kv.Key == key;
            StyleNav();
            if (key == "Layout") RefreshWindowList();
            if (key == "Monitor") RefreshMonitor();
            if (key == "Presets") RefreshPresetList();
        }

        // Separate top-level window (no owner), so its 'Stay on top' never lifts the hub with it.
        MacroForm macroForm;

        void OpenMacro()
        {
            if (macroForm == null || macroForm.IsDisposed) macroForm = new MacroForm();
            if (!macroForm.Visible) macroForm.Show();
            if (macroForm.WindowState == FormWindowState.Minimized) macroForm.WindowState = FormWindowState.Normal;
            macroForm.Activate();
        }

        void StyleNav()
        {
            foreach (KeyValuePair<string, RoundButton> kv in navButtons)
            {
                kv.Value.Active = kv.Key == currentPage;
                kv.Value.Invalidate();
            }
        }

        void StyleViewButtons()
        {
            if (btnViewList == null) return;
            btnViewList.Active = !cardsView;
            btnViewCards.Active = cardsView;
            btnViewList.Invalidate();
            btnViewCards.Invalidate();
        }

        void ApplyTheme()
        {
            Theme.Apply(this);
            if (accMenu != null) StyleMenus();
            StyleNav();
            StyleViewButtons();
            if (lv != null) lv.Invalidate();
            if (previewPanel != null) previewPanel.Invalidate();
            if (cardsView) RebuildCards();
        }

        void StyleMenus()
        {
            foreach (ContextMenuStrip m in new ContextMenuStrip[] { accMenu, addMenu })
            {
                m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
                m.BackColor = Theme.Panel;
                m.ForeColor = Theme.Text;
                foreach (ToolStripItem it in m.Items) StyleItem(it);
            }
        }

        void StyleItem(ToolStripItem it)
        {
            it.BackColor = Theme.Panel;
            it.ForeColor = Theme.Text;
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null)
                foreach (ToolStripItem sub in mi.DropDownItems) StyleItem(sub);
        }

        void OnLogLine(string line, bool replaceLast)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string, bool>(OnLogLine), line, replaceLast); } catch (Exception) { } return; }
            if (replaceLast && logLines.Count > 0)
            {
                logLines[logLines.Count - 1] = line;
                logBox.Text = string.Join("\r\n", logLines.ToArray()) + "\r\n";
            }
            else
            {
                logLines.Add(line);
                if (logLines.Count > 400)
                {
                    logLines.RemoveRange(0, 150);
                    logBox.Text = string.Join("\r\n", logLines.ToArray()) + "\r\n";
                }
                else logBox.AppendText(line + "\r\n");
            }
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        void AddLog(string s) { Log.Write(s); }

        void OnAvatarsUpdated()
        {
            if (IsDisposed) return;
            if (lv != null) lv.Invalidate();
            foreach (Control c in cardHost.Controls) c.Invalidate();
        }

        // =====================================================================
        //  Timer
        // =====================================================================
        void OnTick(object sender, EventArgs e)
        {
            try
            {
                tickN++;
                UpdateMultiChip();
                AutoCaptureTick();
                if (tickN % 2 == 0)
                {
                    UsageMonitor.Update();
                    UpdateStatusCells();
                    UpdateMemUi();
                    MaxAlts.Tick();
                }
                if (currentPage == "Layout" && tickN % 2 == 0) RefreshWindowList();
                if (currentPage == "Monitor" && tickN % 2 == 0) RefreshMonitor();
                if (Store.Settings.ShowPresence && tickN % Math.Max(10, Store.Settings.PresenceSec) == 0) RefreshPresence();
                if (tickN % Math.Max(10, Store.Settings.WatcherSec) == 0) RunWatcher();
                if (Store.Settings.AntiAfk && (DateTime.Now - lastAntiAfk).TotalMinutes >= Math.Max(1, Store.Settings.AntiAfkMin)
                    && (macroForm == null || macroForm.IsDisposed || !macroForm.Busy)) RunAntiAfk();
            }
            catch (Exception ex) { Log.Error("timer", ex); }
        }

        void UpdateMultiChip()
        {
            string st = MultiInstance.State;
            if (!MultiInstance.Enabled)
            {
                lblMulti.Text = "Multi-instance: OFF  (only ONE Roblox window)\r\nClick here to turn it on in Settings.";
                lblMulti.ForeColor = Theme.Warn;
            }
            else if (st == "Active")
            {
                lblMulti.Text = "Multi-instance: ACTIVE\r\nLaunch Roblox only from this app.";
                lblMulti.ForeColor = Theme.Good;
            }
            else if (st == "Waiting")
            {
                lblMulti.Text = "Multi-instance: WAITING - another Roblox\r\nowns the lock. Click here to fix it.";
                lblMulti.ForeColor = Theme.Warn;
            }
            else
            {
                lblMulti.Text = "Multi-instance: " + st;
                lblMulti.ForeColor = Theme.Bad;
            }
        }

        string CpuText(double v)
        {
            return UsageMonitor.CpuAvailable ? v.ToString("0.0") + "%" : "n/a";
        }

        void UpdateStats()
        {
            if (statRun == null) return;
            int count = UsageMonitor.Count;
            statRun.Value.Text = runningIds.Count + " / " + Store.Accounts.Count;
            statRun.Sub.Text = count == 0 ? "nothing running" : count + " Roblox window(s)";
            statCpu.Value.Text = count == 0 ? "0%" : CpuText(UsageMonitor.TotalCpu);
            statRam.Value.Text = Util.FormatBytes(UsageMonitor.TotalRam);
            if (count == 0)
            {
                statCpu.Sub.Text = "-";
                statRam.Sub.Text = "-";
            }
            else
            {
                statCpu.Sub.Text = "avg " + CpuText(UsageMonitor.TotalCpu / count) + " per account";
                statRam.Sub.Text = "avg " + Util.FormatBytes(UsageMonitor.TotalRam / count) + " per account";
            }
            if (lblPerfLive != null)
                lblPerfLive.Text = count == 0
                    ? "Right now: no Roblox window is running."
                    : "Right now: " + count + " Roblox window(s) use " + CpuText(UsageMonitor.TotalCpu) + " CPU and " + Util.FormatBytes(UsageMonitor.TotalRam)
                      + " memory in total  (avg " + CpuText(UsageMonitor.TotalCpu / count) + " and " + Util.FormatBytes(UsageMonitor.TotalRam / count) + " per account).";
        }

        async void RunWatcher()
        {
            if (watcherBusy) return;
            watcherBusy = true;
            try { await Launcher.WatcherTick(AddLog); }
            catch (Exception ex) { Log.Error("watcher", ex); }
            watcherBusy = false;
            UpdateStatusCells();
        }

        // =====================================================================
        //  Form lifecycle
        // =====================================================================
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // never let the window be bigger than the screen (small laptops / high DPI scaling)
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, wa.Width), Math.Min(MinimumSize.Height, wa.Height));
            if (Width > wa.Width || Height > wa.Height)
            {
                Size = new Size(Math.Min(Width, wa.Width), Math.Min(Height, wa.Height));
                Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
            }
            try
            {
                int w = Math.Min(Store.Settings.SplitW, split.Width - split.Panel2MinSize - split.SplitterWidth);
                split.SplitterDistance = Math.Max(split.Panel1MinSize, w);
            }
            catch (Exception ex) { Log.Error("splitter", ex); }
            UpdateMultiChip();
            UsageMonitor.Update();
            UpdateStatusCells();
            if (startMinimized) WindowState = FormWindowState.Minimized;
            FirstRun();
            if (Store.Settings.MultiInstance) MultiInstance.Start();
            StartupChecks();
            RefreshPresence();
            StartMemOnLaunch();
            if (Store.Settings.CheckLoginsAtStartup) CheckLoginsOnStartup();
            if (!string.IsNullOrEmpty(Store.Settings.StartupPreset)) RunStartupPreset();
        }

        void FirstRun()
        {
            if (Store.Settings.FirstRunDone) return;
            string msg = "Welcome to Roblox MultiHub!\r\n\r\n"
                + "To run several Roblox accounts at once the app has to hold Roblox's 'single instance' lock objects "
                + "(ROBLOX_singletonMutex / ROBLOX_singletonEvent). Nothing is injected into Roblox or modified.\r\n\r\n"
                + "Please know: Roblox's anti-cheat team has said that running multiple clients may be treated as abusive behaviour, "
                + "and Roblox can change the client at any time. You use this at your own risk, with your own accounts.\r\n\r\n"
                + "Turn multi-instance ON now?  (You can change this any time in Settings.)";
            DialogResult r = MessageBox.Show(this, msg, "Roblox MultiHub - first run", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            Store.Settings.FirstRunDone = true;
            Store.Settings.MultiInstance = r == DialogResult.Yes;
            Store.SaveSettings();
            SyncSettingsControls();
        }

        void StartupChecks()
        {
            try
            {
                if (Store.Settings.MultiInstance && Launcher.RobloxPids().Count > 0 && Launcher.Instances.Count == 0)
                    AddLog("NOTE: Roblox is already running (started by Windows or by you earlier). Multi-instance only works for clients started AFTER this app is holding the lock. Use Settings > 'Close all Roblox processes'.");
                if (Launcher.FindRobloxExe() == null)
                    AddLog("NOTE: Roblox does not seem to be installed for this Windows user. Install it from roblox.com first.");
            }
            catch (Exception ex) { Log.Error("StartupChecks", ex); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (WindowState == FormWindowState.Normal)
                {
                    Store.Settings.WinW = ClientSize.Width;
                    Store.Settings.WinH = ClientSize.Height;
                    Store.Settings.SplitW = split.SplitterDistance;
                }
                Store.SaveSettings();
                Store.SaveAccounts();
                UnregisterHotkeys();
                if (Store.Settings.MaxAlts) MaxAlts.Reset();
                if (browserCts != null) browserCts.Cancel();
            }
            catch (Exception) { }
            base.OnFormClosing(e);
        }

        // =====================================================================
        //  Accounts panel
        // =====================================================================
        void BuildAccounts()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 4;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            t.Margin = Padding.Empty;
            t.Tag = "card";
            leftHost.Controls.Add(t);

            BuildMenus();

            // ---- toolbar ----
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.Margin = Padding.Empty;
            bar.Tag = "card";
            RoundButton bAdd = Ui.Btn("+ Add account", 110, null, "primary");
            bAdd.Click += delegate { addMenu.Show(bAdd, new Point(0, bAdd.Height)); };
            Ui.Tips.SetToolTip(bAdd, "Add accounts: log in with a browser, paste cookies, or import a file.");
            RoundButton bEdit = Ui.Btn("Edit", 56, delegate { EditSelected(); }, "");
            Ui.Tips.SetToolTip(bEdit, "Edit alias, group, notes, password and the game of the selected account.");
            RoundButton bRemove = Ui.Btn("Remove", 76, delegate { RemoveSelected(); }, "danger");
            Ui.Tips.SetToolTip(bRemove, "Delete the selected account(s) from this app (does not touch the Roblox account itself).");
            RoundButton bCheck = Ui.Btn("Check", 64, delegate { CheckAccounts(); }, "");
            Ui.Tips.SetToolTip(bCheck, "Check cookies: tests whether the saved login of the selected accounts (or all, if none selected) still works.");
            RoundButton bActions = Ui.Btn("More", 64, null, "");
            bActions.Click += delegate { accMenu.Show(bActions, new Point(0, bActions.Height)); };
            Ui.Tips.SetToolTip(bActions, "Copy username/password/cookie, account details, refresh cookie, groups, sorting ... (also on right-click).");
            foreach (RoundButton b in new RoundButton[] { bAdd, bEdit, bRemove, bCheck, bActions })
            {
                b.Margin = new Padding(0, 5, 6, 5);
                bar.Controls.Add(b);
            }
            t.Controls.Add(bar, 0, 0);

            // ---- search / group / view switch ----
            FlowLayoutPanel filt = new FlowLayoutPanel();
            filt.Dock = DockStyle.Fill;
            filt.Margin = Padding.Empty;
            filt.Tag = "card";
            Label l1 = Ui.Lbl("Search", "muted");
            l1.Margin = new Padding(0, 10, 4, 0);
            tbSearch = Ui.Txt(150, "");
            tbSearch.Margin = new Padding(0, 5, 12, 0);
            tbSearch.TextChanged += delegate { RefreshAccountList(); };
            tbSearch.Tip("Type to filter by username, alias, notes or group.");
            Label l2 = Ui.Lbl("Group", "muted");
            l2.Margin = new Padding(0, 10, 4, 0);
            cbGroupFilter = new DarkCombo();
            cbGroupFilter.Width = 130;
            cbGroupFilter.Margin = new Padding(0, 6, 14, 0);
            cbGroupFilter.SelectedIndexChanged += delegate { if (!suspendSel) RefreshAccountList(); };
            btnViewList = Ui.Btn(Lang.T("List"), 52, delegate { SetView(false); }, "nav");
            btnViewList.Margin = new Padding(0, 5, 2, 0);
            Ui.Tips.SetToolTip(btnViewList, "Compact list with profile pictures.");
            btnViewCards = Ui.Btn(Lang.T("Cards"), 60, delegate { SetView(true); }, "nav");
            btnViewCards.Margin = new Padding(0, 5, 0, 0);
            Ui.Tips.SetToolTip(btnViewCards, "Expanded cards: big profile picture, status, a game for each account and a Launch button.");
            filt.Controls.Add(l1);
            filt.Controls.Add(tbSearch);
            filt.Controls.Add(l2);
            filt.Controls.Add(cbGroupFilter);
            filt.Controls.Add(btnViewList);
            filt.Controls.Add(btnViewCards);
            t.Controls.Add(filt, 0, 1);

            // ---- the two views share one cell ----
            Panel viewHost = new Panel();
            viewHost.Dock = DockStyle.Fill;
            viewHost.Margin = Padding.Empty;
            viewHost.Tag = "card";

            lv = new BufferedListView();
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.MultiSelect = true;
            lv.HideSelection = false;
            lv.OwnerDraw = true;
            lv.HeaderStyle = ColumnHeaderStyle.None;     // no table header: every row draws its own, resizable layout
            lv.Tag = "rows";
            ImageList rowHeight = new ImageList();
            rowHeight.ImageSize = new Size(1, 56);        // makes every row tall enough for the profile picture
            lv.SmallImageList = rowHeight;
            lv.Columns.Add("", 400);
            lv.AllowDrop = true;
            lv.DrawItem += LvDrawItem;
            lv.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e) { };
            lv.ClientSizeChanged += delegate { FitListColumn(); };
            lv.HandleCreated += delegate { FitListColumn(); };
            lv.DoubleClick += delegate { EditSelected(); };
            lv.SelectedIndexChanged += LvSelectionChanged;
            lv.ContextMenuStrip = accMenu;
            lv.DragEnter += LvDragEnter;
            lv.DragDrop += LvDragDrop;
            lv.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) RemoveSelected();
                else if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem it in lv.Items) it.Selected = true; }
            };

            cardHost = new FlowLayoutPanel();
            cardHost.Dock = DockStyle.Fill;
            cardHost.AutoScroll = true;
            cardHost.WrapContents = true;
            cardHost.FlowDirection = FlowDirection.LeftToRight;
            cardHost.Padding = new Padding(2, 4, 2, 4);
            cardHost.Tag = "card";
            cardHost.Visible = false;
            cardHost.AllowDrop = true;
            cardHost.DragEnter += LvDragEnter;
            cardHost.DragDrop += LvDragDrop;
            cardHost.Resize += delegate { LayoutCards(); };

            viewHost.Controls.Add(lv);
            viewHost.Controls.Add(cardHost);
            t.Controls.Add(viewHost, 0, 2);

            // ---- footer ----
            FlowLayoutPanel foot = new FlowLayoutPanel();
            foot.Dock = DockStyle.Fill;
            foot.Margin = Padding.Empty;
            foot.Tag = "card";
            GradientButton bLaunch = new GradientButton();
            bLaunch.Text = "\U0001F680  " + Lang.T("Launch selected");
            bLaunch.Size = new Size(158, 34);
            bLaunch.Margin = new Padding(0, 7, 8, 5);
            bLaunch.Click += delegate { LaunchSelected(); };
            Ui.Tips.SetToolTip(bLaunch, "Launch the selected accounts. Accounts with their own game use it; the others use the Launch page.");
            RoundButton bAll = Ui.Btn(Lang.T("Launch all"), 90, delegate { LaunchAllListed(); }, "");
            Ui.Tips.SetToolTip(bAll, "Launch every account that is currently visible in the list.");
            RoundButton bStop = Ui.Btn(Lang.T("Stop all"), 80, delegate { StopAll(); }, "danger");
            Ui.Tips.SetToolTip(bStop, "Close every Roblox window that this app started (windows you opened yourself are left alone).");
            RoundButton bUp = Ui.Btn("Up", 44, delegate { MoveSelected(-1); }, "");
            RoundButton bDown = Ui.Btn("Down", 54, delegate { MoveSelected(1); }, "");
            Ui.Tips.SetToolTip(bUp, "Move the selected account up in your own order.");
            Ui.Tips.SetToolTip(bDown, "Move the selected account down in your own order.");
            lblCount = Ui.Lbl("", "muted");              // kept for the counters; not shown (the Launch page lists the selection)
            foreach (RoundButton b in new RoundButton[] { bAll, bStop, bUp, bDown }) b.Margin = new Padding(0, 8, 6, 6);
            foot.Controls.Add(bLaunch);
            foot.Controls.Add(bAll);
            foot.Controls.Add(bStop);
            foot.Controls.Add(bUp);
            foot.Controls.Add(bDown);
            t.Controls.Add(foot, 0, 3);
        }

        void FitListColumn()
        {
            if (lv == null || lv.Columns.Count == 0) return;
            int w = Math.Max(200, lv.ClientSize.Width);
            if (lv.Columns[0].Width != w) lv.Columns[0].Width = w;
        }

        void BuildMenus()
        {
            addMenu = Ui.Menu();
            addMenu.Items.Add(Ui.Item("Log in with a browser (Edge / Chrome)...", delegate { AddByBrowser(); }));
            addMenu.Items.Add(Ui.Item("Add several with username:password...", delegate { AddSeveralByBrowser(); }));
            addMenu.Items.Add(Ui.Item("Paste cookie(s)...", delegate { AddByPaste(); }));
            addMenu.Items.Add(Ui.Item("Import cookies from a file...", delegate { AddByFile(); }));
            addMenu.Items.Add(new ToolStripSeparator());
            addMenu.Items.Add(Ui.Item("Tip: you can also drag a text file or text onto the accounts", null));
            addMenu.Items[addMenu.Items.Count - 1].Enabled = false;

            accMenu = Ui.Menu();
            accMenu.Items.Add(Ui.Item("Launch selected", delegate { LaunchSelected(); }));
            accMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem copy = Ui.Item("Copy", null);
            copy.DropDownItems.Add(Ui.Item("Username", delegate { CopyField(delegate(Account a) { return a.Username; }); }));
            copy.DropDownItems.Add(Ui.Item("Password", delegate { CopyField(delegate(Account a) { return a.Password; }); }));
            copy.DropDownItems.Add(Ui.Item("Username:Password", delegate { CopyField(delegate(Account a) { return a.Username + ":" + a.Password; }); }));
            copy.DropDownItems.Add(Ui.Item("User ID", delegate { CopyField(delegate(Account a) { return a.UserId.ToString(); }); }));
            copy.DropDownItems.Add(Ui.Item("Cookie (.ROBLOSECURITY) - keep secret!", delegate { CopyField(delegate(Account a) { return a.Cookie; }); }));
            accMenu.Items.Add(copy);
            accMenu.Items.Add(Ui.Item("Show account details (age, Robux, Premium)", delegate { ShowDetails(); }));
            accMenu.Items.Add(Ui.Item("Copy a fresh login ticket", delegate { CopyTicket(); }));
            accMenu.Items.Add(Ui.Item("Open profile in browser", delegate { OpenProfile(); }));
            accMenu.Items.Add(Ui.Item("Refresh cookie (rotates it, signs out other sessions)", delegate { RotateSelected(); }));
            accMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem sort = Ui.Item("Sort accounts by", null);
            sort.DropDownItems.Add(Ui.Item("My own order (Up / Down buttons)", delegate { SetSort(-1); }));
            sort.DropDownItems.Add(Ui.Item("Name  (click again to reverse)", delegate { SetSort(1); }));
            sort.DropDownItems.Add(Ui.Item("Group", delegate { SetSort(2); }));
            sort.DropDownItems.Add(Ui.Item("Status", delegate { SetSort(3); }));
            sort.DropDownItems.Add(Ui.Item("CPU usage", delegate { SetSort(4); }));
            sort.DropDownItems.Add(Ui.Item("Last used", delegate { SetSort(5); }));
            accMenu.Items.Add(sort);
            miGroup = Ui.Item("Move to group", null);
            accMenu.Items.Add(miGroup);
            accMenu.Items.Add(Ui.Item("Edit...", delegate { EditSelected(); }));
            accMenu.Items.Add(Ui.Item("Remove", delegate { RemoveSelected(); }));
            accMenu.Opening += delegate { RebuildGroupMenu(); };
            StyleMenus();
        }

        void RebuildGroupMenu()
        {
            miGroup.DropDownItems.Clear();
            miGroup.DropDownItems.Add(Ui.Item("(no group)", delegate { SetGroup(""); }));
            foreach (string g in Store.AllGroups())
            {
                string name = g;
                miGroup.DropDownItems.Add(Ui.Item(name, delegate { SetGroup(name); }));
            }
            miGroup.DropDownItems.Add(new ToolStripSeparator());
            miGroup.DropDownItems.Add(Ui.Item("New group...", delegate
            {
                string n = Dialogs.Prompt(this, "New group", "Name of the new group:", "");
                if (!string.IsNullOrEmpty(n)) SetGroup(n);
            }));
            foreach (ToolStripItem it in miGroup.DropDownItems) StyleItem(it);
        }

        // =====================================================================
        //  List view: one rounded row per account (profile picture, name, status, group, last used, usage)
        // =====================================================================
        Color DotColor(Account a)
        {
            LiveInfo li = AppState.Live(a);
            if (li.Locked || li.CookieBad) return Theme.Bad;
            if (runningIds.Contains(a.Id)) return Theme.Good;
            if (li.PresenceType == 2) return Theme.Good;
            if (li.PresenceType == 3) return Theme.Purple;
            if (li.PresenceType == 1) return Theme.Info;
            if (li.PresenceType == 0) return Theme.Muted;
            return Theme.Border;
        }

        // Small text segment on the second line; skipped when it no longer fits (so narrow windows stay tidy).
        int DrawSeg(Graphics g, string text, int x, int y, int limit, Color color)
        {
            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int w = TextRenderer.MeasureText(g, text, Theme.Small, new Size(1000, 20), f).Width + 2;
            if (x + w > limit) return x;
            TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(x, y, w, 16), color, f);
            return x + w;
        }

        int DrawChip(Graphics g, string text, int x, int y, int limit)
        {
            TextFormatFlags f = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int w = TextRenderer.MeasureText(g, text, Theme.Small, new Size(1000, 20), f).Width + 16;
            if (x + w > limit) return x;
            Rectangle r = new Rectangle(x, y, w, 17);
            using (System.Drawing.Drawing2D.GraphicsPath p = Shapes.Round(r, 8))
            {
                using (SolidBrush b = new SolidBrush(Theme.Panel)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, text, Theme.Small, r, Theme.Text, f);
            return x + w;
        }

        void LvDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bgb = new SolidBrush(lv.BackColor)) g.FillRectangle(bgb, e.Bounds);
            Account a = e.Item.Tag as Account;
            if (a == null) return;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool sel = e.Item.Selected;
            Color rowColor = sel ? Theme.Sel : Theme.Panel2;
            Rectangle row = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 3, e.Bounds.Width - 5, e.Bounds.Height - 6);
            using (System.Drawing.Drawing2D.GraphicsPath path = Shapes.Round(row, 11))
            {
                using (SolidBrush b = new SolidBrush(rowColor)) g.FillPath(b, path);
                if (sel) using (Pen pen = new Pen(Theme.Accent)) g.DrawPath(pen, path);
            }

            int d = 36;
            Rectangle av = new Rectangle(row.Left + 12, row.Top + (row.Height - d) / 2, d, d);
            Shapes.Avatar(g, a, av);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle dot = new Rectangle(av.Right - 10, av.Bottom - 10, 12, 12);
            using (SolidBrush ring = new SolidBrush(rowColor)) g.FillEllipse(ring, Rectangle.Inflate(dot, 2, 2));
            using (SolidBrush fill = new SolidBrush(DotColor(a))) g.FillEllipse(fill, dot);

            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int x0 = av.Right + 12;
            int right = row.Right - 12;
            int y1 = row.Top + 8;
            int y2 = row.Top + row.Height - 8 - 17;

            // line 1: name on the left, status on the right
            string status = StatusText(a);
            LiveInfo li = AppState.Live(a);
            Color statusColor = li.CookieBad ? Theme.Bad : (runningIds.Contains(a.Id) ? Theme.Good : Theme.Muted);
            int maxStatus = Math.Max(70, (right - x0) * 45 / 100);
            int sw = status.Length == 0 ? 0 : Math.Min(maxStatus, TextRenderer.MeasureText(g, status, Theme.Small, new Size(1000, 20), flags).Width + 2);
            if (sw > 0)
                TextRenderer.DrawText(g, status, Theme.Small, new Rectangle(right - sw, y1, sw, 18), statusColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, DisplayName(a), Theme.Bold, new Rectangle(x0, y1, Math.Max(40, right - sw - 10 - x0), 18), Theme.Text, flags);

            // line 2: @username, group (max 20 characters), last used ... and CPU / RAM on the right
            string usage = UsageText(a);
            int reserve = 0;
            if (usage.Length > 0)
            {
                int uw = TextRenderer.MeasureText(g, usage, Theme.Small, new Size(1000, 20), flags).Width + 2;
                TextRenderer.DrawText(g, usage, Theme.Small, new Rectangle(right - uw, y2, uw, 17), Theme.Text,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                reserve = uw + 12;
            }
            int limit = right - reserve;
            int cx = DrawSeg(g, "@" + a.Username, x0, y2, limit, Theme.Muted);
            if (!string.IsNullOrEmpty(a.Group)) cx = DrawChip(g, Util.Short(a.Group, 20), cx + 8, y2, limit);
            string used = a.LastUsedTicks > 0 ? "used " + Util.Ago(a.LastUsedTicks) : "never used";
            DrawSeg(g, used, cx + 10, y2, limit, Theme.Muted);
        }

        void SetSort(int col)
        {
            if (col < 0) { sortCol = -1; sortAsc = true; }
            else if (sortCol == col) sortAsc = !sortAsc;
            else { sortCol = col; sortAsc = true; }
            RefreshAccountList();
        }

        void LvSelectionChanged(object sender, EventArgs e)
        {
            if (suspendSel) return;
            HashSet<string> ids = new HashSet<string>();
            foreach (ListViewItem it in lv.SelectedItems)
            {
                Account a = it.Tag as Account;
                if (a != null) ids.Add(a.Id);
            }
            selected = ids;
            UpdateCount();
            OnAccountSelectionChanged();
        }

        // =====================================================================
        //  Card view
        // =====================================================================
        void SetView(bool cardsOn)
        {
            cardsView = cardsOn;
            Store.Settings.View = cardsOn ? "Cards" : "List";
            Store.SaveSettings();
            lv.Visible = !cardsOn;
            cardHost.Visible = cardsOn;
            if (cardsOn) RebuildCards();
            ApplySelectionToViews();
            StyleViewButtons();
        }

        void RebuildCards()
        {
            if (cardHost == null) return;
            cardHost.SuspendLayout();
            foreach (Control old in cardHost.Controls.Cast<Control>().ToList())
            {
                cardHost.Controls.Remove(old);
                old.Dispose();
            }
            cards.Clear();
            RebuildRunningIds();
            foreach (Account a in FilteredAccounts())
            {
                AccountCard card = new AccountCard(a);
                card.CardClicked += OnCardClicked;
                card.LaunchClicked += OnCardLaunch;
                card.MenuClicked += OnCardMenu;
                card.GameCommitted += OnCardGame;
                cards[a.Id] = card;
                cardHost.Controls.Add(card);
                Theme.Apply(card);
                RefreshCard(card);
            }
            LayoutCards();
            cardHost.ResumeLayout(true);
        }

        // 1 column in a narrow panel, 2+ columns when you drag the splitter wider.
        void LayoutCards()
        {
            if (cardHost == null || cardHost.Controls.Count == 0) return;
            int avail = cardHost.ClientSize.Width - cardHost.Padding.Horizontal - 2;
            int gap = 10;
            int minW = 330;
            int cols = Math.Max(1, (avail + gap) / (minW + gap));
            int w = Math.Max(300, avail / cols - gap);
            cardHost.SuspendLayout();
            foreach (Control c in cardHost.Controls) c.Width = w;
            cardHost.ResumeLayout(true);
        }

        void PillFor(Account a, out string text, out Color color)
        {
            LiveInfo li = AppState.Live(a);
            if (li.Locked) { text = "Account locked"; color = Theme.Bad; return; }
            if (li.CookieBad) { text = "Cookie expired"; color = Theme.Bad; return; }
            if (runningIds.Contains(a.Id)) { text = "Running"; color = Theme.Good; return; }
            if (li.PresenceType == 2) { text = "In game"; color = Theme.Good; }
            else if (li.PresenceType == 3) { text = "Studio"; color = Theme.Purple; }
            else if (li.PresenceType == 1) { text = "Online"; color = Theme.Info; }
            else if (li.PresenceType == 0) { text = "Offline"; color = Theme.Muted; }
            else { text = "Not checked"; color = Theme.Border; }
        }

        string GameText(Account a)
        {
            if (!string.IsNullOrEmpty(a.GameLink)) return a.GameLink;
            if (Util.ParsePlaceId(a.SavedPlaceId) > 0) return "https://www.roblox.com/games/" + a.SavedPlaceId.Trim();
            return "";
        }

        void RefreshCard(AccountCard card)
        {
            Account a = card.Account;
            string pill;
            Color pc;
            PillFor(a, out pill, out pc);
            bool running = runningIds.Contains(a.Id);
            string last = a.LastUsedTicks > 0 ? "last used " + Util.Ago(a.LastUsedTicks) : "never launched here";
            string info;
            double cpu;
            long ram;
            if (running && UsageFor(a, out cpu, out ram)) info = "CPU " + CpuText(cpu) + "  -  RAM " + Util.FormatBytes(ram) + "  -  " + last;
            else info = last;
            string user = "@" + a.Username + (string.IsNullOrEmpty(a.Group) ? "" : "   -   " + Util.Short(a.Group, 20));
            card.SetState(DisplayName(a), user, info, pill, pc, running, GameText(a), selected.Contains(a.Id));
        }

        void OnCardClicked(AccountCard card, bool ctrl)
        {
            string id = card.Account.Id;
            if (ctrl) { if (!selected.Remove(id)) selected.Add(id); }
            else { selected.Clear(); selected.Add(id); }
            ApplySelectionToViews();
            OnAccountSelectionChanged();
            UpdateCount();
        }

        void OnCardLaunch(AccountCard card)
        {
            Account a = card.Account;
            if (runningIds.Contains(a.Id)) StopAccount(a);
            else LaunchOne(a);
        }

        void OnCardMenu(AccountCard card)
        {
            if (!selected.Contains(card.Account.Id))
            {
                selected.Clear();
                selected.Add(card.Account.Id);
                ApplySelectionToViews();
                OnAccountSelectionChanged();
                UpdateCount();
            }
            accMenu.Show(card.MenuButton, new Point(0, card.MenuButton.Height));
        }

        void OnCardGame(AccountCard card, string text)
        {
            Account a = card.Account;
            a.GameLink = text;
            long pid = Util.ParsePlaceId(text);
            a.SavedPlaceId = pid > 0 ? pid.ToString() : "";
            if (text.Length > 0 && pid <= 0) AddLog("No Place ID found in the game text of " + AppState.Name(a) + " - use a roblox.com/games/... link or a number.");
            else if (pid > 0) AddLog(AppState.Name(a) + " will join place " + pid + " when launched.");
            Store.SaveAccounts();
        }

        void ApplySelectionToViews()
        {
            suspendSel = true;
            foreach (ListViewItem it in lv.Items)
            {
                Account a = it.Tag as Account;
                bool on = a != null && selected.Contains(a.Id);
                if (it.Selected != on) it.Selected = on;
            }
            suspendSel = false;
            foreach (KeyValuePair<string, AccountCard> kv in cards)
            {
                bool on = selected.Contains(kv.Key);
                if (kv.Value.Selected != on) { kv.Value.Selected = on; kv.Value.Invalidate(); }
            }
            UpdateCount();
        }

        // =====================================================================
        //  List content
        // =====================================================================
        string DisplayName(Account a)
        {
            if (!string.IsNullOrEmpty(a.Alias)) return a.Alias;
            if (!string.IsNullOrEmpty(a.DisplayName)) return a.DisplayName;
            return a.Username;
        }

        string StatusText(Account a)
        {
            LiveInfo li = AppState.Live(a);
            if (li.Locked) return "Account locked (HTTP 403) - Roblox blocked this account";
            if (li.CookieBad) return "Cookie expired - Edit > replace cookie";
            string s = li.Where;
            if (runningIds.Contains(a.Id)) return string.IsNullOrEmpty(s) ? "Running (launched here)" : "Running - " + s;
            return s;
        }

        bool UsageFor(Account a, out double cpu, out long ram)
        {
            cpu = 0;
            ram = 0;
            bool any = false;
            foreach (Instance i in Launcher.Instances.ToList())
            {
                if (i.Account != a) continue;
                UsageSample s;
                if (UsageMonitor.Samples.TryGetValue(i.Pid, out s))
                {
                    cpu += s.Cpu;
                    ram += s.Ram;
                    any = true;
                }
            }
            return any;
        }

        string UsageText(Account a)
        {
            double cpu;
            long ram;
            if (runningIds.Contains(a.Id) && UsageFor(a, out cpu, out ram)) return CpuText(cpu) + "  -  " + Util.FormatBytes(ram);
            return "";
        }

        double CpuOf(Account a)
        {
            double cpu;
            long ram;
            return UsageFor(a, out cpu, out ram) ? cpu : -1;
        }

        List<Account> FilteredAccounts()
        {
            string q = tbSearch == null ? "" : tbSearch.Text.Trim().ToLowerInvariant();
            string grp = (cbGroupFilter == null || cbGroupFilter.SelectedItem == null) ? "All groups" : cbGroupFilter.SelectedItem.ToString();
            IEnumerable<Account> list = Store.Accounts;
            if (q.Length > 0)
                list = list.Where(a => (a.Username + " " + a.Alias + " " + a.Description + " " + a.Group + " " + a.DisplayName).ToLowerInvariant().Contains(q));
            if (grp == "(no group)") list = list.Where(a => string.IsNullOrEmpty(a.Group));
            else if (grp != "All groups") list = list.Where(a => a.Group == grp);

            if (sortCol == 1) list = sortAsc ? list.OrderBy(a => DisplayName(a), StringComparer.OrdinalIgnoreCase) : list.OrderByDescending(a => DisplayName(a), StringComparer.OrdinalIgnoreCase);
            else if (sortCol == 2) list = sortAsc ? list.OrderBy(a => a.Group, StringComparer.OrdinalIgnoreCase) : list.OrderByDescending(a => a.Group, StringComparer.OrdinalIgnoreCase);
            else if (sortCol == 3) list = sortAsc ? list.OrderBy(a => StatusText(a), StringComparer.OrdinalIgnoreCase) : list.OrderByDescending(a => StatusText(a), StringComparer.OrdinalIgnoreCase);
            else if (sortCol == 4) list = sortAsc ? list.OrderBy(a => CpuOf(a)) : list.OrderByDescending(a => CpuOf(a));
            else if (sortCol == 5) list = sortAsc ? list.OrderByDescending(a => a.LastUsedTicks) : list.OrderBy(a => a.LastUsedTicks);
            else list = list.OrderBy(a => a.Group ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Order);
            return list.ToList();
        }

        public List<Account> SelectedAccounts()
        {
            List<Account> res = new List<Account>();
            foreach (Account a in FilteredAccounts())
                if (selected.Contains(a.Id)) res.Add(a);
            return res;
        }

        void RebuildGroupFilter()
        {
            string keep = cbGroupFilter.SelectedItem == null ? "All groups" : cbGroupFilter.SelectedItem.ToString();
            suspendSel = true;
            cbGroupFilter.Items.Clear();
            cbGroupFilter.Items.Add("All groups");
            cbGroupFilter.Items.Add("(no group)");
            foreach (string g in Store.AllGroups()) cbGroupFilter.Items.Add(g);
            int idx = cbGroupFilter.Items.IndexOf(keep);
            cbGroupFilter.SelectedIndex = idx >= 0 ? idx : 0;
            suspendSel = false;
        }

        public void RefreshAccountList()
        {
            if (lv == null) return;
            selected.IntersectWith(Store.Accounts.Select(x => x.Id));
            RebuildGroupFilter();
            RebuildRunningIds();
            suspendSel = true;
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (Account a in FilteredAccounts())
            {
                ListViewItem it = new ListViewItem("");
                it.Tag = a;
                it.SubItems.Add(DisplayName(a));
                it.SubItems.Add(a.Group ?? "");
                it.SubItems.Add(StatusText(a));
                it.SubItems.Add(UsageText(a));
                lv.Items.Add(it);
                if (selected.Contains(a.Id)) it.Selected = true;
            }
            lv.EndUpdate();
            suspendSel = false;
            if (cardsView) RebuildCards();
            UpdateCount();
            OnAccountSelectionChanged();
            UpdateStats();
        }

        void RebuildRunningIds()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (Instance i in Launcher.Instances.ToList())
                if (Launcher.IsAlive(i.Pid)) ids.Add(i.Account.Id);
            runningIds = ids;
        }

        // Cheap in-place refresh (keeps selection, scroll position and typing in the card fields).
        void UpdateStatusCells()
        {
            if (lv == null) return;
            RebuildRunningIds();
            if (cardsView)
            {
                foreach (AccountCard c in cardHost.Controls.OfType<AccountCard>()) RefreshCard(c);
            }
            else
            {
                lv.BeginUpdate();
                foreach (ListViewItem it in lv.Items)
                {
                    Account a = it.Tag as Account;
                    if (a == null) continue;
                    it.SubItems[3].Text = StatusText(a);
                    it.SubItems[4].Text = UsageText(a);
                }
                lv.EndUpdate();
                lv.Invalidate();
            }
            UpdateStats();
        }

        void UpdateCount()
        {
            if (lblCount == null) return;
            lblCount.Text = Store.Accounts.Count + " accounts, " + SelectedAccounts().Count + " selected";
        }

        // =====================================================================
        //  Adding accounts
        // =====================================================================
        async Task<bool> AddCookie(string cookie)
        {
            UserInfo u = await Api.Authenticated(cookie);
            if (!u.Valid)
            {
                AddLog("Cookie rejected (" + u.Error + "): " + Util.Trunc(cookie, 38));
                return false;
            }
            Account existing = Store.Accounts.FirstOrDefault(x => x.UserId == u.Id);
            if (existing != null)
            {
                existing.Cookie = cookie;
                existing.Username = u.Name;
                existing.DisplayName = u.Display;
                AppState.Live(existing).CookieBad = false;
                existing.LoginLost = false;
                AddLog("Updated the cookie of existing account " + u.Name + ".");
            }
            else
            {
                Account a = new Account();
                a.Cookie = cookie;
                a.UserId = u.Id;
                a.Username = u.Name;
                a.DisplayName = u.Display;
                a.Order = Store.Accounts.Count;
                Store.Accounts.Add(a);
                AddLog("Added account " + u.Name + ".");
            }
            Store.SaveAccounts();
            return true;
        }

        async void AddByBrowser()
        {
            if (browserCts != null)
            {
                browserCts.Cancel();
                return;
            }
            browserCts = new System.Threading.CancellationTokenSource();
            try
            {
                string cookie = await BrowserLogin.GetCookie(AddLog, browserCts.Token, async delegate(string c)
                {
                    UserInfo u = await Api.Authenticated(c);
                    return u.Valid;
                });
                if (cookie != null)
                {
                    await AddCookie(cookie);
                    RefreshAccountList();
                    RefreshPresence();
                }
            }
            catch (Exception ex) { Log.Error("AddByBrowser", ex); }
            browserCts = null;
        }

        // Opens a prefilled browser login for each "username:password" line, one after another.
        // The user presses Log In on each; the cookie is captured and the account added automatically.
        async void AddSeveralByBrowser()
        {
            if (browserCts != null) { browserCts.Cancel(); return; }
            string raw = Dialogs.PasteUserPass(this);
            if (raw == null) return;

            List<string[]> pairs = new List<string[]>();
            foreach (string line in raw.Replace("\r\n", "\n").Split('\n'))
            {
                string s = line.Trim();
                int sep = s.IndexOf(':');
                if (sep <= 0) continue;
                string user = s.Substring(0, sep).Trim();
                string pass = s.Substring(sep + 1);     // keep the password exactly, even with spaces
                if (user.Length > 0) pairs.Add(new string[] { user, pass });
            }
            if (pairs.Count == 0) { AddLog("No valid username:password lines were found."); return; }

            AddLog("Adding " + pairs.Count + " account(s) with username:password. A browser opens for each - press 'Log In' on each window.");
            int added = 0;
            browserCts = new System.Threading.CancellationTokenSource();
            try
            {
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (browserCts.IsCancellationRequested) break;
                    AddLog("Account " + (i + 1) + " of " + pairs.Count + ": " + pairs[i][0] + " - waiting for you to log in...");
                    string cookie = await BrowserLogin.GetCookie(AddLog, browserCts.Token, async delegate(string c)
                    {
                        UserInfo u = await Api.Authenticated(c);
                        return u.Valid;
                    }, pairs[i][0], pairs[i][1]);
                    if (cookie != null) { if (await AddCookie(cookie)) added++; RefreshAccountList(); RefreshPresence(); }
                    else AddLog("Account " + (i + 1) + " (" + pairs[i][0] + ") was skipped (no login detected).");
                }
            }
            catch (Exception ex) { Log.Error("AddSeveralByBrowser", ex); }
            browserCts = null;
            AddLog("Finished: " + added + " of " + pairs.Count + " account(s) added.");
        }

        void AddByPaste()
        {
            string raw = Dialogs.PasteCookies(this);
            if (raw != null) AddFromText(raw);
        }

        void AddByFile()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Choose a text file that contains cookies";
                d.Filter = "Text / JSON files|*.txt;*.json;*.csv;*.log|All files|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { AddFromText(File.ReadAllText(d.FileName)); }
                catch (Exception ex) { MessageBox.Show(this, "Could not read that file: " + ex.Message); }
            }
        }

        async void AddFromText(string raw)
        {
            List<string> cookies = Util.ExtractCookies(raw);
            if (cookies.Count == 0)
            {
                MessageBox.Show(this, "No Roblox cookies were found in that text.\r\nA cookie starts with  _|WARNING:-DO-NOT-SHARE-THIS...", "Roblox MultiHub");
                return;
            }
            int ok = 0;
            int bad = 0;
            AddLog("Checking " + cookies.Count + " cookie(s)...");
            foreach (string c in cookies)
            {
                if (await AddCookie(c)) ok++; else bad++;
                await Task.Delay(300);
            }
            AddLog("Import finished: " + ok + " account(s) added/updated, " + bad + " cookie(s) did not work.");
            RefreshAccountList();
            RefreshPresence();
        }

        void LvDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText)) e.Effect = DragDropEffects.Copy;
        }

        void LvDragDrop(object sender, DragEventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                    foreach (string f in files) { try { sb.AppendLine(File.ReadAllText(f)); } catch (Exception) { } }
                string text = e.Data.GetData(DataFormats.UnicodeText) as string;
                if (text != null) sb.AppendLine(text);
                AddFromText(sb.ToString());
            }
            catch (Exception ex) { Log.Error("drop", ex); }
        }

        // =====================================================================
        //  Account actions
        // =====================================================================
        Account FirstSelected()
        {
            List<Account> s = SelectedAccounts();
            return s.Count > 0 ? s[0] : null;
        }

        void EditSelected()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select an account first."); return; }
            if (Dialogs.EditAccounts(this, sel))
            {
                foreach (Account a in sel)
                    if (!string.IsNullOrEmpty(a.Group) && !Store.Settings.Groups.Contains(a.Group)) Store.Settings.Groups.Add(a.Group);
                Store.SaveSettings();
                if (sel.Count > 1) AddLog("Saved the changes to " + sel.Count + " accounts.");
                Store.SaveAccounts();
                RefreshAccountList();
            }
        }

        void RemoveSelected()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) return;
            string msg = sel.Count == 1 ? "Remove account '" + AppState.Name(sel[0]) + "' from this app?" : "Remove " + sel.Count + " accounts from this app?";
            if (MessageBox.Show(this, msg + "\r\n(The Roblox account itself is not touched.)", "Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            foreach (Account a in sel) Store.Accounts.Remove(a);
            Store.RenumberOrder();
            Store.SaveAccounts();
            RefreshAccountList();
        }

        void SetGroup(string group)
        {
            foreach (Account a in SelectedAccounts()) a.Group = group;
            if (!string.IsNullOrEmpty(group) && !Store.Settings.Groups.Contains(group)) Store.Settings.Groups.Add(group);
            Store.SaveAccounts();
            Store.SaveSettings();
            RefreshAccountList();
        }

        void MoveSelected(int dir)
        {
            Account a = FirstSelected();
            if (a == null) return;
            sortCol = -1;
            List<Account> vis = FilteredAccounts();
            int idx = vis.IndexOf(a);
            int to = idx + dir;
            if (idx < 0 || to < 0 || to >= vis.Count) return;
            Account other = vis[to];
            int tmp = a.Order;
            a.Order = other.Order;
            other.Order = tmp;
            if (a.Order == other.Order) a.Order += dir;
            Store.SaveAccounts();
            RefreshAccountList();
        }

        void CopyField(Func<Account, string> get)
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select an account first."); return; }
            string text = string.Join("\r\n", sel.Select(a => get(a)).ToArray());
            if (string.IsNullOrEmpty(text.Trim())) { AddLog("That field is empty for the selected account."); return; }
            Util.Copy(text);
            AddLog("Copied to clipboard (" + sel.Count + " account(s)).");
        }

        async void CheckAccounts()
        {
            List<Account> list = SelectedAccounts();
            if (list.Count == 0) list = Store.Accounts.ToList();
            if (list.Count == 0) return;
            int good = 0;
            int bad = 0;
            AddLog("Checking " + list.Count + " account(s)...");
            foreach (Account a in list)
            {
                UserInfo u = await Api.Authenticated(a.Cookie);
                LiveInfo li = AppState.Live(a);
                if (u.Valid)
                {
                    a.UserId = u.Id;
                    a.Username = u.Name;
                    a.DisplayName = u.Display;
                    li.CookieBad = false;
                    li.Locked = false;
                    li.LockedAlerted = false;
                    a.LoginLost = false;
                    good++;
                }
                else
                {
                    if (u.Locked)
                    {
                        if (!li.LockedAlerted && Store.Settings.WebhookLocked)
                        {
                            Webhook.Send("Account locked", AppState.Name(a) + " — Roblox returned HTTP 403 (locked / blocked). Unlock it on roblox.com.", Webhook.Red);
                            li.LockedAlerted = true;
                        }
                        li.Locked = true;
                    }
                    else if (u.Error.Contains("expired")) li.CookieBad = true;
                    bad++;
                    AddLog(AppState.Name(a) + ": " + u.Error);
                }
                await Task.Delay(250);
            }
            Store.SaveAccounts();
            RefreshAccountList();
            AddLog("Check done: " + good + " working, " + bad + " not working.");
        }

        async void ShowDetails()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select an account first."); return; }
            if (sel.Count > 1) AddLog("Loading the details of " + sel.Count + " accounts...");
            StringBuilder sb = new StringBuilder();
            foreach (Account a in sel)
            {
                if (sel.Count > 1)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine("==== " + DisplayName(a) + " (@" + a.Username + ") ====");
                }
                sb.Append(await Api.Details(a));
            }
            string title = "Account details - " + string.Join(" - ", sel.Select(x => x.Username).ToArray());
            Dialogs.ShowText(this, title, sb.ToString());
        }

        async void CopyTicket()
        {
            Account a = FirstSelected();
            if (a == null) { AddLog("Select an account first."); return; }
            TicketResult t = await Api.GetTicket(a.Cookie);
            if (t.Ticket == null) { AddLog("Could not get a ticket: " + t.Error); return; }
            Util.Copy(t.Ticket);
            AddLog("Login ticket copied. It is single-use and expires quickly - do not share it.");
        }

        void OpenProfile()
        {
            Account a = FirstSelected();
            if (a == null || a.UserId <= 0) { AddLog("Select an account first."); return; }
            try { Process.Start(new ProcessStartInfo("https://www.roblox.com/users/" + a.UserId + "/profile") { UseShellExecute = true }); }
            catch (Exception ex) { AddLog("Could not open the browser: " + ex.Message); }
        }

        async void RotateSelected()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select an account first."); return; }
            foreach (Account a in sel)
            {
                string nc = await Api.RotateCookie(a.Cookie, AddLog);
                if (nc != null)
                {
                    a.Cookie = nc;
                    AppState.Live(a).CookieBad = false;
                    Store.SaveAccounts();
                    AddLog(AppState.Name(a) + ": cookie refreshed.");
                }
            }
        }

        // =====================================================================
        //  Presence
        // =====================================================================
        async void RefreshPresence()
        {
            if (presenceBusy || !Store.Settings.ShowPresence) return;
            Account src = Store.Accounts.FirstOrDefault(x => x.Cookie.Length > 0 && !AppState.Live(x).CookieBad);
            if (src == null) return;
            List<long> ids = Store.Accounts.Where(x => x.UserId > 0).Select(x => x.UserId).Distinct().ToList();
            if (ids.Count == 0) return;
            presenceBusy = true;
            try
            {
                Dictionary<long, PresenceInfo> map = await Api.Presence(src.Cookie, ids);
                foreach (Account acc in Store.Accounts)
                {
                    PresenceInfo pi;
                    if (!map.TryGetValue(acc.UserId, out pi)) continue;
                    LiveInfo li = AppState.Live(acc);
                    li.PresenceType = pi.Type;
                    if (pi.Type == 2) li.Where = "In game: " + (string.IsNullOrEmpty(pi.Location) ? "(unknown)" : pi.Location);
                    else if (pi.Type == 3) li.Where = "In Roblox Studio";
                    else if (pi.Type == 1) li.Where = "Online (website / app)";
                    else li.Where = "Offline";
                }
            }
            catch (Exception ex) { Log.Error("presence", ex); }
            presenceBusy = false;
            UpdateStatusCells();
        }
    }
}
