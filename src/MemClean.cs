using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RobloxMultiHub
{
    // Result of the last clean, shared between the admin helper and the app through a small file.
    public class MemStatus
    {
        public int Pid;
        public long Beat;                   // heartbeat of the helper (UTC ticks)
        public long LastTicks;              // when the last clean happened (UTC ticks)
        public long LastFreed;              // bytes of RAM that became free
        public int LoadBefore;
        public int LoadAfter;
        public string Reason = "";
        public string Errors = "";
        public int Count;
    }

    // RAM cleaner in the style of Mem Reduct. Emptying memory lists needs administrator rights, so the
    // hub itself stays a normal program and starts a tiny copy of itself as administrator ("--memhelper")
    // that watches the RAM use and cleans when it goes over the limit. It stops when the hub closes.
    public static class MemClean
    {
        public static string StatusPath { get { return Path.Combine(Store.Dir, "memclean.json"); } }
        public static string RequestPath { get { return Path.Combine(Store.Dir, "memclean.request"); } }
        const int CooldownSec = 60;         // never auto-clean more than once a minute

        // ---------------- memory numbers (no admin needed) ----------------
        public static bool Status(out int load, out long total, out long avail)
        {
            MEMORYSTATUSEX m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            load = 0; total = 0; avail = 0;
            if (!GlobalMemoryStatusEx(ref m)) return false;
            load = (int)m.dwMemoryLoad;
            total = (long)m.ullTotalPhys;
            avail = (long)m.ullAvailPhys;
            return true;
        }

        public static bool IsElevated()
        {
            try
            {
                using (System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent())
                    return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception) { return false; }
        }

        // ---------------- cleaning (admin) ----------------
        public static MemStatus Clean(MemSettings s, string reason)
        {
            MemStatus st = ReadStatus() ?? new MemStatus();
            int load0; long total; long avail0;
            Status(out load0, out total, out avail0);
            List<string> errors = new List<string>();
            EnablePrivilege("SeProfileSingleProcessPrivilege");
            EnablePrivilege("SeIncreaseQuotaPrivilege");

            // same order as Mem Reduct: working sets, file cache, modified pages, standby list, combine
            if (s.WorkingSets) Command(2, "working sets", errors);                 // MemoryEmptyWorkingSets
            if (s.FileCache)
            {
                if (!SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0)) errors.Add("file cache (" + Marshal.GetLastWin32Error() + ")");
            }
            if (s.ModifiedList) Command(3, "modified pages", errors);              // MemoryFlushModifiedList
            if (s.StandbyFull) Command(4, "standby list", errors);                 // MemoryPurgeStandbyList
            else if (s.StandbyLow) Command(5, "low-priority standby list", errors); // MemoryPurgeLowPriorityStandbyList
            if (s.Combine)
            {
                try
                {
                    MEMORY_COMBINE_INFORMATION_EX c = new MEMORY_COMBINE_INFORMATION_EX();
                    int r = NtSetSystemInformation(130, ref c, Marshal.SizeOf(typeof(MEMORY_COMBINE_INFORMATION_EX)));   // SystemCombinePhysicalMemoryInformation
                    if (r != 0) errors.Add("combine memory (0x" + r.ToString("X8") + ")");
                }
                catch (Exception) { errors.Add("combine memory (not supported on this Windows)"); }
            }

            int load1; long avail1;
            Status(out load1, out total, out avail1);
            st.LastTicks = DateTime.UtcNow.Ticks;
            st.LastFreed = Math.Max(0, avail1 - avail0);
            st.LoadBefore = load0;
            st.LoadAfter = load1;
            st.Reason = reason;
            st.Errors = string.Join(", ", errors.ToArray());
            st.Count++;
            return st;
        }

        static void Command(int cmd, string what, List<string> errors)
        {
            int c = cmd;
            int r = NtSetSystemInformation(80, ref c, 4);   // SystemMemoryListInformation
            if (r != 0) errors.Add(what + " (0x" + r.ToString("X8") + ")");
        }

        // Without admin rights only programs of your own user can be trimmed - used for the Roblox windows.
        public static long TrimProcesses(IEnumerable<int> pids)
        {
            int l0; long t; long a0;
            Status(out l0, out t, out a0);
            foreach (int pid in pids)
            {
                IntPtr h = OpenProcess(0x1000 | 0x0100, false, pid);   // QUERY_LIMITED_INFORMATION | SET_QUOTA
                if (h == IntPtr.Zero) continue;
                try { EmptyWorkingSet(h); } finally { CloseHandle(h); }
            }
            int l1; long a1;
            Status(out l1, out t, out a1);
            return Math.Max(0, a1 - a0);
        }

        // ---------------- the admin helper process ----------------
        public static void RunHelper(int parentPid)
        {
            bool created;
            Mutex one = new Mutex(true, "RobloxMultiHub_MemHelper", out created);
            if (!created) return;                           // a helper is already running
            try
            {
                DateTime lastAuto = DateTime.MinValue;
                DateTime lastTimer = DateTime.UtcNow;
                DateTime settingsTime = DateTime.MinValue;
                int beat = 0;
                while (ParentAlive(parentPid))
                {
                    try
                    {
                        DateTime wt = File.GetLastWriteTimeUtc(Store.SettingsPath);
                        if (wt != settingsTime) { settingsTime = wt; Store.LoadSettings(); }   // the hub changed a setting
                        MemSettings s = Store.Settings.Mem;
                        MemStatus done = null;
                        if (File.Exists(RequestPath))
                        {
                            try { File.Delete(RequestPath); } catch (Exception) { }
                            done = Clean(s, "manual");
                        }
                        else if (s.AutoClean)
                        {
                            int load; long total; long avail;
                            Status(out load, out total, out avail);
                            if (load >= s.Threshold && (DateTime.UtcNow - lastAuto).TotalSeconds >= CooldownSec)
                                done = Clean(s, "auto: " + load + "% in use");
                            else if (s.IntervalMin > 0 && (DateTime.UtcNow - lastTimer).TotalMinutes >= s.IntervalMin)
                                done = Clean(s, "every " + s.IntervalMin + " min");
                        }
                        if (done != null)
                        {
                            lastAuto = DateTime.UtcNow;
                            lastTimer = DateTime.UtcNow;
                            WriteStatus(done);
                        }
                        else if (beat++ % 2 == 0)
                        {
                            MemStatus st = ReadStatus() ?? new MemStatus();
                            WriteStatus(st);
                        }
                    }
                    catch (Exception ex) { Log.Error("MemHelper", ex); }
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                try { MemStatus st = ReadStatus(); if (st != null) { st.Beat = 0; st.Pid = 0; WriteStatus(st, false); } } catch (Exception) { }
                one.ReleaseMutex();
            }
        }

        // One clean as administrator, then exit ("Clean now" while the helper is not running).
        public static void RunOnce()
        {
            MemStatus st = Clean(Store.Settings.Mem, "manual");
            MemStatus old = ReadStatus();
            if (old != null) { st.Beat = old.Beat; st.Pid = old.Pid; }
            WriteStatus(st, false);
        }

        static bool ParentAlive(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                    return !p.HasExited && p.ProcessName.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        // ---------------- status file ----------------
        public static MemStatus ReadStatus()
        {
            try
            {
                if (!File.Exists(StatusPath)) return null;
                return Json.From<MemStatus>(File.ReadAllText(StatusPath, Encoding.UTF8));
            }
            catch (Exception) { return null; }
        }

        static void WriteStatus(MemStatus st) { WriteStatus(st, true); }

        static void WriteStatus(MemStatus st, bool beat)
        {
            if (beat)
            {
                st.Beat = DateTime.UtcNow.Ticks;
                st.Pid = Process.GetCurrentProcess().Id;
            }
            try
            {
                string tmp = StatusPath + ".tmp";
                File.WriteAllText(tmp, Json.To(st), Encoding.UTF8);
                File.Copy(tmp, StatusPath, true);
                File.Delete(tmp);
            }
            catch (Exception) { }
        }

        public static bool HelperAlive()
        {
            MemStatus st = ReadStatus();
            return st != null && st.Beat > 0 && (DateTime.UtcNow - new DateTime(st.Beat, DateTimeKind.Utc)).TotalSeconds < 8;
        }

        // ---------------- native ----------------
        static void EnablePrivilege(string name)
        {
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out token)) return;   // ADJUST_PRIVILEGES | QUERY
            try
            {
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.Count = 1;
                tp.Attributes = 2;                          // SE_PRIVILEGE_ENABLED
                if (!LookupPrivilegeValue(null, name, out tp.Luid)) return;
                AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { CloseHandle(token); }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_COMBINE_INFORMATION_EX { public IntPtr Handle; public UIntPtr PagesCombined; public uint Flags; }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct TOKEN_PRIVILEGES { public uint Count; public long Luid; public uint Attributes; }

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        [DllImport("ntdll.dll")]
        static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [DllImport("ntdll.dll")]
        static extern int NtSetSystemInformation(int infoClass, ref MEMORY_COMBINE_INFORMATION_EX info, int length);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetSystemFileCacheSize(IntPtr min, IntPtr max, int flags);

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr process);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool LookupPrivilegeValue(string system, string name, out long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, uint len, IntPtr prev, IntPtr ret);
    }
}
