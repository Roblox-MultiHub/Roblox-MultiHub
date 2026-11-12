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

        // ---- live status embed: sent once, then EDITED in place on every refresh ----
        // The message id is persisted in settings, so an app restart keeps editing the SAME message
        // instead of posting a new one. The chart PNG rides along as an attachment the embed
        // references (attachment://chart.png) and is re-uploaded on every refresh.

        // Returns the message id, or "" when the post failed.
        static async Task<string> PostStatusAsync(string url, Dictionary<string, object> embed, byte[] chart)
        {
            HttpResponseMessage r = await Http.PostAsync(url.Trim(), StatusForm(embed, chart, false));
            if ((int)r.StatusCode == 429)
            {
                await Task.Delay(1500);
                r = await Http.PostAsync(url.Trim(), StatusForm(embed, chart, false));
            }
            r.EnsureSuccessStatusCode();
            IDictionary<string, object> msg = Json.Obj(Json.Parse(await r.Content.ReadAsStringAsync()));
            object id;
            return msg != null && msg.TryGetValue("id", out id) && id != null ? id.ToString() : "";
        }

        static MultipartFormDataContent StatusForm(Dictionary<string, object> embed, byte[] chart, bool forEdit)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["username"] = "Roblox MultiHub";
            body["embeds"] = new object[] { embed };
            if (chart != null && chart.Length > 0 && forEdit)
            {
                // on EDIT, attachments not listed here are dropped and files[] are APPENDED -
                // without this the old chart stayed and every refresh piled up a new file
                Dictionary<string, object> keep = new Dictionary<string, object>();
                keep["id"] = 0;                     // 0 = the index of files[0] being uploaded now
                keep["filename"] = "chart.png";
                body["attachments"] = new object[] { keep };
            }
            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(Json.To(body), Encoding.UTF8), "payload_json");
            if (chart != null && chart.Length > 0)
            {
                ByteArrayContent file = new ByteArrayContent(chart);
                file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                form.Add(file, "files[0]", "chart.png");
            }
            return form;
        }

        public static void SendStatus(string title, string desc, int color, byte[] chart)
        {
            if (!Enabled) return;
            Dictionary<string, object> embed = new Dictionary<string, object>();
            embed["title"] = title ?? "";
            embed["description"] = desc ?? "";
            embed["color"] = color;
            embed["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            Dictionary<string, object> foot = new Dictionary<string, object>();
            foot["text"] = "Roblox MultiHub";
            embed["footer"] = foot;
            if (chart != null && chart.Length > 0)
            {
                Dictionary<string, object> img = new Dictionary<string, object>();
                img["url"] = "attachment://chart.png";
                embed["image"] = img;
            }
            string url = Store.Settings.WebhookUrl;
            string id = Store.Settings.WebhookStatusMsgId;
            Task.Run(async delegate
            {
                try
                {
                    if (id != null && id.Length > 0)
                    {
                        // edit the existing status message in place; if it was deleted, send a new one
                        HttpRequestMessage patch = new HttpRequestMessage(new HttpMethod("PATCH"),
                            url.TrimEnd('/') + "/messages/" + id);
                        patch.Content = StatusForm(embed, chart, true);
                        HttpResponseMessage r = await Http.SendAsync(patch);
                        if ((int)r.StatusCode != 404)
                        {
                            r.EnsureSuccessStatusCode();
                            return;
                        }
                    }
                    string newId = await PostStatusAsync(url, embed, chart);
                    if (newId.Length > 0)
                    {
                        Store.Settings.WebhookStatusMsgId = newId;
                        Store.SaveSettings();
                    }
                }
                catch (Exception ex) { Log.Error("WebhookStatus", ex); }
            });
        }

        // Always sent (used by the Lang.T("Send a test message") button, which passes its own URL).
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
