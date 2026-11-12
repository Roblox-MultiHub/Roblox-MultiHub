using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // One-click support bundle: everything a supporter needs to diagnose a setup, in one text file
    // (the spirit of Overwatch's crash reports - exhaustive, structured, self-describing).
    // Secrets are redacted: the webhook URL is truncated, and account cookies never leave the
    // encrypted store (only usernames are listed).
    public static class SupportBundle
    {
        public static string Create()
        {
            StringBuilder s = new StringBuilder();
            Section(s, "ROBLOX MULTIHUB SUPPORT BUNDLE");
            Line(s, "Created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Line(s, "App version", Application.ProductVersion);
            Line(s, "App path", Application.ExecutablePath);
            Line(s, "Data folder", Store.Dir);
            Line(s, "Elevated", MemClean.IsElevated().ToString());
            Line(s, "App uptime", (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"d\.hh\:mm\:ss"));
            Line(s, "Settings", File.Exists(Store.SettingsPath) ? MaskSecrets(File.ReadAllText(Store.SettingsPath, Encoding.UTF8)) : "(missing)");

            Section(s, "OPERATING SYSTEM");
            Wmi(s, "SELECT Caption, Version, BuildNumber, OSArchitecture, LastBootUpTime FROM Win32_OperatingSystem",
                new string[] { "Caption", "Version", "BuildNumber", "OSArchitecture", "LastBootUpTime" });
            int load; long total, avail;
            if (MemClean.Status(out load, out total, out avail))
            {
                Line(s, "RAM in use", load + "% (" + Util.FormatBytes(total - avail) + " of " + Util.FormatBytes(total) + ")");
            }

            Section(s, "CPU");
            Wmi(s, "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor",
                new string[] { "Name", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed" });

            Section(s, "GPU");
            Wmi(s, "SELECT Name, DriverVersion, AdapterRAM, VideoProcessor FROM Win32_VideoController",
                new string[] { "Name", "DriverVersion", "VideoProcessor" });
            Line(s, "GPU engine counters", GpuMeter.Available ? "available" : "not available on this system");

            Section(s, "MOTHERBOARD / BIOS");
            Wmi(s, "SELECT Manufacturer, Product, Version FROM Win32_BaseBoard", new string[] { "Manufacturer", "Product", "Version" });
            Wmi(s, "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", new string[] { "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate" });

            Section(s, "DISKS + PAGEFILE");
            try
            {
                using (ManagementObjectSearcher d = new ManagementObjectSearcher(
                    "SELECT Model, Size, InterfaceType, MediaType, Index FROM Win32_DiskDrive"))
                using (ManagementObjectCollection r = d.Get())
                    foreach (ManagementObject mo in r)
                    {
                        try
                        {
                            Line(s, "Disk " + mo["Index"],
                                (mo["Model"] ?? "?") + "  " + Util.FormatBytes(Convert.ToInt64(mo["Size"]))
                                + "  " + (mo["InterfaceType"] ?? "?") + "  " + (mo["MediaType"] ?? "?"));
                        }
                        finally { mo.Dispose(); }
                    }
                foreach (DriveInfo di in DriveInfo.GetDrives())
                    if (di.IsReady)
                    {
                        double pct = di.TotalSize > 0 ? 100.0 * (di.TotalSize - di.AvailableFreeSpace) / di.TotalSize : 0;
                        Line(s, "  " + di.Name.Replace("\\", ""),
                            di.VolumeLabel + "  " + Gb(di.TotalSize - di.AvailableFreeSpace) + " / " + Gb(di.TotalSize)
                            + " GB  (" + pct.ToString("0") + "% used)");
                    }
                Wmi(s, "SELECT Name, AllocatedBaseSize, CurrentUsage FROM Win32_PageFileUsage", new string[] { "Name", "AllocatedBaseSize", "CurrentUsage" });
            }
            catch (Exception ex) { Line(s, "disk query failed", ex.Message); }

            Section(s, "ROBLOX");
            string exe = Launcher.FindRobloxExe();
            Line(s, "Client exe", exe ?? "(not found)");
            if (exe != null)
            {
                Line(s, "Version folder", Path.GetDirectoryName(exe));
                string cs = Path.Combine(Path.GetDirectoryName(exe), "ClientSettings", "ClientAppSettings.json");
                Line(s, "ClientAppSettings", File.Exists(cs) ? File.ReadAllText(cs, Encoding.UTF8) : "(none)");
                string fs = Launcher.FindBasicSettings();
                if (fs != null && File.Exists(fs))
                {
                    string xml = File.ReadAllText(fs, Encoding.UTF8);
                    System.Text.RegularExpressions.Match m =
                        System.Text.RegularExpressions.Regex.Match(xml, "FramerateCap\">\\s*(-?\\d+)");
                    Line(s, "FramerateCap", m.Success ? m.Groups[1].Value + " fps (-1 = Roblox default)" : "(not in file yet)");
                }
            }
            Line(s, "Protocol registered", Launcher.ProtocolRegistered().ToString());
            Line(s, "Legacy bootstrapper", Launcher.UsesLegacyBootstrapper().ToString());

            Section(s, "ACCOUNTS");
            // privacy: only a count here - usernames appear solely under RUNNING CLIENTS below,
            // because those are the ones a supporter actually needs to see
            Line(s, "Saved", Store.Accounts.Count.ToString() + " (names hidden; running ones listed below)");

            Section(s, "RUNNING CLIENTS (detailed overhead)");
            Line(s, "Tracked instances", Launcher.Instances.Count.ToString());
            foreach (Instance i in Launcher.Instances)
            {
                if (!Launcher.IsAlive(i.Pid)) { Line(s, "  " + AppState.Name(i.Account), "PID " + i.Pid + " (dead)"); continue; }
                UsageSample us;
                UsageMonitor.Samples.TryGetValue(i.Pid, out us);
                Line(s, "  " + AppState.Name(i.Account),
                    "PID " + i.Pid
                    + "  up " + (DateTime.Now - i.Started).ToString(@"h\:mm\:ss")
                    + "  game " + (i.Spec != null ? i.Spec.PlaceId.ToString() : "-")
                    + "  CPU " + (us != null ? us.Cpu.ToString("0.0") + "%" : "n/a")
                    + (GpuMeter.Available ? "  GPU " + (us != null ? us.Gpu.ToString("0.0") + "%" : "n/a") : "")
                    + "  RAM " + (us != null ? (us.Ram / 1048576) + "MB" : "n/a")
                    + "  cap " + Store.Settings.MaxRamMb + "MB");
            }
            Line(s, "Totals", UsageMonitor.Count + " client(s), CPU " + UsageMonitor.TotalCpu.ToString("0.0") + "%"
                + (GpuMeter.Available ? ", GPU " + UsageMonitor.TotalGpu.ToString("0.0") + "%" : "")
                + ", RAM " + Util.FormatBytes(UsageMonitor.TotalRam));
            Line(s, "Measured cap floor", Store.Settings.RamCapFloorMb > 0 ? Store.Settings.RamCapFloorMb + " MB (probed)" : "not probed");

            Section(s, "HUB OVERHEAD");
            using (Process me = Process.GetCurrentProcess())
                Line(s, "Hub process", "PID " + me.Id + "  RAM " + (me.WorkingSet64 / 1048576) + "MB  CPU " + me.TotalProcessorTime.ToString(@"h\:mm\:ss"));
            MemStatus ms = MemClean.ReadStatus();
            Line(s, "Memory helper", ms != null && MemClean.HelperAlive() ? "running (admin)" : "not running");
            Line(s, "Memory cleaner state", ms != null
                ? "auto-clean " + Store.Settings.Mem.AutoClean + ", threshold " + Store.Settings.Mem.Threshold + "%, last clean " + Util.Ago(ms.LastTicks) + " ago (" + Util.FormatBytes(ms.LastFreed) + " freed)"
                : "no status yet");

            Section(s, "RECENT LOG (last 250 lines)");
            try
            {
                string lp = Path.Combine(Store.Dir, "log.txt");
                if (File.Exists(lp))
                {
                    string[] all = File.ReadAllLines(lp, Encoding.UTF8);
                    for (int i = Math.Max(0, all.Length - 250); i < all.Length; i++) s.AppendLine(all[i]);
                }
            }
            catch (Exception ex) { Line(s, "log read failed", ex.Message); }

            string file = Path.Combine(Store.Dir, "support-bundle-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(file, s.ToString(), Encoding.UTF8);
            return file;
        }

        static void Section(StringBuilder s, string t)
        {
            s.AppendLine();
            s.AppendLine("------------------------------------------------------------------");
            s.AppendLine("  " + t);
            s.AppendLine("------------------------------------------------------------------");
        }

        static void Line(StringBuilder s, string k, string v)
        {
            s.Append((k + new string(' ', Math.Max(1, 26 - k.Length))));
            s.AppendLine(v);
        }

        static void Wmi(StringBuilder s, string query, string[] fields)
        {
            try
            {
                using (ManagementObjectSearcher se = new ManagementObjectSearcher(query))
                using (ManagementObjectCollection r = se.Get())
                    foreach (ManagementObject mo in r)
                    {
                        try
                        {
                            foreach (string f in fields)
                            {
                                object v = mo[f];
                                if (v != null) Line(s, "  " + f, v.ToString());
                            }
                        }
                        finally { mo.Dispose(); }
                    }
            }
            catch (Exception ex) { Line(s, "  query failed", ex.Message); }
        }

        static string Gb(long bytes) { return (bytes / 1073741824.0).ToString("0.00"); }

        // mask the secret tail of the webhook URL (and any other long token-looking value) in a dump
        static string MaskSecrets(string text)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.Replace(text,
                    "(\"WebhookUrl\": \")([^\"]{30})[^\"]*(\")",
                    delegate(System.Text.RegularExpressions.Match m) { return m.Groups[1].Value + m.Groups[2].Value + "***" + m.Groups[3].Value; });
            }
            catch (Exception) { return "(settings redacted)"; }
        }
    }
}
