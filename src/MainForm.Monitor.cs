using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        FlowLayoutPanel monitorHost;
        Label lblMonitorEmpty;
        readonly Dictionary<int, MonitorCard> monCards = new Dictionary<int, MonitorCard>();

        void BuildMonitorPage()
        {
            Panel page = NewPage("Monitor");

            Label title = Ui.Lbl(Lang.T("Monitor"), "title");
            title.Location = new Point(PageBuilder.PadX, 14);
            page.Controls.Add(title);
            Label sub = Ui.Lbl("Every Roblox window this app sees, live - CPU and memory over time, with a preview and quick controls.", "muted");
            sub.AutoSize = false;
            sub.Width = PageW - PageBuilder.PadX * 2;
            sub.Height = 20;
            sub.Location = new Point(PageBuilder.PadX, 50);
            page.Controls.Add(sub);

            lblMonitorEmpty = Ui.Lbl(Lang.T("No Roblox window is running. Launch some accounts and they will show up here."), "muted");
            lblMonitorEmpty.AutoSize = false;
            lblMonitorEmpty.Width = PageW - PageBuilder.PadX * 2;
            lblMonitorEmpty.Height = 24;
            lblMonitorEmpty.Location = new Point(PageBuilder.PadX, 84);
            page.Controls.Add(lblMonitorEmpty);

            monitorHost = new FlowLayoutPanel();
            monitorHost.Tag = "card";
            monitorHost.AutoScroll = true;
            monitorHost.Location = new Point(PageBuilder.PadX - 6, 78);
            monitorHost.Padding = new Padding(0);
            monitorHost.Margin = new Padding(0);
            page.Controls.Add(monitorHost);
            new DarkVBar(monitorHost);
            page.Resize += delegate { SizeMonitorHost(page); };
            SizeMonitorHost(page);
        }

        void SizeMonitorHost(Panel page)
        {
            if (monitorHost == null) return;
            monitorHost.Size = new Size(page.ClientSize.Width - (PageBuilder.PadX - 6) * 2, Math.Max(100, page.ClientSize.Height - 86));
        }

        void RefreshMonitor()
        {
            if (monitorHost == null) return;
            List<WinInfo> wins = WindowLayout.FindRobloxWindows();
            HashSet<int> seen = new HashSet<int>();

            monitorHost.SuspendLayout();
            foreach (WinInfo w in wins)
            {
                seen.Add(w.Pid);
                MonitorCard card;
                if (!monCards.TryGetValue(w.Pid, out card))
                {
                    card = new MonitorCard(FocusCard, MinimizeCard, CloseCard);
                    card.Pid = w.Pid;
                    monCards[w.Pid] = card;
                    monitorHost.Controls.Add(card);
                    // new cards grow into place; the flow panel pushes the others down smoothly
                    int full = card.Height;
                    card.Height = 24;
                    Anim.Go("moncard" + w.Pid, 200, delegate(float e)
                    {
                        card.Height = 24 + (int)((full - 24) * e);
                    }, delegate { card.Height = full; });
                }
                UsageSample s;
                UsageMonitor.Samples.TryGetValue(w.Pid, out s);
                List<double> cpuH, ramH;
                UsageMonitor.CpuHist.TryGetValue(w.Pid, out cpuH);
                UsageMonitor.RamHist.TryGetValue(w.Pid, out ramH);
                card.Update(w, s, cpuH, ramH, w.Pid == MaxAlts.ActiveRobloxPid(), Store.Settings.MaxAlts);
            }

            // drop cards whose process is gone
            foreach (int pid in new List<int>(monCards.Keys))
            {
                if (seen.Contains(pid)) continue;
                MonitorCard card = monCards[pid];
                monitorHost.Controls.Remove(card);
                card.Dispose();
                monCards.Remove(pid);
            }
            monitorHost.ResumeLayout();
            lblMonitorEmpty.Visible = monCards.Count == 0;
        }

        void FocusCard(MonitorCard c) { WindowLayout.Focus(c.Hwnd); }
        void MinimizeCard(MonitorCard c) { WindowLayout.Minimize(c.Hwnd); }

        void CloseCard(MonitorCard c)
        {
            Instance inst = Launcher.FindInstance(c.Pid);
            if (inst != null) { inst.UserClosed = true; Launcher.Instances.Remove(inst); }
            Launcher.KillPid(c.Pid);
            AddLog("Closed Roblox window (PID " + c.Pid + ") from the Monitor.");
            RefreshMonitor();
            UpdateStatusCells();
        }
    }

    // One live card on the Monitor page: name, a best-effort preview, CPU and memory sparklines and controls.
    public class MonitorCard : RoundPanel
    {
        public int Pid;
        public IntPtr Hwnd;

        readonly Label name = Ui.Lbl("", "cardname");
        readonly Label status = Ui.Lbl("", "muted");
        readonly PictureBox thumb = new PictureBox();
        readonly Label cpuLbl = Ui.Lbl("", "");
        readonly Label ramLbl = Ui.Lbl("", "");
        readonly Label upLbl = Ui.Lbl("", "muted");
        readonly Sparkline cpuSpark = new Sparkline();
        readonly Sparkline ramSpark = new Sparkline();
        readonly RoundButton bFocus, bMin, bClose;
        DateTime lastShot = DateTime.MinValue;

        public MonitorCard(Action<MonitorCard> onFocus, Action<MonitorCard> onMin, Action<MonitorCard> onClose)
        {
            Tag = "card2";
            Size = new Size(336, 236);
            Margin = new Padding(8);
            BackColor = Theme.Panel2;

            name.Font = Theme.CardName;
            name.Location = new Point(14, 10);
            name.AutoSize = true;
            Controls.Add(name);

            status.Location = new Point(14, 32);
            status.AutoSize = false;
            status.Size = new Size(308, 18);
            Controls.Add(status);

            thumb.Location = new Point(15, 52);
            thumb.Size = new Size(306, 96);
            thumb.BackColor = Theme.Bg;
            thumb.SizeMode = PictureBoxSizeMode.Normal;
            Controls.Add(thumb);

            cpuLbl.Location = new Point(14, 154);
            cpuLbl.AutoSize = false;
            cpuLbl.Size = new Size(120, 18);
            Controls.Add(cpuLbl);
            cpuSpark.Location = new Point(150, 153);
            cpuSpark.Size = new Size(170, 18);
            cpuSpark.BackColor = Theme.Panel2;
            cpuSpark.LineColor = Theme.Info;
            Controls.Add(cpuSpark);

            ramLbl.Location = new Point(14, 178);
            ramLbl.AutoSize = false;
            ramLbl.Size = new Size(120, 18);
            Controls.Add(ramLbl);
            ramSpark.Location = new Point(150, 177);
            ramSpark.Size = new Size(170, 18);
            ramSpark.BackColor = Theme.Panel2;
            ramSpark.LineColor = Theme.Purple;
            Controls.Add(ramSpark);

            upLbl.Location = new Point(14, 206);
            upLbl.AutoSize = false;
            upLbl.Size = new Size(110, 22);
            Controls.Add(upLbl);

            bFocus = Ui.Btn(Lang.T("Focus"), 66, delegate { onFocus(this); }, "");
            bMin = Ui.Btn(Lang.T("Min"), 54, delegate { onMin(this); }, "");
            bClose = Ui.Btn(Lang.T("Close"), 64, delegate { onClose(this); }, "danger");
            bFocus.Height = 24; bMin.Height = 24; bClose.Height = 24;
            bFocus.Location = new Point(132, 206);
            bMin.Location = new Point(202, 206);
            bClose.Location = new Point(260, 206);
            Controls.Add(bFocus); Controls.Add(bMin); Controls.Add(bClose);
        }

        public void Update(WinInfo w, UsageSample s, List<double> cpuH, List<double> ramH, bool active, bool maxAlts)
        {
            Hwnd = w.Hwnd;
            string nm = string.IsNullOrEmpty(w.Account) ? ("PID " + w.Pid) : w.Account;
            name.Text = nm + (active && maxAlts ? "   ▶ playing" : "");
            name.ForeColor = Theme.Text;

            string st;
            if (!w.HasWindow) st = "starting / no window yet";
            else if (w.Minimized) st = "minimized";
            else st = Util.Short(string.IsNullOrEmpty(w.Title) ? "running" : w.Title, 46);
            if (maxAlts && !active && w.HasWindow) st += "  • throttled";
            status.Text = st;
            status.ForeColor = active && maxAlts ? Theme.Good : Theme.Muted;

            if (s != null)
            {
                cpuLbl.Text = "CPU  " + (UsageMonitor.CpuAvailable ? s.Cpu.ToString("0.0") + "%" : "n/a");
                ramLbl.Text = "RAM  " + Util.FormatBytes(s.Ram);
            }
            else { cpuLbl.Text = "CPU  -"; ramLbl.Text = "RAM  -"; }
            cpuLbl.ForeColor = Theme.Text;
            ramLbl.ForeColor = Theme.Text;
            cpuSpark.SetData(cpuH, 0);
            ramSpark.SetData(ramH, 0);

            Instance inst = Launcher.FindInstance(w.Pid);
            upLbl.Text = inst != null ? "up " + Fmt(DateTime.Now - inst.Started) : "";

            // refresh the preview at most every 3 s (PrintWindow is not free)
            if (w.HasWindow && !w.Minimized && (DateTime.UtcNow - lastShot).TotalSeconds >= 3)
            {
                lastShot = DateTime.UtcNow;
                Bitmap bmp = Thumb.Capture(w.Hwnd, thumb.Width, thumb.Height);
                Image old = thumb.Image;
                thumb.Image = bmp;              // may be null (GPU window came back black) - placeholder shows
                if (old != null) old.Dispose();
            }
        }

        static string Fmt(TimeSpan t)
        {
            if (t.TotalHours >= 1) return ((int)t.TotalHours) + "h " + t.Minutes + "m";
            if (t.TotalMinutes >= 1) return t.Minutes + "m " + t.Seconds + "s";
            return t.Seconds + "s";
        }
    }
}
