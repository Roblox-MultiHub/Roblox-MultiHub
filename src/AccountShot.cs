using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace RobloxMultiHub
{
    // Builds ONE composite PNG of every running Roblox window - a labelled grid of per-account
    // previews. Shared by the periodic webhook screenshot and (later) the on-demand bot command.
    // Uses the same best-effort PrintWindow capture as the Monitor page (Thumb.Capture), so it also
    // works for windows that are behind others. Minimized windows (and GPU frames that come back
    // black) get a labelled placeholder tile instead, so you still see which accounts are up.
    public static class AccountShot
    {
        const int TileW = 448, TileH = 252;   // 16:9 preview per account
        const int Label = 28;                  // name bar under each tile
        const int Pad = 12;                    // gap between tiles and around the edge
        const int HeadH = 34;                  // title strip at the top

        // The running windows, newest grid order. Count is how many tiles we drew.
        static List<WinInfo> LiveWindows()
        {
            List<WinInfo> live = new List<WinInfo>();
            try
            {
                using (new DpiScope())
                    foreach (WinInfo w in WindowLayout.FindRobloxWindows())
                        if (w.HasWindow) live.Add(w);
            }
            catch (Exception) { }
            return live;
        }

        // A one-line summary for the embed/message that goes with the image.
        public static string Summary(out int count)
        {
            List<WinInfo> live = LiveWindows();
            count = live.Count;
            if (count == 0) return "No Roblox windows are running.";
            StringBuilder sb = new StringBuilder();
            sb.Append(count).Append(count == 1 ? " account running: " : " accounts running: ");
            List<string> names = new List<string>();
            foreach (WinInfo w in live) names.Add(NameOf(w));
            sb.Append(string.Join(", ", names.ToArray()));
            return sb.ToString();
        }

        static string NameOf(WinInfo w)
        {
            if (!string.IsNullOrEmpty(w.Account)) return w.Account;
            if (!string.IsNullOrEmpty(w.Title)) return w.Title;
            return "PID " + w.Pid;
        }

        // Returns PNG bytes of a labelled grid of all running windows, or null if none are running.
        public static byte[] CapturePng(out int count)
        {
            List<WinInfo> live = LiveWindows();
            count = live.Count;
            if (count == 0) return null;

            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling(count / (double)cols);
            int cellW = TileW + Pad;
            int cellH = TileH + Label + Pad;
            int W = Pad + cols * cellW;
            int H = Pad + HeadH + rows * cellH;

            using (Bitmap canvas = new Bitmap(W, H))
            using (Graphics g = Graphics.FromImage(canvas))
            using (MemoryStream ms = new MemoryStream())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 25, 28)))     // Discord embed tone
                    g.FillRectangle(bg, 0, 0, W, H);

                using (Font hf = new Font("Segoe UI Semibold", 11f))
                using (SolidBrush ht = new SolidBrush(Color.FromArgb(220, 221, 224)))
                    g.DrawString("Roblox MultiHub  -  " + count + (count == 1 ? " account" : " accounts")
                        + "  -  " + DateTime.Now.ToString("HH:mm:ss"), hf, ht, Pad, 9);

                using (Font nf = new Font("Segoe UI", 9.5f))
                for (int i = 0; i < live.Count; i++)
                {
                    WinInfo w = live[i];
                    int cx = Pad + (i % cols) * cellW;
                    int cy = Pad + HeadH + (i / cols) * cellH;
                    Rectangle tile = new Rectangle(cx, cy, TileW, TileH);

                    Bitmap shot = null;
                    try { if (!w.Minimized) shot = Thumb.Capture(w.Hwnd, TileW, TileH); } catch (Exception) { }
                    if (shot != null)
                    {
                        using (shot) g.DrawImage(shot, tile);
                    }
                    else
                    {
                        using (SolidBrush ph = new SolidBrush(Color.FromArgb(34, 35, 39)))
                            g.FillRectangle(ph, tile);
                        string note = w.Minimized ? "(minimized)" : "(no preview)";
                        using (SolidBrush pt = new SolidBrush(Color.FromArgb(120, 122, 128)))
                        {
                            SizeF sz = g.MeasureString(note, nf);
                            g.DrawString(note, nf, pt, cx + (TileW - sz.Width) / 2, cy + (TileH - sz.Height) / 2);
                        }
                    }
                    using (Pen edge = new Pen(Color.FromArgb(52, 54, 58)))
                        g.DrawRectangle(edge, tile);

                    // name bar
                    Rectangle bar = new Rectangle(cx, cy + TileH, TileW, Label);
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(32, 33, 37)))
                        g.FillRectangle(bb, bar);
                    using (SolidBrush nt = new SolidBrush(Color.FromArgb(205, 207, 212)))
                    {
                        string name = NameOf(w);
                        if (name.Length > 46) name = name.Substring(0, 45) + "…";
                        g.DrawString(name, nf, nt, cx + 8, cy + TileH + 6);
                    }
                }

                canvas.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
