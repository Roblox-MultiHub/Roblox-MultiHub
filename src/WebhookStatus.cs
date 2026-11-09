using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RobloxMultiHub
{
    // The live status embed ("Log status" in Settings): one message in the channel, edited in
    // place on every refresh. One line per running account:
    //   [username](profile) - <t:start:R> uptime - [game](link) - avg CPU% / avg RAM MB
    // Averages come from the UsageMonitor history covered by the refresh interval.
    public static class WebhookStatus
    {
        static DateTime lastSent = DateTime.MinValue;

        // Called from the main timer (every second).
        public static void Tick()
        {
            if (!Store.Settings.WebhookStatus || !Webhook.Enabled) return;
            int every = Math.Max(15, Store.Settings.WebhookStatusSec);
            if ((DateTime.UtcNow - lastSent).TotalSeconds < every) return;
            lastSent = DateTime.UtcNow;
            Webhook.SendStatus("Status - " + Launcher.Instances.Count(i => Launcher.IsAlive(i.Pid)) + " running",
                BuildDescription(every), Webhook.Green);
        }

        static string BuildDescription(int windowSec)
        {
            List<Instance> live = Launcher.Instances.Where(i => Launcher.IsAlive(i.Pid)).ToList();
            if (live.Count == 0) return "No accounts running.";
            int samples = Math.Max(1, windowSec / 2);          // UsageMonitor samples every ~2 s
            StringBuilder sb = new StringBuilder();
            int shown = 0;
            foreach (Instance inst in live)
            {
                if (shown >= 40) { sb.Append("\n(+ ").Append(live.Count - shown).Append(" more)"); break; }
                shown++;
                Account a = inst.Account;
                long uid = a.UserId > 0 ? a.UserId : 0;
                string name = string.IsNullOrEmpty(a.Username) ? AppState.Name(a) : a.Username;
                string profile = uid > 0
                    ? "[" + name + "](https://www.roblox.com/users/" + uid + "/profile)"
                    : name;
                long start = new DateTimeOffset(inst.Started).ToUnixTimeSeconds();
                long place = inst.Spec != null ? inst.Spec.PlaceId : 0;
                string gameName = null;
                if (place > 0)
                {
                    GameEntry g = Store.Settings.Recent.FirstOrDefault(x => x.PlaceId == place);
                    gameName = g != null && !string.IsNullOrEmpty(g.Name) ? g.Name : "#" + place;
                }
                string game = place > 0
                    ? "[" + gameName + "](https://www.roblox.com/games/" + place + ")"
                    : "-";
                UsageSample s;
                UsageMonitor.Samples.TryGetValue(inst.Pid, out s);
                double cpu = Avg(UsageMonitor.CpuHist, inst.Pid, samples, s != null ? s.Cpu : 0);
                long ram = (long)Avg(UsageMonitor.RamHist, inst.Pid, samples, s != null ? s.Ram / 1048576.0 : 0);
                sb.Append(profile)
                  .Append(" - <t:").Append(start).Append(":R> - ")
                  .Append(game)
                  .Append(" - ").Append(cpu.ToString("0.0")).Append("% / ").Append(ram).Append("MB\n");
            }
            return sb.ToString().TrimEnd('\n');
        }

        static double Avg(Dictionary<int, List<double>> hist, int pid, int samples, double fallback)
        {
            List<double> l;
            if (hist == null || !hist.TryGetValue(pid, out l) || l.Count == 0) return fallback;
            int take = Math.Min(samples, l.Count);
            double sum = 0;
            for (int i = l.Count - take; i < l.Count; i++) sum += l[i];
            return sum / take;
        }
    }
}
