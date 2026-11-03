using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public class UpdateInfo
    {
        public bool Available;
        public string Latest = "";
        public string Current = "";
        public string Url = "";
        public string Error = "";
    }

    // Phase 1 update check: ask GitHub for the latest release and compare version numbers. It only
    // READS a version and, when a newer one exists, points you at the download page - it never
    // downloads or replaces anything itself, so it adds no "downloader" behaviour for antivirus to flag.
    public static class Updater
    {
        public const string LatestApi = "https://api.github.com/repos/Roblox-MultiHub/Roblox-MultiHub/releases/latest";
        public const string ReleasesPage = "https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/latest";

        static readonly HttpClient Http = new HttpClient();
        static Updater()
        {
            try
            {
                Http.Timeout = TimeSpan.FromSeconds(12);
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxMultiHub");   // GitHub refuses requests with no User-Agent
            }
            catch (Exception) { }
        }

        public static string CurrentVersion()
        {
            try { return Application.ProductVersion; } catch (Exception) { return "0"; }
        }

        public static async Task<UpdateInfo> Check()
        {
            UpdateInfo info = new UpdateInfo();
            info.Current = CurrentVersion();
            info.Url = ReleasesPage;
            try
            {
                string json = await Http.GetStringAsync(LatestApi);
                object o = Json.Parse(json);
                string tag = Json.Str(o, "tag_name");
                string url = Json.Str(o, "html_url");
                info.Latest = (tag ?? "").TrimStart('v', 'V').Trim();
                if (!string.IsNullOrEmpty(url)) info.Url = url;
                info.Available = IsNewer(info.Latest, info.Current);
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
                Log.Error("Updater", ex);
            }
            return info;
        }

        static int[] Parse(string v)
        {
            List<int> parts = new List<int>();
            foreach (string s in (v ?? "").Split('.'))
            {
                int n;
                string digits = "";
                foreach (char c in s) { if (c >= '0' && c <= '9') digits += c; else break; }
                int.TryParse(digits, out n);
                parts.Add(n);
            }
            while (parts.Count < 4) parts.Add(0);
            return parts.ToArray();
        }

        // True if "latest" is a higher version than "current".
        public static bool IsNewer(string latest, string current)
        {
            if (string.IsNullOrEmpty(latest)) return false;
            int[] a = Parse(latest);
            int[] b = Parse(current);
            for (int i = 0; i < 4; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
            return false;
        }
    }
}
