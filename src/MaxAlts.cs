using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RobloxMultiHub
{
    // Lang.T("Max alts"): keeps the Roblox windows you are NOT playing light on the PC. The client in the
    // foreground is left at full speed; every other client gets Idle CPU priority plus Windows'
    // own efficiency mode (EcoQoS), and now and then a memory trim. Nothing inside Roblox is
    // touched - only the standard Windows process controls that Task Manager also uses.
    public static class MaxAlts
    {
        const int ProcessPowerThrottling = 4;
        const uint EXECUTION_SPEED = 0x1;
        const uint PROCESS_SET_INFORMATION = 0x0200;
        const uint PROCESS_SET_QUOTA = 0x0100;

        static DateTime lastTrim = DateTime.MinValue;

        // Called from the main timer (every couple of seconds).
        public static void Tick()
        {
            if (!Store.Settings.MaxAlts) return;
            int active = ActiveRobloxPid();
            bool trimNow = (DateTime.UtcNow - lastTrim).TotalSeconds >= 20;
            bool trimmedAny = false;
            // ONE window enumeration for the whole tick (MinimizeIfNeeded used to enumerate PER PID:
            // nine clients meant nine full desktop walks every two seconds)
            List<WinInfo> wins = null;
            if (Store.Settings.MaxAltsMinimize)
            {
                try { using (new DpiScope()) wins = WindowLayout.FindRobloxWindows(); }
                catch (Exception) { wins = null; }
            }

            foreach (int pid in Launcher.RobloxPids())
            {
                bool isActive = pid == active;
                if (isActive)
                {
                    SetPriority(pid, ProcessPriorityClass.Normal);
                    SetEfficiency(pid, false);
                    continue;
                }
                if (Store.Settings.MaxAltsIdle)
                {
                    SetPriority(pid, ProcessPriorityClass.Idle);
                    SetEfficiency(pid, true);
                }
                if (wins != null) MinimizeIfNeeded(pid, wins);
                if (Store.Settings.MaxAltsTrim && trimNow) { TrimWorkingSet(pid); trimmedAny = true; }
            }
            if (trimmedAny) lastTrim = DateTime.UtcNow;
        }

        // Puts every Roblox client back to normal (called when the switch is turned off, or at shutdown).
        public static void Reset()
        {
            foreach (int pid in Launcher.RobloxPids())
            {
                SetPriority(pid, ProcessPriorityClass.Normal);
                SetEfficiency(pid, false);
            }
        }

        // The process id of the Roblox window that currently has the keyboard focus, or -1 if the
        // foreground window is not a Roblox client (then every client counts as a background one).
        public static int ActiveRobloxPid()
        {
            IntPtr fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero) return -1;
            uint pid;
            Native.GetWindowThreadProcessId(fg, out pid);
            foreach (int p in Launcher.RobloxPids())
                if (p == (int)pid) return p;
            return -1;
        }

        static void SetPriority(int pid, ProcessPriorityClass pc)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                    if (p.PriorityClass != pc) p.PriorityClass = pc;
            }
            catch (Exception) { }   // Roblox can protect its process; then we simply leave it alone
        }

        static void SetEfficiency(int pid, bool on)
        {
            IntPtr h = Native.OpenProcess(PROCESS_SET_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return;
            try
            {
                PROCESS_POWER_THROTTLING_STATE s = new PROCESS_POWER_THROTTLING_STATE();
                s.Version = 1;
                s.ControlMask = EXECUTION_SPEED;
                s.StateMask = on ? EXECUTION_SPEED : 0;
                SetProcessInformation(h, ProcessPowerThrottling, ref s, Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE)));
            }
            catch (Exception) { }
            finally { Native.CloseHandle(h); }
        }

        static void TrimWorkingSet(int pid)
        {
            IntPtr h = Native.OpenProcess(PROCESS_SET_QUOTA, false, pid);
            if (h == IntPtr.Zero) return;
            try { EmptyWorkingSet(h); }
            catch (Exception) { }
            finally { Native.CloseHandle(h); }
        }

        static void MinimizeIfNeeded(int pid, List<WinInfo> wins)
        {
            foreach (WinInfo w in wins)
                if (w.Pid == pid && w.HasWindow && !w.Minimized) { WindowLayout.Minimize(w.Hwnd); return; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetProcessInformation(IntPtr hProcess, int infoClass, ref PROCESS_POWER_THROTTLING_STATE info, int size);

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr process);
    }
}
