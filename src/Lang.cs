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
                new string[] { "ar", "العربية (Arabic)" },
                new string[] { "hi", "हिन्दी (Hindi)" },
                new string[] { "id", "Bahasa Indonesia" },
                new string[] { "vi", "Tiếng Việt (Vietnamese)" },
                new string[] { "th", "ไทย (Thai)" },
                new string[] { "ko", "한국어 (Korean)" },
                new string[] { "ja", "日本語 (Japanese)" },
                new string[] { "zh-CN", "简体中文 (Chinese Simplified)" },
                new string[] { "zh-TW", "繁體中文 (Chinese Traditional)" },
                new string[] { "he", "עברית (Hebrew)" }
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
            if (Current == "ar") Fill(Arabic);
            if (Current == "hi") Fill(Hindi);
            if (Current == "id") Fill(Indonesian);
            if (Current == "vi") Fill(Vietnamese);
            if (Current == "th") Fill(Thai);
            if (Current == "ko") Fill(Korean);
            if (Current == "ja") Fill(Japanese);
            if (Current == "zh-CN") Fill(ChineseSimplified);
            if (Current == "zh-TW") Fill(ChineseTraditional);
            if (Current == "he") Fill(Hebrew);

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

        static readonly Dictionary<string, string> Dutch = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Starten" },
            { "\U0001F310  Servers", "\U0001F310  Servers" },
            { "\u25A6  Layout", "\u25A6  Indeling" },
            { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Presets" },
            { "⚡  Perf", "⚡  Prestaties" },
            { "⚙  Settings", "⚙  Opties" },
            { "❓  Help", "❓  Help" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Macro" },
            { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Multi-account launcher" },
            { "Running", "Actief" },
            { "Roblox CPU", "Roblox CPU" },
            { "Roblox memory", "Geheugen" },
            { "Launch selected", "Start selectie" },
            { "Launch all", "Start alles" },
            { "Launch all listed", "Start alles" },
            { "Stop", "Stop" },
            { "Stop all", "Stop alles" },
            { "List", "Lijst" },
            { "Cards", "Kaarten" },
            { "Monitor", "Monitor" },
            { "Focus", "Focus" },
            { "Min", "Min" },
            { "Close", "Sluiten" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Er draait geen Roblox-venster. Start enkele accounts en ze verschijnen hier." },
        };

        // Languages with non-Latin scripts get RTL text direction for free from Windows text
        // shaping; the layout itself stays LTR (manual positioning does not mirror safely).
        // The lang files on disk can extend or override every entry of these tables.

        static readonly Dictionary<string, string> Spanish = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Iniciar" }, { "\U0001F310  Servers", "\U0001F310  Servidores" },
            { "\u25A6  Layout", "\u25A6  Diseño" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Perfiles" }, { "⚡  Perf", "⚡  Rendimiento" },
            { "⚙  Settings", "⚙  Ajustes" }, { "❓  Help", "❓  Ayuda" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Macro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Lanzador multi-cuenta" }, { "Running", "Activos" },
            { "Roblox CPU", "CPU de Roblox" }, { "Roblox memory", "Memoria de Roblox" },
            { "Launch selected", "Iniciar selección" }, { "Launch all", "Iniciar todo" },
            { "Launch all listed", "Iniciar todo" }, { "Stop", "Detener" }, { "Stop all", "Detener todo" },
            { "List", "Lista" }, { "Cards", "Tarjetas" }, { "Monitor", "Monitor" },
            { "Focus", "Enfocar" }, { "Min", "Min" }, { "Close", "Cerrar" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "No hay ventanas de Roblox abiertas. Inicia cuentas y aparecerán aquí." }
        };

        static readonly Dictionary<string, string> Portuguese = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Iniciar" }, { "\U0001F310  Servers", "\U0001F310  Servidores" },
            { "\u25A6  Layout", "\u25A6  Layout" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Perfis" }, { "⚡  Perf", "⚡  Desempenho" },
            { "⚙  Settings", "⚙  Config." }, { "❓  Help", "❓  Ajuda" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Macro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Lançador multi-conta" }, { "Running", "Ativos" },
            { "Roblox CPU", "CPU do Roblox" }, { "Roblox memory", "Memória do Roblox" },
            { "Launch selected", "Iniciar seleção" }, { "Launch all", "Iniciar tudo" },
            { "Launch all listed", "Iniciar tudo" }, { "Stop", "Parar" }, { "Stop all", "Parar tudo" },
            { "List", "Lista" }, { "Cards", "Cartões" }, { "Monitor", "Monitor" },
            { "Focus", "Focar" }, { "Min", "Min" }, { "Close", "Fechar" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Nenhuma janela do Roblox aberta. Inicie contas e elas aparecerão aqui." }
        };

        static readonly Dictionary<string, string> French = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Lancer" }, { "\U0001F310  Servers", "\U0001F310  Serveurs" },
            { "\u25A6  Layout", "\u25A6  Disposition" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Moniteur" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Profils" }, { "⚡  Perf", "⚡  Performances" },
            { "⚙  Settings", "⚙  Réglages" }, { "❓  Help", "❓  Aide" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Macro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Bêta" },
            { "Multi-account launcher", "Lanceur multi-comptes" }, { "Running", "Actifs" },
            { "Roblox CPU", "CPU Roblox" }, { "Roblox memory", "Mémoire Roblox" },
            { "Launch selected", "Lancer la sélection" }, { "Launch all", "Tout lancer" },
            { "Launch all listed", "Tout lancer" }, { "Stop", "Arrêter" }, { "Stop all", "Tout arrêter" },
            { "List", "Liste" }, { "Cards", "Cartes" }, { "Monitor", "Moniteur" },
            { "Focus", "Focus" }, { "Min", "Min" }, { "Close", "Fermer" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Aucune fenêtre Roblox ouverte. Lancez des comptes et elles apparaîtront ici." }
        };

        static readonly Dictionary<string, string> German = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Starten" }, { "\U0001F310  Servers", "\U0001F310  Server" },
            { "\u25A6  Layout", "\u25A6  Layout" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Profile" }, { "⚡  Perf", "⚡  Leistung" },
            { "⚙  Settings", "⚙  Optionen" }, { "❓  Help", "❓  Hilfe" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Makro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Multi-Account-Starter" }, { "Running", "Aktiv" },
            { "Roblox CPU", "Roblox-CPU" }, { "Roblox memory", "Roblox-Speicher" },
            { "Launch selected", "Auswahl starten" }, { "Launch all", "Alle starten" },
            { "Launch all listed", "Alle starten" }, { "Stop", "Stopp" }, { "Stop all", "Alle stoppen" },
            { "List", "Liste" }, { "Cards", "Karten" }, { "Monitor", "Monitor" },
            { "Focus", "Fokussieren" }, { "Min", "Min" }, { "Close", "Schließen" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Kein Roblox-Fenster offen. Starte Accounts, dann erscheinen sie hier." }
        };

        static readonly Dictionary<string, string> Italian = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Avvia" }, { "\U0001F310  Servers", "\U0001F310  Server" },
            { "\u25A6  Layout", "\u25A6  Layout" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Profili" }, { "⚡  Perf", "⚡  Prestazioni" },
            { "⚙  Settings", "⚙  Impostazioni" }, { "❓  Help", "❓  Aiuto" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Macro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Avviatore multi-account" }, { "Running", "Attivi" },
            { "Roblox CPU", "CPU Roblox" }, { "Roblox memory", "Memoria Roblox" },
            { "Launch selected", "Avvia selezione" }, { "Launch all", "Avvia tutti" },
            { "Launch all listed", "Avvia tutti" }, { "Stop", "Ferma" }, { "Stop all", "Ferma tutti" },
            { "List", "Elenco" }, { "Cards", "Schede" }, { "Monitor", "Monitor" },
            { "Focus", "Messa a fuoco" }, { "Min", "Min" }, { "Close", "Chiudi" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Nessuna finestra Roblox aperta. Avvia degli account e compariranno qui." }
        };

        static readonly Dictionary<string, string> Polish = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Uruchom" }, { "\U0001F310  Servers", "\U0001F310  Serwery" },
            { "\u25A6  Layout", "\u25A6  Układ" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Profile" }, { "⚡  Perf", "⚡  Wydajność" },
            { "⚙  Settings", "⚙  Ustawienia" }, { "❓  Help", "❓  Pomoc" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Makro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Launcher wielokontowy" }, { "Running", "Aktywne" },
            { "Roblox CPU", "CPU Roblox" }, { "Roblox memory", "Pamięć Roblox" },
            { "Launch selected", "Uruchom zaznaczone" }, { "Launch all", "Uruchom wszystkie" },
            { "Launch all listed", "Uruchom wszystkie" }, { "Stop", "Stop" }, { "Stop all", "Zatrzymaj wszystkie" },
            { "List", "Lista" }, { "Cards", "Karty" }, { "Monitor", "Monitor" },
            { "Focus", "Przybliż" }, { "Min", "Min" }, { "Close", "Zamknij" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Brak otwartych okien Roblox. Uruchom konta, a pojawią się tutaj." }
        };

        static readonly Dictionary<string, string> Turkish = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Başlat" }, { "\U0001F310  Servers", "\U0001F310  Sunucular" },
            { "\u25A6  Layout", "\u25A6  Yerleşim" }, { "\U0001F4CA  Monitor", "\U0001F4CA  İzleyici" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Profiller" }, { "⚡  Perf", "⚡  Performans" },
            { "⚙  Settings", "⚙  Ayarlar" }, { "❓  Help", "❓  Yardım" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Makro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Çoklu hesap başlatıcı" }, { "Running", "Aktif" },
            { "Roblox CPU", "Roblox CPU" }, { "Roblox memory", "Roblox belleği" },
            { "Launch selected", "Seçileni başlat" }, { "Launch all", "Tümünü başlat" },
            { "Launch all listed", "Tümünü başlat" }, { "Stop", "Durdur" }, { "Stop all", "Tümünü durdur" },
            { "List", "Liste" }, { "Cards", "Kartlar" }, { "Monitor", "İzleyici" },
            { "Focus", "Odakla" }, { "Min", "Min" }, { "Close", "Kapat" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Açık Roblox penceresi yok. Hesapları başlat, burada görünecekler." }
        };

        static readonly Dictionary<string, string> Russian = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Запуск" }, { "\U0001F310  Servers", "\U0001F310  Серверы" },
            { "\u25A6  Layout", "\u25A6  Раскладка" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Монитор" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Профили" }, { "⚡  Perf", "⚡  Производительность" },
            { "⚙  Settings", "⚙  Настройки" }, { "❓  Help", "❓  Помощь" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Макрос" }, { "\U0001F9EA  Beta", "\U0001F9EA  Бета" },
            { "Multi-account launcher", "Мульти-аккаунт лаунчер" }, { "Running", "Активно" },
            { "Roblox CPU", "ЦП Roblox" }, { "Roblox memory", "Память Roblox" },
            { "Launch selected", "Запустить выбранные" }, { "Launch all", "Запустить все" },
            { "Launch all listed", "Запустить все" }, { "Stop", "Стоп" }, { "Stop all", "Остановить все" },
            { "List", "Список" }, { "Cards", "Карточки" }, { "Monitor", "Монитор" },
            { "Focus", "Фокус" }, { "Min", "Мин" }, { "Close", "Закрыть" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Нет открытых окон Roblox. Запустите аккаунты — они появятся здесь." }
        };

        static readonly Dictionary<string, string> Ukrainian = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Запуск" }, { "\U0001F310  Servers", "\U0001F310  Сервери" },
            { "\u25A6  Layout", "\u25A6  Розкладка" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Монітор" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Профілі" }, { "⚡  Perf", "⚡  Продуктивність" },
            { "⚙  Settings", "⚙  Налаштування" }, { "❓  Help", "❓  Довідка" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Макрос" }, { "\U0001F9EA  Beta", "\U0001F9EA  Бета" },
            { "Multi-account launcher", "Мульти-акаунт лаунчер" }, { "Running", "Активні" },
            { "Roblox CPU", "ЦП Roblox" }, { "Roblox memory", "Пам'ять Roblox" },
            { "Launch selected", "Запустити вибрані" }, { "Launch all", "Запустити всі" },
            { "Launch all listed", "Запустити всі" }, { "Stop", "Стоп" }, { "Stop all", "Зупинити всі" },
            { "List", "Список" }, { "Cards", "Картки" }, { "Monitor", "Монітор" },
            { "Focus", "Фокус" }, { "Min", "Мін" }, { "Close", "Закрити" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Немає відкритих вікон Roblox. Запустіть акаунти — вони з'являться тут." }
        };

        static readonly Dictionary<string, string> Arabic = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  تشغيل" }, { "\U0001F310  Servers", "\U0001F310  السيرفرات" },
            { "\u25A6  Layout", "\u25A6  التخطيط" }, { "\U0001F4CA  Monitor", "\U0001F4CA  المراقبة" },
            { "\U0001F4BE  Presets", "\U0001F4BE  الإعدادات المحفوظة" }, { "⚡  Perf", "⚡  الأداء" },
            { "⚙  Settings", "⚙  الإعدادات" }, { "❓  Help", "❓  المساعدة" },
            { "\U0001F3AC  Macro", "\U0001F3AC  الماكرو" }, { "\U0001F9EA  Beta", "\U0001F9EA  بيتا" },
            { "Multi-account launcher", "مشغل متعدد الحسابات" }, { "Running", "قيد التشغيل" },
            { "Roblox CPU", "معالج روبلوكس" }, { "Roblox memory", "ذاكرة روبلوكس" },
            { "Launch selected", "تشغيل المحدد" }, { "Launch all", "تشغيل الكل" },
            { "Launch all listed", "تشغيل الكل" }, { "Stop", "إيقاف" }, { "Stop all", "إيقاف الكل" },
            { "List", "قائمة" }, { "Cards", "بطاقات" }, { "Monitor", "المراقبة" },
            { "Focus", "تركيز" }, { "Min", "تصغير" }, { "Close", "إغلاق" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "لا توجد نوافذ روبلوكس مفتوحة. شغّل بعض الحسابات وستظهر هنا." }
        };

        static readonly Dictionary<string, string> Hebrew = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  הפעלה" }, { "\U0001F310  Servers", "\U0001F310  שרתים" },
            { "\u25A6  Layout", "\u25A6  פריסה" }, { "\U0001F4CA  Monitor", "\U0001F4CA  מוניטור" },
            { "\U0001F4BE  Presets", "\U0001F4BE  פרופילים" }, { "⚡  Perf", "⚡  ביצועים" },
            { "⚙  Settings", "⚙  הגדרות" }, { "❓  Help", "❓  עזרה" },
            { "\U0001F3AC  Macro", "\U0001F3AC  מאקרו" }, { "\U0001F9EA  Beta", "\U0001F9EA  בטא" },
            { "Multi-account launcher", "מריץ רב-חשבונות" }, { "Running", "פעילים" },
            { "Roblox CPU", "מעבד רובלוקס" }, { "Roblox memory", "זיכרון רובלוקס" },
            { "Launch selected", "הפעל בחירה" }, { "Launch all", "הפעל הכול" },
            { "Launch all listed", "הפעל הכול" }, { "Stop", "עצור" }, { "Stop all", "עצור הכול" },
            { "List", "רשימה" }, { "Cards", "כרטיסים" }, { "Monitor", "מוניטור" },
            { "Focus", "מיקוד" }, { "Min", "מזער" }, { "Close", "סגור" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "אין חלונות רובלוקס פתוחים. הפעל חשבונות והם יופיעו כאן." }
        };

        static readonly Dictionary<string, string> Hindi = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  लॉन्च" }, { "\U0001F310  Servers", "\U0001F310  सर्वर" },
            { "\u25A6  Layout", "\u25A6  लेआउट" }, { "\U0001F4CA  Monitor", "\U0001F4CA  मॉनिटर" },
            { "\U0001F4BE  Presets", "\U0001F4BE  प्रीसेट" }, { "⚡  Perf", "⚡  परफ़ॉर्मेंस" },
            { "⚙  Settings", "⚙  सेटिंग्स" }, { "❓  Help", "❓  सहायता" },
            { "\U0001F3AC  Macro", "\U0001F3AC  मैक्रो" }, { "\U0001F9EA  Beta", "\U0001F9EA  बीटा" },
            { "Multi-account launcher", "मल्टी-अकाउंट लॉन्चर" }, { "Running", "चालू" },
            { "Roblox CPU", "रोब्लॉक्स CPU" }, { "Roblox memory", "रोब्लॉक्स मेमोरी" },
            { "Launch selected", "चयनित लॉन्च करें" }, { "Launch all", "सब लॉन्च करें" },
            { "Launch all listed", "सब लॉन्च करें" }, { "Stop", "रोकें" }, { "Stop all", "सब रोकें" },
            { "List", "सूची" }, { "Cards", "कार्ड" }, { "Monitor", "मॉनिटर" },
            { "Focus", "फोकस" }, { "Min", "छोटा" }, { "Close", "बंद करें" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "कोई रोब्लॉक्स विंडो नहीं चल रही। अकाउंट लॉन्च करें, वे यहाँ दिखेंगे।" }
        };

        static readonly Dictionary<string, string> Indonesian = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Mulai" }, { "\U0001F310  Servers", "\U0001F310  Server" },
            { "\u25A6  Layout", "\u25A6  Tata letak" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Monitor" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Preset" }, { "⚡  Perf", "⚡  Performa" },
            { "⚙  Settings", "⚙  Pengaturan" }, { "❓  Help", "❓  Bantuan" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Makro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Peluncur multi-akun" }, { "Running", "Aktif" },
            { "Roblox CPU", "CPU Roblox" }, { "Roblox memory", "Memori Roblox" },
            { "Launch selected", "Mulai terpilih" }, { "Launch all", "Mulai semua" },
            { "Launch all listed", "Mulai semua" }, { "Stop", "Hentikan" }, { "Stop all", "Hentikan semua" },
            { "List", "Daftar" }, { "Cards", "Kartu" }, { "Monitor", "Monitor" },
            { "Focus", "Fokus" }, { "Min", "Min" }, { "Close", "Tutup" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Tidak ada jendela Roblox berjalan. Mulai akun dan akan muncul di sini." }
        };

        static readonly Dictionary<string, string> Vietnamese = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  Khởi chạy" }, { "\U0001F310  Servers", "\U0001F310  Máy chủ" },
            { "\u25A6  Layout", "\u25A6  Bố cục" }, { "\U0001F4CA  Monitor", "\U0001F4CA  Giám sát" },
            { "\U0001F4BE  Presets", "\U0001F4BE  Cấu hình" }, { "⚡  Perf", "⚡  Hiệu năng" },
            { "⚙  Settings", "⚙  Cài đặt" }, { "❓  Help", "❓  Trợ giúp" },
            { "\U0001F3AC  Macro", "\U0001F3AC  Macro" }, { "\U0001F9EA  Beta", "\U0001F9EA  Beta" },
            { "Multi-account launcher", "Trình khởi chạy đa tài khoản" }, { "Running", "Đang chạy" },
            { "Roblox CPU", "CPU Roblox" }, { "Roblox memory", "Bộ nhớ Roblox" },
            { "Launch selected", "Chạy mục đã chọn" }, { "Launch all", "Chạy tất cả" },
            { "Launch all listed", "Chạy tất cả" }, { "Stop", "Dừng" }, { "Stop all", "Dừng tất cả" },
            { "List", "Danh sách" }, { "Cards", "Thẻ" }, { "Monitor", "Giám sát" },
            { "Focus", "Tập trung" }, { "Min", "Thu nhỏ" }, { "Close", "Đóng" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Không có cửa sổ Roblox nào đang chạy. Khởi chạy tài khoản và chúng sẽ hiện ở đây." }
        };

        static readonly Dictionary<string, string> Thai = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  เปิด" }, { "\U0001F310  Servers", "\U0001F310  เซิร์ฟเวอร์" },
            { "\u25A6  Layout", "\u25A6  เลย์เอาต์" }, { "\U0001F4CA  Monitor", "\U0001F4CA  มอนิเตอร์" },
            { "\U0001F4BE  Presets", "\U0001F4BE  พรีเซ็ต" }, { "⚡  Perf", "⚡  เพอร์ฟอร์แมนซ์" },
            { "⚙  Settings", "⚙  ตั้งค่า" }, { "❓  Help", "❓  ช่วยเหลือ" },
            { "\U0001F3AC  Macro", "\U0001F3AC  มาโคร" }, { "\U0001F9EA  Beta", "\U0001F9EA  เบต้า" },
            { "Multi-account launcher", "ตัวเปิดหลายบัญชี" }, { "Running", "กำลังทำงาน" },
            { "Roblox CPU", "ซีพียู Roblox" }, { "Roblox memory", "หน่วยความจำ Roblox" },
            { "Launch selected", "เปิดที่เลือก" }, { "Launch all", "เปิดทั้งหมด" },
            { "Launch all listed", "เปิดทั้งหมด" }, { "Stop", "หยุด" }, { "Stop all", "หยุดทั้งหมด" },
            { "List", "รายการ" }, { "Cards", "การ์ด" }, { "Monitor", "มอนิเตอร์" },
            { "Focus", "โฟกัส" }, { "Min", "ย่อ" }, { "Close", "ปิด" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "ไม่มีหน้าต่าง Roblox ที่กำลังทำงาน เปิดบัญชีแล้วจะปรากฏที่นี่" }
        };

        static readonly Dictionary<string, string> Korean = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  실행" }, { "\U0001F310  Servers", "\U0001F310  서버" },
            { "\u25A6  Layout", "\u25A6  레이아웃" }, { "\U0001F4CA  Monitor", "\U0001F4CA  모니터" },
            { "\U0001F4BE  Presets", "\U0001F4BE  프리셋" }, { "⚡  Perf", "⚡  성능" },
            { "⚙  Settings", "⚙  설정" }, { "❓  Help", "❓  도움말" },
            { "\U0001F3AC  Macro", "\U0001F3AC  매크로" }, { "\U0001F9EA  Beta", "\U0001F9EA  베타" },
            { "Multi-account launcher", "다중 계정 런처" }, { "Running", "실행 중" },
            { "Roblox CPU", "Roblox CPU" }, { "Roblox memory", "Roblox 메모리" },
            { "Launch selected", "선택 항목 실행" }, { "Launch all", "모두 실행" },
            { "Launch all listed", "모두 실행" }, { "Stop", "정지" }, { "Stop all", "모두 정지" },
            { "List", "목록" }, { "Cards", "카드" }, { "Monitor", "모니터" },
            { "Focus", "포커스" }, { "Min", "최소화" }, { "Close", "닫기" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "실행 중인 Roblox 창이 없습니다. 계정을 실행하면 여기에 나타납니다." }
        };

        static readonly Dictionary<string, string> Japanese = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  起動" }, { "\U0001F310  Servers", "\U0001F310  サーバー" },
            { "\u25A6  Layout", "\u25A6  レイアウト" }, { "\U0001F4CA  Monitor", "\U0001F4CA  モニター" },
            { "\U0001F4BE  Presets", "\U0001F4BE  プリセット" }, { "⚡  Perf", "⚡  パフォーマンス" },
            { "⚙  Settings", "⚙  設定" }, { "❓  Help", "❓  ヘルプ" },
            { "\U0001F3AC  Macro", "\U0001F3AC  マクロ" }, { "\U0001F9EA  Beta", "\U0001F9EA  ベータ" },
            { "Multi-account launcher", "マルチアカウントランチャー" }, { "Running", "実行中" },
            { "Roblox CPU", "Roblox CPU" }, { "Roblox memory", "Robloxメモリ" },
            { "Launch selected", "選択を起動" }, { "Launch all", "すべて起動" },
            { "Launch all listed", "すべて起動" }, { "Stop", "停止" }, { "Stop all", "すべて停止" },
            { "List", "リスト" }, { "Cards", "カード" }, { "Monitor", "モニター" },
            { "Focus", "フォーカス" }, { "Min", "最小化" }, { "Close", "閉じる" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "Robloxのウィンドウが実行されていません。アカウントを起動するとここに表示されます。" }
        };

        static readonly Dictionary<string, string> ChineseSimplified = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  启动" }, { "\U0001F310  Servers", "\U0001F310  服务器" },
            { "\u25A6  Layout", "\u25A6  布局" }, { "\U0001F4CA  Monitor", "\U0001F4CA  监控" },
            { "\U0001F4BE  Presets", "\U0001F4BE  预设" }, { "⚡  Perf", "⚡  性能" },
            { "⚙  Settings", "⚙  设置" }, { "❓  Help", "❓  帮助" },
            { "\U0001F3AC  Macro", "\U0001F3AC  宏" }, { "\U0001F9EA  Beta", "\U0001F9EA  测试版" },
            { "Multi-account launcher", "多账号启动器" }, { "Running", "运行中" },
            { "Roblox CPU", "Roblox CPU" }, { "Roblox memory", "Roblox 内存" },
            { "Launch selected", "启动所选" }, { "Launch all", "全部启动" },
            { "Launch all listed", "全部启动" }, { "Stop", "停止" }, { "Stop all", "全部停止" },
            { "List", "列表" }, { "Cards", "卡片" }, { "Monitor", "监控" },
            { "Focus", "聚焦" }, { "Min", "最小化" }, { "Close", "关闭" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "没有正在运行的 Roblox 窗口。启动账号后它们会显示在这里。" }
        };

        static readonly Dictionary<string, string> ChineseTraditional = new Dictionary<string, string>
        {
            { "\U0001F680  Launch", "\U0001F680  啟動" }, { "\U0001F310  Servers", "\U0001F310  伺服器" },
            { "\u25A6  Layout", "\u25A6  版面" }, { "\U0001F4CA  Monitor", "\U0001F4CA  監控" },
            { "\U0001F4BE  Presets", "\U0001F4BE  預設" }, { "⚡  Perf", "⚡  效能" },
            { "⚙  Settings", "⚙  設定" }, { "❓  Help", "❓  說明" },
            { "\U0001F3AC  Macro", "\U0001F3AC  巨集" }, { "\U0001F9EA  Beta", "\U0001F9EA  測試版" },
            { "Multi-account launcher", "多帳號啟動器" }, { "Running", "執行中" },
            { "Roblox CPU", "Roblox CPU" }, { "Roblox memory", "Roblox 記憶體" },
            { "Launch selected", "啟動所選" }, { "Launch all", "全部啟動" },
            { "Launch all listed", "全部啟動" }, { "Stop", "停止" }, { "Stop all", "全部停止" },
            { "List", "清單" }, { "Cards", "卡片" }, { "Monitor", "監控" },
            { "Focus", "聚焦" }, { "Min", "最小化" }, { "Close", "關閉" },
            { "No Roblox window is running. Launch some accounts and they will show up here.",
              "沒有正在執行的 Roblox 視窗。啟動帳號後它們會顯示在這裡。" }
        };
    }
}
