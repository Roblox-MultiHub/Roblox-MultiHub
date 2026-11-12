using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
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
        RoundPanel logHost;
        TableLayoutPanel rightTable;
        Label logHeader;
        bool logOpen;
        RoundPanel topBarRef;
        Label topTitleRef, topSubRef;
        Control[] headerInteractive = new Control[0];
        RoundButton btnWinMin, btnWinClose;
        System.Windows.Forms.Timer logAnim;
        float logHFrom, logHTo;
        int logT0;
        const int LogAnimMs = 220;
        const float LogClosedH = 34F, LogOpenH = 190F;
        int logClosedPx = 34, logOpenPx = 190;         // real pixel heights (DPI scaled in ScaleLayout)
        TableLayoutPanel bodyTable;
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
        GradientButton footLaunch;
        RoundButton footAll, footStop, footUp, footDown;

        public MainForm(bool minimized)
        {
            startMinimized = minimized;
            Theme.Set(Store.Settings.Theme != "Light");
            // RTL languages (Arabic, Hebrew): text direction flips, layout does not - the manual
            // positioning does not mirror safely, and Windows shapes the scripts either way
            RightToLeft = (Store.Settings.Language == "ar" || Store.Settings.Language == "he")
                ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = false;
            Text = "Roblox MultiHub";
            Font = Theme.Base;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Math.Max(1180, Store.Settings.WinW), Math.Max(660, Store.Settings.WinH));
            MinimumSize = new Size(1180, 660);
            FormBorderStyle = FormBorderStyle.None;          // custom chrome: the top bar is the title bar
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            BuildUi();

            Log.Line += OnLogLine;
            Avatars.Updated += OnAvatarsUpdated;
            Application.AddMessageFilter(new WheelRedirect());   // mouse wheel scrolls the page, not a hovered text box
            tick = new System.Windows.Forms.Timer();
            tick.Interval = 1000;
            tick.Tick += OnTick;
            tick.Start();
        }

        // Builds EVERYTHING visible. Runs once at start and again when the language changes -
        // rebuilding in place applies the new language to all text with no restart (a restart also
        // collided with the single-instance mutex, which is why it said 'already running').
        void BuildUi()
        {
            // stop everything that still points at the old controls
            try
            {
                if (tick != null) tick.Stop();
                if (logAnim != null) { logAnim.Stop(); logAnim = null; }
                Anim.Clear();
                if (root != null)
                {
                    foreach (Control c in root.Controls) c.Dispose();
                    Controls.Remove(root);
                    root.Dispose();
                }
                pages.Clear();
                navButtons.Clear();
                cards.Clear();
                monCards.Clear();
                winRows.Clear();
            }
            catch (Exception) { }

            BuildShell();
            BuildHomePage();
            BuildAccounts();
            BuildLaunchPage();
            BuildServersPage();
            BuildLayoutPage();
            BuildMonitorPage();
            BuildPresetsPage();
            BuildPerfPage();
            BuildSettingsPage();
            BuildHelpPage();
            BuildBetaPage();
            ApplyTheme();
            ShowPage(currentPage != "" ? currentPage : "Home");
            RefreshAccountList();
            SetView(Store.Settings.View == Lang.T("Cards"));
            if (tick != null) tick.Start();
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
            root.Padding = new Padding(10);          // the top bar ends where the body ends - no overhang
            Controls.Add(root);

            // ---- top bar: title, live usage, multi-instance status ----
            RoundPanel topBar = new RoundPanel();
            topBar.Dock = DockStyle.Fill;
            topBar.Tag = "card";
            topBar.Margin = Padding.Empty;
            topBarRef = topBar;
            Label title = Ui.Lbl("Roblox MultiHub", "title");
            title.Location = new Point(18, 7);
            topBar.Controls.Add(title);
            topTitleRef = title;
            Label sub = Ui.Lbl(Lang.T("Multi-account launcher"), "muted");
            sub.Location = new Point(21, 43);
            topBar.Controls.Add(sub);
            topSubRef = sub;
            DiscordIcon disc = new DiscordIcon();
            disc.Location = new Point(sub.Right + 10, sub.Top + (sub.Height - disc.Height) / 2);
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
            Ui.Tips.SetToolTip(lblMulti, Lang.T("Click to open Settings. Multi-instance is what lets several Roblox windows run at the same time."));
            topBar.Controls.Add(lblMulti);

            // real clicks over child controls never reach the form's hit-test, so every header control
            // starts the drag itself. Exceptions: the Discord icon and the window buttons keep their
            // clicks; the multi-instance label only drags once the mouse actually MOVES, so a plain
            // click still opens Settings.
            MouseEventHandler dragDown = delegate(object s, MouseEventArgs me)
            {
                if (me.Button == MouseButtons.Left) Native.DragWindow((Control)s);
            };
            HookHeaderDrag(topBar, dragDown, disc, btnWinMin, btnWinClose, lblMulti);
            Point multiDown = new Point(int.MinValue, int.MinValue);
            bool multiDragging = false;
            lblMulti.MouseDown += delegate(object s, MouseEventArgs me)
            {
                multiDown = me.Location;
                multiDragging = false;
            };
            lblMulti.MouseMove += delegate(object s, MouseEventArgs me)
            {
                if (multiDragging || me.Button != MouseButtons.Left || multiDown.X == int.MinValue) return;
                if (Math.Abs(me.X - multiDown.X) > 4 || Math.Abs(me.Y - multiDown.Y) > 4)
                {
                    multiDragging = true;
                    Native.DragWindow(lblMulti);
                }
            };
            lblMulti.MouseUp += delegate { multiDown = new Point(int.MinValue, int.MinValue); multiDragging = false; };
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
                lblMulti.Location = new Point(topBar.Width - lblMulti.Width - 130, 12);
                // spacing from the buttons' REAL (text-fitted) widths: close keeps an 18px margin to
                // the bar edge (clear of the window's 16px corner curve), min sits left of it
                int by = Math.Max(6, (topBar.Height - btnWinClose.Height) / 2);
                btnWinClose.Location = new Point(topBar.Width - btnWinClose.Width - 18, by);
                btnWinMin.Location = new Point(btnWinClose.Left - btnWinMin.Width - 8, by);
            };

            // window buttons (borderless form): minimize + close, laid out by topBar.Resize above
            btnWinMin = Ui.Btn("\u2014", 30, delegate { WindowState = FormWindowState.Minimized; }, "nav");
            btnWinMin.Height = 30;
            Ui.Tips.SetToolTip(btnWinMin, "Minimize");
            topBar.Controls.Add(btnWinMin);
            btnWinClose = Ui.Btn("\u2715", 30, delegate { Close(); }, "danger");
            btnWinClose.Height = 30;
            Ui.Tips.SetToolTip(btnWinClose, Lang.T("Close Roblox MultiHub"));
            topBar.Controls.Add(btnWinClose);

            // header controls that must keep their own clicks
            headerInteractive = new Control[] { disc, btnWinMin, btnWinClose };
            root.Controls.Add(topBar, 0, 0);

            // ---- body: sidebar | (accounts | page), with a draggable splitter ----
            TableLayoutPanel body = new TableLayoutPanel();
            bodyTable = body;
            body.Dock = DockStyle.Fill;
            body.ColumnCount = 2;
            body.RowCount = 1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 208F));   // sidebar (178 button + padding + slack)
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            body.Padding = new Padding(0, 10, 10, 10);   // gap to the top bar; outer spacing comes from root.Padding
            body.Margin = Padding.Empty;
            root.Controls.Add(body, 0, 1);

            BuildSidebar(body);

            split = new SplitContainer();
            split.Size = new Size(900, 600);
            try
            {
                split.Orientation = Orientation.Vertical;
                split.SplitterWidth = 10;
                split.FixedPanel = FixedPanel.Panel1;
                split.Panel1MinSize = 300;
                split.Panel2MinSize = 470;
            }
            catch (Exception ex) { Log.Error("split", ex); }
            split.BorderStyle = BorderStyle.None;
            split.TabStop = false;
            split.Dock = DockStyle.Fill;
            split.Margin = Padding.Empty;
            split.SplitterMoved += delegate { LayoutCards(); };
            body.Controls.Add(split, 1, 0);

            leftHost = new RoundPanel();
            leftHost.Dock = DockStyle.Fill;
            leftHost.Tag = "card";
            leftHost.Padding = new Padding(8);
            split.Panel1.Controls.Add(leftHost);

            rightTable = new TableLayoutPanel();
            rightTable.Dock = DockStyle.Fill;
            rightTable.ColumnCount = 1;
            rightTable.RowCount = 2;
            rightTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));     // page
            rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));     // log (collapsed by default)
            rightTable.Margin = Padding.Empty;
            split.Panel2.Controls.Add(rightTable);

            pageHost = new RoundPanel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.Tag = "card";
            pageHost.Padding = new Padding(6);
            pageHost.Margin = Padding.Empty;
            rightTable.Controls.Add(pageHost, 0, 0);

            // ---- collapsible activity log (closed by default to keep the screen calm) ----
            logHost = new RoundPanel();
            logHost.Dock = DockStyle.Fill;
            logHost.Tag = "card";
            logHost.Radius = 8;                    // smaller radius: 14 on the collapsed strip looks like a pill
            logHost.Padding = new Padding(8, 0, 8, 0);
            logHost.Margin = new Padding(0, 10, 0, 0);

            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill;
            logBox.Font = Theme.Mono;
            logBox.Tag = "log";
            logBox.Visible = false;
            logHost.Controls.Add(logBox);

            logHeader = Ui.Lbl("▸  Activity log", "muted");
            logHeader.AutoSize = false;
            logHeader.Dock = DockStyle.Top;
            logHeader.Height = logClosedPx;               // fills the collapsed band completely, so the
            logHeader.TextAlign = ContentAlignment.MiddleLeft;   // centered text sits dead-center at ANY DPI
            logHeader.Cursor = Cursors.Hand;
            logHeader.Click += delegate { ToggleLog(); };
            Ui.Tips.SetToolTip(logHeader, "Everything the app does is written here. Click to open or close it.");
            logHost.Controls.Add(logHeader);
            // self-correcting: while collapsed the header always EXACTLY matches the real strip
            // height (DPI scale, collapse animation, row changes - whatever resized it), so the
            // centered text cannot end up off-center through any code path
            logHost.Resize += delegate
            {
                if (!logOpen) logHeader.Height = logHost.ClientSize.Height;
            };
            rightTable.Controls.Add(logHost, 0, 1);
        }

        // Left navigation sidebar. Main pages on top, Settings / Help / Macro pinned at the bottom
        // so they are always in the same place and easy to find.
        // TableLayoutPanel Absolute row/column sizes are NOT scaled by Windows DPI scaling - only the
        // controls inside them are. At 125%/150% the rows then clip their contents (window buttons on
        // top of the top bar, the nav highlight cut off on the right). Re-applied with the real factor
        // once the form has been scaled. Also fixes the log heights for the same reason.
        void ScaleLayout()
        {
            float sx = AutoScaleFactor.Width, sy = AutoScaleFactor.Height;
            if (root != null) root.RowStyles[0] = new RowStyle(SizeType.Absolute, 68F * sy);
            if (bodyTable != null) bodyTable.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 208F * sx);
            logClosedPx = Math.Max(34, (int)(LogClosedH * sy));
            logOpenPx = Math.Max(60, (int)(LogOpenH * sy));
            if (logHeader != null && !logOpen) logHeader.Height = logClosedPx;   // stay dead-center at this DPI
            if (rightTable != null && !logOpen)
                rightTable.RowStyles[1] = new RowStyle(SizeType.Absolute, logClosedPx);
        }

        // attaches the drag handler to a header control and all its children, except the listed ones
        // The sliding nav pill: fully transparent to the mouse (HTTRANSPARENT), so the buttons
        // underneath stay clickable while the pill and its label ride on top of them.
        class NavPill : RoundPanel
        {
            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84;
                if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)(-1); return; }   // HTTRANSPARENT
                base.WndProc(ref m);
            }
        }

        void HookHeaderDrag(Control c, MouseEventHandler handler, params Control[] skip)
        {
            foreach (Control x in skip) if (x == c) return;
            c.MouseDown += handler;
            foreach (Control ch in c.Controls) HookHeaderDrag(ch, handler, skip);
        }

        void BuildSidebar(TableLayoutPanel body)
        {
            RoundPanel side = new RoundPanel();
            side.Dock = DockStyle.Fill;
            side.Tag = "card";
            side.Padding = new Padding(11, 12, 11, 12);
            side.Margin = Padding.Empty;            // spacing to the accounts panel comes from body.Padding
            body.Controls.Add(side, 0, 0);

            // bottom group first, so Dock=Bottom keeps it pinned under the top group
            FlowLayoutPanel bottom = NewNavFlow();
            bottom.Dock = DockStyle.Bottom;
            Panel sep = new Panel();
            sep.Tag = "line";
            sep.Height = 1;
            sep.Width = 178;
            sep.Margin = new Padding(1, 6, 1, 8);
            bottom.Controls.Add(sep);
            bottom.Controls.Add(NavBtn("Settings", Lang.T("⚙  Settings"), false, Lang.T("Open all options and toggles. Multi-instance, performance, alerts, startup and more.")));
            bottom.Controls.Add(NavBtn("Help", Lang.T("❓  Help"), false, Lang.T("Guides, FAQ and troubleshooting.")));
            bottom.Controls.Add(NavBtn("Macro", Lang.T("\U0001F3AC  Macro"), true, Lang.T("Record and replay mouse + keyboard (like TinyTask) in its own window.")));

            FlowLayoutPanel top = NewNavFlow();
            top.Dock = DockStyle.Top;
            top.Controls.Add(NavBtn("Home", Lang.T("\U0001F3E0  Home"), false, null));
            top.Controls.Add(NavBtn("Launch", Lang.T("\U0001F680  Launch"), false, null));
            top.Controls.Add(NavBtn("Servers", Lang.T("\U0001F310  Servers"), false, null));
            top.Controls.Add(NavBtn("Layout", Lang.T("\u25A6  Layout"), false, null));
            top.Controls.Add(NavBtn(Lang.T("Monitor"), Lang.T("\U0001F4CA  Monitor"), false, null));
            top.Controls.Add(NavBtn("Presets", Lang.T("\U0001F4BE  Presets"), false, null));
            top.Controls.Add(NavBtn("Perf", Lang.T("⚡  Perf"), false, null));
            top.Controls.Add(NavBtn("Beta", Lang.T("\U0001F9EA  Beta"), false, Lang.T("New features that are still being tested. They move to their proper page once proven.")));

            side.Controls.Add(bottom);
            side.Controls.Add(top);
        }

        FlowLayoutPanel NewNavFlow()
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.FlowDirection = FlowDirection.TopDown;
            f.WrapContents = false;
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            f.Margin = Padding.Empty;
            f.Tag = "card";
            return f;
        }

        RoundButton NavBtn(string key, string label, bool macro, string tip)
        {
            RoundButton b = Ui.Btn(label, 178, null, "nav");
            b.Width = 178;
            b.Height = 40;
            b.LeftAlign = true;
            b.Margin = new Padding(0, 2, 0, 2);
            b.Click += delegate { if (macro) OpenMacro(); else ShowPage(key); };
            if (!string.IsNullOrEmpty(tip)) Ui.Tips.SetToolTip(b, tip);
            navButtons[key] = b;
            return b;
        }

        void ToggleLog()
        {
            logOpen = !logOpen;
            logBox.Visible = true;                     // stays visible while animating; clipped by the shrinking row
            logHeader.Text = (logOpen ? "▾" : "▸") + "  Activity log";
            if (rightTable == null) return;
            logHFrom = rightTable.RowStyles[1].Height;
            logHTo = logOpen ? logOpenPx : logClosedPx;
            if (logHFrom == logHTo) { logBox.Visible = logOpen; return; }
            if (logAnim == null)
            {
                logAnim = new System.Windows.Forms.Timer();
                logAnim.Interval = 15;
                logAnim.Tick += delegate { LogAnimTick(); };
            }
            logT0 = Environment.TickCount;
            logAnim.Start();
        }

        // One frame of the log collapse/expand: ease-out cubic over ~220 ms. New RowStyle objects each
        // frame (assigning .Height alone never re-runs the table layout - see FitAccountBars).
        void LogAnimTick()
        {
            float t = Math.Min(1f, (Environment.TickCount - logT0) / (float)LogAnimMs);
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            rightTable.RowStyles[1] = new RowStyle(SizeType.Absolute, logHFrom + (logHTo - logHFrom) * e);
            if (t >= 1f)
            {
                logAnim.Stop();
                if (!logOpen) logBox.Visible = false;
            }
        }

        // ---- custom chrome for the borderless window ----

        // subtle drop shadow behind the borderless window
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;               // CS_DROPSHADOW
                return cp;
            }
        }

        // Rounded window shape on every Windows version (DWM corner preference is 11+ only).
        // DROPPED while the window is being dragged: a shaped window makes DWM re-composite the
        // region on every move, which is what made the window trail the cursor on fast drags.
        // (MainForm.Layout.cs removes the region on WM_ENTERSIZEMOVE and reapplies it after.)
        void ApplyRoundRegion()
        {
            if (Width > 8 && Height > 8)
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 16, 16);
                SetWindowRgn(Handle, rgn, true);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundRegion();
        }

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

        [DllImport("user32.dll")]
        static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        // Dragging = top bar (except its interactive controls); resizing = a thin band at every edge.
        // Called from the shared WndProc in MainForm.Layout.cs after the hotkey handling.
        void ChromeHitTest(ref Message m)
        {
            base.WndProc(ref m);
            if ((int)m.Result != 1) return;             // HTCLIENT
            long lp = m.LParam.ToInt64();
            Point screen = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
            Point c = PointToClient(screen);
            const int edge = 6, corner = 14;
            bool l = c.X < edge, r = c.X >= ClientSize.Width - edge, t = c.Y < edge, b = c.Y >= ClientSize.Height - edge;
            if (t && l) { m.Result = (IntPtr)13; return; }        // HTTOPLEFT
            if (t && r) { m.Result = (IntPtr)14; return; }        // HTTOPRIGHT
            if (b && l) { m.Result = (IntPtr)16; return; }        // HTBOTTOMLEFT
            if (b && r) { m.Result = (IntPtr)17; return; }        // HTBOTTOMRIGHT
            if (l) { m.Result = (IntPtr)10; return; }             // HTLEFT
            if (r) { m.Result = (IntPtr)11; return; }             // HTRIGHT
            if (t) { m.Result = (IntPtr)12; return; }             // HTTOP
            if (b) { m.Result = (IntPtr)15; return; }             // HTBOTTOM
            if (topBarRef != null && c.Y < topBarRef.Bottom)
            {
                // the whole header band is the title bar except over interactive controls. Pure
                // rectangle math on a cached list - GetChildAtPoint is far too slow for a message
                // (WM_NCHITTEST) that fires on every mouse move, which made dragging feel laggy.
                Point inBar = topBarRef.PointToClient(screen);
                bool interactive = false;
                foreach (Control ic in headerInteractive)
                {
                    if (inBar.X >= ic.Left && inBar.X < ic.Right && inBar.Y >= ic.Top && inBar.Y < ic.Bottom)
                    {
                        interactive = true;
                        break;
                    }
                }
                if (!interactive)
                    m.Result = (IntPtr)2;                          // HTCAPTION - drag / snap / double-click
            }
        }

        Panel NewPage(string key)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.Tag = "card";
            p.Visible = false;
            Util.SetBuf(p);                     // pages slide; without buffering they tear
            pages[key] = p;
            pageHost.Controls.Add(p);
            return p;
        }

        // A calm welcome page (the sidebar's default), in the style of modern launchers.
        void BuildHomePage()
        {
            Panel page = NewPage("Home");
            page.AutoScroll = true;

            Label title = Ui.Lbl(Lang.T("Welcome to Roblox MultiHub"), "title");
            title.Location = new Point(20, 20);
            page.Controls.Add(title);

            Label sub = Ui.Lbl("Run all your Roblox accounts at once. Three steps to your first game.", "muted");
            sub.AutoSize = false; sub.Size = new Size(PageW - 40, 22); sub.Location = new Point(20, 60);
            page.Controls.Add(sub);

            string[][] steps = new string[][]
            {
                new string[] { "1", Lang.T("Add your accounts"), "Sign in with a browser, or paste cookies. They're encrypted and stay on this PC." },
                new string[] { "2", Lang.T("Pick a game"), "Paste a game link, a place ID or a private server on the Launch page." },
                new string[] { "3", Lang.T("Press Launch"), "Launch as many accounts as you want, then line the windows up on the Layout page." }
            };
            int cardW = 206, cardH = 158, gap = 12, x = 20, y = 96;
            foreach (string[] s in steps)
            {
                RoundPanel card = new RoundPanel();
                card.Tag = "card2";
                card.BackColor = Theme.Panel2;
                card.Size = new Size(cardW, cardH);
                card.Location = new Point(x, y);
                Label n = Ui.Lbl(s[0], "section"); n.Font = Theme.Big; n.Location = new Point(16, 12); card.Controls.Add(n);
                Label h = Ui.Lbl(s[1], ""); h.Font = Theme.CardName; h.Location = new Point(16, 56); card.Controls.Add(h);
                Label d = Ui.Lbl(s[2], "muted"); d.AutoSize = false; d.Size = new Size(cardW - 32, 72); d.Location = new Point(16, 84); card.Controls.Add(d);
                page.Controls.Add(card);
                x += cardW + gap;
            }

            RoundButton add = Ui.Btn("+  Add account", 170, null, "primary");
            add.Height = 40; add.Location = new Point(20, y + cardH + 24);
            add.Click += delegate { if (addMenu != null) addMenu.Show(add, new Point(0, add.Height)); };
            page.Controls.Add(add);

            RoundButton goLaunch = Ui.Btn(Lang.T("Go to Launch"), 140, delegate { ShowPage("Launch"); }, "");
            goLaunch.Height = 40; goLaunch.Location = new Point(206, y + cardH + 24);
            page.Controls.Add(goLaunch);
        }

        static readonly string[] PageOrder = new string[] { "Home", "Launch", "Servers", "Layout", "Monitor", "Presets", "Perf", "Beta", "Settings", "Help" };

        void ShowPage(string key)
        {
            // the page that is really visible right now (a page still sliding in is not docked)
            Panel fromP = null;
            foreach (KeyValuePair<string, Panel> kv in pages)
            {
                if (kv.Value.Visible && kv.Value.Dock == DockStyle.Fill && kv.Key != key) fromP = kv.Value;
                else if (kv.Key != key) { kv.Value.Visible = false; kv.Value.Dock = DockStyle.None; }
            }
            currentPage = key;
            Panel toP;
            if (!pages.TryGetValue(key, out toP)) { StyleNav(); return; }
            StyleNav();
            if (key == "Layout") RefreshWindowList();
            if (key == "Monitor") RefreshMonitor();
            if (key == "Presets") RefreshPresetList();

            if (fromP == null || toP == fromP) { toP.Visible = true; toP.Dock = DockStyle.Fill; toP.BringToFront(); return; }

            // slide the new page in over the old one, from the side the button was on
            int fromIdx = Array.IndexOf(PageOrder, pages.FirstOrDefault(x => x.Value == fromP).Key);
            int toIdx = Array.IndexOf(PageOrder, key);
            int dir = toIdx > fromIdx ? 1 : -1;
            Padding pad = pageHost.Padding;
            Size sz = new Size(pageHost.ClientSize.Width - pad.Horizontal, pageHost.ClientSize.Height - pad.Vertical);
            Point home = new Point(pad.Left, pad.Top);
            fromP.Dock = DockStyle.None;
            fromP.Location = home;
            fromP.Size = sz;
            toP.Size = sz;
            toP.Visible = true;
            toP.BringToFront();
            int travel = 46;
            Util.SetBuf(toP);
            Anim.Cancel("page");
            Anim.Go("page", Anim.Ms, delegate(float e)
            {
                toP.Location = new Point(home.X + (int)((1f - e) * travel * dir), home.Y);
            }, delegate
            {
                fromP.Visible = false;
                toP.Dock = DockStyle.Fill;
            });
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

        // The active highlight is painted by the buttons themselves, exactly like it always was.
        // On a switch the new tab's highlight evolves out of the click point on the same clock as
        // the ripple, and the old tab's dissolves.
        void StyleNav()
        {
            foreach (KeyValuePair<string, RoundButton> kv in navButtons)
            {
                bool act = kv.Key == currentPage;
                RoundButton b = kv.Value;
                if (b.Active != act) b.Evolve(act ? 1f : 0f);
                else if (act && b.Wipe != 1f && !b.Evolving) { b.Wipe = 1f; }
                b.Active = act;
                b.Invalidate();
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
                    WebhookStatus.Tick();
                    Launcher.SweepZombies();
                    Launcher.ApplyLowMemoryPriority();
                    Launcher.ApplyTaskbarHiding();
                    CapProber.Tick(UsageMonitor.Count, AddLog);
                    UpdateStatusCells();
                    UpdateMemUi();
                    MaxAlts.Tick();
                    RamCap.Tick();
                    UpdateBetaLive();
                    UpdatePerfCap();
                    DiscordRpc.Update(UsageMonitor.Count, Store.Accounts.Count);
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
                lblMulti.Text = Lang.T("Multi-instance: OFF  (only ONE Roblox window)\r\nClick here to turn it on in Settings.");
                lblMulti.ForeColor = Theme.Warn;
            }
            else if (st == "Active")
            {
                lblMulti.Text = Lang.T("Multi-instance: ACTIVE\r\nLaunch Roblox only from this app.");
                lblMulti.ForeColor = Theme.Good;
            }
            else if (st == "Waiting")
            {
                lblMulti.Text = "Multi-instance: WAITING\r\nClose all Roblox, or restart your PC, then reopen.";
                lblMulti.ForeColor = Theme.Warn;
            }
            else
            {
                lblMulti.Text = "Multi-instance: " + st + "\r\nTry restarting your PC, then reopen this app.";
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
            statRun.Sub.Text = count == 0 ? Lang.T("nothing running") : count + " Roblox window(s)";
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
                    ? Lang.T("Right now: no Roblox window is running.")
                    : "Right now: " + count + " Roblox window(s) use " + CpuText(UsageMonitor.TotalCpu) + " CPU and " + Util.FormatBytes(UsageMonitor.TotalRam)
                      + " memory in total  (avg " + CpuText(UsageMonitor.TotalCpu / count) + " and " + Util.FormatBytes(UsageMonitor.TotalRam / count) + " per account).";
            UpdateFooterButtons();
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
            ScaleLayout();
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
            if (Store.Settings.CheckUpdates) CheckUpdatesOnStartup();
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
                if (Launcher.FindRobloxExe() == null && !Launcher.ProtocolRegistered())
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
                DiscordRpc.Stop();
                if (Store.Settings.MaxAlts) MaxAlts.Reset();
                RamCap.Reset();

                // full reset: close EVERY Roblox process and confirm they are gone before we release
                // anything, so the machine is exactly as before the app ran - and a restart is clean
                int n = Launcher.KillAllRoblox();
                if (n > 0)
                {
                    Log.Write("Close: closing " + n + " Roblox process(es)...");
                    for (int i = 0; i < 40 && Launcher.RobloxPids().Count > 0; i++) System.Threading.Thread.Sleep(250);
                    foreach (int pid in Launcher.RobloxPids()) Launcher.KillPid(pid);   // stubborn ones
                    for (int i = 0; i < 8 && Launcher.RobloxPids().Count > 0; i++) System.Threading.Thread.Sleep(250);
                    Log.Write(Launcher.RobloxPids().Count == 0
                        ? "Close: all Roblox processes confirmed gone."
                        : "Close: " + Launcher.RobloxPids().Count + " Roblox process(es) would not close - close them yourself.");
                }

                // release the multi-instance locks ONLY after the clients are dead, so the next
                // start finds a clean slate (this also releases the memory helper's parent link)
                MultiInstance.Stop();
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
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // toolbar: grows to a 2nd row when the panel is narrow
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // search / group / view: grows instead of clipping
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // footer buttons: grow instead of clipping
            t.Margin = Padding.Empty;
            t.Tag = "card";
            leftHost.Controls.Add(t);

            BuildMenus();

            // ---- toolbar ----
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.AutoSize = true;
            bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bar.WrapContents = true;                  // buttons wrap to a second row instead of being cut off
            bar.MinimumSize = new Size(0, 44);
            bar.Margin = Padding.Empty;
            bar.Tag = "card";
            RoundButton bAdd = Ui.Btn("+ Add", 62, null, "primary");
            bAdd.Click += delegate { addMenu.Show(bAdd, new Point(0, bAdd.Height)); };
            Ui.Tips.SetToolTip(bAdd, "Add accounts: log in with a browser, paste cookies, or import a file.");
            RoundButton bEdit = Ui.Btn("Edit", 48, delegate { EditSelected(); }, "");
            Ui.Tips.SetToolTip(bEdit, "Edit alias, group, notes, password and the game of each account.");
            RoundButton bRemove = Ui.Btn("Remove", 66, delegate { RemoveSelected(); }, "danger");
            Ui.Tips.SetToolTip(bRemove, "Delete the selected account(s) from this app (does not touch the Roblox account itself).");
            RoundButton bCheck = Ui.Btn(Lang.T("Check"), 54, delegate { CheckAccounts(); }, "");
            Ui.Tips.SetToolTip(bCheck, "Check cookies: tests whether the saved login of the selected accounts (or all, if none selected) still works.");
            RoundButton bActions = Ui.Btn("More", 52, null, "");
            bActions.Click += delegate { accMenu.Show(bActions, new Point(0, bActions.Height)); };
            Ui.Tips.SetToolTip(bActions, "Copy username/password/cookie, account details, refresh cookie, groups, sorting ... (also on right-click).");
            bar.WrapContents = false;                    // the five buttons stay on one row
            foreach (RoundButton b in new RoundButton[] { bAdd, bEdit, bRemove, bCheck, bActions })
            {
                b.Margin = new Padding(0, 7, 4, 7);     // centered in the 44px band, like the row below
                bar.Controls.Add(b);
            }
            bar.Padding = new Padding(12, 0, 12, 0);     // line up with the account rows below
            t.Controls.Add(bar, 0, 0);

            // ---- search / group / view switch ----
            FlowLayoutPanel filt = new FlowLayoutPanel();
            filt.Dock = DockStyle.Fill;
            filt.AutoSize = true;
            filt.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            filt.WrapContents = true;
            filt.MinimumSize = new Size(0, 44);            // same band height as the toolbar above
            filt.Margin = new Padding(0, 6, 0, 0);       // breathing room below the toolbar band
            filt.Tag = "card";
            // every control centered in the 44px band: 28px inputs -> top 8, 30px buttons -> top 7,
            // 13px labels -> top 15 so their text baseline sits on the input's middle
            Label l1 = Ui.Lbl("Search", "muted");
            l1.Margin = new Padding(0, 15, 4, 0);
            tbSearch = Ui.Txt(150, "");
            tbSearch.Margin = new Padding(0, 8, 12, 0);
            tbSearch.TextChanged += delegate { RefreshAccountList(); NudgeList(); };
            tbSearch.Tip("Type to filter by username, alias, notes or group.");
            cbGroupFilter = new DarkCombo();
            cbGroupFilter.Width = 140;
            cbGroupFilter.Margin = new Padding(0, 8, 14, 0);   // no label: the dropdown says it all
            cbGroupFilter.SelectedIndexChanged += delegate { if (!suspendSel) { RefreshAccountList(); NudgeList(); } };
            btnViewList = Ui.Btn(Lang.T("List"), 52, delegate { SetView(false); }, "toggle");
            btnViewList.Margin = new Padding(0, 7, 2, 0);
            Ui.Tips.SetToolTip(btnViewList, Lang.T("Compact list with profile pictures."));
            btnViewCards = Ui.Btn(Lang.T("Cards"), 60, delegate { SetView(true); }, "toggle");
            btnViewCards.Margin = new Padding(0, 7, 0, 0);
            filt.Padding = new Padding(12, 0, 12, 0);        // same left edge as the toolbar and rows
            Ui.Tips.SetToolTip(btnViewCards, "Expanded cards: big profile picture, status, a game for each account and a Launch button.");
            filt.Controls.Add(l1);
            filt.Controls.Add(tbSearch);
            filt.Controls.Add(cbGroupFilter);
            filt.Controls.Add(btnViewList);
            filt.Controls.Add(btnViewCards);
            t.Controls.Add(filt, 0, 1);

            // ---- the two views share one cell ----
            Panel viewHost = new Panel();
            viewHost.Dock = DockStyle.Fill;
            viewHost.Margin = new Padding(0, 6, 0, 0);   // breathing room below the filter band
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
            Util.SetBuf(cardHost);                 // cards glide to new slots; buffer so they do not tear
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
            foot.AutoSize = true;
            foot.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            foot.WrapContents = true;
            foot.MinimumSize = new Size(0, 46);
            foot.Margin = Padding.Empty;
            foot.Tag = "card";
            GradientButton bLaunch = new GradientButton();
            bLaunch.Text = "\U0001F680  " + Lang.T("Launch selected");
            bLaunch.Size = new Size(158, 34);
            bLaunch.Margin = new Padding(12, 6, 8, 6);      // centered in the 46px footer band
            bLaunch.Click += delegate { LaunchSelected(); };
            Ui.Tips.SetToolTip(bLaunch, "Launch the selected accounts. Accounts with their own game use it; the others use the Launch page.");
            RoundButton bAll = Ui.Btn(Lang.T("Launch all"), 90, delegate { LaunchAllListed(); }, "");
            Ui.Tips.SetToolTip(bAll, "Launch every account that is currently visible in the list.");
            RoundButton bStop = Ui.Btn(Lang.T("Stop all"), 80, delegate { StopAll(); }, "danger");
            Ui.Tips.SetToolTip(bStop, "Close every Roblox window that this app started (windows you opened yourself are left alone).");
            RoundButton bUp = Ui.Btn("\u2191", 44, delegate { MoveSelected(-1); }, "");
            RoundButton bDown = Ui.Btn("\u2193", 44, delegate { MoveSelected(1); }, "");
            Ui.Tips.SetToolTip(bUp, "Move the selected account(s) up in your own order.");
            Ui.Tips.SetToolTip(bDown, "Move the selected account(s) down in your own order.");
            lblCount = Ui.Lbl("", "muted");              // kept for the counters; not shown (the Launch page lists the selection)
            foreach (RoundButton b in new RoundButton[] { bAll, bStop, bUp, bDown }) b.Margin = new Padding(0, 8, 6, 8);
            foot.Controls.Add(bLaunch);
            foot.Controls.Add(bAll);
            foot.Controls.Add(bStop);
            foot.Controls.Add(bUp);
            foot.Controls.Add(bDown);
            t.Controls.Add(foot, 0, 3);
            footLaunch = bLaunch;
            footAll = bAll;
            footStop = bStop;
            footUp = bUp;
            footDown = bDown;
            bAll.Tag = bAll.Width;                 // real width for the show/hide animation
            bStop.Tag = bStop.Width;
            bUp.Tag = bUp.Width;
            bDown.Tag = bDown.Width;
            UpdateFooterButtons();
        }

        // Footer of the accounts panel, every button only present when it can be used:
        // Stop while something is running (Lang.T("Stop") / "Stop all"), 'Launch all' while at least one
        // listed account is not running, and the order arrows while an account is selected.
        void UpdateFooterButtons()
        {
            if (footStop == null || footAll == null) return;
            int running = runningIds.Count;
            string want = running == 1 ? Lang.T("Stop") : Lang.T("Stop all");
            if (footStop.Text != want) footStop.Text = want;
            SetFooterBtn(footStop, running > 0);
            bool anyToLaunch = false;
            foreach (Account a in FilteredAccounts())
                if (!runningIds.Contains(a.Id)) { anyToLaunch = true; break; }
            SetFooterBtn(footAll, anyToLaunch);
            bool hasSel = selected.Count > 0;
            SetFooterBtn(footUp, hasSel);
            SetFooterBtn(footDown, hasSel);
        }

        // Tag holds the button's real width, set when the footer is built. Always animates FROM the
        // current size/opacity, so a button that is re-shown mid-collapse grows back from where it is.
        void SetFooterBtn(RoundButton b, bool show)
        {
            int target = b.Tag is int ? (int)b.Tag : 80;
            if (show)
            {
                if (b.Visible && b.Width == target && b.Opacity > 0.99f) return;
                int w0 = b.Visible ? b.Width : 1;
                float o0 = b.Visible ? b.Opacity : 0f;
                if (!b.Visible) { b.Visible = true; b.Width = 1; }
                Anim.Go(b, 170, delegate(float e)
                {
                    b.Width = Math.Max(1, w0 + (int)((target - w0) * e));
                    b.Opacity = Math.Min(1f, o0 + (1f - o0) * Math.Min(1f, e * 2f));
                    b.TextOpacity = b.Opacity;
                }, delegate { b.Width = target; b.Opacity = 1f; b.TextOpacity = 1f; });
            }
            else
            {
                if (!b.Visible) return;
                int w0 = b.Width;
                Anim.Go(b, 150, delegate(float e)
                {
                    b.Width = Math.Max(1, w0 - (int)(w0 * e));
                    b.Opacity = Math.Max(0f, 1f - e * 2f);
                    b.TextOpacity = b.Opacity;
                }, delegate { b.Visible = false; b.Width = target; b.Opacity = 1f; b.TextOpacity = 1f; });
            }
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
            addMenu.Items.Add(Ui.Item(Lang.T("Log in with a browser (Edge / Chrome)..."), delegate { AddByBrowser(); }));
            addMenu.Items.Add(Ui.Item(Lang.T("Add several with username:password..."), delegate { AddSeveralByBrowser(); }));
            addMenu.Items.Add(Ui.Item(Lang.T("Paste cookie(s)..."), delegate { AddByPaste(); }));
            addMenu.Items.Add(Ui.Item(Lang.T("Import cookies from a file..."), delegate { AddByFile(); }));
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
            copy.DropDownItems.Add(Ui.Item(Lang.T("User ID"), delegate { CopyField(delegate(Account a) { return a.UserId.ToString(); }); }));
            copy.DropDownItems.Add(Ui.Item("Cookie (.ROBLOSECURITY) - keep secret!", delegate { CopyField(delegate(Account a) { return a.Cookie; }); }));
            accMenu.Items.Add(copy);
            accMenu.Items.Add(Ui.Item(Lang.T("Show account details (age, Robux, Premium)"), delegate { ShowDetails(); }));
            accMenu.Items.Add(Ui.Item(Lang.T("Copy a fresh login ticket"), delegate { CopyTicket(); }));
            accMenu.Items.Add(Ui.Item(Lang.T("Open profile in browser"), delegate { OpenProfile(); }));
            accMenu.Items.Add(Ui.Item("Refresh cookie (rotates it, signs out other sessions)", delegate { RotateSelected(); }));
            accMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem sort = Ui.Item(Lang.T("Sort accounts by"), null);
            sort.DropDownItems.Add(Ui.Item(Lang.T("My own order (Up / Down buttons)"), delegate { SetSort(-1); }));
            sort.DropDownItems.Add(Ui.Item(Lang.T("Name  (click again to reverse)"), delegate { SetSort(1); }));
            sort.DropDownItems.Add(Ui.Item("Group", delegate { SetSort(2); }));
            sort.DropDownItems.Add(Ui.Item("Status", delegate { SetSort(3); }));
            sort.DropDownItems.Add(Ui.Item(Lang.T("CPU usage"), delegate { SetSort(4); }));
            sort.DropDownItems.Add(Ui.Item(Lang.T("Last used"), delegate { SetSort(5); }));
            accMenu.Items.Add(sort);
            miGroup = Ui.Item(Lang.T("Move to group"), null);
            accMenu.Items.Add(miGroup);
            accMenu.Items.Add(Ui.Item("Edit...", delegate { EditSelected(); }));
            accMenu.Items.Add(Ui.Item("Remove", delegate { RemoveSelected(); }));
            accMenu.Opening += delegate { RebuildGroupMenu(); };
            StyleMenus();
        }

        void RebuildGroupMenu()
        {
            miGroup.DropDownItems.Clear();
            miGroup.DropDownItems.Add(Ui.Item(Lang.T("(no group)"), delegate { SetGroup(""); }));
            foreach (string g in Store.AllGroups())
            {
                string name = g;
                miGroup.DropDownItems.Add(Ui.Item(name, delegate { SetGroup(name); }));
            }
            miGroup.DropDownItems.Add(new ToolStripSeparator());
            miGroup.DropDownItems.Add(Ui.Item(Lang.T("New group..."), delegate
            {
                string n = Dialogs.Prompt(this, Lang.T("New group"), Lang.T("Name of the new group:"), "");
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
            // rows end 10px before the scrollbar - same gap as card-edge to row on the left
            Rectangle row = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 3, e.Bounds.Width - 5 - 10, e.Bounds.Height - 6);
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
            NudgeList();
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
            // one click raises this twice (row deselected, row selected); evaluate the footer once
            // the burst is over, so the arrows never collapse for the empty in-between state
            BeginInvoke((Action)delegate { UpdateFooterButtons(); });
            OnAccountSelectionChanged();
        }

        // =====================================================================
        //  Card view
        // =====================================================================
        void SetView(bool cardsOn)
        {
            Control from = cardsView ? (cardHost.Visible ? (Control)cardHost : null) : (lv.Visible ? (Control)lv : null);
            cardsView = cardsOn;
            Store.Settings.View = cardsOn ? "Cards" : "List";
            Store.SaveSettings();
            Control to = cardsOn ? (Control)cardHost : lv;
            if (cardsOn) RebuildCards();
            ApplySelectionToViews();
            StyleViewButtons();

            // the views cross-fade by sliding the incoming one over the outgoing, same as pages -
            // an instant swap here was one of the remaining snaps
            if (from == null || to == from || !Store.Settings.Animations)
            {
                lv.Visible = !cardsOn;
                cardHost.Visible = cardsOn;
                return;
            }
            Size sz = from.Size;
            Point home = from.Location;
            to.Size = sz;
            to.Location = home;
            to.Visible = true;
            to.BringToFront();
            Anim.Go("setview", Anim.Ms, delegate(float e)
            {
                to.Location = new Point(home.X, home.Y + (int)((1f - e) * 26));       // slides up into place
            }, delegate
            {
                from.Visible = false;
                to.Dock = DockStyle.Fill;
            });
        }

        void RebuildCards()
        {
            if (cardHost == null) return;
            cardHost.SuspendLayout();
            Dictionary<string, Point> oldPos = new Dictionary<string, Point>();
            foreach (AccountCard c in cardHost.Controls.Cast<Control>().OfType<AccountCard>().ToList())
                oldPos[c.Account.Id] = c.Location;
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
            // cards that already existed glide to their new slot instead of teleporting (sorting, filtering)
            foreach (KeyValuePair<string, AccountCard> kv in cards)
            {
                Point from;
                if (!oldPos.TryGetValue(kv.Key, out from) || from == kv.Value.Location) continue;
                AccountCard c = kv.Value;
                Point to = c.Location;
                Point d = new Point(to.X - from.X, to.Y - from.Y);
                c.Location = from;
                Anim.Go("card" + kv.Key, 260, delegate(float e)
                {
                    c.Location = new Point(from.X + (int)(d.X * e), from.Y + (int)(d.Y * e));
                }, delegate { c.Location = to; });
            }
        }

        // the list briefly settles down into place after its order changed (sort, filter, view switch)
        void NudgeList()
        {
            if (lv == null || !lv.Visible) return;
            Anim.Go("lvnudge", 170, delegate(float e) { lv.Padding = new Padding(0, (int)((1f - e) * 12), 0, 0); }, null);
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
            if (li.Locked) { text = Lang.T("Account locked"); color = Theme.Bad; return; }
            if (li.CookieBad) { text = Lang.T("Cookie expired"); color = Theme.Bad; return; }
            if (runningIds.Contains(a.Id)) { text = Lang.T("Running"); color = Theme.Good; return; }
            if (li.PresenceType == 2) { text = Lang.T("In game"); color = Theme.Good; }
            else if (li.PresenceType == 3) { text = "Studio"; color = Theme.Purple; }
            else if (li.PresenceType == 1) { text = Lang.T("Online"); color = Theme.Info; }
            else if (li.PresenceType == 0) { text = Lang.T("Offline"); color = Theme.Muted; }
            else { text = Lang.T("Not checked"); color = Theme.Border; }
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
            string grp = (cbGroupFilter == null || cbGroupFilter.SelectedItem == null) ? Lang.T("All groups") : cbGroupFilter.SelectedItem.ToString();
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
                d.Title = Lang.T("Choose a text file that contains cookies");
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
            if (sel.Count == 0) { AddLog(Lang.T("Select an account first.")); return; }
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
            if (selected.Count == 0) return;
            sortCol = -1;
            List<Account> vis = FilteredAccounts();
            if (vis.Count < 2) return;
            // every selected account shifts one slot; selected accounts that touch each other move
            // as one block (they only swap with an unselected neighbour), keeping their relative order
            if (dir < 0)
            {
                for (int i = 1; i < vis.Count; i++)
                    if (selected.Contains(vis[i].Id) && !selected.Contains(vis[i - 1].Id))
                    {
                        Account t = vis[i]; vis[i] = vis[i - 1]; vis[i - 1] = t;
                    }
            }
            else
            {
                for (int i = vis.Count - 2; i >= 0; i--)
                    if (selected.Contains(vis[i].Id) && !selected.Contains(vis[i + 1].Id))
                    {
                        Account t = vis[i]; vis[i] = vis[i + 1]; vis[i + 1] = t;
                    }
            }
            for (int i = 0; i < vis.Count; i++) vis[i].Order = (i + 1) * 10;
            Store.SaveAccounts();
            RefreshAccountList();
            NudgeList();
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
                    else li.Where = Lang.T("Offline");
                }
            }
            catch (Exception ex) { Log.Error("presence", ex); }
            presenceBusy = false;
            UpdateStatusCells();
        }
    }
}
