using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RobloxMultiHub
{
    // Holds Roblox's single-instance lock objects (ROBLOX_singletonMutex + ROBLOX_singletonEvent) so
    // that more than one Roblox client may run. Must be running BEFORE the first Roblox client starts.
    // Nothing is injected into Roblox and no Roblox memory is touched - these are plain Windows objects.
    public static class MultiInstance
    {
        static Thread thread;
        static volatile bool stop;
        static volatile string state = "Off";

        public static string State { get { return state; } }
        public static bool Active { get { return state == "Active"; } }
        public static bool Enabled { get { return thread != null; } }

        public static void Start()
        {
            if (thread != null) return;
            stop = false;
            state = "Waiting";
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "MultiInstanceHolder";
            thread.Start();
        }

        public static void Stop()
        {
            if (thread == null) return;
            stop = true;
            try { thread.Join(2000); } catch (Exception) { }
            thread = null;
            state = "Off";
        }

        static void Run()
        {
            Mutex mutex = null;
            EventWaitHandle ev = null;
            bool owned = false;
            try
            {
                bool created;
                ev = new EventWaitHandle(false, EventResetMode.ManualReset, "ROBLOX_singletonEvent", out created);
                mutex = new Mutex(false, "ROBLOX_singletonMutex");
                while (!stop)
                {
                    bool got = false;
                    try { got = mutex.WaitOne(400); }
                    catch (AbandonedMutexException) { got = true; }
                    if (got)
                    {
                        owned = true;
                        state = "Active";
                        while (!stop) Thread.Sleep(250);
                    }
                    else
                    {
                        state = "Waiting";
                    }
                }
            }
            catch (Exception ex)
            {
                state = "Error: " + ex.Message;
                Log.Error("MultiInstance", ex);
            }
            finally
            {
                try { if (owned && mutex != null) mutex.ReleaseMutex(); } catch (Exception) { }
                try { if (mutex != null) mutex.Close(); } catch (Exception) { }
                try { if (ev != null) ev.Close(); } catch (Exception) { }
                if (!state.StartsWith("Error")) state = "Off";
            }
        }
    }

    public static class Launcher
    {
        public static readonly List<Instance> Instances = new List<Instance>();
        static readonly Random Rng = new Random();
        const string ProcName = "RobloxPlayerBeta";

        // ---------- crash breaker ----------
        // Counts how often the watcher has relaunched an account inside a short window. Once it reaches the
        // limit the account is left alone for a cooldown, so a client that keeps crashing never loops forever.
        class BreakerState { public int Count; public DateTime WindowStart; public bool GaveUp; public DateTime GaveUpAt; }
        static readonly Dictionary<string, BreakerState> breaker = new Dictionary<string, BreakerState>();

        static BreakerState Breaker(Account a)
        {
            BreakerState s;
            if (!breaker.TryGetValue(a.Id, out s)) { s = new BreakerState(); s.WindowStart = DateTime.UtcNow; breaker[a.Id] = s; }
            return s;
        }

        // May the watcher relaunch this account right now? (false = the breaker has given up for a while)
        public static bool BreakerAllows(Account a)
        {
            if (!Store.Settings.CrashBreaker) return true;
            BreakerState s = Breaker(a);
            if (s.GaveUp)
            {
                if ((DateTime.UtcNow - s.GaveUpAt).TotalMinutes >= Math.Max(1, Store.Settings.CrashBreakerCooldownMin))
                {
                    s.GaveUp = false; s.Count = 0; s.WindowStart = DateTime.UtcNow;   // cooldown over: try again
                    return true;
                }
                return false;
            }
            return true;
        }

        // Record one watcher relaunch; returns true if that tipped the account over the limit (just gave up).
        public static bool BreakerRecord(Account a)
        {
            if (!Store.Settings.CrashBreaker) return false;
            BreakerState s = Breaker(a);
            int windowMin = Math.Max(1, Store.Settings.CrashBreakerWindowMin);
            if ((DateTime.UtcNow - s.WindowStart).TotalMinutes > windowMin) { s.WindowStart = DateTime.UtcNow; s.Count = 0; }
            s.Count++;
            if (s.Count >= Math.Max(1, Store.Settings.CrashBreakerMax)) { s.GaveUp = true; s.GaveUpAt = DateTime.UtcNow; return true; }
            return false;
        }

        // A deliberate (user) launch clears the breaker for that account.
        public static void BreakerClear(Account a)
        {
            breaker.Remove(a.Id);
        }

        // ---------- process helpers ----------
        public static List<int> RobloxPids()
        {
            List<int> pids = new List<int>();
            foreach (Process p in Process.GetProcessesByName(ProcName))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
            return pids;
        }

        // Uses the process snapshot only (no process handle), so it also works for protected Roblox processes.
        public static bool IsAlive(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    return p.ProcessName.Equals(ProcName, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception) { return false; }
        }

        public static void KillPid(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    if (p.ProcessName.Equals(ProcName, StringComparison.OrdinalIgnoreCase)) p.Kill();
                }
            }
            catch (ArgumentException) { }
            catch (Exception ex) { Log.Write("Could not close Roblox process " + pid + ": " + ex.Message); }
        }

        // Closes every Roblox client and the Roblox bootstrapper (used before turning multi-instance on).
        public static int KillAllRoblox()
        {
            int n = 0;
            foreach (string name in new string[] { ProcName, "RobloxPlayerLauncher", "RobloxCrashHandler" })
            {
                foreach (Process p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(); n++; } catch (Exception) { }
                    p.Dispose();
                }
            }
            Instances.Clear();
            return n;
        }

        public static void CloseInstancesOf(Account a, Action<string> log)
        {
            List<Instance> mine = Instances.Where(i => i.Account == a).ToList();
            foreach (Instance i in mine)
            {
                i.UserClosed = true;
                if (IsAlive(i.Pid))
                {
                    KillPid(i.Pid);
                    if (log != null) log("[" + AppState.Name(a) + "] closed previous Roblox window (PID " + i.Pid + ")");
                }
                Instances.Remove(i);
            }
        }

        public static Instance FindInstance(int pid)
        {
            return Instances.FirstOrDefault(i => i.Pid == pid);
        }

        // Finds the Roblox client that carries this account's stable browser-tracker id on its command line.
        // This identifies the right process even when Roblox reused a background process (no new PID appears).
        // Returns 0 if command lines can't be read (e.g. the process is protected) - the caller then falls back.
        public static int FindByTracker(long tracker)
        {
            if (tracker <= 0) return 0;
            string needle = tracker.ToString();
            try
            {
                using (System.Management.ManagementObjectSearcher searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'RobloxPlayerBeta.exe'"))
                using (System.Management.ManagementObjectCollection results = searcher.Get())
                {
                    foreach (System.Management.ManagementObject mo in results)
                    {
                        try
                        {
                            object cl = mo["CommandLine"];
                            if (cl == null) continue;
                            if (cl.ToString().IndexOf(needle, StringComparison.Ordinal) >= 0)
                            {
                                object pidObj = mo["ProcessId"];
                                if (pidObj != null) return Convert.ToInt32(pidObj);
                            }
                        }
                        finally { mo.Dispose(); }
                    }
                }
            }
            catch (Exception) { }
            return 0;
        }

        // True if WMI lets us read any RobloxPlayerBeta command line at all (used for diagnostics).
        public static bool CanReadCommandLines()
        {
            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE Name = 'RobloxPlayerBeta.exe'"))
                using (System.Management.ManagementObjectCollection r = s.Get())
                    foreach (System.Management.ManagementObject mo in r)
                    {
                        try { if (mo["CommandLine"] != null) return true; } finally { mo.Dispose(); }
                    }
            }
            catch (Exception) { }
            return false;
        }

        // ---------- locating Roblox ----------
        static string ExtractExe(string command)
        {
            if (string.IsNullOrEmpty(command)) return null;
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                int end = command.IndexOf('"', 1);
                if (end > 1) return command.Substring(1, end - 1);
                return null;
            }
            int sp = command.IndexOf(' ');
            return sp > 0 ? command.Substring(0, sp) : command;
        }

        public static string FindRobloxExe()
        {
            // the protocol handler can be registered per-user or per-machine; HKCR shows both
            RegistryKey[][] roots = new RegistryKey[][]
            {
                new RegistryKey[] { Registry.CurrentUser, Registry.CurrentUser.OpenSubKey(@"Software\Classes") },
                new RegistryKey[] { Registry.ClassesRoot, Registry.ClassesRoot }
            };
            foreach (RegistryKey[] pair in roots)
            {
                try
                {
                    using (RegistryKey classes = pair[1])
                    {
                        if (classes == null) continue;
                        using (RegistryKey k = classes.OpenSubKey(@"roblox-player\shell\open\command"))
                        {
                            if (k == null) continue;
                            string handler = ExtractExe(k.GetValue("") as string);
                            if (handler != null)
                            {
                                string dir = Path.GetDirectoryName(handler);
                                if (dir != null)
                                {
                                    string beta = Path.Combine(dir, ProcName + ".exe");
                                    if (File.Exists(beta)) return beta;
                                }
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
            try
            {
                // Roblox installs per-user into LocalAppData and per-machine into Program Files
                foreach (string rootDir in new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Roblox", "Versions"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Roblox", "Versions")
                })
                {
                    if (!Directory.Exists(rootDir)) continue;
                    string best = null;
                    DateTime bestTime = DateTime.MinValue;
                    foreach (string d in Directory.GetDirectories(rootDir))
                    {
                        string exe = Path.Combine(d, ProcName + ".exe");
                        if (!File.Exists(exe)) continue;
                        DateTime t = File.GetLastWriteTime(exe);
                        if (t > bestTime) { bestTime = t; best = exe; }
                    }
                    if (best != null) return best;
                }
            }
            catch (Exception) { }
            return null;
        }

        // True when the roblox-player: protocol is registered at all (per-user or per-machine).
        public static bool ProtocolRegistered()
        {
            try
            {
                using (RegistryKey k = Registry.ClassesRoot.OpenSubKey("roblox-player"))
                    return k != null;
            }
            catch (Exception) { return false; }
        }

        // True if the roblox-player URL handler points at the old bootstrapper, which closes other clients.
        public static bool UsesLegacyBootstrapper()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\roblox-player\shell\open\command"))
                {
                    if (k == null) return false;
                    string v = (k.GetValue("") as string) ?? "";
                    return v.IndexOf("RobloxPlayerLauncher", StringComparison.OrdinalIgnoreCase) >= 0
                        || v.IndexOf("RobloxPlayerInstaller", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception) { return false; }
        }

        // ---------- client settings (graphics flags) ----------
        // Writes the allowlisted flags chosen on the Performance page into Roblox's ClientAppSettings.json.
        // Called right before every launch, so the same settings apply to every account until changed.
        public static void WriteClientSettings(string exe)
        {
            try
            {
                List<string> ignored;
                Dictionary<string, object> flags = Gfx.Final(out ignored);
                string dir = Path.Combine(Path.GetDirectoryName(exe), "ClientSettings");
                string file = Path.Combine(dir, "ClientAppSettings.json");
                if (flags.Count == 0)
                {
                    // user wants Roblox defaults: only remove a file that this app wrote earlier
                    if (Store.Settings.WroteClientFile)
                    {
                        if (File.Exists(file)) File.Delete(file);
                        Store.Settings.WroteClientFile = false;
                        Store.SaveSettings();
                    }
                    return;
                }
                Directory.CreateDirectory(dir);
                File.WriteAllText(file, Json.To(flags));
                if (!Store.Settings.WroteClientFile)
                {
                    Store.Settings.WroteClientFile = true;
                    Store.SaveSettings();
                }
            }
            catch (Exception ex) { Log.Write("Could not write ClientAppSettings.json: " + ex.Message); }
        }

        public static string RemoveClientSettings()
        {
            string exe = FindRobloxExe();
            if (exe == null) return "Roblox was not found.";
            try
            {
                string f = Path.Combine(Path.GetDirectoryName(exe), "ClientSettings", "ClientAppSettings.json");
                Store.Settings.WroteClientFile = false;
                Store.SaveSettings();
                if (File.Exists(f)) { File.Delete(f); return "Roblox default settings restored (deleted " + f + ")."; }
                return "Roblox default settings restored (there was no settings file).";
            }
            catch (Exception ex) { return "Failed: " + ex.Message; }
        }

        // ---------- FPS cap (Roblox's own settings file, NOT a Fast Flag) ----------
        // Roblox keeps its "Max frame rate" in %LOCALAPPDATA%\Roblox\GlobalBasicSettings_13.xml as
        // <int name="FramerateCap">-1</int>. Changing it before a client starts is what the manual
        // "edit the XML in Notepad" method does. Roblox reads it at startup.
        internal static string FindBasicSettings()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");
                if (!Directory.Exists(dir)) return null;
                string prefix = "GlobalBasicSettings_";
                string best = null;
                int bestNum = -1;
                foreach (string f in Directory.GetFiles(dir, prefix + "*.xml"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    int n;
                    if (!int.TryParse(name.Substring(prefix.Length), out n)) continue;   // skips the Studio file
                    if (n > bestNum) { bestNum = n; best = f; }
                }
                return best;
            }
            catch (Exception) { return null; }
        }

        public static void ApplyFpsCap(Action<string> log)
        {
            SetFpsCap(Store.Settings.FpsCap, log);
        }

        const string FpsPattern = "(<int name=\"FramerateCap\">)(-?\\d+)(</int>)";

        // Puts "want" into Roblox's settings file (one attempt) and reads it back.
        // Returns "" when the file now holds that value, otherwise the reason it does not.
        internal static string TryWriteCap(string file, int want, out bool changed)
        {
            changed = false;
            try
            {
                string xml = File.ReadAllText(file, Encoding.UTF8);
                Match m = Regex.Match(xml, FpsPattern);
                if (!m.Success) return "the settings file has no FramerateCap entry yet - start Roblox once normally, close it, then try again";
                int have;
                int.TryParse(m.Groups[2].Value, out have);
                if (have != want)
                {
                    string backup = file + ".multihub-backup";
                    if (!File.Exists(backup)) File.Copy(file, backup);
                    xml = xml.Substring(0, m.Groups[2].Index) + want + xml.Substring(m.Groups[2].Index + m.Groups[2].Length);
                    File.WriteAllText(file, xml, new UTF8Encoding(false));
                    changed = true;
                }
                Match check = Regex.Match(File.ReadAllText(file, Encoding.UTF8), FpsPattern);
                return (check.Success && check.Groups[2].Value == want.ToString()) ? "" : "Roblox changed the file again right away";
            }
            catch (IOException ex) { return ex.Message; }
            catch (UnauthorizedAccessException ex) { return ex.Message; }
        }

        // Writes one specific cap (0 = give control back to Roblox). Used for the global cap and for per-account caps.
        // Retries when Roblox still has the file open and checks that the value really is in the file afterwards.
        public static void SetFpsCap(int cap, Action<string> log)
        {
            bool restore = cap <= 0;
            if (restore && !Store.Settings.WroteFpsCap) return;       // never asked for a cap: leave Roblox alone
            string file = FindBasicSettings();
            if (file == null)
            {
                if (!restore && log != null) log("FPS cap: Roblox's settings file was not found. Start Roblox once normally, close it, then try again.");
                return;
            }
            int want = restore ? -1 : cap;
            string problem = "";
            bool changed = false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                bool c;
                problem = TryWriteCap(file, want, out c);
                if (c) changed = true;
                if (problem.Length == 0) break;
                Thread.Sleep(150);
            }
            if (problem.Length > 0)
            {
                if (log != null) log("Could not set the FPS cap in Roblox's settings file: " + problem);
                return;
            }
            if (restore)
            {
                Store.Settings.WroteFpsCap = false;
                Store.SaveSettings();
                if (changed && log != null) log("FPS cap removed: Roblox uses its own frame rate setting again.");
            }
            else
            {
                if (!Store.Settings.WroteFpsCap) { Store.Settings.WroteFpsCap = true; Store.SaveSettings(); }
                if (changed && log != null) log("FPS cap " + cap + " written to Roblox's settings file.");
            }
        }

        // Waits until the Roblox window of that process exists (the client has read its settings by then).
        static async Task WaitForWindow(int pid, int timeoutMs)
        {
            DateTime end = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < end)
            {
                foreach (WinInfo w in WindowLayout.FindRobloxWindows())
                    if (w.Pid == pid && w.HasWindow) return;
                await Task.Delay(700);
            }
        }

        // ---------- CPU priority ----------
        public static bool ApplyPriority(int pid, bool includeNormal)
        {
            ProcessPriorityClass pc;
            string s = Store.Settings.Priority;
            if (s == "BelowNormal") pc = ProcessPriorityClass.BelowNormal;
            else if (s == "Idle") pc = ProcessPriorityClass.Idle;
            else if (includeNormal) pc = ProcessPriorityClass.Normal;
            else return true;
            try
            {
                using (Process p = Process.GetProcessById(pid)) { p.PriorityClass = pc; }
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Could not change the CPU priority of process " + pid + " (Roblox may protect it): " + ex.Message);
                return false;
            }
        }

        public static int ApplyPriorityAll()
        {
            int ok = 0;
            foreach (int pid in RobloxPids())
                if (ApplyPriority(pid, true)) ok++;
            return ok;
        }

        // ---------- launching ----------
        // Same shape as Roblox Account Manager: 6 digits (100000-174999) followed by 6 digits (100000-899999).
        static long RandomTracker()
        {
            return long.Parse(Rng.Next(100000, 175000).ToString() + Rng.Next(100000, 900000).ToString());
        }

        // The join request the Roblox client sends to Roblox, built exactly like Roblox Account Manager does
        // (same parameters in the same order). Accounts launched like that are not asked for human verification.
        public static string PlaceLauncherUrl(LaunchSpec s, long tracker)
        {
            string b = Store.Settings.PlaceLauncherUrl;
            if (string.IsNullOrWhiteSpace(b)) b = "https://assetgame.roblox.com/game/PlaceLauncher.ashx";
            if (s.Kind == "Job")
                return b + "?request=RequestGameJob&browserTrackerId=" + tracker + "&placeId=" + s.PlaceId + "&gameId=" + s.JobId + "&isPlayTogetherGame=false";
            if (s.Kind == "Private")
                return b + "?request=RequestPrivateGame&placeId=" + s.PlaceId + "&linkCode=" + s.LinkCode;
            if (s.Kind == "Follow")
                return b + "?request=RequestFollowUser&userId=" + s.FollowUserId;
            return b + "?request=RequestGame&browserTrackerId=" + tracker + "&placeId=" + s.PlaceId + "&isPlayTogetherGame=false";
        }

        // Same encoding as .NET's HttpUtility.UrlEncode (lower-case hex, space = '+'), which Roblox Account Manager uses.
        // Start time of a process (local time), or DateTime.MinValue when Windows will not tell us.
        static DateTime ProcessStartTime(int pid)
        {
            IntPtr h = Native.OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return DateTime.MinValue;
            try
            {
                Native.FILETIME c, e, k, u;
                if (!Native.GetProcessTimes(h, out c, out e, out k, out u)) return DateTime.MinValue;
                return DateTime.FromFileTime(((long)c.High << 32) | (long)c.Low);
            }
            finally { Native.CloseHandle(h); }
        }

        static bool HasWindow(int pid)
        {
            foreach (WinInfo w in WindowLayout.FindRobloxWindows())
                if (w.Pid == pid && w.HasWindow) return true;
            return false;
        }

        static int sweepAt;

        // ---- VeryLow memory priority + taskbar hiding, applied to every client ----
        static readonly HashSet<int> lowMemDone = new HashSet<int>();

        // One call per client: memory priority VeryLow, so Windows reclaims Roblox pages first
        // whenever anything else needs RAM.
        public static void ApplyLowMemoryPriority()
        {
            if (!Store.Settings.VeryLowMem) return;
            foreach (int pid in RobloxPids())
            {
                if (lowMemDone.Contains(pid)) continue;
                IntPtr h = Native.OpenProcess(0x0200, false, pid);          // PROCESS_SET_INFORMATION
                if (h == IntPtr.Zero) continue;
                try
                {
                    int prio = 1;                                            // MEMORY_PRIORITY_VERYLOW
                    SetProcessInformation(h, 3, ref prio, 4);               // ProcessMemoryPriority
                    lowMemDone.Add(pid);
                }
                finally { Native.CloseHandle(h); }
            }
        }

        // Tool-window style takes Roblox off the taskbar and Alt-Tab; the windows stay on screen.
        static readonly HashSet<int> hiddenDone = new HashSet<int>();
        public static void ApplyTaskbarHiding()
        {
            bool hide = Store.Settings.HideTaskbar;
            foreach (WinInfo w in WindowLayout.FindRobloxWindows())
            {
                if (!w.HasWindow) continue;
                if (hide)
                {
                    if (hiddenDone.Contains(w.Pid)) continue;
                    int ex = Native.GetWindowLong(w.Hwnd, Native.GWL_EXSTYLE);
                    Native.SetWindowLong(w.Hwnd, Native.GWL_EXSTYLE, ex | 0x00000080);      // WS_EX_TOOLWINDOW
                    Native.SetWindowPos(w.Hwnd, IntPtr.Zero, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED);
                    hiddenDone.Add(w.Pid);
                }
                else if (hiddenDone.Contains(w.Pid))
                {
                    int ex = Native.GetWindowLong(w.Hwnd, Native.GWL_EXSTYLE);
                    Native.SetWindowLong(w.Hwnd, Native.GWL_EXSTYLE, ex & ~0x00000080);
                    Native.SetWindowPos(w.Hwnd, IntPtr.Zero, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED);
                    hiddenDone.Remove(w.Pid);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetProcessInformation(IntPtr hProcess, int infoClass, ref int info, int size);

        // Roblox leaves windowless background processes behind. They show up as "no window yet"
        // rows, and the next launch can mistake one for its client (that is how an account ends
        // up with a nameless window plus a second real client). Anything untracked and windowless
        // for over two minutes is closed.
        public static void SweepZombies()
        {
            if (Environment.TickCount < sweepAt) return;
            sweepAt = Environment.TickCount + 15000;
            foreach (int pid in RobloxPids())
            {
                if (FindInstance(pid) != null) continue;              // ours: the watcher owns it
                if (HasWindow(pid)) continue;
                DateTime st = ProcessStartTime(pid);
                if (st == DateTime.MinValue || (DateTime.Now - st).TotalSeconds < 120) continue;
                KillPid(pid);
                Log.Write("Closed a leftover background Roblox process (PID " + pid + ", no window for over 2 minutes).");
            }
        }

        static string UrlEncode(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in Encoding.UTF8.GetBytes(s))
            {
                char c = (char)b;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || "-_.!*()".IndexOf(c) >= 0) sb.Append(c);
                else if (c == ' ') sb.Append('+');
                else sb.Append('%').Append(((int)b).ToString("x2"));
            }
            return sb.ToString();
        }

        public static async Task<bool> LaunchAsync(Account a, LaunchSpec spec, Action<string> log)
        {
            string who = "[" + AppState.Name(a) + "] ";
            FpsGuard guard = null;
            try
            {
                if (string.IsNullOrEmpty(a.Cookie)) { log(who + "has no cookie saved."); return false; }

                int max = Store.Settings.MaxInstances;
                if (max > 0 && RobloxPids().Count >= max)
                {
                    log(who + "not launched: Max instances (" + max + ") reached. Close one or raise the limit in Settings.");
                    return false;
                }

                if (Store.Settings.RefreshCookieOnLaunch)
                {
                    string nc = await Api.RotateCookie(a.Cookie, log);
                    if (nc != null) { a.Cookie = nc; Store.SaveAccounts(); log(who + "cookie refreshed."); }
                }

                log(who + "requesting login ticket...");
                TicketResult t = await Api.GetTicket(a.Cookie);
                if (t.Ticket == null)
                {
                    log(who + "failed: " + t.Error);
                    if (t.Unauthorized) AppState.Live(a).CookieBad = true;
                    if (t.Locked)
                    {
                        LiveInfo li = AppState.Live(a);
                        if (!li.LockedAlerted && Store.Settings.WebhookLocked)
                        {
                            Webhook.Send("Account locked", AppState.Name(a) + " — Roblox returned HTTP 403 (locked / blocked). Unlock it on roblox.com.", Webhook.Red);
                            li.LockedAlerted = true;
                        }
                        li.Locked = true;
                        log(who + "this account looks LOCKED / blocked by Roblox (HTTP 403). Unlock it on roblox.com.");
                    }
                    return false;
                }
                AppState.Live(a).CookieBad = false;
                AppState.Live(a).Locked = false;
                AppState.Live(a).LockedAlerted = false;

                if (Store.Settings.AutoCloseLast) CloseInstancesOf(a, log);
                if (a.TrackerId == 0) a.TrackerId = RandomTracker();

                if (!MultiInstance.Active && RobloxPids().Count > 0)
                    log(who + "WARNING: multi-instance is not active, so starting this will likely close your other Roblox windows.");

                bool direct = Store.Settings.LaunchMethod != "Protocol";
                string exe = FindRobloxExe();
                if (direct && exe == null) { log(who + "Roblox was not found. Install it from roblox.com (or switch the launch method to Protocol in Settings)."); return false; }
                if (exe != null) WriteClientSettings(exe);
                if (a.FpsCap > 0)
                {
                    SetFpsCap(a.FpsCap, log);                      // this account has its own cap for this launch ...
                    guard = new FpsGuard(a.FpsCap, log);           // ... and nothing may overwrite it while the client starts
                }
                else ApplyFpsCap(log);

                List<int> before = RobloxPids();
                long launchTime = Util.NowMs();
                string url = PlaceLauncherUrl(spec, a.TrackerId);

                try
                {
                    if (direct)
                    {
                        string args = "--app -t " + t.Ticket + " -j \"" + url + "\" -b " + a.TrackerId
                            + " --launchtime=" + launchTime + " --rloc en_us --gloc en_us";
                        ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                        psi.UseShellExecute = false;
                        psi.WorkingDirectory = Path.GetDirectoryName(exe);
                        // Start off the UI thread: ShellExecute/CreateProcess can briefly block, and when Roblox
                        // grabs the foreground it stalls the app's message pump until the next mouse click.
                        await Task.Run(delegate { using (Process pr = Process.Start(psi)) { } });
                    }
                    else
                    {
                        string proto = "roblox-player:1+launchmode:play+gameinfo:" + t.Ticket + "+launchtime:" + launchTime
                            + "+placelauncherurl:" + UrlEncode(url) + "+browsertrackerid:" + a.TrackerId
                            + "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
                        ProcessStartInfo psi = new ProcessStartInfo(proto);
                        psi.UseShellExecute = true;
                        await Task.Run(delegate { using (Process pr = Process.Start(psi)) { } });
                    }
                }
                catch (Exception ex)
                {
                    log(who + "could not start Roblox: " + ex.Message);
                    if (guard != null) { guard.Dispose(); ApplyFpsCap(log); }
                    return false;
                }

                log(who + "Roblox is starting (" + spec.Describe() + ")...");
                // Which Roblox processes already had a visible window before we launched.
                HashSet<int> windowedBefore = new HashSet<int>();
                foreach (WinInfo w0 in WindowLayout.FindRobloxWindows())
                    if (w0.HasWindow) windowedBefore.Add(w0.Pid);

                // Poll for the client (UI thread - Roblox is STARTED on a background thread above, so the pump
                // isn't blocked; and the shared Instances list is only touched on the UI thread).
                // A candidate must have a WINDOW before it is believed: Roblox keeps windowless
                // background processes around, and adopting one as "the client" leaves a window
                // that never gets its name, plus a second real client on the next launch.
                DateTime minStart = DateTime.Now.AddSeconds(-2);   // our client cannot be older than the spawn
                int pid = 0;
                bool already = false;
                for (int i = 0; i < 120 && pid == 0; i++)
                {
                    await Task.Delay(500);

                    // Best signal: the process whose command line carries this account's tracker id. This finds
                    // the right client whether Roblox spawned a new process or REUSED a background one.
                    int tp = FindByTracker(a.TrackerId);
                    if (tp != 0 && HasWindow(tp))
                    {
                        Instance ex = FindInstance(tp);
                        if (ex == null) { pid = tp; break; }
                        if (ex.Account == a) { pid = tp; already = true; break; }
                    }
                    // Fallback 1: a brand-new process (younger than this launch, with a window).
                    foreach (int p in RobloxPids())
                        if (!before.Contains(p) && FindInstance(p) == null && HasWindow(p) && ProcessStartTime(p) >= minStart)
                        {
                            pid = p;
                            break;
                        }
                    // Fallback 2: an untracked process that just gained a window (reuse, when the tracker can't be read).
                    if (pid == 0)
                        foreach (WinInfo w in WindowLayout.FindRobloxWindows())
                            if (w.HasWindow && !windowedBefore.Contains(w.Pid) && FindInstance(w.Pid) == null) { pid = w.Pid; break; }
                }

                a.LastUsedTicks = DateTime.UtcNow.Ticks;
                Store.SaveAccounts();

                if (pid == 0)
                {
                    // Diagnostic dump so the real cause is visible in the log instead of guessed at.
                    System.Text.StringBuilder diag = new System.Text.StringBuilder();
                    foreach (int p in RobloxPids()) diag.Append(p).Append(FindInstance(p) != null ? "(tracked) " : " ");
                    int win = 0; foreach (WinInfo w in WindowLayout.FindRobloxWindows()) if (w.HasWindow) win++;
                    log(who + "NOT detected. Roblox procs: [" + diag.ToString().Trim() + "]  windows:" + win
                        + "  trackerId:" + a.TrackerId + "  cmdline-readable:" + Launcher.CanReadCommandLines()
                        + ". If a window is actually open, press 'Stop all' or close it in Task Manager, then relaunch.");
                    if (guard != null) { guard.Dispose(); ApplyFpsCap(log); }
                    return false;
                }
                if (already)
                {
                    Instance exi = FindInstance(pid);
                    if (exi != null) { exi.UserClosed = false; exi.Spec = spec.Copy(); exi.Account = a; }
                    ApplyPriority(pid, false);
                    log(who + "re-used its running window (PID " + pid + ").");
                    if (guard != null) { guard.Dispose(); ApplyFpsCap(log); }
                    return true;
                }
                Instance inst = new Instance();
                inst.Account = a;
                inst.Pid = pid;
                inst.Started = DateTime.Now;
                inst.Spec = spec.Copy();
                Instances.Add(inst);
                ApplyPriority(pid, false);
                log(who + "started (PID " + pid + ").");
                if (guard != null)
                {
                    // Wait until the Roblox window is really up (the client has read its settings by then), then put the
                    // global cap back so the next account gets the global one again.
                    await WaitForWindow(pid, 60000);
                    await Task.Delay(8000);
                    guard.Dispose();
                    guard = null;
                    ApplyFpsCap(log);
                    log(who + "ran with its own FPS cap (" + a.FpsCap + "); the global FPS setting is active again.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("LaunchAsync", ex);
                log(who + "unexpected error: " + ex.Message);
                if (guard != null) { guard.Dispose(); ApplyFpsCap(log); }
                return false;
            }
        }

        // ---------- watcher: relaunch accounts whose Roblox window disappeared ----------
        public static async Task WatcherTick(Action<string> log)
        {
            // Crash/disconnect alerts go out whether or not auto-relaunch is on.
            if (Store.Settings.WebhookCrash)
                foreach (Instance g in Instances.Where(i => !i.UserClosed && !IsAlive(i.Pid)).ToList())
                    Webhook.Send("Client closed", AppState.Name(g.Account) + " — its Roblox window disappeared unexpectedly.", Webhook.Red);

            if (!Store.Settings.AutoRelaunch)
            {
                Instances.RemoveAll(i => !IsAlive(i.Pid));
                return;
            }

            List<Instance> dead = Instances.Where(i => !i.UserClosed && !IsAlive(i.Pid)).ToList();

            if (Store.Settings.WatcherUsePresence)
            {
                foreach (Instance i in Instances.ToList())
                {
                    if (i.UserClosed || !IsAlive(i.Pid) || i.Account.UserId <= 0) continue;
                    if ((DateTime.Now - i.Started).TotalSeconds < 150) continue;
                    List<long> ids = new List<long>();
                    ids.Add(i.Account.UserId);
                    Dictionary<long, PresenceInfo> pr = await Api.Presence(i.Account.Cookie, ids);
                    PresenceInfo pi;
                    if (!pr.TryGetValue(i.Account.UserId, out pi)) continue;
                    if (pi.Type == 2) { i.Strikes = 0; continue; }
                    i.Strikes++;
                    if (i.Strikes >= 2)
                    {
                        log("Watcher: " + AppState.Name(i.Account) + " is not in a game anymore - closing and relaunching.");
                        KillPid(i.Pid);
                        dead.Add(i);
                    }
                }
            }

            if (dead.Count == 0) return;
            if (!await Api.InternetOk()) { log("Watcher: no internet connection, will retry."); return; }

            foreach (Instance d in dead)
            {
                Instances.Remove(d);
                if (!BreakerAllows(d.Account))
                {
                    log("Watcher: " + AppState.Name(d.Account) + " keeps crashing - the crash breaker stopped relaunching it. Launch it yourself to try again.");
                    continue;
                }
                log("Watcher: relaunching " + AppState.Name(d.Account) + "...");
                await LaunchAsync(d.Account, d.Spec, log);
                bool gaveUp = BreakerRecord(d.Account);
                if (Store.Settings.WebhookRelaunch)
                    Webhook.Send("Client relaunched", AppState.Name(d.Account) + " was brought back by the watcher.", Webhook.Yellow);
                if (gaveUp)
                {
                    log("Watcher: " + AppState.Name(d.Account) + " has crashed too often - the crash breaker will stop relaunching it for " + Math.Max(1, Store.Settings.CrashBreakerCooldownMin) + " min.");
                    if (Store.Settings.WebhookCrash)
                        Webhook.Send("Crash breaker", AppState.Name(d.Account) + " crashed too many times - auto-relaunch paused for " + Math.Max(1, Store.Settings.CrashBreakerCooldownMin) + " min.", Webhook.Red);
                }
                await Task.Delay(Math.Max(1, Store.Settings.LaunchDelaySec) * 1000);
            }
        }
    }

    // While a client with its own FPS cap is starting, Roblox may rewrite its settings file (for example a window that
    // is still closing). This watches the file and puts the wanted value straight back.
    public class FpsGuard : IDisposable
    {
        readonly int want;
        readonly Action<string> log;
        FileSystemWatcher watcher;
        DateTime last = DateTime.MinValue;
        readonly object gate = new object();

        public FpsGuard(int cap, Action<string> log)
        {
            want = cap;
            this.log = log;
            try
            {
                string file = Launcher.FindBasicSettings();
                if (file == null) return;
                watcher = new FileSystemWatcher(Path.GetDirectoryName(file), Path.GetFileName(file));
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                watcher.Changed += OnChanged;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception) { watcher = null; }
        }

        void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (gate)
            {
                if ((DateTime.Now - last).TotalMilliseconds < 400) return;
                last = DateTime.Now;
            }
            Thread.Sleep(300);                                    // let the writer finish
            bool changed;
            Launcher.TryWriteCap(e.FullPath, want, out changed);
            if (changed && log != null) log("Roblox rewrote its FPS setting while the window was starting - put " + want + " back.");
        }

        public void Dispose()
        {
            FileSystemWatcher w = watcher;
            watcher = null;
            if (w != null)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
        }
    }
}
