using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RobloxMultiHub
{
    public static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

        public const int SW_MINIMIZE = 6;
        public const int SW_RESTORE = 9;
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOSENDCHANGING = 0x0400;   // the window never gets WM_WINDOWPOSCHANGING, so it cannot clamp to its minimum size
        public const uint GW_OWNER = 4;
        public const uint WM_SETTEXT = 0x000C;
        public const int WM_HOTKEY = 0x0312;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
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
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

        public const uint WM_ACTIVATE = 0x0006;
        public const uint WM_SETFOCUS = 0x0007;
        public const uint WM_KILLFOCUS = 0x0008;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;

        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint WM_MOUSEMOVE = 0x0200;

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        public const uint MOUSEEVENTF_MOVE = 0x0001;

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        // SetForegroundWindow is refused when another program has the focus; borrowing the input queue of
        // the current foreground window makes Windows accept it.
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

        [DllImport("user32.dll", EntryPoint = "PostMessageW")]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out RECT rect, int size);

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint Low;
            public uint High;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        public static extern bool GetProcessTimes(IntPtr handle, out FILETIME creation, out FILETIME exit, out FILETIME kernel, out FILETIME user);

        // Total CPU time of a process in 100 ns units, or -1 if Windows will not tell us.
        // Uses the minimal "limited information" right, which protected processes still allow.
        public static long CpuTime100ns(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return -1;
            try
            {
                FILETIME c, e, k, u;
                if (!GetProcessTimes(h, out c, out e, out k, out u)) return -1;
                long kk = ((long)k.High << 32) | (long)k.Low;
                long uu = ((long)u.High << 32) | (long)u.Low;
                return kk + uu;
            }
            finally { CloseHandle(h); }
        }

        public static string GetTitle(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    // While alive, this thread reports real (physical) pixel coordinates, so window placement is exact
    // even on mixed-DPI multi-monitor setups. Silently does nothing on old Windows versions.
    public class DpiScope : IDisposable
    {
        IntPtr old = IntPtr.Zero;
        bool ok;

        public DpiScope()
        {
            try
            {
                old = Native.SetThreadDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2
                ok = old != IntPtr.Zero;
            }
            catch (Exception) { ok = false; }
        }

        public void Dispose()
        {
            if (!ok) return;
            try { Native.SetThreadDpiAwarenessContext(old); }
            catch (Exception) { }
        }
    }
}
