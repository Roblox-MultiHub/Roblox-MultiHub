using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace RobloxMultiHub
{
    class SecretTemplate
    {
        public string Path;
        public int W;
        public int H;
        public byte[] Gray;
        public int[] OffX;
        public int[] OffY;
        public double[] TplDiff;
        public int Count;
        public double Var;

        public static SecretTemplate Load(string path)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                SecretTemplate t = new SecretTemplate();
                t.Path = path;
                t.W = bmp.Width;
                t.H = bmp.Height;
                int n = t.W * t.H;
                t.Gray = new byte[n];
                bool[] mask = new bool[n];
                SecretPixel.Read(bmp, t.Gray, mask);

                int count = 0;
                for (int i = 0; i < n; i++) if (mask[i]) count++;
                t.Count = count;
                t.OffX = new int[count];
                t.OffY = new int[count];
                t.TplDiff = new double[count];

                int idx = 0;
                double sum = 0;
                for (int y = 0; y < t.H; y++)
                    for (int x = 0; x < t.W; x++)
                    {
                        int i = y * t.W + x;
                        if (!mask[i]) continue;
                        t.OffX[idx] = x;
                        t.OffY[idx] = y;
                        sum += t.Gray[i];
                        idx++;
                    }

                double mean = sum / count;
                double var = 0;
                for (int k = 0; k < count; k++)
                {
                    double d = t.Gray[t.OffY[k] * t.W + t.OffX[k]] - mean;
                    t.TplDiff[k] = d;
                    var += d * d;
                }
                t.Var = var;
                return t;
            }
        }
    }

    static class SecretPixel
    {
        public static void Read(Bitmap bmp, byte[] gray, bool[] mask)
        {
            Bitmap src = bmp;
            Bitmap tmp = null;
            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                tmp = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(tmp)) g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                src = tmp;
            }

            int w = src.Width;
            int h = src.Height;
            BitmapData data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] buf = new byte[stride * h];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                bool anyTransparent = false;
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    int gr = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 4;
                        byte b = buf[i];
                        byte g = buf[i + 1];
                        byte r = buf[i + 2];
                        byte a = buf[i + 3];
                        gray[gr + x] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                        bool opaque = a >= 128;
                        mask[gr + x] = opaque;
                        if (!opaque) anyTransparent = true;
                    }
                }
                if (!anyTransparent)
                    for (int i = 0; i < mask.Length; i++) mask[i] = true;
            }
            finally
            {
                src.UnlockBits(data);
                if (tmp != null) tmp.Dispose();
            }
        }

        public static byte[] ToGray(Bitmap bmp)
        {
            int n = bmp.Width * bmp.Height;
            byte[] gray = new byte[n];
            bool[] mask = new bool[n];
            Read(bmp, gray, mask);
            return gray;
        }

        public static bool IsUniform(byte[] gray)
        {
            if (gray.Length < 2) return true;
            byte first = gray[0];
            for (int i = 1; i < gray.Length; i++)
                if (gray[i] != first) return false;
            return true;
        }
    }

    static class SecretMatch
    {
        class Candidate { public int X; public int Y; public double Score; }

        public static bool Find(byte[] src, int sw, int sh, SecretTemplate t, out int x, out int y, out double score)
        {
            x = 0; y = 0; score = 0;
            int maxX = sw - t.W;
            int maxY = sh - t.H;
            if (maxX < 0 || maxY < 0) return false;

            int step = 3;
            List<Candidate> best = new List<Candidate>();
            for (int yy = 0; yy <= maxY; yy += step)
                for (int xx = 0; xx <= maxX; xx += step)
                {
                    Candidate c = new Candidate();
                    c.X = xx; c.Y = yy; c.Score = Zncc(src, sw, xx, yy, t);
                    Consider(best, c, 6);
                }

            Candidate res = null;
            foreach (Candidate c in best)
            {
                int x0 = Math.Max(0, c.X - step);
                int x1 = Math.Min(maxX, c.X + step);
                int y0 = Math.Max(0, c.Y - step);
                int y1 = Math.Min(maxY, c.Y + step);
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        double s = Zncc(src, sw, xx, yy, t);
                        if (res == null || s > res.Score)
                        {
                            res = new Candidate();
                            res.X = xx; res.Y = yy; res.Score = s;
                        }
                    }
            }

            x = res.X;
            y = res.Y;
            score = res.Score;
            return true;
        }

        static void Consider(List<Candidate> list, Candidate m, int keep)
        {
            int at = list.Count;
            for (int i = 0; i < list.Count; i++)
                if (m.Score > list[i].Score) { at = i; break; }
            list.Insert(at, m);
            if (list.Count > keep) list.RemoveAt(list.Count - 1);
        }

        static double Zncc(byte[] src, int sw, int x0, int y0, SecretTemplate t)
        {
            int c = t.Count;
            double sumS = 0;
            for (int k = 0; k < c; k++)
                sumS += src[(y0 + t.OffY[k]) * sw + (x0 + t.OffX[k])];
            double meanS = sumS / c;

            double num = 0;
            double denS = 0;
            for (int k = 0; k < c; k++)
            {
                double ds = src[(y0 + t.OffY[k]) * sw + (x0 + t.OffX[k])] - meanS;
                num += ds * t.TplDiff[k];
                denS += ds * ds;
            }

            double den = Math.Sqrt(denS * t.Var);
            if (den < 1e-9) return 0;
            return num / den;
        }
    }

    public static class SecretDetector
    {
        const double Threshold = 0.85;
        const int RearmSec = 10;
        const int PollSec = 5;

        public static event Action<int, string, int, int> Detected;
        public static event Action<Bitmap, string> Preview;

        static Thread thread;
        static volatile bool stop;
        static SecretTemplate tpl;
        static string loadedPath;
        static bool loggedNoImage;
        static readonly Dictionary<int, State> states = new Dictionary<int, State>();

        class State
        {
            public bool Detected;
            public DateTime GoneSince = DateTime.MinValue;
            public int Attempts;
        }

        class RawWin
        {
            public IntPtr Hwnd;
            public int Pid;
            public string Title = "";
            public Rectangle Rect;
        }

        public static bool Running { get { return thread != null; } }

        public static bool RequirementsMet()
        {
            return Store.Settings.Layout != null && Store.Settings.Layout.LabelTitles
                && Webhook.Enabled && Store.Settings.WebhookStatus;
        }

        public static void Start()
        {
            if (thread != null) return;
            stop = false;
            loggedNoImage = false;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "SecretDetector";
            thread.Start();
        }

        public static void Stop()
        {
            if (thread == null) return;
            stop = true;
            try { thread.Join(2000); } catch (Exception) { }
            thread = null;
        }

        static void Run()
        {
            while (!stop)
            {
                try
                {
                    if (!RequirementsMet())
                    {
                        Log.Write("Detect Secrets: a requirement was turned off - detection stopped.");
                        break;
                    }
                    Tick();
                }
                catch (Exception ex) { Log.Error("SecretDetector", ex); }
                for (int i = 0; i < PollSec * 10 && !stop; i++) Thread.Sleep(100);
            }
        }

        static void Tick()
        {
            string imgPath = Store.Settings.SecretImagePath ?? "";
            int holdMs = Store.Settings.SecretHoldMs;
            int offX = Store.Settings.SecretOffsetX;
            int offY = Store.Settings.SecretOffsetY;
            bool allowClick = Store.Settings.SecretAllowClick;
            int maxTries = Math.Max(1, Store.Settings.SecretMaxTries);

            if (tpl == null || !string.Equals(loadedPath, imgPath, StringComparison.OrdinalIgnoreCase))
            {
                tpl = null;
                loadedPath = imgPath;
                loggedNoImage = false;
                if (imgPath.Length > 0 && File.Exists(imgPath))
                {
                    try
                    {
                        tpl = SecretTemplate.Load(imgPath);
                        Log.Write("Detect Secrets: using image " + imgPath + " (" + tpl.W + "x" + tpl.H + ").");
                    }
                    catch (Exception ex) { Log.Write("Detect Secrets: could not load image: " + ex.Message); }
                }
                else if (!loggedNoImage) { loggedNoImage = true; Log.Write("Detect Secrets: no image selected yet (pick one in the Beta tab)."); }
            }

            if (tpl == null)
            {
                RaisePreview(null, "Detect Secrets is running but no image is loaded yet (pick one in the Beta tab).");
                return;
            }

            List<RawWin> wins = FindWindowsRaw();
            HashSet<int> livePids = new HashSet<int>();
            int scanned = 0;
            int oldEnough = 0;
            int matched = 0;
            double bestScore = -2;
            string bestName = "";
            Bitmap previewBmp = null;

            foreach (RawWin w in wins)
            {
                livePids.Add(w.Pid);
                if (!w.Title.StartsWith("Roblox - ", StringComparison.OrdinalIgnoreCase)) continue;
                scanned++;
                if (!WindowOldEnough(w.Pid)) continue;
                oldEnough++;
                Bitmap bmp = CaptureWindow(w.Hwnd);
                if (bmp == null) continue;
                byte[] gray = SecretPixel.ToGray(bmp);
                int bw = bmp.Width;
                int bh = bmp.Height;
                if (SecretPixel.IsUniform(gray)) { bmp.Dispose(); continue; }

                int mx, my;
                double score;
                if (!SecretMatch.Find(gray, bw, bh, tpl, out mx, out my, out score)) { bmp.Dispose(); continue; }

                State st;
                if (!states.TryGetValue(w.Pid, out st)) { st = new State(); states[w.Pid] = st; }

                if (score >= Threshold)
                {
                    matched++;
                    st.GoneSince = DateTime.MinValue;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestName = ParseAccount(w.Title);
                        Bitmap prev = previewBmp;
                        previewBmp = Annotate(bmp, mx, my, offX, offY);
                        if (prev != null) prev.Dispose();
                    }

                    int cx = w.Rect.Left + mx + tpl.W / 2 + offX;
                    int cy = w.Rect.Top + my + tpl.H / 2 + offY;
                    string name = ParseAccount(w.Title);

                    if (!st.Detected)
                    {
                        st.Detected = true;
                        st.Attempts = 1;
                        if (allowClick) Click(w.Hwnd, cx, cy, holdMs);
                        Action<int, string, int, int> ev = Detected;
                        if (ev != null) ev(w.Pid, name, cx, cy);
                    }
                    else if (st.Attempts < maxTries)
                    {
                        st.Attempts++;
                        if (allowClick) Click(w.Hwnd, cx, cy, holdMs);
                        Log.Write("Detect Secrets: " + name + " still shows the image - retry " + st.Attempts + "/" + maxTries + ".");
                    }
                }
                else if (st.Detected)
                {
                    if (st.GoneSince == DateTime.MinValue) st.GoneSince = DateTime.UtcNow;
                    else if ((DateTime.UtcNow - st.GoneSince).TotalSeconds >= RearmSec)
                    {
                        st.Detected = false;
                        st.GoneSince = DateTime.MinValue;
                        st.Attempts = 0;
                    }
                }

                bmp.Dispose();
            }

            foreach (int pid in new List<int>(states.Keys))
                if (!livePids.Contains(pid)) states.Remove(pid);

            if (previewBmp != null)
            {
                RaisePreview(previewBmp, "Found in " + bestName + " at score " + bestScore.ToString("0.000")
                    + " · click " + (allowClick ? "ON" : "OFF") + " · hold " + holdMs + " ms · offset " + offX + "," + offY);
            }
            else
            {
                RaisePreview(null, "Scanned " + scanned + " Roblox window(s), " + oldEnough + " old enough, no match yet.");
            }
        }

        static void Click(IntPtr hwnd, int cx, int cy, int holdMs)
        {
            Native.POINT orig;
            bool haveOrig = Native.GetCursorPos(out orig);

            bool fg = Native.ForceForeground(hwnd);
            for (int i = 0; i < 3 && !fg; i++)
            {
                Thread.Sleep(200);
                fg = Native.ForceForeground(hwnd);
            }
            Thread.Sleep(150);

            Native.SetCursorPos(cx + 3, cy);
            Thread.Sleep(40);
            Native.SetCursorPos(cx, cy);
            Thread.Sleep(400);
            Native.MouseClickHold(cx, cy, holdMs);
            Thread.Sleep(50);

            if (haveOrig) Native.SetCursorPos(orig.X, orig.Y);
        }

        static Bitmap Annotate(Bitmap bmp, int mx, int my, int offX, int offY)
        {
            int cx = mx + tpl.W / 2 + offX;
            int cy = my + tpl.H / 2 + offY;

            int half = Math.Max(tpl.W, tpl.H) * 3;
            int x0 = Math.Max(0, cx - half);
            int y0 = Math.Max(0, cy - half);
            int x1 = Math.Min(bmp.Width, cx + half);
            int y1 = Math.Min(bmp.Height, cy + half);
            int cw = Math.Max(1, x1 - x0);
            int ch = Math.Max(1, y1 - y0);
            const int zoom = 2;

            Bitmap outBmp = new Bitmap(cw * zoom, ch * zoom, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(outBmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bmp, new Rectangle(0, 0, cw * zoom, ch * zoom), new Rectangle(x0, y0, cw, ch), GraphicsUnit.Pixel);

                int bx0 = (mx - x0) * zoom;
                int by0 = (my - y0) * zoom;
                int dotX = (cx - x0) * zoom;
                int dotY = (cy - y0) * zoom;

                using (Pen lime = new Pen(Color.Lime, 3))
                    g.DrawRectangle(lime, bx0, by0, tpl.W * zoom, tpl.H * zoom);
                using (SolidBrush red = new SolidBrush(Color.Red))
                    g.FillEllipse(red, dotX - 9, dotY - 9, 18, 18);
                using (Pen white = new Pen(Color.White, 2))
                    g.DrawEllipse(white, dotX - 9, dotY - 9, 18, 18);
            }
            return outBmp;
        }

        static void RaisePreview(Bitmap bmp, string text)
        {
            Action<Bitmap, string> ev = Preview;
            if (ev == null) { if (bmp != null) bmp.Dispose(); return; }
            try { ev(bmp, text); }
            catch (Exception) { if (bmp != null) bmp.Dispose(); }
        }

        static Bitmap CaptureWindow(IntPtr hwnd)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(hwnd, out r)) return null;
            int w = r.Width;
            int h = r.Height;
            if (w <= 0 || h <= 0) return null;

            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (new DpiScope())
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try { Native.PrintWindow(hwnd, hdc, Native.PW_RENDERFULLCONTENT); }
                finally { g.ReleaseHdc(hdc); }
            }
            return bmp;
        }

        static List<RawWin> FindWindowsRaw()
        {
            List<RawWin> result = new List<RawWin>();
            HashSet<int> pids = new HashSet<int>();
            foreach (Process p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
            if (pids.Count == 0) return result;

            Dictionary<int, RawWin> byPid = new Dictionary<int, RawWin>();
            using (new DpiScope())
            {
                Native.EnumWindows(delegate(IntPtr h, IntPtr l)
                {
                    uint pid;
                    Native.GetWindowThreadProcessId(h, out pid);
                    if (!pids.Contains((int)pid)) return true;
                    if (!Native.IsWindowVisible(h)) return true;
                    if (Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero) return true;
                    Native.RECT r;
                    if (!Native.GetWindowRect(h, out r)) return true;
                    if (Native.IsIconic(h)) return true;
                    if (r.Width < 120 || r.Height < 90) return true;

                    RawWin existing;
                    if (byPid.TryGetValue((int)pid, out existing))
                        if (existing.Rect.Width * existing.Rect.Height >= r.Width * r.Height) return true;

                    RawWin w = new RawWin();
                    w.Hwnd = h;
                    w.Pid = (int)pid;
                    w.Title = Native.GetTitle(h);
                    w.Rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    byPid[(int)pid] = w;
                    return true;
                }, IntPtr.Zero);
            }

            result.AddRange(byPid.Values);
            return result;
        }

        static string ParseAccount(string title)
        {
            if (string.IsNullOrEmpty(title)) return "(unknown)";
            if (title.Equals("Roblox", StringComparison.OrdinalIgnoreCase)) return "(not renamed)";
            if (title.StartsWith("Roblox - ", StringComparison.OrdinalIgnoreCase)) return title.Substring(9).Trim();
            return title;
        }

        static bool WindowOldEnough(int pid)
        {
            IntPtr h = Native.OpenProcess(0x1000, false, pid);
            if (h == IntPtr.Zero) return true;
            try
            {
                Native.FILETIME c, e, k, u;
                if (!Native.GetProcessTimes(h, out c, out e, out k, out u)) return true;
                DateTime start = DateTime.FromFileTime(((long)c.High << 32) | (long)c.Low);
                return (DateTime.Now - start).TotalSeconds >= 30;
            }
            finally { Native.CloseHandle(h); }
        }
    }
}
