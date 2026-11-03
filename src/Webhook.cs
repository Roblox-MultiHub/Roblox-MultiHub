using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RobloxMultiHub
{
    // Posts short alerts to a Discord webhook the user pasted in Settings. Opt-in, and the URL
    // is only ever sent to Discord. Everything here is fire-and-forget: a failure is logged, never thrown.
    public static class Webhook
    {
        public static readonly int Green = 0x57F287;
        public static readonly int Red = 0xED4245;
        public static readonly int Yellow = 0xFEE75C;
        public static readonly int Blurple = 0x5865F2;

        static readonly HttpClient Http = new HttpClient();

        static Webhook()
        {
            try { Http.Timeout = TimeSpan.FromSeconds(10); } catch (Exception) { }
        }

        public static bool Enabled
        {
            get { return Store.Settings.Webhook && LooksLikeWebhook(Store.Settings.WebhookUrl); }
        }

        public static bool LooksLikeWebhook(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            url = url.Trim();
            return url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://canary.discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://ptb.discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
        }

        // Only sent when alerts are on and this kind is enabled.
        public static void Send(string title, string desc, int color)
        {
            if (!Enabled) return;
            Post(Store.Settings.WebhookUrl, title, desc, color);
        }

        // Always sent (used by the "Send a test message" button, which passes its own URL).
        public static void SendTo(string url, string title, string desc, int color)
        {
            Post(url, title, desc, color);
        }

        static void Post(string url, string title, string desc, int color)
        {
            if (!LooksLikeWebhook(url)) return;
            Task.Run(async delegate
            {
                try
                {
                    Dictionary<string, object> embed = new Dictionary<string, object>();
                    embed["title"] = title ?? "";
                    embed["description"] = desc ?? "";
                    embed["color"] = color;
                    embed["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    Dictionary<string, object> foot = new Dictionary<string, object>();
                    foot["text"] = "Roblox MultiHub";
                    embed["footer"] = foot;

                    Dictionary<string, object> body = new Dictionary<string, object>();
                    body["username"] = "Roblox MultiHub";
                    body["embeds"] = new object[] { embed };

                    StringContent content = new StringContent(Json.To(body), Encoding.UTF8, "application/json");
                    HttpResponseMessage r = await Http.PostAsync(url.Trim(), content);
                    // Discord answers 429 with a retry-after when you post too fast; wait once and retry.
                    if ((int)r.StatusCode == 429)
                    {
                        await Task.Delay(1500);
                        await Http.PostAsync(url.Trim(), new StringContent(Json.To(body), Encoding.UTF8, "application/json"));
                    }
                }
                catch (Exception ex) { Log.Error("Webhook", ex); }
            });
        }
    }
}
