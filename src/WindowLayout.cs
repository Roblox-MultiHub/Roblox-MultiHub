using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public class WinInfo
    {
        public IntPtr Hwnd;
        public int Pid;
        public string Title = "";
        public Rectangle Rect;
        public bool HasWindow;
        public string Account = "";
        public bool Minimized;
    }

    public class MonitorDesc
    {
        public string Device = "";
        public string Label = "";
        public Rectangle Bounds;
        public Rectangle Work;
        public bool Primary;
    }

    public static class WindowLayout
    {
        // Roblox clamps its window to >= 800x600 client size (816 x 638 with a frame) while it processes
        // WM_WINDOWPOSCHANGING / WM_GETMINMAXINFO. SetWindowPos with SWP_NOSENDCHANGING skips that message, so the
        // window can be made smaller. Borderless windows are not asked for a minimum size at all - the fallback.
        public const int FramedMinW = 816;
        public const int FramedMinH = 638;

        // Original window styles, so Lang.T("Restore borders") can undo borderless mode.
        public static readonly Dictionary<IntPtr, int> OriginalStyles = new Dictionary<IntPtr, int>();

        // ---------------- monitors ----------------
        public static List<MonitorDesc> GetMonitors()
        {
            List<MonitorDesc> list = new List<MonitorDesc>();
            using (new DpiScope())
            {
                Native.MonitorEnumProc cb = delegate(IntPtr hMon, IntPtr hdc, IntPtr rc, IntPtr data)
                {
                    Native.MONITORINFOEX mi = new Native.MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFOEX));
                    if (Native.GetMonitorInfo(hMon, ref mi))
                    {
                        MonitorDesc m = new MonitorDesc();
                        m.Device = mi.szDevice ?? "";
                        m.Bounds = Rectangle.FromLTRB(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
                        m.Work = Rectangle.FromLTRB(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);
                        m.Primary = (mi.dwFlags & 1) != 0;
                        list.Add(m);
                    }
                    return true;
                };
                Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
            }
            if (list.Count == 0)
            {
                MonitorDesc m = new MonitorDesc();
                m.Device = "PRIMARY";
                m.Bounds = Screen.PrimaryScreen.Bounds;
                m.Work = Screen.PrimaryScreen.WorkingArea;
                m.Primary = true;
                list.Add(m);
            }
            list.Sort(delegate(MonitorDesc x, MonitorDesc y)
            {
                if (x.Primary != y.Primary) return x.Primary ? -1 : 1;
                return x.Bounds.Left.CompareTo(y.Bounds.Left);
            });
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Label = "Monitor " + (i + 1) + "  -  " + list[i].Bounds.Width + " x " + list[i].Bounds.Height
                    + (list[i].Primary ? "  (primary)" : "");
            }
            return list;
        }

        public static MonitorDesc PickMonitor(List<MonitorDesc> all, string device)
        {
            foreach (MonitorDesc m in all)
                if (!string.IsNullOrEmpty(device) && m.Device == device) return m;
            foreach (MonitorDesc m in all)
                if (m.Primary) return m;
            return all[0];
        }

        // ---------------- finding Roblox windows ----------------
        public static List<WinInfo> FindRobloxWindows()
        {
            List<WinInfo> result = new List<WinInfo>();
            Dictionary<uint, WinInfo> byPid = new Dictionary<uint, WinInfo>();
            List<int> pidList = Launcher.RobloxPids();
            if (pidList.Count == 0) return result;
            HashSet<uint> pids = new HashSet<uint>();
            foreach (int p in pidList) pids.Add((uint)p);

            using (new DpiScope())
            {
                Native.EnumWindows(delegate(IntPtr h, IntPtr l)
                {
                    uint pid;
                    Native.GetWindowThreadProcessId(h, out pid);
                    if (!pids.Contains(pid)) return true;
                    if (!Native.IsWindowVisible(h)) return true;
                    if (Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero) return true;
                    Native.RECT r;
                    if (!Native.GetWindowRect(h, out r)) return true;
                    bool min = Native.IsIconic(h);
                    if (!min && (r.Width < 120 || r.Height < 90)) return true;
                    WinInfo existing;
                    if (byPid.TryGetValue(pid, out existing))
                    {
                        // keep the biggest top-level window of that process
                        if (existing.Rect.Width * existing.Rect.Height >= r.Width * r.Height) return true;
                    }
                    WinInfo w = new WinInfo();
                    w.Hwnd = h;
                    w.Pid = (int)pid;
                    w.Title = Native.GetTitle(h);
                    w.Rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    w.HasWindow = true;
                    w.Minimized = min;
                    byPid[pid] = w;
                    return true;
                }, IntPtr.Zero);
            }

            foreach (int p in pidList)
            {
                WinInfo w;
                if (byPid.TryGetValue((uint)p, out w)) result.Add(w);
                else
                {
                    WinInfo ghost = new WinInfo();
                    ghost.Pid = p;
                    ghost.HasWindow = false;
                    result.Add(ghost);
                }
            }
            foreach (WinInfo w in result)
            {
                Instance inst = Launcher.FindInstance(w.Pid);
                if (inst != null) w.Account = AppState.Name(inst.Account);
            }
            return result;
        }

        // ---------------- layout math ----------------
        // minW / minH = the smallest size Roblox accepts for its window (0 = not measured yet).
        // When the grid cells would be smaller than that, the windows cannot be tiled any more:
        // they are then spread out WITH overlap (overlap = true), each one showing its own corner.
        public static List<Rectangle> ComputeRects(int n, Rectangle area, LayoutSettings s, int minW, int minH, out bool overlap)
        {
            overlap = false;
            List<Rectangle> res = new List<Rectangle>();
            if (n <= 0) return res;
            int margin = Math.Max(0, s.Margin);
            Rectangle a = Rectangle.FromLTRB(area.Left + margin, area.Top + margin, area.Right - margin, area.Bottom - margin);
            if (a.Width < 100 || a.Height < 100) a = area;
            int gap = Math.Max(0, s.Gap);

            if (s.Mode == "FixedSize")
            {
                int fw = Math.Max(Math.Max(200, s.FixedW), minW);
                int fh = Math.Max(Math.Max(150, s.FixedH), minH);
                int x = a.Left;
                int y = a.Top;
                int off = 0;
                for (int i = 0; i < n; i++)
                {
                    if (x + fw > a.Right && x > a.Left + off) { x = a.Left + off; y += fh + gap; }
                    if (y + fh > a.Bottom && y > a.Top + off) { off += 32; x = a.Left + off; y = a.Top + off; overlap = true; }
                    res.Add(new Rectangle(x, y, fw, fh));
                    x += fw + gap;
                }
                return res;
            }

            int cols;
            int rows;
            GridSize(n, a, gap, s, minW, minH, out cols, out rows);
            double cellW = (a.Width - gap * (cols - 1)) / (double)cols;
            double cellH = (a.Height - gap * (rows - 1)) / (double)rows;
            int usedRows = (n + cols - 1) / cols;

            if (cellW + 0.5 >= minW && cellH + 0.5 >= minH)
            {
                int scale = Math.Min(100, Math.Max(20, s.Scale));
                for (int i = 0; i < n; i++)
                {
                    int row = i / cols;
                    int col = i % cols;
                    int colsThisRow = cols;
                    if (s.StretchLast && row == usedRows - 1)
                    {
                        int cnt = n - row * cols;
                        if (cnt > 0 && cnt < cols) colsThisRow = cnt;
                    }
                    int top = a.Top + (int)Math.Round(row * (a.Height + gap) / (double)rows);
                    int bottom = a.Top + (int)Math.Round((row + 1) * (a.Height + gap) / (double)rows) - gap;
                    int left = a.Left + (int)Math.Round(col * (a.Width + gap) / (double)colsThisRow);
                    int right = a.Left + (int)Math.Round((col + 1) * (a.Width + gap) / (double)colsThisRow) - gap;
                    Rectangle cell = Rectangle.FromLTRB(left, top, right, bottom);
                    if (scale < 100)
                    {
                        int w = Math.Max(minW, cell.Width * scale / 100);
                        int h = Math.Max(minH, cell.Height * scale / 100);
                        w = Math.Min(w, cell.Width);
                        h = Math.Min(h, cell.Height);
                        cell = new Rectangle(cell.Left + (cell.Width - w) / 2, cell.Top + (cell.Height - h) / 2, w, h);
                    }
                    res.Add(cell);
                }
                return res;
            }

            // Not enough room for windows of the minimum size: spread them evenly with overlap.
            // Later windows are brought to the front by the caller, so every window shows the corner of its own cell.
            overlap = true;
            int ow = Math.Max(minW, 200);
            int oh = Math.Max(minH, 150);
            for (int i = 0; i < n; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int x = (cols > 1 && a.Width > ow) ? a.Left + (int)Math.Round(col * (double)(a.Width - ow) / (cols - 1)) : a.Left;
                int y = (rows > 1 && a.Height > oh) ? a.Top + (int)Math.Round(row * (double)(a.Height - oh) / (rows - 1)) : a.Top;
                res.Add(new Rectangle(x, y, ow, oh));
            }
            return res;
        }

        // How many windows of the minimum size fit on the area without overlapping (0 = minimum not measured yet).
        public static int Capacity(Rectangle area, LayoutSettings s, int minW, int minH)
        {
            if (minW <= 0 || minH <= 0) return 0;
            int margin = Math.Max(0, s.Margin);
            int gap = Math.Max(0, s.Gap);
            int aw = area.Width - 2 * margin;
            int ah = area.Height - 2 * margin;
            int c = Math.Max(1, (aw + gap) / (minW + gap));
            int r = Math.Max(1, (ah + gap) / (minH + gap));
            return c * r;
        }

        public static void GridSize(int n, Rectangle a, int gap, LayoutSettings s, int minW, int minH, out int cols, out int rows)
        {
            if (s.Mode == "Grid" && (s.Cols > 0 || s.Rows > 0))
            {
                if (s.Cols > 0 && s.Rows > 0)
                {
                    cols = s.Cols;
                    rows = s.Rows;
                    if (cols * rows < n) rows = (n + cols - 1) / cols;
                }
                else if (s.Cols > 0)
                {
                    cols = s.Cols;
                    rows = (n + cols - 1) / cols;
                }
                else
                {
                    rows = s.Rows;
                    cols = (n + rows - 1) / rows;
                }
                return;
            }
            // Automatic: the grid whose cells are closest to a comfortable 3:2 shape.
            // Grids whose cells are big enough for Roblox's minimum window size are preferred.
            double bestFeasible = double.MaxValue;
            double bestAny = double.MaxValue;
            int fc = 0;
            int fr = 0;
            int ac = 1;
            int ar = n;
            for (int c = 1; c <= n; c++)
            {
                int r = (n + c - 1) / c;
                double cw = (a.Width - gap * (c - 1)) / (double)c;
                double ch = (a.Height - gap * (r - 1)) / (double)r;
                if (cw < 1 || ch < 1) continue;
                int empty = c * r - n;
                double score = Math.Abs(Math.Log(cw / ch) - Math.Log(1.5)) + 0.05 * empty;
                bool feasible = cw + 0.5 >= minW && ch + 0.5 >= minH;
                if (feasible && score < bestFeasible - 1e-9) { bestFeasible = score; fc = c; fr = r; }
                if (score < bestAny - 1e-9) { bestAny = score; ac = c; ar = r; }
            }
            if (fc > 0) { cols = fc; rows = fr; }
            else { cols = ac; rows = ar; }
        }

        // ---------------- moving windows ----------------
        static void FrameDelta(IntPtr h, out int l, out int t, out int r, out int b)
        {
            l = 0; t = 0; r = 0; b = 0;
            try
            {
                Native.RECT wr;
                Native.RECT fr;
                if (!Native.GetWindowRect(h, out wr)) return;
                if (Native.DwmGetWindowAttribute(h, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out fr, Marshal.SizeOf(typeof(Native.RECT))) != 0) return;
                l = fr.Left - wr.Left;
                t = fr.Top - wr.Top;
                r = wr.Right - fr.Right;
                b = wr.Bottom - fr.Bottom;
                if (l < 0 || t < 0 || r < 0 || b < 0 || l > 40 || r > 40 || b > 40 || t > 40) { l = 0; t = 0; r = 0; b = 0; }
            }
            catch (Exception) { l = 0; t = 0; r = 0; b = 0; }
        }

        // Moves one window and returns the rectangle it REALLY ended up with (Roblox refuses to go below a
        // minimum size, so the result can be bigger than asked). toFront stacks windows in call order.
        public static Rectangle Place(WinInfo w, Rectangle target, LayoutSettings s, bool toFront)
        {
            IntPtr h = w.Hwnd;
            if (h == IntPtr.Zero || !Native.IsWindow(h)) return target;
            if (Native.IsIconic(h) || Native.IsZoomed(h))
            {
                Native.ShowWindow(h, Native.SW_RESTORE);
                Thread.Sleep(80);
            }
            if (s.Borderless)
            {
                int style = Native.GetWindowLong(h, Native.GWL_STYLE);
                if (!OriginalStyles.ContainsKey(h)) OriginalStyles[h] = style;
                int ns = style & ~(Native.WS_CAPTION | Native.WS_THICKFRAME);
                if (ns != style)
                {
                    Native.SetWindowLong(h, Native.GWL_STYLE, ns);
                    Native.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
                }
            }
            int dl, dt, dr, db;
            FrameDelta(h, out dl, out dt, out dr, out db);
            int x = target.X - dl;
            int y = target.Y - dt;
            int cw = target.Width + dl + dr;
            int ch = target.Height + dt + db;

            bool isTop = (Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0;
            IntPtr after = IntPtr.Zero;
            uint flags = Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW | Native.SWP_NOSENDCHANGING;
            if (s.AlwaysOnTop) after = Native.HWND_TOPMOST;
            else if (isTop) after = Native.HWND_NOTOPMOST;
            else if (!toFront) flags |= Native.SWP_NOZORDER;
            Native.SetWindowPos(h, after, x, y, cw, ch, flags);

            Native.RECT rr;
            if (Native.GetWindowRect(h, out rr))
                return Rectangle.FromLTRB(rr.Left + dl, rr.Top + dt, rr.Right - dr, rr.Bottom - db);
            return target;
        }

        // The visible rectangle of a window (without the invisible resize border), in the same
        // coordinates Place() takes. Call it inside a DpiScope.
        public static Rectangle VisibleRect(IntPtr h)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(h, out r)) return Rectangle.Empty;
            int dl, dt, dr, db;
            FrameDelta(h, out dl, out dt, out dr, out db);
            return Rectangle.FromLTRB(r.Left + dl, r.Top + dt, r.Right - dr, r.Bottom - db);
        }

        public static bool IsBorderless(IntPtr h)
        {
            return (Native.GetWindowLong(h, Native.GWL_STYLE) & Native.WS_CAPTION) != Native.WS_CAPTION;
        }

        public static void RestoreBorders()
        {
            foreach (KeyValuePair<IntPtr, int> kv in new List<KeyValuePair<IntPtr, int>>(OriginalStyles))
            {
                if (!Native.IsWindow(kv.Key)) continue;
                Native.SetWindowLong(kv.Key, Native.GWL_STYLE, kv.Value);
                Native.SetWindowPos(kv.Key, IntPtr.Zero, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
            }
            OriginalStyles.Clear();
        }

        public static void SetTitle(IntPtr h, string title)
        {
            try
            {
                IntPtr res;
                Native.SendMessageTimeout(h, Native.WM_SETTEXT, IntPtr.Zero, title, Native.SMTO_ABORTIFHUNG, 500, out res);
            }
            catch (Exception) { }
        }

        public static void Focus(IntPtr h)
        {
            if (h == IntPtr.Zero) return;
            if (Native.IsIconic(h)) Native.ShowWindow(h, Native.SW_RESTORE);
            Native.SetForegroundWindow(h);
        }

        public static void Minimize(IntPtr h)
        {
            if (h != IntPtr.Zero) Native.ShowWindow(h, Native.SW_MINIMIZE);
        }
    }
}
