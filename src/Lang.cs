using System;
using System.Collections.Generic;
using System.IO;

namespace RobloxMultiHub
{
    // Lightweight translation layer. Strings are looked up by their English text, so any string that
    // is not translated falls back cleanly to English - a half-translated language still reads fine.
    //
    // English and Dutch are built in. More languages (or more strings for an existing one) can be
    // dropped in as  <data folder>\lang\<code>.json  - a flat JSON object of { "English": "translation" }.
    public static class Lang
    {
        public static string Current = "en";
        static readonly Dictionary<string, string> map = new Dictionary<string, string>();

        // code, display name. "en" is always first.
        public static List<string[]> Available()
        {
            List<string[]> list = new List<string[]>();
            list.Add(new string[] { "en", "English" });
            list.Add(new string[] { "nl", "Nederlands (Dutch)" });
            try
            {
                string dir = Path.Combine(Store.Dir, "lang");
                if (Directory.Exists(dir))
                    foreach (string f in Directory.GetFiles(dir, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(f);
                        if (code == "en" || code == "nl") continue;
                        list.Add(new string[] { code, code });
                    }
            }
            catch (Exception) { }
            return list;
        }

        public static void Init() { Load(Store.Settings.Language); }

        public static void Load(string code)
        {
            Current = string.IsNullOrEmpty(code) ? "en" : code;
            map.Clear();
            if (Current == "en") return;
            if (Current == "nl") foreach (KeyValuePair<string, string> kv in Dutch) map[kv.Key] = kv.Value;

            // a lang file on disk extends or overrides the built-in table
            try
            {
                string f = Path.Combine(Store.Dir, "lang", Current + ".json");
                if (File.Exists(f))
                {
                    object o = Json.Parse(File.ReadAllText(f, System.Text.Encoding.UTF8));
                    IDictionary<string, object> d = Json.Obj(o);
                    if (d != null) foreach (KeyValuePair<string, object> kv in d) map[kv.Key] = Convert.ToString(kv.Value);
                }
            }
            catch (Exception ex) { Log.Error("Lang.Load", ex); }
        }

        // Translate English text to the current language (or return it unchanged).
        public static string T(string english)
        {
            if (Current == "en" || string.IsNullOrEmpty(english)) return english;
            string v;
            return map.TryGetValue(english, out v) && !string.IsNullOrEmpty(v) ? v : english;
        }

        // Built-in Dutch for the parts of the interface you see most. Everything else stays English
        // until it is added here or in a lang file.
        static readonly Dictionary<string, string> Dutch = new Dictionary<string, string>
        {
            // navigation (the buttons grow to fit, so Dutch may be a little longer)
            { "\U0001F680  Launch", "\U0001F680  Starten" },
            { "\U0001F310  Servers", "\U0001F310  Servers" },
            { "\U0001FA9F  Layout", "\U0001FA9F  Indeling" },
            { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Presets" },
            { "⚡  Perf", "⚡  Prestaties" },
            { "⚙  Settings", "⚙  Opties" },
            { "❓  Help", "❓  Help" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Macro" },
            // top bar
            { "Multi-account launcher", "Multi-account launcher" },
            { "Running", "Actief" },
            { "Roblox CPU", "Roblox CPU" },
            { "Roblox memory", "Geheugen" },
            // launch / account controls (kept short to fit the buttons)
            { "Launch selected", "Start selectie" },
            { "Launch all", "Start alles" },
            { "Launch all listed", "Start alles" },
            { "Stop", "Stop" },
            { "Stop all", "Stop alles" },
            { "List", "Lijst" },
            { "Cards", "Kaarten" },
            // monitor page
            { "Monitor", "Monitor" },
            { "Focus", "Focus" },
            { "Min", "Min" },
            { "Close", "Sluiten" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Er draait geen Roblox-venster. Start enkele accounts en ze verschijnen hier." },
        };
    }
}
