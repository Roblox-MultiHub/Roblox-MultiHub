using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public class WinRow
    {
        public WinInfo Info;
        public bool Include = true;
        public Rectangle? Pin;
    }

    public partial class MainForm
    {
        ComboBox cbMonitor;
        CheckBox chkWork;
        RadioButton rbAuto, rbGrid, rbFixed;
        NumericUpDown numRows, numCols, numGap, numMargin, numScale, numFixW, numFixH, numAutoDelay;
        CheckBox chkStretch, chkBorderless, chkTop, chkTitles, chkAuto, chkHotkeys;
        ComboBox cbSortMode;
        static readonly string[] SortModeKeys = new string[] { "Manual", "Name", "Pid" };
        Panel previewPanel;
        ListView lvWins;
        Label lblWinInfo;
        Label lblMinInfo;

        List<MonitorDesc> monitors = new List<MonitorDesc>();
        List<WinRow> winRows = new List<WinRow>();
        HashSet<IntPtr> lastSet = new HashSet<IntPtr>();
        DateTime changeSince = DateTime.MinValue;
        bool autoArmed;
        string lastWinSig = "";
        bool loadingLayout;
        bool hotkeysRegistered;

        const int HK_ARRANGE = 9001;
        const int HK_PREV = 9002;
        const int HK_NEXT = 9003;

        void BuildLayoutPage()
        {
            LayoutSettings L = Store.Settings.Layout;
            loadingLayout = true;
            Panel page = NewPage("Layout");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Windows && Layout", "Spread your Roblox windows over the screen.");

            // ---- preview ----
            p.Section("Preview");
            previewPanel = new BufferedPanel();
            previewPanel.Tag = "preview";
            previewPanel.Size = new Size(PageW - PageBuilder.PadX * 2, 200);
            previewPanel.Paint += PaintPreview;
            p.Add(previewPanel);
            lblMinInfo = Ui.Lbl("", "muted");
            lblMinInfo.AutoSize = false;
            lblMinInfo.Size = new Size(380, 34);
            p.RowTip("Roblox normally refuses to go below about 816 x 638. The app resizes windows without letting Roblox answer with that minimum (the same trick as a standalone resize tool), so windows can be any size.\n\nIf a window still refuses, the app removes its border. Only if even that fails does it measure the smallest size and, when your screen is too small for that many windows, stack them with overlap so each window's corner stays visible.",
                lblMinInfo, Ui.Btn("Re-measure", 100, delegate { Remeasure(); }, ""));

            // ---- screen ----
            p.Section("Screen");
            monitors = WindowLayout.GetMonitors();
            cbMonitor = new DarkCombo();
            cbMonitor.Width = 360;
            foreach (MonitorDesc m in monitors) cbMonitor.Items.Add(m.Label);
            cbMonitor.SelectedIndex = Math.Max(0, monitors.IndexOf(WindowLayout.PickMonitor(monitors, L.Monitor)));
            p.Opt("Which screen the windows go on.",
                "All windows are placed on this monitor. Pick another one if you have several.",
                Ui.Fld("Monitor", 80), cbMonitor);
            chkWork = Ui.Chk("Keep windows clear of the taskbar", L.UseWorkArea, null);
            p.Opt("Stays out of the taskbar area.",
                "On = use the usable part of the screen. Off = use the entire screen, even behind the taskbar.",
                chkWork);

            // ---- mode ----
            p.Section("Layout mode");
            rbAuto = Ui.Rad("Automatic grid  (default)", L.Mode != "Grid" && L.Mode != "FixedSize", null);
            p.Opt("Automatic rows and columns.",
                "Picks rows and columns from the number of windows and your screen shape: 2 windows = side by side, 4 = 2x2, 6 = 3x2, 9 = 3x3. Every window gets the same share of the screen.\n\nIf Roblox's minimum window size does not allow that, the windows overlap neatly instead (the preview shows it).",
                rbAuto);
            rbGrid = Ui.Rad("Custom grid", L.Mode == "Grid", null);
            p.Opt("You choose the rows and columns.",
                "Forces a specific grid. Use 0 for 'automatic' in either box.",
                rbGrid);
            numRows = Ui.Num(0, 20, L.Rows, 60);
            numCols = Ui.Num(0, 20, L.Cols, 60);
            p.Row(Ui.Fld("Rows", 50), numRows, Ui.Fld("Columns", 70), numCols, Ui.Lbl("0 = automatic", "muted"));
            rbFixed = Ui.Rad("Fixed window size", L.Mode == "FixedSize", null);
            p.Opt("Fixed size for every window.",
                "Windows are placed left to right and wrap to the next row; when the screen is full they cascade. Roblox's minimum window size still applies.",
                rbFixed);
            numFixW = Ui.Num(200, 8000, L.FixedW, 80);
            numFixH = Ui.Num(150, 8000, L.FixedH, 80);
            p.Row(Ui.Fld("Width", 50), numFixW, Ui.Fld("Height", 70), numFixH, Ui.Lbl("pixels", "muted"));

            // ---- fine tuning ----
            p.Section("Fine tuning");
            numScale = Ui.Num(20, 100, L.Scale, 70);
            p.Opt("Shrinks all windows together.",
                "A quick way to make all windows smaller or bigger together: lower numbers shrink every window and keep it centred in its cell. It never goes below Roblox's minimum window size.",
                Ui.Fld("Window scale (%)", 190), numScale);
            numGap = Ui.Num(0, 200, L.Gap, 70);
            p.Opt("Space between the windows.", "Empty space between neighbouring windows, in pixels.", Ui.Fld("Gap (pixels)", 190), numGap);
            numMargin = Ui.Num(0, 400, L.Margin, 70);
            p.Opt("Space around the whole layout.", "Empty space between the layout and the edge of the screen, in pixels.", Ui.Fld("Screen margin (pixels)", 190), numMargin);
            chkStretch = Ui.Chk("Stretch the last row to fill empty space", L.StretchLast, null);
            p.Opt("No hole in the last row.",
                "Example: with 3 windows, 2 are on top and the third is as wide as the screen instead of leaving a hole.",
                chkStretch);
            chkBorderless = Ui.Chk("Remove window borders / title bar", L.Borderless, null);
            p.Opt("Optional: no border or title bar.",
                "Windows are made small with the same trick as a standalone resize tool: they are resized without letting Roblox answer with its minimum size, so a border is NOT needed.\n\nIf a window still refuses to shrink, the app removes its border automatically (a borderless window is never asked for a minimum size). 'Restore borders' at the bottom of this page brings the title bars back.",
                chkBorderless);
            chkTop = Ui.Chk("Keep windows always on top", L.AlwaysOnTop, null);
            p.Opt("Roblox windows stay above other programs.", "Handy when you work next to the windows. Turn it off to let other programs cover them.", chkTop);
            chkTitles = Ui.Chk("Rename windows to the account name", L.LabelTitles, null);
            p.Opt("Rename windows to the account name.",
                "Changes the title of windows started by this app to 'Roblox - AccountName', so you can tell them apart in the taskbar. OFF by default.",
                chkTitles);

            // ---- automation ----
            p.Section("Automation");
            chkAuto = Ui.Chk("Auto-capture new Roblox windows", L.AutoCapture, null);
            p.Opt("Re-arranges when windows open or close.",
                "When ON, the layout is applied automatically a moment after a Roblox window opens or closes. It never moves windows continuously, so you can still drag one by hand in between.",
                chkAuto);
            numAutoDelay = Ui.Num(300, 20000, L.AutoCaptureDelayMs, 80);
            p.Opt("Wait before arranging new windows.",
                "How long to wait after a new window appears before the layout is applied.",
                Ui.Fld("Wait (milliseconds)", 190), numAutoDelay);
            chkHotkeys = Ui.Chk("Enable global hotkeys", L.Hotkeys, null);
            p.Opt("Ctrl+Alt+F9 arrange, F10 / F11 focus.",
                "Work from anywhere, even while a game has focus. Ctrl+Alt+F9 = arrange windows now, Ctrl+Alt+F10 = focus the previous Roblox window, Ctrl+Alt+F11 = focus the next one.",
                chkHotkeys);

            // ---- window list ----
            p.Section("Your Roblox windows");
            lblWinInfo = Ui.Lbl("", "muted");
            p.Row(lblWinInfo);
            lvWins = new BufferedListView();
            lvWins.Size = new Size(PageW - PageBuilder.PadX * 2, 170);
            lvWins.Columns.Add("#", 30);
            lvWins.Columns.Add("Account / title", 170);
            lvWins.Columns.Add("PID", 60);
            lvWins.Columns.Add("Position and size", 150);
            lvWins.Columns.Add("In grid", 66);
            lvWins.MultiSelect = true;
            ListStyle.Attach(lvWins);
            lvWins.DoubleClick += delegate { ToggleInclude(); };
            p.Add(lvWins);
            cbSortMode = new DarkCombo();
            cbSortMode.Width = 150;
            cbSortMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cbSortMode.Items.AddRange(new object[] { "Sort: Manual", "Sort: Account name", "Sort: PID" });
            cbSortMode.SelectedIndex = Math.Max(0, Array.IndexOf(SortModeKeys, Store.Settings.Layout.SortMode));
            cbSortMode.SelectedIndexChanged += delegate
            {
                if (loadingLayout) return;
                Store.Settings.Layout.SortMode = SortModeKeys[Math.Max(0, cbSortMode.SelectedIndex)];
                Store.SaveSettings();
                RefreshWindowList();
                Anim.Go("winsnudge", 170, delegate(float e) { lvWins.Padding = new Padding(0, (int)((1f - e) * 10), 0, 0); }, null);
            };
            p.Opt("Moves every window into the layout.",
                "'Arrange now' applies the layout. 'Refresh list' reads the running windows again. 'Restore borders' brings back title bars removed by the borderless option.\n\n"
                + "Sort Mode fixes the order the grid is filled in: 'Manual' keeps the order of the list (move rows up/down yourself), 'Account name' sorts by account (account1, account3, account10 - digits count as numbers), 'PID' sorts by process id, lowest first. Windows without an account name go last.",
                Ui.Btn("Arrange now", 130, delegate { ArrangeNow(); }, "primary"),
                Ui.Btn("Refresh list", 110, delegate { RefreshWindowList(); }, ""),
                Ui.Btn("Restore borders", 130, delegate { WindowLayout.RestoreBorders(); AddLog("Window borders restored."); }, ""),
                cbSortMode);
            p.RowTip("The order of this list decides which window gets which grid cell (left to right, top to bottom). 'Include / exclude' (or a double-click) takes a window out of the grid. 'Pin' places one window at an exact position and size and remembers it for that account. A line without a title is a Roblox process that has not opened its window yet, or a background process.",
                Ui.Btn("Move up", 78, delegate { MoveRow(-1); }, ""),
                Ui.Btn("Move down", 90, delegate { MoveRow(1); }, ""),
                Ui.Btn("Include / exclude", 130, delegate { ToggleInclude(); }, ""),
                Ui.Btn("Pin size/pos...", 120, delegate { PinSelected(); }, ""),
                Ui.Btn("Unpin", 64, delegate { UnpinSelected(); }, ""));
            p.RowTip("Focus brings a window to the front, Minimize minimizes it, 'Close selected' closes the selected Roblox processes and 'Close ALL Roblox' closes every Roblox process.",
                Ui.Btn("Focus", 70, delegate { FocusSelected(); }, ""),
                Ui.Btn("Minimize", 84, delegate { MinimizeSelected(); }, ""),
                Ui.Btn("Close selected", 120, delegate { CloseSelectedWindows(); }, "danger"),
                Ui.Btn("Close ALL Roblox", 130, delegate { CloseAllRoblox(); }, "danger"));

            EventHandler changed = delegate { OnLayoutChanged(); };
            cbMonitor.SelectedIndexChanged += changed;
            chkWork.CheckedChanged += changed;
            rbAuto.CheckedChanged += changed;
            rbGrid.CheckedChanged += changed;
            rbFixed.CheckedChanged += changed;
            numRows.ValueChanged += changed;
            numCols.ValueChanged += changed;
            numFixW.ValueChanged += changed;
            numFixH.ValueChanged += changed;
            numScale.ValueChanged += changed;
            numGap.ValueChanged += changed;
            numMargin.ValueChanged += changed;
            chkStretch.CheckedChanged += changed;
            chkBorderless.CheckedChanged += changed;
            chkTop.CheckedChanged += changed;
            chkTitles.CheckedChanged += changed;
            chkAuto.CheckedChanged += changed;
            numAutoDelay.ValueChanged += changed;
            chkHotkeys.CheckedChanged += changed;

            loadingLayout = false;
            UpdateLayoutEnabled();
            UpdateMinInfo();
        }

        // ---------------- settings <-> controls ----------------
        void OnLayoutChanged()
        {
            if (loadingLayout) return;
            LayoutSettings L = Store.Settings.Layout;
            L.Mode = rbGrid.Checked ? "Grid" : (rbFixed.Checked ? "FixedSize" : "Auto");
            L.Rows = (int)numRows.Value;
            L.Cols = (int)numCols.Value;
            L.FixedW = (int)numFixW.Value;
            L.FixedH = (int)numFixH.Value;
            L.Scale = (int)numScale.Value;
            L.Gap = (int)numGap.Value;
            L.Margin = (int)numMargin.Value;
            L.UseWorkArea = chkWork.Checked;
            L.StretchLast = chkStretch.Checked;
            L.Borderless = chkBorderless.Checked;
            L.AlwaysOnTop = chkTop.Checked;
            L.LabelTitles = chkTitles.Checked;
            L.AutoCapture = chkAuto.Checked;
            L.AutoCaptureDelayMs = (int)numAutoDelay.Value;
            L.Hotkeys = chkHotkeys.Checked;
            if (cbMonitor.SelectedIndex >= 0 && cbMonitor.SelectedIndex < monitors.Count) L.Monitor = monitors[cbMonitor.SelectedIndex].Device;
            Store.SaveSettings();
            UpdateLayoutEnabled();
            RegisterHotkeys();
            UpdateMinInfo();
            previewPanel.Invalidate();
        }

        // settings -> controls (used when a preset puts its layout back)
        void SyncLayoutControls()
        {
            LayoutSettings L = Store.Settings.Layout;
            loadingLayout = true;
            monitors = WindowLayout.GetMonitors();
            cbMonitor.Items.Clear();
            foreach (MonitorDesc m in monitors) cbMonitor.Items.Add(m.Label);
            cbMonitor.SelectedIndex = Math.Max(0, monitors.IndexOf(WindowLayout.PickMonitor(monitors, L.Monitor)));
            chkWork.Checked = L.UseWorkArea;
            rbGrid.Checked = L.Mode == "Grid";
            rbFixed.Checked = L.Mode == "FixedSize";
            rbAuto.Checked = L.Mode != "Grid" && L.Mode != "FixedSize";
            SetNum(numRows, L.Rows);
            SetNum(numCols, L.Cols);
            SetNum(numFixW, L.FixedW);
            SetNum(numFixH, L.FixedH);
            SetNum(numScale, L.Scale);
            SetNum(numGap, L.Gap);
            SetNum(numMargin, L.Margin);
            chkStretch.Checked = L.StretchLast;
            chkBorderless.Checked = L.Borderless;
            chkTop.Checked = L.AlwaysOnTop;
            chkTitles.Checked = L.LabelTitles;
            chkAuto.Checked = L.AutoCapture;
            SetNum(numAutoDelay, L.AutoCaptureDelayMs);
            chkHotkeys.Checked = L.Hotkeys;
            loadingLayout = false;
            UpdateLayoutEnabled();
            RegisterHotkeys();
            UpdateMinInfo();
            previewPanel.Invalidate();
        }

        void UpdateLayoutEnabled()
        {
            numRows.Enabled = rbGrid.Checked;
            numCols.Enabled = rbGrid.Checked;
            numFixW.Enabled = rbFixed.Checked;
            numFixH.Enabled = rbFixed.Checked;
            numScale.Enabled = !rbFixed.Checked;
            chkStretch.Enabled = !rbFixed.Checked;
            numAutoDelay.Enabled = chkAuto.Checked;
        }

        MonitorDesc CurrentMonitor()
        {
            monitors = WindowLayout.GetMonitors();
            return WindowLayout.PickMonitor(monitors, Store.Settings.Layout.Monitor);
        }

        // ---------------- preview ----------------
        void PaintPreview(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (monitors.Count == 0) return;
            MonitorDesc mon = WindowLayout.PickMonitor(monitors, Store.Settings.Layout.Monitor);
            LayoutSettings L = Store.Settings.Layout;
            Rectangle full = mon.Bounds;
            Rectangle area = mon.Bounds;      // the preview is a schematic: cells fill the whole screen,
                                               // never the taskbar-inset work area (that left a dead band)
            Rectangle host = Rectangle.Inflate(previewPanel.ClientRectangle, -14, -14);
            host.Height -= 12;
            double sc = Math.Min(host.Width / (double)full.Width, host.Height / (double)full.Height);
            int sw = (int)(full.Width * sc);
            int sh = (int)(full.Height * sc);
            int ox = host.Left + (host.Width - sw) / 2;
            int oy = host.Top + (host.Height - sh) / 2;
            using (System.Drawing.Drawing2D.GraphicsPath screen = Shapes.Round(new Rectangle(ox, oy, sw, sh), 8))
            {
                using (SolidBrush b = new SolidBrush(Theme.Panel2)) g.FillPath(b, screen);
                using (Pen pen = new Pen(Theme.Border, 2f)) g.DrawPath(pen, screen);
            }

            List<WinRow> tile = winRows.Where(r => r.Info.HasWindow && r.Include && !r.Pin.HasValue).ToList();
            bool sample = tile.Count == 0 && winRows.All(r => !r.Info.HasWindow || !r.Pin.HasValue);
            int n = sample ? 4 : tile.Count;
            bool overlap;
            List<Rectangle> rects = WindowLayout.ComputeRects(n, area, L, L.MinW, L.MinH, out overlap);
            for (int i = 0; i < rects.Count; i++)
            {
                Rectangle r = rects[i];
                Rectangle q = new Rectangle(ox + (int)((r.X - full.X) * sc), oy + (int)((r.Y - full.Y) * sc), Math.Max(6, (int)(r.Width * sc)), Math.Max(6, (int)(r.Height * sc)));
                using (System.Drawing.Drawing2D.GraphicsPath rp = Shapes.Round(q, 5))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(sample ? 70 : (overlap ? 150 : 120), overlap ? Theme.Warn : Theme.Accent))) g.FillPath(b, rp);
                    using (Pen pen = new Pen(overlap ? Theme.Warn : Theme.Accent, 1.5f)) g.DrawPath(pen, rp);
                }
                string label = (i + 1).ToString();
                if (!sample && !string.IsNullOrEmpty(tile[i].Info.Account)) label += "  " + tile[i].Info.Account;
                TextRenderer.DrawText(g, label, Theme.Bold, new Rectangle(q.Left, q.Top, Math.Min(q.Width, 160), Math.Min(q.Height, 24)), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            string cap;
            Color capColor = Theme.Muted;
            if (sample) cap = "Example with 4 windows - launch accounts to see your real layout";
            else if (overlap)
            {
                cap = tile.Count + " windows overlap: Roblox cannot be smaller than " + L.MinW + " x " + L.MinH + " px";
                capColor = Theme.Warn;
            }
            else cap = tile.Count + " window(s) in the grid  -  " + mon.Label;
            TextRenderer.DrawText(g, cap, Theme.Small, new Point(14, previewPanel.Height - 20), capColor);
        }

        void UpdateMinInfo()
        {
            if (lblMinInfo == null) return;
            LayoutSettings L = Store.Settings.Layout;
            if (L.MinW <= 0 || L.MinH <= 0)
            {
                lblMinInfo.Text = "Windows can be resized freely: Roblox's minimum size is bypassed (measured when you arrange).";
                return;
            }
            if (monitors.Count == 0) return;
            MonitorDesc mon = WindowLayout.PickMonitor(monitors, L.Monitor);
            Rectangle area = L.UseWorkArea ? mon.Work : mon.Bounds;
            lblMinInfo.Text = "Smallest window Roblox accepts here: " + L.MinW + " x " + L.MinH + " px (measured). Room for "
                + WindowLayout.Capacity(area, L, L.MinW, L.MinH) + " window(s) on this monitor without overlap.";
        }

        void Remeasure()
        {
            Store.Settings.Layout.MinW = 0;
            Store.Settings.Layout.MinH = 0;
            Store.SaveSettings();
            UpdateMinInfo();
            previewPanel.Invalidate();
            AddLog("The smallest Roblox window size will be measured again at the next 'Arrange now'.");
        }

        // ---------------- window list ----------------
        static DateTime StartTimeOf(int pid)
        {
            try { using (Process pr = Process.GetProcessById(pid)) return pr.StartTime; }
            catch (Exception) { return DateTime.MaxValue; }
        }

        PinnedRect FindPinned(string account)
        {
            if (string.IsNullOrEmpty(account)) return null;
            string key = account.ToLowerInvariant();
            return Store.Settings.Pinned.FirstOrDefault(x => x.Key == key);
        }

        void RefreshWindowList()
        {
            if (lvWins == null) return;
            List<WinInfo> now = WindowLayout.FindRobloxWindows();
            List<WinRow> keep = new List<WinRow>();
            foreach (WinRow old in winRows)
            {
                WinInfo w = now.FirstOrDefault(x => x.Pid == old.Info.Pid);
                if (w != null) { old.Info = w; keep.Add(old); }
            }
            List<WinInfo> fresh = now.Where(x => !keep.Any(k => k.Info.Pid == x.Pid)).OrderBy(x => StartTimeOf(x.Pid)).ToList();
            foreach (WinInfo w in fresh)
            {
                WinRow nr = new WinRow();
                nr.Info = w;
                PinnedRect pr = FindPinned(w.Account);
                if (pr != null) nr.Pin = new Rectangle(pr.X, pr.Y, pr.W, pr.H);
                keep.Add(nr);
            }
            winRows = keep;
            ApplySortMode();

            StringBuilder sig = new StringBuilder();
            foreach (WinRow sr in winRows)
                sig.Append(sr.Info.Pid).Append('|').Append(sr.Info.Account).Append('|').Append(sr.Info.Title).Append('|')
                   .Append(sr.Info.HasWindow).Append(sr.Info.Minimized).Append(sr.Include).Append(sr.Pin.HasValue)
                   .Append(sr.Info.Rect.ToString()).Append(';');
            string sigText = sig.ToString();
            if (sigText == lastWinSig)
            {
                previewPanel.Invalidate();
                return;
            }
            lastWinSig = sigText;

            HashSet<int> selected = new HashSet<int>();
            foreach (ListViewItem it in lvWins.SelectedItems)
            {
                WinRow r = it.Tag as WinRow;
                if (r != null) selected.Add(r.Info.Pid);
            }
            lvWins.BeginUpdate();
            lvWins.Items.Clear();
            for (int i = 0; i < winRows.Count; i++)
            {
                WinRow r = winRows[i];
                string who = r.Info.Account;
                if (string.IsNullOrEmpty(who)) who = r.Info.HasWindow ? (string.IsNullOrEmpty(r.Info.Title) ? "Roblox" : r.Info.Title) : "(no window yet / background)";
                else if (r.Info.HasWindow && !string.IsNullOrEmpty(r.Info.Title) && !r.Info.Title.Equals("Roblox", StringComparison.OrdinalIgnoreCase) && !r.Info.Title.StartsWith("Roblox - ")) who += "  -  " + r.Info.Title;
                string pos = !r.Info.HasWindow ? "-" : (r.Info.Minimized ? "minimized" : r.Info.Rect.X + ", " + r.Info.Rect.Y + "   " + r.Info.Rect.Width + " x " + r.Info.Rect.Height);
                string grid = !r.Info.HasWindow ? "-" : (r.Pin.HasValue ? "pinned" : (r.Include ? "yes" : "no"));
                ListViewItem it = new ListViewItem(new string[] { (i + 1).ToString(), who, r.Info.Pid.ToString(), pos, grid });
                it.Tag = r;
                if (!r.Info.HasWindow || (!r.Include && !r.Pin.HasValue)) it.ForeColor = Theme.Muted;
                lvWins.Items.Add(it);
                if (selected.Contains(r.Info.Pid)) it.Selected = true;
            }
            lvWins.EndUpdate();
            int withWin = winRows.Count(x => x.Info.HasWindow);
            lblWinInfo.Text = winRows.Count == 0 ? "No Roblox is running right now." : withWin + " window(s) found, " + (winRows.Count - withWin) + " process(es) without a window.";
            previewPanel.Invalidate();
        }

        // Sort Mode ('Sort' next to 'Arrange now'): re-orders the window list so the grid always gets
        // the same cells, also after the app or a client restarted. Manual = leave the list alone.
        void ApplySortMode()
        {
            string sm = Store.Settings.Layout.SortMode;
            if (sm == "Name")
                winRows = winRows.OrderBy(r => string.IsNullOrEmpty(r.Info.Account) ? 1 : 0)
                                  .ThenBy(r => r.Info.Account ?? "", new NaturalCmp())
                                  .ToList();
            else if (sm == "Pid")
                winRows = winRows.OrderBy(r => r.Info.HasWindow ? 0 : 1)
                                  .ThenBy(r => r.Info.Pid)
                                  .ToList();
        }

        // "account3" before "account10": digit runs are compared as numbers, letters case-insensitively.
        class NaturalCmp : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                a = a ?? ""; b = b ?? "";
                int i = 0, j = 0;
                while (i < a.Length && j < b.Length)
                {
                    if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                    {
                        int si = i, sj = j;
                        while (i < a.Length && char.IsDigit(a[i])) i++;
                        while (j < b.Length && char.IsDigit(b[j])) j++;
                        string na = a.Substring(si, i - si).TrimStart('0'), nb = b.Substring(sj, j - sj).TrimStart('0');
                        int cmp = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                        if (cmp != 0) return cmp;
                    }
                    else
                    {
                        int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                        if (cmp != 0) return cmp;
                        i++; j++;
                    }
                }
                return (a.Length - i).CompareTo(b.Length - j);
            }
        }

        List<WinRow> SelectedRows()
        {
            List<WinRow> res = new List<WinRow>();
            foreach (ListViewItem it in lvWins.SelectedItems)
            {
                WinRow r = it.Tag as WinRow;
                if (r != null) res.Add(r);
            }
            return res;
        }

        void MoveRow(int dir)
        {
            List<WinRow> sel = SelectedRows();
            if (sel.Count != 1) { AddLog("Select exactly one window to move it."); return; }
            int idx = winRows.IndexOf(sel[0]);
            int to = idx + dir;
            if (idx < 0 || to < 0 || to >= winRows.Count) return;
            WinRow tmp = winRows[to];
            winRows[to] = winRows[idx];
            winRows[idx] = tmp;
            RefreshWindowList();
        }

        void ToggleInclude()
        {
            foreach (WinRow r in SelectedRows()) { r.Include = !r.Include; if (r.Include) r.Pin = null; }
            RefreshWindowList();
        }

        void PinSelected()
        {
            List<WinRow> sel = SelectedRows();
            if (sel.Count != 1) { AddLog("Select exactly one window to pin."); return; }
            WinRow r = sel[0];
            if (!r.Info.HasWindow) { AddLog("That process has no window yet."); return; }
            Rectangle start = r.Pin.HasValue ? r.Pin.Value : r.Info.Rect;
            Rectangle? res = Dialogs.PinRect(this, start);
            if (!res.HasValue) return;
            r.Pin = res.Value;
            if (!string.IsNullOrEmpty(r.Info.Account))
            {
                string key = r.Info.Account.ToLowerInvariant();
                Store.Settings.Pinned.RemoveAll(x => x.Key == key);
                PinnedRect pr = new PinnedRect();
                pr.Key = key;
                pr.X = res.Value.X; pr.Y = res.Value.Y; pr.W = res.Value.Width; pr.H = res.Value.Height;
                Store.Settings.Pinned.Add(pr);
                Store.SaveSettings();
            }
            AddLog("Window pinned. It is skipped by the grid and placed at the exact spot when you arrange.");
            RefreshWindowList();
        }

        void UnpinSelected()
        {
            foreach (WinRow r in SelectedRows())
            {
                r.Pin = null;
                if (!string.IsNullOrEmpty(r.Info.Account))
                {
                    string key = r.Info.Account.ToLowerInvariant();
                    Store.Settings.Pinned.RemoveAll(x => x.Key == key);
                }
            }
            Store.SaveSettings();
            RefreshWindowList();
        }

        void FocusSelected()
        {
            List<WinRow> sel = SelectedRows();
            if (sel.Count > 0 && sel[0].Info.HasWindow) WindowLayout.Focus(sel[0].Info.Hwnd);
        }

        void MinimizeSelected()
        {
            foreach (WinRow r in SelectedRows())
                if (r.Info.HasWindow) WindowLayout.Minimize(r.Info.Hwnd);
            RefreshWindowList();
        }

        void CloseSelectedWindows()
        {
            List<WinRow> sel = SelectedRows();
            if (sel.Count == 0) return;
            foreach (WinRow r in sel)
            {
                Instance inst = Launcher.FindInstance(r.Info.Pid);
                if (inst != null) { inst.UserClosed = true; Launcher.Instances.Remove(inst); }
                Launcher.KillPid(r.Info.Pid);
            }
            AddLog("Closed " + sel.Count + " Roblox process(es).");
            RefreshWindowList();
            UpdateStatusCells();
        }

        void CloseAllRoblox()
        {
            if (MessageBox.Show(this, "Close every running Roblox window and process?", "Close all Roblox", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            int n = Launcher.KillAllRoblox();
            AddLog("Closed " + n + " Roblox process(es).");
            RefreshWindowList();
            UpdateStatusCells();
        }

        // ---------------- arranging ----------------
        void ArrangeNow()
        {
            try
            {
                RefreshWindowList();
                List<WinRow> tile = winRows.Where(r => r.Info.HasWindow && r.Include && !r.Pin.HasValue).ToList();
                List<WinRow> pinned = winRows.Where(r => r.Info.HasWindow && r.Pin.HasValue).ToList();
                if (tile.Count == 0 && pinned.Count == 0) { AddLog("No Roblox windows found to arrange."); return; }
                LayoutSettings L = Store.Settings.Layout;
                MonitorDesc mon = CurrentMonitor();
                Rectangle area = L.UseWorkArea ? mon.Work : mon.Bounds;
                bool overlap;
                List<Rectangle> rects = WindowLayout.ComputeRects(tile.Count, area, L, L.MinW, L.MinH, out overlap);
                using (new DpiScope())
                {
                    // Windows are resized with SWP_NOSENDCHANGING, so Roblox never gets the chance to answer with its
                    // minimum size. If a window still ends up bigger than asked, escalate: first remove its border,
                    // then remember the real minimum and - only if needed - stack the windows with overlap.
                    for (int pass = 0; pass < 3; pass++)
                    {
                        int seenW = 0;
                        int seenH = 0;
                        for (int i = 0; i < tile.Count && i < rects.Count; i++)
                        {
                            Rectangle actual = WindowLayout.Place(tile[i].Info, rects[i], L, overlap);
                            if (actual.Width > rects[i].Width + 3) seenW = Math.Max(seenW, actual.Width);
                            if (actual.Height > rects[i].Height + 3) seenH = Math.Max(seenH, actual.Height);
                        }
                        if (seenW == 0 && seenH == 0) break;
                        if (!L.Borderless)
                        {
                            L.Borderless = true;
                            Store.SaveSettings();
                            loadingLayout = true;
                            chkBorderless.Checked = true;
                            loadingLayout = false;
                            AddLog("Some windows would not shrink with a border - borders removed (they can then be any size).");
                            continue;
                        }
                        if (seenW <= L.MinW && seenH <= L.MinH) break;
                        L.MinW = Math.Max(L.MinW, seenW);
                        L.MinH = Math.Max(L.MinH, seenH);
                        Store.SaveSettings();
                        rects = WindowLayout.ComputeRects(tile.Count, area, L, L.MinW, L.MinH, out overlap);
                    }
                    foreach (WinRow r in pinned) WindowLayout.Place(r.Info, r.Pin.Value, L, false);
                    if (L.LabelTitles)
                        foreach (WinRow r in winRows)
                            if (r.Info.HasWindow && !string.IsNullOrEmpty(r.Info.Account)) WindowLayout.SetTitle(r.Info.Hwnd, "Roblox - " + r.Info.Account);
                }
                lastSet = new HashSet<IntPtr>(winRows.Where(r => r.Info.HasWindow).Select(r => r.Info.Hwnd));
                if (overlap)
                    AddLog("Roblox will not make its windows smaller than " + L.MinW + " x " + L.MinH + " px, so " + tile.Count + " windows overlap (each shows its own corner). Fewer windows, a bigger screen or a second monitor avoids that.");
                else
                    AddLog("Arranged " + (tile.Count + pinned.Count) + " window(s) on " + mon.Label + ".");
                RefreshWindowList();
                UpdateMinInfo();
            }
            catch (Exception ex) { Log.Error("ArrangeNow", ex); AddLog("Arranging failed: " + ex.Message); }
        }

        // Runs every second. Does nothing unless auto-capture is ON.
        void AutoCaptureTick()
        {
            LayoutSettings L = Store.Settings.Layout;
            if (!L.AutoCapture) { autoArmed = false; changeSince = DateTime.MinValue; return; }
            HashSet<IntPtr> set = new HashSet<IntPtr>(WindowLayout.FindRobloxWindows().Where(w => w.HasWindow).Select(w => w.Hwnd));
            if (!autoArmed) { lastSet = set; autoArmed = true; changeSince = DateTime.MinValue; return; }
            if (set.SetEquals(lastSet)) { changeSince = DateTime.MinValue; return; }
            if (changeSince == DateTime.MinValue) { changeSince = DateTime.Now; return; }
            if ((DateTime.Now - changeSince).TotalMilliseconds >= L.AutoCaptureDelayMs)
            {
                changeSince = DateTime.MinValue;
                AddLog("Auto-capture: Roblox windows changed - re-arranging.");
                ArrangeNow();
                lastSet = set;
            }
        }

        void FocusStep(int dir)
        {
            List<WinInfo> wins = WindowLayout.FindRobloxWindows().Where(w => w.HasWindow).ToList();
            if (wins.Count == 0) return;
            List<IntPtr> order = new List<IntPtr>();
            foreach (WinRow r in winRows)
                if (r.Info.HasWindow && wins.Any(w => w.Hwnd == r.Info.Hwnd)) order.Add(r.Info.Hwnd);
            foreach (WinInfo w in wins)
                if (!order.Contains(w.Hwnd)) order.Add(w.Hwnd);
            int idx = order.IndexOf(Native.GetForegroundWindow());
            int next = idx < 0 ? 0 : (idx + dir + order.Count) % order.Count;
            WindowLayout.Focus(order[next]);
        }

        // ---------------- hotkeys ----------------
        void RegisterHotkeys()
        {
            UnregisterHotkeys();
            if (!Store.Settings.Layout.Hotkeys || !IsHandleCreated) return;
            uint mod = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT;
            bool a = Native.RegisterHotKey(Handle, HK_ARRANGE, mod, 0x78);
            bool b = Native.RegisterHotKey(Handle, HK_PREV, mod, 0x79);
            bool c = Native.RegisterHotKey(Handle, HK_NEXT, mod, 0x7A);
            hotkeysRegistered = true;
            if (!a || !b || !c) AddLog("Some hotkeys could not be registered (another program already uses them).");
        }

        void UnregisterHotkeys()
        {
            if (!hotkeysRegistered || !IsHandleCreated) return;
            Native.UnregisterHotKey(Handle, HK_ARRANGE);
            Native.UnregisterHotKey(Handle, HK_PREV);
            Native.UnregisterHotKey(Handle, HK_NEXT);
            hotkeysRegistered = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeys();
            int round = 2;                              // DWMWCP_ROUND (Windows 11; older Windows ignores it)
            DwmSetWindowAttribute(Handle, 33, ref round, 4);   // DWMWA_WINDOW_CORNER_PREFERENCE
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HK_ARRANGE) ArrangeNow();
                else if (id == HK_PREV) FocusStep(-1);
                else if (id == HK_NEXT) FocusStep(1);
            }
            if (m.Msg == 0x84) { ChromeHitTest(ref m); return; }   // borderless drag / resize
            if (m.Msg == 0xA3) { m.Result = IntPtr.Zero; return; } // WM_NCLBUTTONDBLCLK: no caption double-click maximize, so a quick drag-release-drag keeps working
            if (m.Msg == 0x231)                                     // WM_ENTERSIZEMOVE: drop the shaped-window
            {                                                       // region and pause timers/animation frames so
                if (tick != null) tick.Stop();                      // nothing competes with the move loop
                Anim.Suspend();
                SetWindowRgn(Handle, IntPtr.Zero, false);
            }
            else if (m.Msg == 0x232)                                // WM_EXITSIZEMOVE: put it all back
            {
                ApplyRoundRegion();
                if (tick != null) tick.Start();
                Anim.Resume();
            }
            base.WndProc(ref m);
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}
