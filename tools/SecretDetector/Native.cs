using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SecretDetector
{
    public class WinInfo
    {
        public IntPtr Hwnd;
        public int Pid;
        public string Title = "";
        public Rectangle Rect;
    }

    public static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

        public const uint GW_OWNER = 4;
        public const uint PW_RENDERFULLCONTENT = 0x00000002;
        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const int SW_RESTORE = 9;
        public const int SW_MINIMIZE = 6;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

        public static string GetTitle(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool ForceForeground(IntPtr h)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == h) return true;
            uint dummy;
            uint fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out dummy);
            uint me = GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
            try
            {
                BringWindowToTop(h);
                SetForegroundWindow(h);
            }
            finally { if (attached) AttachThreadInput(me, fgThread, false); }
            return GetForegroundWindow() == h;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion u; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int index);

        const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        public static void MouseClickHold(int x, int y)
        {
            int vx = GetSystemMetrics(76);
            int vy = GetSystemMetrics(77);
            int vw = Math.Max(2, GetSystemMetrics(78));
            int vh = Math.Max(2, GetSystemMetrics(79));
            int ax = (int)Math.Round((x - vx) * 65535.0 / (vw - 1));
            int ay = (int)Math.Round((y - vy) * 65535.0 / (vh - 1));
            SendMouse(ax, ay, 0x0001);
            Thread.Sleep(60);
            SendMouse(ax, ay, 0x0001 | 0x0002);
            Thread.Sleep(350);
            SendMouse(ax, ay, 0x0001 | 0x0004);
        }

        static void SendMouse(int ax, int ay, uint flags)
        {
            INPUT i = new INPUT();
            i.type = 0;
            i.u.mi.dx = ax;
            i.u.mi.dy = ay;
            i.u.mi.mouseData = 0;
            i.u.mi.dwFlags = flags | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;
            SendInput(1, new INPUT[] { i }, Marshal.SizeOf(typeof(INPUT)));
        }

        public static List<WinInfo> FindRobloxWindows()
        {
            List<WinInfo> result = new List<WinInfo>();
            HashSet<int> pids = new HashSet<int>();
            foreach (Process p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
            if (pids.Count == 0) return result;

            Dictionary<int, WinInfo> byPid = new Dictionary<int, WinInfo>();
            using (new DpiScope())
            {
                EnumWindows(delegate(IntPtr h, IntPtr l)
                {
                    uint pid;
                    GetWindowThreadProcessId(h, out pid);
                    if (!pids.Contains((int)pid)) return true;
                    if (!IsWindowVisible(h)) return true;
                    if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
                    RECT r;
                    if (!GetWindowRect(h, out r)) return true;
                    if (IsIconic(h)) return true;
                    if (r.Width < 120 || r.Height < 90) return true;

                    WinInfo existing;
                    if (byPid.TryGetValue((int)pid, out existing))
                        if (existing.Rect.Width * existing.Rect.Height >= r.Width * r.Height) return true;

                    WinInfo w = new WinInfo();
                    w.Hwnd = h;
                    w.Pid = (int)pid;
                    w.Title = GetTitle(h);
                    w.Rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    byPid[(int)pid] = w;
                    return true;
                }, IntPtr.Zero);
            }

            result.AddRange(byPid.Values);
            return result;
        }
    }

    public class DpiScope : IDisposable
    {
        IntPtr old = IntPtr.Zero;
        bool ok;

        public DpiScope()
        {
            try
            {
                old = Native.SetThreadDpiAwarenessContext(new IntPtr(-4));
                ok = old != IntPtr.Zero;
            }
            catch (Exception) { ok = false; }
        }

        public void Dispose()
        {
            if (!ok) return;
            try { Native.SetThreadDpiAwarenessContext(old); } catch (Exception) { }
        }
    }
}
