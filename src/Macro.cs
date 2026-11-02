using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RobloxMultiHub
{
    // One recorded input event. T = milliseconds since the recording started.
    public class MacroEvent
    {
        public const int Move = 0, Button = 1, Wheel = 2, HWheel = 3, Key = 4;
        public int T;
        public int Kind;
        public int A;       // Button: 1 left, 2 right, 3 middle, 4 X1, 5 X2 | Wheel: delta | Key: virtual key
        public int B;       // Button: 1 down, 0 up | Key: scan code
        public int C;       // Key: bit 0 = up, bit 1 = extended key
        public int X;       // mouse position (Move, Button, Wheel)
        public int Y;
    }

    // Records the mouse and keyboard with low-level hooks (like TinyTask). Input injected by programs -
    // including this app's own playback and anti-AFK - is ignored.
    public static class MacroRecorder
    {
        public static bool Recording { get; private set; }
        public static readonly List<MacroEvent> Events = new List<MacroEvent>();
        public static bool RecordMoves = true;
        public static bool HiRes = true;

        static IntPtr kbHook = IntPtr.Zero;
        static IntPtr msHook = IntPtr.Zero;
        static HookProc kbProc = KeyboardProc;          // kept in fields so the GC never collects them
        static HookProc msProc = MouseProc;
        static readonly Stopwatch clock = new Stopwatch();
        static int lastMoveT = -1000;

        public static bool Start()
        {
            if (Recording) return true;
            Events.Clear();
            lastMoveT = -1000;
            IntPtr mod = GetModuleHandle(null);
            kbHook = SetWindowsHookEx(13, kbProc, mod, 0);   // WH_KEYBOARD_LL
            msHook = SetWindowsHookEx(14, msProc, mod, 0);   // WH_MOUSE_LL
            if (kbHook == IntPtr.Zero || msHook == IntPtr.Zero) { Unhook(); return false; }
            clock.Restart();
            Recording = true;
            return true;
        }

        public static void Stop()
        {
            if (!Recording) return;
            Recording = false;
            Unhook();
            clock.Stop();
            Normalize(Events);
        }

        static void Unhook()
        {
            if (kbHook != IntPtr.Zero) { UnhookWindowsHookEx(kbHook); kbHook = IntPtr.Zero; }
            if (msHook != IntPtr.Zero) { UnhookWindowsHookEx(msHook); msHook = IntPtr.Zero; }
        }

        // Drops releases whose press happened before the recording started (the start hotkey / click) and
        // presses that were never released (the stop hotkey / click), so playback never leaves a key held.
        public static void Normalize(List<MacroEvent> ev)
        {
            HashSet<string> down = new HashSet<string>();
            List<MacroEvent> keep = new List<MacroEvent>();
            foreach (MacroEvent e in ev)
            {
                string id = PressId(e);
                if (id == null) { keep.Add(e); continue; }
                bool isDown = e.Kind == MacroEvent.Key ? (e.C & 1) == 0 : e.B == 1;
                if (isDown) { down.Add(id); keep.Add(e); }
                else if (down.Remove(id)) keep.Add(e);
            }
            for (int i = keep.Count - 1; i >= 0 && down.Count > 0; i--)
            {
                string id = PressId(keep[i]);
                if (id == null) continue;
                bool isDown = keep[i].Kind == MacroEvent.Key ? (keep[i].C & 1) == 0 : keep[i].B == 1;
                if (isDown && down.Remove(id)) keep.RemoveAt(i);
            }
            ev.Clear();
            ev.AddRange(keep);
        }

        static string PressId(MacroEvent e)
        {
            if (e.Kind == MacroEvent.Key) return "k" + e.A;
            if (e.Kind == MacroEvent.Button) return "b" + e.A;
            return null;
        }

        static IntPtr KeyboardProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && Recording)
            {
                KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                if ((k.flags & 0x10) == 0)                  // LLKHF_INJECTED
                {
                    MacroEvent e = new MacroEvent();
                    e.T = (int)clock.ElapsedMilliseconds;
                    e.Kind = MacroEvent.Key;
                    e.A = (int)k.vkCode;
                    e.B = (int)k.scanCode;
                    e.C = ((k.flags & 0x80) != 0 ? 1 : 0) | ((k.flags & 0x01) != 0 ? 2 : 0);   // LLKHF_UP, LLKHF_EXTENDED
                    Events.Add(e);
                }
            }
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        static IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && Recording)
            {
                MSLLHOOKSTRUCT m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                if ((m.flags & 0x01) == 0)                  // LLMHF_INJECTED
                {
                    int msg = wParam.ToInt32();
                    MacroEvent e = new MacroEvent();
                    e.T = (int)clock.ElapsedMilliseconds;
                    e.X = m.x;
                    e.Y = m.y;
                    short hi = (short)((m.mouseData >> 16) & 0xFFFF);
                    bool add = true;
                    switch (msg)
                    {
                        case 0x0200:                        // WM_MOUSEMOVE
                            e.Kind = MacroEvent.Move;
                            add = RecordMoves && (HiRes || e.T - lastMoveT >= 15);
                            if (add) lastMoveT = e.T;
                            break;
                        case 0x0201: e.Kind = MacroEvent.Button; e.A = 1; e.B = 1; break;
                        case 0x0202: e.Kind = MacroEvent.Button; e.A = 1; e.B = 0; break;
                        case 0x0204: e.Kind = MacroEvent.Button; e.A = 2; e.B = 1; break;
                        case 0x0205: e.Kind = MacroEvent.Button; e.A = 2; e.B = 0; break;
                        case 0x0207: e.Kind = MacroEvent.Button; e.A = 3; e.B = 1; break;
                        case 0x0208: e.Kind = MacroEvent.Button; e.A = 3; e.B = 0; break;
                        case 0x020B: e.Kind = MacroEvent.Button; e.A = hi == 2 ? 5 : 4; e.B = 1; break;
                        case 0x020C: e.Kind = MacroEvent.Button; e.A = hi == 2 ? 5 : 4; e.B = 0; break;
                        case 0x020A: e.Kind = MacroEvent.Wheel; e.A = hi; break;
                        case 0x020E: e.Kind = MacroEvent.HWheel; e.A = hi; break;
                        default: add = false; break;
                    }
                    if (add) Events.Add(e);
                }
            }
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        // ---------------- file format: a header line, then one event per line ----------------
        const string Header = "RobloxMultiHubMacro 1";

        public static void Save(string path, List<MacroEvent> ev)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Header);
            foreach (MacroEvent e in ev)
                sb.Append(e.T).Append(' ').Append(e.Kind).Append(' ').Append(e.A).Append(' ').Append(e.B).Append(' ')
                  .Append(e.C).Append(' ').Append(e.X).Append(' ').Append(e.Y).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
        }

        public static List<MacroEvent> Load(string path)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0].Trim() != Header) throw new InvalidDataException("This is not a Roblox MultiHub macro file.");
            List<MacroEvent> ev = new List<MacroEvent>();
            for (int i = 1; i < lines.Length; i++)
            {
                string[] p = lines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 7) continue;
                MacroEvent e = new MacroEvent();
                e.T = int.Parse(p[0], CultureInfo.InvariantCulture);
                e.Kind = int.Parse(p[1], CultureInfo.InvariantCulture);
                e.A = int.Parse(p[2], CultureInfo.InvariantCulture);
                e.B = int.Parse(p[3], CultureInfo.InvariantCulture);
                e.C = int.Parse(p[4], CultureInfo.InvariantCulture);
                e.X = int.Parse(p[5], CultureInfo.InvariantCulture);
                e.Y = int.Parse(p[6], CultureInfo.InvariantCulture);
                ev.Add(e);
            }
            return ev;
        }

        public static int LengthMs(List<MacroEvent> ev) { return ev.Count == 0 ? 0 : ev[ev.Count - 1].T; }

        // ---------------- native ----------------
        delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr extra; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSLLHOOKSTRUCT { public int x; public int y; public uint mouseData; public uint flags; public uint time; public IntPtr extra; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr mod, uint threadId);

        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);
    }

    // Plays a recording back with SendInput on its own thread: at a chosen speed, once, N times or until stopped.
    public class MacroPlayer
    {
        public double Speed = 1.0;
        public int Repeats = 1;                 // ignored when Continuous
        public bool Continuous;
        public int PauseMs;                     // pause between two repeats
        public event Action<int> RepeatStarted; // 1-based
        public event Action Finished;

        volatile bool stop;
        Thread thread;
        readonly HashSet<int> keysDown = new HashSet<int>();
        readonly HashSet<int> buttonsDown = new HashSet<int>();

        public bool Playing { get { return thread != null && thread.IsAlive; } }

        public void Start(List<MacroEvent> events)
        {
            if (Playing || events.Count == 0) return;
            List<MacroEvent> copy = events.ToList();
            stop = false;
            thread = new Thread(delegate() { Run(copy); });
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.AboveNormal;
            thread.Start();
        }

        public void Stop() { stop = true; }

        void Run(List<MacroEvent> ev)
        {
            timeBeginPeriod(1);
            try
            {
                double speed = Math.Max(0.01, Speed);
                for (int rep = 1; Continuous || rep <= Repeats; rep++)
                {
                    if (stop) break;
                    if (RepeatStarted != null) RepeatStarted(rep);
                    Stopwatch sw = Stopwatch.StartNew();
                    foreach (MacroEvent e in ev)
                    {
                        if (!WaitUntil(sw, e.T / speed)) break;
                        Send(e);
                    }
                    ReleaseAll();
                    if (stop) break;
                    if ((Continuous || rep < Repeats) && PauseMs > 0 && !WaitUntil(Stopwatch.StartNew(), PauseMs)) break;
                }
            }
            catch (Exception ex) { Log.Error("MacroPlayer", ex); }
            finally
            {
                ReleaseAll();
                timeEndPeriod(1);
                if (Finished != null) Finished();
            }
        }

        // Sleeps until the stopwatch reaches 'ms', in small steps so Stop reacts at once. False = stopped.
        bool WaitUntil(Stopwatch sw, double ms)
        {
            while (true)
            {
                if (stop) return false;
                double left = ms - sw.Elapsed.TotalMilliseconds;
                if (left <= 0) return true;
                if (left > 3) Thread.Sleep((int)Math.Min(50, left - 2));
                else Thread.Yield();
            }
        }

        void Send(MacroEvent e)
        {
            if (e.Kind == MacroEvent.Key)
            {
                bool up = (e.C & 1) != 0;
                uint flags = (up ? 0x0002u : 0u) | ((e.C & 2) != 0 ? 0x0001u : 0u);   // KEYEVENTF_KEYUP, KEYEVENTF_EXTENDEDKEY
                SendKey((ushort)e.A, (ushort)e.B, flags);
                if (up) keysDown.Remove(e.A); else keysDown.Add(e.A);
                return;
            }
            if (e.Kind == MacroEvent.Move) { SendMouse(e.X, e.Y, 0x0001, 0); return; }   // MOUSEEVENTF_MOVE
            if (e.Kind == MacroEvent.Wheel) { SendMouse(e.X, e.Y, 0x0001 | 0x0800, e.A); return; }
            if (e.Kind == MacroEvent.HWheel) { SendMouse(e.X, e.Y, 0x0001 | 0x01000, e.A); return; }
            if (e.Kind == MacroEvent.Button)
            {
                bool down = e.B == 1;
                uint f;
                int data = 0;
                switch (e.A)
                {
                    case 1: f = down ? 0x0002u : 0x0004u; break;
                    case 2: f = down ? 0x0008u : 0x0010u; break;
                    case 3: f = down ? 0x0020u : 0x0040u; break;
                    default: f = down ? 0x0080u : 0x0100u; data = e.A == 5 ? 2 : 1; break;
                }
                SendMouse(e.X, e.Y, 0x0001 | f, data);                // move to where it was clicked, then click
                if (down) buttonsDown.Add(e.A); else buttonsDown.Remove(e.A);
            }
        }

        void ReleaseAll()
        {
            foreach (int vk in keysDown.ToList()) SendKey((ushort)vk, 0, 0x0002);
            keysDown.Clear();
            foreach (int b in buttonsDown.ToList())
            {
                uint f = b == 1 ? 0x0004u : b == 2 ? 0x0010u : b == 3 ? 0x0040u : 0x0100u;
                SendInputs(MouseInput(0, 0, f, b == 5 ? 2 : (b == 4 ? 1 : 0)));
            }
            buttonsDown.Clear();
        }

        static void SendKey(ushort vk, ushort scan, uint flags)
        {
            INPUT i = new INPUT();
            i.type = 1;
            i.u.ki.wVk = vk;
            i.u.ki.wScan = scan;
            i.u.ki.dwFlags = flags;
            SendInputs(i);
        }

        // Absolute position on the whole virtual desktop (all monitors).
        static void SendMouse(int x, int y, uint flags, int data)
        {
            int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77);
            int vw = Math.Max(2, GetSystemMetrics(78)), vh = Math.Max(2, GetSystemMetrics(79));
            int ax = (int)Math.Round((x - vx) * 65535.0 / (vw - 1));
            int ay = (int)Math.Round((y - vy) * 65535.0 / (vh - 1));
            SendInputs(MouseInput(ax, ay, flags | 0x8000 | 0x4000, data));   // MOUSEEVENTF_ABSOLUTE | VIRTUALDESK
        }

        static INPUT MouseInput(int dx, int dy, uint flags, int data)
        {
            INPUT i = new INPUT();
            i.type = 0;
            i.u.mi.dx = dx;
            i.u.mi.dy = dy;
            i.u.mi.mouseData = unchecked((uint)data);
            i.u.mi.dwFlags = flags;
            return i;
        }

        static void SendInputs(INPUT i)
        {
            SendInput(1, new INPUT[] { i }, Marshal.SizeOf(typeof(INPUT)));
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

        [DllImport("winmm.dll")]
        static extern uint timeBeginPeriod(uint ms);

        [DllImport("winmm.dll")]
        static extern uint timeEndPeriod(uint ms);
    }
}
