using System;
using System.Threading.Tasks;

namespace RobloxMultiHub
{
    // Keeps accounts from being kicked for idling (Roblox kicks after about 20 minutes without input).
    // Every X minutes each Roblox window started by this app is brought to the front for a fraction of a
    // second, gets a small input (exactly like your own mouse / keyboard), and the window you were using is
    // put back. "Mouse nudge" is the default: it resets the idle timer without moving your character, so it
    // does not interrupt things like auto-hatch. "Jump" presses Space (older behaviour).
    public static class AntiAfk
    {
        public const int DefaultMin = 12;
        static bool busy;

        public static readonly string[] ActionNames = new string[]
        {
            "Mouse nudge  (no gameplay effect - recommended)",
            "Jump  (press Space)",
            "Zoom camera  (press I then O)"
        };
        public static readonly string[] ActionIds = new string[] { "Nudge", "Space", "Zoom" };

        public static async Task<int> Pulse(Action<string> log)
        {
            if (busy) return 0;
            busy = true;
            int n = 0;
            IntPtr fg = Native.GetForegroundWindow();
            try
            {
                foreach (WinInfo w in WindowLayout.FindRobloxWindows())
                {
                    if (!w.HasWindow || w.Hwnd == fg) continue;              // the window you are using needs nothing
                    Instance inst = Launcher.FindInstance(w.Pid);
                    if (inst == null) continue;                               // only windows this app started
                    if (await FocusPoke(w.Hwnd, w.Minimized)) n++;
                    else log("Anti-AFK: could not reach " + AppState.Name(inst.Account) + " (Windows refused to bring it to the front).");
                    await Task.Delay(50);
                }
            }
            catch (Exception ex) { Log.Error("AntiAfk", ex); }
            if (fg != IntPtr.Zero && Native.IsWindow(fg)) Native.ForceForeground(fg);   // give you your window back
            busy = false;
            return n;
        }

        // Front for ~0.3 s, one small input, then on to the next window. Minimized windows are minimized again.
        static async Task<bool> FocusPoke(IntPtr h, bool minimized)
        {
            if (!Native.IsWindow(h)) return false;
            if (minimized) Native.ShowWindow(h, Native.SW_RESTORE);
            if (!Native.ForceForeground(h))
            {
                await Task.Delay(100);
                if (!Native.ForceForeground(h)) { if (minimized) Native.ShowWindow(h, Native.SW_MINIMIZE); return false; }
            }
            await Task.Delay(120);                          // let Roblox notice it has the focus
            await DoAction(h);
            await Task.Delay(80);
            if (minimized) Native.ShowWindow(h, Native.SW_MINIMIZE);
            return true;
        }

        static async Task DoAction(IntPtr h)
        {
            string act = Store.Settings.AntiAfkAction ?? "Nudge";
            if (act == "Space")
            {
                await Key(0x20, 0x39);                      // Space = jump
            }
            else if (act == "Zoom")
            {
                await Key(0x49, 0x17);                      // I = zoom in
                await Task.Delay(60);
                await Key(0x4F, 0x18);                      // O = zoom out (back to where it was)
            }
            else
            {
                // Mouse nudge. Roblox only sees mouse movement that happens OVER its own window, so first move
                // the cursor to the middle of this window (the old code nudged wherever the cursor happened to
                // be, which did nothing unless it was already on top of the window), nudge a few pixels, then
                // put the cursor back where you had it. No character movement, so auto-hatch / auto-farm keeps
                // running.
                Native.POINT orig;
                bool haveOrig = Native.GetCursorPos(out orig);
                Native.RECT r;
                if (Native.GetWindowRect(h, out r) && r.Width > 20 && r.Height > 20)
                {
                    int cx = (r.Left + r.Right) / 2;
                    int cy = (r.Top + r.Bottom) / 2;
                    Native.SetCursorPos(cx, cy);
                    await Task.Delay(30);
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE, 4, 2, 0, UIntPtr.Zero);
                    await Task.Delay(40);
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE, -4, -2, 0, UIntPtr.Zero);
                    await Task.Delay(20);
                    if (haveOrig) Native.SetCursorPos(orig.X, orig.Y);   // restore your cursor
                }
                else
                {
                    // fallback if the window rectangle is unavailable: plain relative nudge
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE, 4, 0, 0, UIntPtr.Zero);
                    await Task.Delay(40);
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE, -4, 0, 0, UIntPtr.Zero);
                }
            }
        }

        static async Task Key(byte vk, byte scan)
        {
            Native.keybd_event(vk, scan, 0, UIntPtr.Zero);
            await Task.Delay(70);
            Native.keybd_event(vk, scan, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
