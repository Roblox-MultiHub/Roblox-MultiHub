using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // The live status embed ("Log status" in Settings): ONE message in the channel, edited in
    // place on every refresh, with a chart image of total CPU / RAM history attached.
    //   Accounts: 7/7 | CPU: 12.3% | RAM: 4.2 GB
    //   **[game](link)**
    //   [name](profile)  <t:start:R>  6.9% / 222MB
    public static class WebhookStatus
    {
        static DateTime lastSent = DateTime.MinValue;

        // Called from the main timer (every second).
        public static void Tick()
        {
            if (!Store.Settings.WebhookStatus || !Webhook.Enabled) return;
            int every = Math.Max(15, Store.Settings.WebhookStatusSec);
            if ((DateTime.UtcNow - lastSent).TotalSeconds < every) return;
            lastSent = DateTime.UtcNow;
            List<Instance> live = Launcher.Instances.Where(i => Launcher.IsAlive(i.Pid)).ToList();
            string title = "Accounts: " + live.Count + "/" + Store.Accounts.Count
                + " | CPU: " + UsageMonitor.TotalCpu.ToString("0.0") + "%"
                + (GpuMeter.Available ? " | GPU: " + UsageMonitor.TotalGpu.ToString("0.0") + "%" : "")
                + " | RAM: " + Util.FormatBytes(UsageMonitor.TotalRam);
            Webhook.SendStatus(title, BuildDescription(live, every), Webhook.Green, live.Count == 0 ? null : RenderChart());
        }

        static string BuildDescription(List<Instance> live, int windowSec)
        {
            if (live.Count == 0) return "No accounts running.";
            int samples = Math.Max(1, windowSec / 2);          // UsageMonitor samples every ~2 s
            // group by game so one block belongs to each game
            var groups = live.GroupBy(i => i.Spec != null ? i.Spec.PlaceId : 0)
                             .OrderBy(g => g.Key)
                             .ToList();
            StringBuilder sb = new StringBuilder();
            foreach (var g in groups)
            {
                long place = g.Key;
                string gameName = null;
                if (place > 0)
                {
                    GameEntry ge = Store.Settings.Recent.FirstOrDefault(x => x.PlaceId == place);
                    gameName = ge != null && !string.IsNullOrEmpty(ge.Name) ? ge.Name : "#" + place;
                }
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(place > 0
                    ? "**[" + gameName + "](https://www.roblox.com/games/" + place + ")**"
                    : "**(no game)**");

                List<Instance> rows = g.OrderBy(i => i.Account == null ? "" : i.Account.Username,
                    Comparer<string>.Create(Util.NaturalCompare)).ToList();
                int nameW = rows.Max(i => DisplayName(i.Account).Length);
                foreach (Instance inst in rows)
                {
                    Account a = inst.Account;
                    string name = DisplayName(a);
                    long uid = a != null ? a.UserId : 0;
                    string profile = uid > 0
                        ? "[" + name + "](https://www.roblox.com/users/" + uid + "/profile)"
                        : name;
                    long start = new DateTimeOffset(inst.Started).ToUnixTimeSeconds();
                    UsageSample s;
                    UsageMonitor.Samples.TryGetValue(inst.Pid, out s);
                    double cpu = Avg(UsageMonitor.CpuHist, inst.Pid, samples, s != null ? s.Cpu : 0);
                    long ram = (long)Avg(UsageMonitor.RamHist, inst.Pid, samples, s != null ? s.Ram / 1048576.0 : 0);
                    string gpu = GpuMeter.Available && s != null ? s.Gpu.ToString("0.0") + "% / " : "";
                    sb.Append("\n").Append(profile)
                      .Append(' ', Math.Max(1, nameW - name.Length + 2))     // column-ish alignment
                      .Append("<t:").Append(start).Append(":R>   ")
                      .Append(gpu).Append(cpu.ToString("0.0")).Append("% / ").Append(ram).Append("MB");
                }
            }
            return sb.ToString();
        }

        static string DisplayName(Account a)
        {
            if (a == null) return "(unknown)";
            return string.IsNullOrEmpty(a.Username) ? AppState.Name(a) : a.Username;
        }

        static double Avg(Dictionary<int, List<double>> hist, int pid, int samples, double fallback)
        {
            List<double> l;
            if (hist == null || !hist.TryGetValue(pid, out l) || l.Count == 0) return fallback;
            int take = Math.Min(samples, l.Count);
            double sum = 0;
            for (int i = l.Count - take; i < l.Count; i++) sum += l[i];
            return sum / take;
        }

        // ---- the chart: total CPU % and total RAM (MB) of all clients over the history window ----
        static byte[] RenderChart()
        {
            const int W = 600, H = 200, padL = 44, padR = 60, padT = 14, padB = 22;
            List<double> cpu = SumSeries(UsageMonitor.CpuHist);
            List<double> ram = SumSeries(UsageMonitor.RamHist);
            if (cpu.Count < 2) return null;
            double maxCpu = Math.Max(5.0, cpu.Max()) * 1.15;
            double maxRam = Math.Max(512.0, ram.Max()) * 1.15;

            using (Bitmap bmp = new Bitmap(W, H))
            using (Graphics g = Graphics.FromImage(bmp))
            using (MemoryStream ms = new MemoryStream())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(30, 31, 34)))     // Discord embed tone
                    g.FillRectangle(bg, 0, 0, W, H);
                Rectangle plot = new Rectangle(padL, padT, W - padL - padR, H - padT - padB);
                using (Pen grid = new Pen(Color.FromArgb(52, 54, 58)))
                {
                    for (int k = 0; k <= 3; k++)
                    {
                        int y = plot.Top + plot.Height * k / 3;
                        g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    }
                }
                DrawLine(g, cpu, maxCpu, plot, Color.FromArgb(87, 242, 135));           // green: CPU
                DrawLine(g, ram, maxRam, plot, Color.FromArgb(88, 101, 242));           // blurple: RAM
                using (Font f = new Font("Segoe UI", 8.5f))
                {
                    using (SolidBrush t = new SolidBrush(Color.FromArgb(180, 180, 188)))
                    {
                        string legCpu = "CPU " + cpu[cpu.Count - 1].ToString("0.0") + "%";
                        string legRam = "RAM " + (ram[ram.Count - 1] / 1024.0).ToString("0.00") + " GB";
                        int lx = plot.Right + 8;
                        g.DrawLine(new Pen(Color.FromArgb(87, 242, 135), 2), lx, padT + 4, lx + 10, padT + 4);
                        TextRenderer.DrawText(g, legCpu, f, new Point(lx + 14, padT - 3), Color.FromArgb(180, 180, 188));
                        g.DrawLine(new Pen(Color.FromArgb(88, 101, 242), 2), lx, padT + 18, lx + 10, padT + 18);
                        TextRenderer.DrawText(g, legRam, f, new Point(lx + 14, padT + 11), Color.FromArgb(180, 180, 188));
                        TextRenderer.DrawText(g, "0", f, new Point(plot.Left - 22, plot.Bottom - 6), Color.FromArgb(120, 120, 128));
                        TextRenderer.DrawText(g, ((int)maxCpu) + "%", f, new Point(plot.Left - 22, plot.Top - 6), Color.FromArgb(120, 120, 128));
                    }
                }
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }

        // sums every pid's history per sample index (samples are taken on the same 2s tick)
        static List<double> SumSeries(Dictionary<int, List<double>> hist)
        {
            List<double> sum = new List<double>();
            if (hist == null) return sum;
            foreach (KeyValuePair<int, List<double>> kv in hist)
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    while (sum.Count <= i) sum.Add(0);
                    sum[i] += kv.Value[i];
                }
            return sum;
        }

        static void DrawLine(Graphics g, List<double> v, double max, Rectangle plot, Color color)
        {
            using (Pen pen = new Pen(color, 2f))
            {
                for (int i = 1; i < v.Count; i++)
                {
                    float x1 = plot.Left + plot.Width * (i - 1) / (float)Math.Max(1, v.Count - 1);
                    float x2 = plot.Left + plot.Width * i / (float)Math.Max(1, v.Count - 1);
                    float y1 = plot.Bottom - (float)(plot.Height * Math.Min(1.0, v[i - 1] / max));
                    float y2 = plot.Bottom - (float)(plot.Height * Math.Min(1.0, v[i] / max));
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }
    }
}
