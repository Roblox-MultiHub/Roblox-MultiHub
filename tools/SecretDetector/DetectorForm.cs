using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SecretDetector
{
    public class DetectorForm : Form
    {
        const double Threshold = 0.85;
        const int RearmSec = 10;
        const int NudgeOffsetUpPx = 18;

        TextBox txtLog;
        Label lblStatus;
        Button btnSnapshot;
        CheckBox chkNudge;
        PictureBox picPreview;
        Thread worker;
        volatile bool running = true;
        volatile bool scanNow;
        volatile bool nudgeEnabled = true;
        DateTime lastScan = DateTime.MinValue;
        Template tpl1, tpl2;
        readonly Dictionary<int, SecretState> states = new Dictionary<int, SecretState>();

        class SecretState
        {
            public bool Detected;
            public DateTime GoneSince = DateTime.MinValue;
        }

        public DetectorForm()
        {
            Text = "Roblox Secret Detector (test harness)";
            ClientSize = new Size(1280, 600);
            StartPosition = FormStartPosition.CenterScreen;

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.Padding = new Padding(8, 8, 8, 4);
            top.FlowDirection = FlowDirection.LeftToRight;
            top.WrapContents = false;

            btnSnapshot = new Button();
            btnSnapshot.Text = "Snapshot now";
            btnSnapshot.Width = 120;
            btnSnapshot.Height = 28;
            btnSnapshot.Click += delegate { scanNow = true; };
            top.Controls.Add(btnSnapshot);

            chkNudge = new CheckBox();
            chkNudge.Text = "Nudge cursor on detect";
            chkNudge.Checked = true;
            chkNudge.AutoSize = true;
            chkNudge.Padding = new Padding(10, 6, 0, 0);
            chkNudge.CheckedChanged += delegate { nudgeEnabled = chkNudge.Checked; };
            top.Controls.Add(chkNudge);

            lblStatus = new Label();
            lblStatus.Text = "Starting...";
            lblStatus.AutoSize = true;
            lblStatus.Padding = new Padding(12, 8, 0, 0);
            top.Controls.Add(lblStatus);

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 9f);

            picPreview = new PictureBox();
            picPreview.Dock = DockStyle.Right;
            picPreview.Width = 430;
            picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            picPreview.BackColor = Color.FromArgb(30, 30, 30);

            Controls.Add(txtLog);
            Controls.Add(picPreview);
            Controls.Add(top);

            LoadTemplates();
            worker = new Thread(Work);
            worker.IsBackground = true;
            worker.Start();
        }

        void LoadTemplates()
        {
            string p1 = Resolve("test-img.png");
            string p2 = Resolve("test-img2.png");
            if (p1 == null)
            {
                Log("test-img.png not found. It is the detection trigger - put it next to this exe.");
            }
            else
            {
                try
                {
                    tpl1 = Template.Load(p1, "test-img");
                    Log("Loaded trigger template: " + p1 + "  (" + tpl1.W + "x" + tpl1.H + ", " + tpl1.Count + " px)");
                }
                catch (Exception ex) { Log("Could not load test-img.png: " + ex.Message); }
            }
            if (p2 != null)
            {
                try
                {
                    tpl2 = Template.Load(p2, "test-img2");
                    Log("Loaded info template: " + p2 + "  (" + tpl2.W + "x" + tpl2.H + ", " + tpl2.Count + " px)");
                }
                catch (Exception ex) { Log("Could not load test-img2.png: " + ex.Message); }
            }
        }

        static string Resolve(string name)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(exeDir, name),
                Path.Combine(Directory.GetCurrentDirectory(), name),
                Path.Combine(exeDir, "..", "..", name)
            };
            foreach (string c in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(c);
                    if (File.Exists(full)) return full;
                }
                catch (Exception) { }
            }
            return null;
        }

        void Work()
        {
            while (running)
            {
                bool go = false;
                if (scanNow) { scanNow = false; go = true; }
                else if ((DateTime.UtcNow - lastScan).TotalSeconds >= 5) go = true;

                if (go)
                {
                    lastScan = DateTime.UtcNow;
                    try { Scan(); }
                    catch (Exception ex) { Log("Scan error: " + ex.Message); }
                }
                Thread.Sleep(150);
            }
        }

        void Scan()
        {
            List<WinInfo> wins = Native.FindRobloxWindows();
            SetStatus(wins.Count + " window(s) found - scanning...");
            Log("---- scan " + DateTime.Now.ToString("HH:mm:ss") + "   (" + wins.Count + " window(s)) ----");
            if (wins.Count == 0)
            {
                SetStatus("No RobloxPlayerBeta windows found.");
                return;
            }

            foreach (WinInfo w in wins)
            {
                string account = ParseAccount(w.Title);
                Bitmap bmp = WindowCapture.CaptureWindow(w.Hwnd);
                if (bmp == null)
                {
                    Log("  [pid " + w.Pid + "] " + account + "  -  capture failed");
                    continue;
                }

                byte[] gray = Pixel.ToGray(bmp);
                int bw = bmp.Width;
                int bh = bmp.Height;

                if (Pixel.IsUniform(gray))
                {
                    Log("  [pid " + w.Pid + "] " + account + "  -  capture is blank (minimized or render unavailable), skipped");
                    bmp.Dispose();
                    continue;
                }

                MatchResult m1 = tpl1 != null ? Matcher.Find(gray, bw, bh, tpl1) : null;
                MatchResult m2 = tpl2 != null ? Matcher.Find(gray, bw, bh, tpl2) : null;

                string s1 = m1 != null ? "test-img " + m1.Score.ToString("0.000") + " @ " + m1.X + "," + m1.Y : "test-img n/a";
                string s2 = m2 != null ? "test-img2 " + m2.Score.ToString("0.000") + " @ " + m2.X + "," + m2.Y : "test-img2 n/a";

                bool present = m2 != null && m2.Score >= Threshold;

                SecretState st;
                if (!states.TryGetValue(w.Pid, out st))
                {
                    st = new SecretState();
                    states[w.Pid] = st;
                }

                if (!present)
                {
                    Log("  [pid " + w.Pid + "] " + account + "  -  no secret    | " + s1 + "   |   " + s2);
                    if (st.Detected)
                    {
                        if (st.GoneSince == DateTime.MinValue) st.GoneSince = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - st.GoneSince).TotalSeconds >= RearmSec)
                        {
                            st.Detected = false;
                            st.GoneSince = DateTime.MinValue;
                            Log("  [pid " + w.Pid + "] " + account + "  -  rearmed (secret gone " + RearmSec + "s)");
                        }
                    }
                    bmp.Dispose();
                    continue;
                }

                int px = m2.X + tpl2.W / 2;
                int py = m2.Y + tpl2.H / 2 - NudgeOffsetUpPx;
                int tx = w.Rect.Left + px;
                int ty = w.Rect.Top + py;
                string win = "win " + w.Rect.Left + "," + w.Rect.Top + " " + w.Rect.Width + "x" + w.Rect.Height;

                Bitmap preview = new Bitmap(bmp);
                using (Graphics g = Graphics.FromImage(preview))
                {
                    using (Pen box = new Pen(Color.Lime, 2))
                        g.DrawRectangle(box, m2.X, m2.Y, tpl2.W, tpl2.H);
                    using (Pen mark = new Pen(Color.Red, 2))
                    {
                        g.DrawEllipse(mark, px - 6, py - 6, 12, 12);
                        g.DrawLine(mark, px - 12, py, px + 12, py);
                        g.DrawLine(mark, px, py - 12, px, py + 12);
                    }
                    if (m1 != null && m1.Score >= Threshold)
                        using (Pen icon = new Pen(Color.DodgerBlue, 1))
                            g.DrawRectangle(icon, m1.X, m1.Y, tpl1.W, tpl1.H);
                }
                ShowPreview(preview);

                if (!st.Detected)
                {
                    st.Detected = true;
                    st.GoneSince = DateTime.MinValue;
                    bool moved = false;
                    if (nudgeEnabled) moved = NudgeAt(w.Hwnd, tx, ty);
                    Log("  [pid " + w.Pid + "] " + account + "  -  SECRET " + s2 + "  -> " + (nudgeEnabled ? (moved ? "NUDGED" : "nudge FAILED") : "target") + " (" + tx + "," + ty + ")  " + win + "   | " + s1);
                }
                else
                {
                    Log("  [pid " + w.Pid + "] " + account + "  -  SECRET " + s2 + "  (already nudged, waiting for it to leave)  " + win + "   | " + s1);
                }
                bmp.Dispose();
            }
            SetStatus(wins.Count + " window(s) scanned.");
        }

        static bool NudgeAt(IntPtr hwnd, int cx, int cy)
        {
            IntPtr fg = Native.GetForegroundWindow();
            Native.POINT orig;
            bool haveOrig = Native.GetCursorPos(out orig);

            Native.ForceForeground(hwnd);
            Thread.Sleep(120);

            bool moved = Native.SetCursorPos(cx, cy);
            Thread.Sleep(500);
            Native.MouseClickHold(cx, cy);
            Thread.Sleep(20);

            if (haveOrig) Native.SetCursorPos(orig.X, orig.Y);
            if (fg != IntPtr.Zero && fg != hwnd) Native.ForceForeground(fg);
            return moved;
        }

        static string ParseAccount(string title)
        {
            if (string.IsNullOrEmpty(title)) return "(no title)";
            if (title.Equals("Roblox", StringComparison.OrdinalIgnoreCase)) return "(not renamed)";
            if (title.StartsWith("Roblox - ", StringComparison.OrdinalIgnoreCase)) return title.Substring(9).Trim();
            return title;
        }

        void SetStatus(string s)
        {
            if (lblStatus.IsDisposed) return;
            if (lblStatus.InvokeRequired)
            {
                try { lblStatus.BeginInvoke(new Action<string>(SetStatus), s); } catch (Exception) { }
                return;
            }
            lblStatus.Text = s;
        }

        void Log(string s)
        {
            if (txtLog.IsDisposed) return;
            if (txtLog.InvokeRequired)
            {
                try { txtLog.BeginInvoke(new Action<string>(Log), s); } catch (Exception) { }
                return;
            }
            txtLog.AppendText(s + Environment.NewLine);
        }

        void ShowPreview(Bitmap bmp)
        {
            if (picPreview.IsDisposed) { bmp.Dispose(); return; }
            if (picPreview.InvokeRequired)
            {
                try { picPreview.BeginInvoke(new Action<Bitmap>(ShowPreview), bmp); }
                catch (Exception) { bmp.Dispose(); }
                return;
            }
            Image old = picPreview.Image;
            picPreview.Image = bmp;
            if (old != null) old.Dispose();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            running = false;
            if (worker != null)
            {
                try { worker.Join(1500); } catch (Exception) { }
                worker = null;
            }
            base.OnFormClosing(e);
        }
    }
}
