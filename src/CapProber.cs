using System;
using System.Collections.Generic;
using System.Management;

namespace RobloxMultiHub
{
    // Finds the REAL lowest reliable RAM cap for this PC by measuring hard page faults. A cap above
    // the floor keeps an idle client's pages resident (near-zero disk reads); below it, pages thrash
    // back from the pagefile and "Memory: Pages Input/sec" climbs. The probe steps the cap down and
    // watches THAT signal on THIS machine - no hardcoded number decides anything.
    //
    // One step = apply cap, let the one-time eviction settle, then measure the steady window. The
    // probe trips when the steady fault rate stays clearly above baseline; the last cap that held
    // clean is the floor. Fault spikes right after a memory clean are ignored (the standby purge
    // itself refaults benign pages).
    public static class CapProber
    {
        const int StepMb = 50;
        const int FloorMb = 300;
        const int SettleSec = 75;              // after a cap change: the eviction burst must pass
        const int MeasureSec = 120;            // steady window that decides the step
        const double TripFactor = 3.0;         // steady faults must exceed baseline x this
        const double TripMin = 8.0;            // ...and at least this many pages/s, whatever the baseline

        public enum Phase { Off, Settle, Measure, Done }

        public static Phase State;
        public static int CurrentCap;          // the cap being tested right now
        public static double FaultsNow;        // last fault-rate sample (pages/s, all clients)
        public static double Baseline;         // faults/s with the cap that is known good
        public static int FoundFloor;          // outcome: the lowest cap that stayed clean
        public static string Status = "";

        static int phaseStart;
        static double measureSum;
        static int measureN;
        static double lastFaults;
        static DateTime lastSample = DateTime.MinValue;
        static DateTime lastClean = DateTime.MinValue;

        // WMI gives locale-independent property names; the English "Memory" counter names do not survive localization.
        static double ReadFaults()
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT PagesInputPersec FROM Win32_PerfFormattedData_PerfOS_Memory"))
                using (ManagementObjectCollection r = s.Get())
                    foreach (ManagementObject mo in r)
                    {
                        try
                        {
                            object v = mo["PagesInputPersec"];
                            if (v != null) return Convert.ToDouble(v);
                        }
                        finally { mo.Dispose(); }
                    }
            }
            catch (Exception) { }
            return -1;
        }

        // Called from the main timer (every 2 s) while a probe is running.
        public static void Tick(int runningClients, Action<string> log)
        {
            if (State == Phase.Off || State == Phase.Done) return;

            double f = ReadFaults();
            if (f >= 0) { FaultsNow = f; lastFaults = f; }
            MemStatus st = MemClean.ReadStatus();
            if (st != null && st.LastTicks > 0 && new DateTime(st.LastTicks, DateTimeKind.Utc) > lastClean)
            {
                lastClean = new DateTime(st.LastTicks, DateTimeKind.Utc);   // a clean just happened
                phaseStart = Environment.TickCount;                          // re-settle this window
                measureSum = 0; measureN = 0;
            }
            if (runningClients == 0)
            {
                Stop(log, "probe stopped: no clients are running. The last known-good cap stays.");
                return;
            }

            int now = Environment.TickCount;
            if (State == Phase.Settle)
            {
                if (now - phaseStart < SettleSec * 1000)
                {
                    Status = "settling at " + CurrentCap + " MB (eviction burst passing)...";
                    return;
                }
                State = Phase.Measure;
                phaseStart = now;
                measureSum = 0; measureN = 0;
            }
            if (State == Phase.Measure)
            {
                int elapsed = (now - phaseStart) / 1000;
                if (lastFaults >= 0) { measureSum += lastFaults; measureN++; }
                Status = "testing " + CurrentCap + " MB - hard faults " + lastFaults.ToString("0") + "/s (baseline " + Baseline.ToString("0") + ")";
                if (elapsed < MeasureSec) return;

                double avg = measureN > 0 ? measureSum / measureN : 0;
                if (avg > Math.Max(TripMin, Baseline * TripFactor))
                {
                    FoundFloor = CurrentCap + StepMb;
                    Store.Settings.RamCapFloorMb = FoundFloor;
                    Store.Settings.MaxRamMb = FoundFloor;         // land on the last clean cap
                    Store.SaveSettings();
                    RamCap.Tick();
                    Stop(log, "tripped at " + CurrentCap + " MB (steady " + avg.ToString("0") + " hard faults/s vs baseline " + Baseline.ToString("0") + ").");
                    log("YOUR measured floor: " + FoundFloor + " MB per window. Cap set to it - if any window hitches, raise it 50 MB.");
                    return;
                }
                // clean step: this cap holds - remember it and step down
                if (CurrentCap - StepMb >= FloorMb)
                {
                    FoundFloor = CurrentCap;
                    CurrentCap -= StepMb;
                    Store.Settings.MaxRamMb = CurrentCap;
                    Store.SaveSettings();
                    RamCap.Tick();
                    log("Cap probe: " + (CurrentCap + StepMb) + " MB held clean (" + avg.ToString("0.0") + " faults/s) - trying " + CurrentCap + " MB.");
                    if (Baseline < 0.5 && avg > 0.5) Baseline = avg;   // first real activity becomes the reference
                    State = Phase.Settle;
                    phaseStart = Environment.TickCount;
                    measureSum = 0; measureN = 0;
                }
                else
                {
                    FoundFloor = FloorMb;
                    Store.Settings.RamCapFloorMb = FloorMb;
                    Store.SaveSettings();
                    Stop(log, "reached the " + FloorMb + " MB safety floor with clean faults - your clients need even less.");
                    log("YOUR measured floor: " + FloorMb + " MB per window (probe limit).");
                }
            }
        }

        public static void Start(Action<string> log)
        {
            if (State == Phase.Settle || State == Phase.Measure) { Stop(log, "probe stopped by hand."); return; }
            int start = Store.Settings.MaxRamMb > 0 ? Store.Settings.MaxRamMb : 800;
            Baseline = Math.Max(0.5, lastFaults >= 0 ? lastFaults : 0.5);
            CurrentCap = Math.Max(FloorMb, start - StepMb);
            Store.Settings.MaxRamMb = CurrentCap;
            Store.SaveSettings();
            RamCap.Tick();
            State = Phase.Settle;
            phaseStart = Environment.TickCount;
            measureSum = 0; measureN = 0;
            log("Cap probe started at " + CurrentCap + " MB. Keep the accounts idling like normal; this steps down 50 MB every ~3 minutes and watches hard page faults.");
        }

        static void Stop(Action<string> log, string why)
        {
            State = Phase.Off;
            Status = "";
            log("Cap probe: " + why);
        }
    }
}
