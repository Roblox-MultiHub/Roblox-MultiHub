using System;
using System.Collections.Generic;
using System.IO;

namespace RobloxMultiHub
{
    public partial class Lang
    {
        public static string Current = "en";
        static readonly Dictionary<string, string> map = new Dictionary<string, string>();

        // code, display name. "en" is always first. RTL languages are intentionally absent.
        public static List<string[]> Available()
        {
            List<string[]> list = new List<string[]>
            {
                new string[] { "en", "English" },
                new string[] { "nl", "Nederlands (Dutch)" },
                new string[] { "es", "Español (Spanish)" },
                new string[] { "pt", "Português (Portuguese)" },
                new string[] { "fr", "Français (French)" },
                new string[] { "de", "Deutsch (German)" },
                new string[] { "it", "Italiano (Italian)" },
                new string[] { "pl", "Polski (Polish)" },
                new string[] { "tr", "Türkçe (Turkish)" },
                new string[] { "ru", "Русский (Russian)" },
                new string[] { "uk", "Українська (Ukrainian)" },
                new string[] { "hi", "हिन्दी (Hindi)" },
                new string[] { "id", "Bahasa Indonesia" },
                new string[] { "vi", "Tiếng Việt (Vietnamese)" },
                new string[] { "th", "ไทย (Thai)" },
                new string[] { "ko", "한국어 (Korean)" },
                new string[] { "ja", "日本語 (Japanese)" },
                new string[] { "zh-CN", "简体中文 (Chinese Simplified)" },
                new string[] { "zh-TW", "繁體中文 (Chinese Traditional)" }
            };
            try
            {
                string dir = Path.Combine(Store.Dir, "lang");
                if (Directory.Exists(dir))
                    foreach (string f in Directory.GetFiles(dir, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(f);
                        bool known = false;
                        foreach (string[] e in list) if (e[0] == code) known = true;
                        if (!known) list.Add(new string[] { code, code });
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
            if (Current == "nl") Fill(Dutch);
            if (Current == "es") Fill(Spanish);
            if (Current == "pt") Fill(Portuguese);
            if (Current == "fr") Fill(French);
            if (Current == "de") Fill(German);
            if (Current == "it") Fill(Italian);
            if (Current == "pl") Fill(Polish);
            if (Current == "tr") Fill(Turkish);
            if (Current == "ru") Fill(Russian);
            if (Current == "uk") Fill(Ukrainian);
            if (Current == "hi") Fill(Hindi);
            if (Current == "id") Fill(Indonesian);
            if (Current == "vi") Fill(Vietnamese);
            if (Current == "th") Fill(Thai);
            if (Current == "ko") Fill(Korean);
            if (Current == "ja") Fill(Japanese);
            if (Current == "zh-CN") Fill(ChineseSimplified);
            if (Current == "zh-TW") Fill(ChineseTraditional);

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

        static void Fill(Dictionary<string, string> d) { foreach (KeyValuePair<string, string> kv in d) map[kv.Key] = kv.Value; }

        // dictionaries live in Lang.<code>.cs (one file per language)
    }
}
