using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    public static class Log
    {
        static readonly object gate = new object();
        static string path;
        static string lastMsg = "";
        static string lastStamp = "";
        static int repeat;

        // (text to show, true = replace the previous line instead of adding a new one)
        public static event Action<string, bool> Line;

        public static void Init(string dir)
        {
            path = Path.Combine(dir, "log.txt");
            try { if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Delete(path); }
            catch (Exception) { }
        }

        // Deletes the log file once it is older than the chosen number of hours (12 or 24).
        public static void TrimOld(int hours)
        {
            try
            {
                if (path == null || !File.Exists(path)) return;
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromHours(Math.Max(1, hours)))
                    File.Delete(path);
            }
            catch (Exception) { }
        }

        public static void Write(string msg)
        {
            string now = DateTime.Now.ToString("HH:mm:ss");
            string shown;
            bool replace;
            lock (gate)
            {
                // Same message again right after itself: show "(2x)", "(3x)" ... and keep the time of the first one.
                replace = repeat > 0 && msg == lastMsg;
                if (replace)
                {
                    repeat++;
                    shown = lastStamp + "  " + msg + "  (" + repeat + "x)";
                }
                else
                {
                    lastMsg = msg;
                    lastStamp = now;
                    repeat = 1;
                    shown = now + "  " + msg;
                }
                try { if (path != null) File.AppendAllText(path, now + "  " + msg + Environment.NewLine); }
                catch (Exception) { }
            }
            Action<string, bool> h = Line;
            if (h != null) h(shown, replace);
        }

        public static void Error(string where, Exception ex)
        {
            Write("ERROR in " + where + ": " + ex.GetType().Name + ": " + ex.Message);
            lock (gate)
            {
                try { if (path != null) File.AppendAllText(path, ex.ToString() + Environment.NewLine); }
                catch (Exception) { }
            }
        }
    }

    public static class Json
    {
        static JavaScriptSerializer Make()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 200;
            return s;
        }

        public static string To(object o) { return Make().Serialize(o); }
        public static T From<T>(string s) { return Make().Deserialize<T>(s); }
        public static object Parse(string s) { return Make().DeserializeObject(s); }

        public static IDictionary<string, object> Obj(object o) { return o as IDictionary<string, object>; }

        // Arrays can come back as object[] or ArrayList depending on the runtime, so read them generically.
        public static List<object> Arr(object o)
        {
            List<object> list = new List<object>();
            if (o == null || o is string || o is IDictionary) return list;
            IEnumerable e = o as IEnumerable;
            if (e == null) return list;
            foreach (object x in e) list.Add(x);
            return list;
        }

        public static object Get(object o, string key)
        {
            IDictionary<string, object> d = Obj(o);
            if (d == null) return null;
            object v;
            if (d.TryGetValue(key, out v)) return v;
            return null;
        }

        public static string Str(object o, string key)
        {
            object v = Get(o, key);
            if (v == null) return "";
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static long Long(object o, string key)
        {
            object v = Get(o, key);
            if (v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); }
            catch (Exception)
            {
                try { return (long)Convert.ToDouble(v, CultureInfo.InvariantCulture); }
                catch (Exception) { return 0; }
            }
        }

        public static bool Bool(object o, string key)
        {
            object v = Get(o, key);
            if (v is bool) return (bool)v;
            return false;
        }
    }

    public static class Util
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long NowMs() { return (long)(DateTime.UtcNow - Epoch).TotalMilliseconds; }

        public static string Ago(long ticks)
        {
            if (ticks <= 0) return "never";
            TimeSpan t = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            if (t.TotalSeconds < 60) return "just now";
            if (t.TotalMinutes < 60) return ((int)t.TotalMinutes) + " min ago";
            if (t.TotalHours < 48) return ((int)t.TotalHours) + " h ago";
            return ((int)t.TotalDays) + " days ago";
        }

        public static string Trunc(string s, int n)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= n ? s : s.Substring(0, n) + "...";
        }

        // Shows at most max characters; longer text is cut and ends in "..".
        public static string Short(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max).TrimEnd() + "..";
        }

        public static void Copy(string text)
        {
            try { Clipboard.SetText(text); }
            catch (Exception) { MessageBox.Show("Could not access the clipboard. Try again.", "Roblox MultiHub"); }
        }

        // Finds every .ROBLOSECURITY cookie inside any pasted text / file contents.
        public static List<string> ExtractCookies(string text)
        {
            List<string> found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            foreach (Match m in Regex.Matches(text, @"_\|WARNING:[^|\s""]*\|_[A-Za-z0-9_\-\.%]+"))
            {
                if (!found.Contains(m.Value)) found.Add(m.Value);
            }
            return found;
        }

        // Accepts "123456", "https://www.roblox.com/games/123456/Some-Game" etc. Returns 0 if nothing usable.
        public static long ParsePlaceId(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            Match m = Regex.Match(s, @"/games/(\d+)");
            if (!m.Success) m = Regex.Match(s, @"placeId=(\d+)", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(s, @"^\s*(\d{3,})");
            if (!m.Success) return 0;
            long v;
            if (long.TryParse(m.Groups[1].Value, out v)) return v;
            return 0;
        }

        public static string FormatBytes(long b)
        {
            if (b >= 1073741824L) return (b / 1073741824.0).ToString("0.00") + " GB";
            return (b / 1048576L) + " MB";
        }

        public static bool LooksLikeGuid(string s)
        {
            Guid g;
            return !string.IsNullOrEmpty(s) && Guid.TryParse(s.Trim(), out g);
        }

        // DoubleBuffered is protected; this switches it on for any control so moving it does not tear.
        public static void SetBuf(Control c)
        {
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(c, true, null);
        }

        // "account3" before "account10": digit runs compare as numbers, letters case-insensitively.
        public static int NaturalCompare(string a, string b)
        {
            a = a ?? ""; b = b ?? "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    string na = a.Substring(si, i - si).TrimStart('0'), nb = b.Substring(sj, j - sj).TrimStart('0');
                    int cmp = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                    if (cmp != 0) return cmp;
                    i++; j++;
                }
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }
    }
}
