using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RobloxMultiHub
{
    // On-demand screenshots over a Discord BOT (opt-in, OFF by default). A webhook can only send, so a
    // command needs a bot: this watches ONE channel by polling the REST API (no gateway, no extra
    // libraries) and, when someone types the command (default "!shot"), posts a screenshot grid of all
    // running accounts back into that channel. The bot token is only ever sent to Discord.
    //
    // Reading message text over REST does NOT need the privileged "Message Content" intent (that only
    // gates the gateway); the bot just needs Read Messages / Read Message History / Send Messages /
    // Attach Files in the channel. Everything here is best-effort and never throws to the UI.
    public static class DiscordBot
    {
        const string Api = "https://discord.com/api/v10";
        const int PollMs = 4000;

        static readonly object gate = new object();
        static CancellationTokenSource cts;
        static HttpClient http;
        public static string Status = "off";

        public static bool Configured
        {
            get
            {
                return Store.Settings.DiscordBot
                    && !string.IsNullOrEmpty(Store.Settings.DiscordBotToken)
                    && !string.IsNullOrEmpty(Store.Settings.DiscordBotChannel);
            }
        }

        // Start, stop or restart to match the current settings. Safe to call from the UI thread and on
        // every settings change; it only actually restarts when something it needs changed.
        public static void Apply()
        {
            lock (gate)
            {
                StopLocked();
                if (!Configured) { Status = "off"; return; }

                string token = Store.Settings.DiscordBotToken.Trim();
                string channel = Store.Settings.DiscordBotChannel.Trim();
                http = new HttpClient();
                try
                {
                    http.Timeout = TimeSpan.FromSeconds(15);
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", token);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxMultiHub (https://github.com/Roblox-MultiHub/Roblox-MultiHub, 1.0)");
                }
                catch (Exception) { }
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                Status = "starting";
                Task.Run(delegate { return Run(channel, ct); });
            }
        }

        public static void Stop()
        {
            lock (gate) { StopLocked(); Status = "off"; }
        }

        static void StopLocked()
        {
            try { if (cts != null) cts.Cancel(); } catch (Exception) { }
            try { if (http != null) http.Dispose(); } catch (Exception) { }
            cts = null;
            http = null;
        }

        static async Task Run(string channel, CancellationToken ct)
        {
            HttpClient c = http;
            if (c == null) return;
            string botId = "";
            try
            {
                // who am I? (so the bot ignores its own messages, and for a friendly log line)
                HttpResponseMessage me = await c.GetAsync(Api + "/users/@me", ct);
                if ((int)me.StatusCode == 401)
                {
                    Status = "bad token";
                    Log.Write("Discord bot: the bot token was rejected (401). Check the token in Settings.");
                    return;
                }
                if (me.IsSuccessStatusCode)
                {
                    object j = Json.Parse(await me.Content.ReadAsStringAsync());
                    botId = Json.Str(j, "id");
                    string uname = Json.Str(j, "username");
                    Log.Write("Discord bot connected as " + (string.IsNullOrEmpty(uname) ? "(unknown)" : uname)
                        + ". Type " + Store.Settings.DiscordBotCmd + " in the channel for a screenshot.");
                }
                Status = "listening";
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { Log.Error("DiscordBot identify", ex); }

            // baseline: only react to messages sent AFTER we start, so old commands are not replayed
            string lastId = "";
            try { lastId = await LatestId(c, channel, ct); }
            catch (Exception) { }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    string url = Api + "/channels/" + channel + "/messages?limit=10"
                        + (string.IsNullOrEmpty(lastId) ? "" : "&after=" + lastId);
                    HttpResponseMessage r = await c.GetAsync(url, ct);
                    if ((int)r.StatusCode == 429)
                    {
                        await DelayForRetry(r, ct);
                        continue;
                    }
                    if ((int)r.StatusCode == 401) { Status = "bad token"; Log.Write("Discord bot: token rejected (401); stopping."); return; }
                    if ((int)r.StatusCode == 403) { Status = "no access"; Log.Write("Discord bot: no access to that channel (403). Invite the bot and give it Read/Send/Attach permissions."); await Task.Delay(PollMs * 3, ct); continue; }
                    if (r.IsSuccessStatusCode)
                    {
                        List<object> msgs = Json.Arr(Json.Parse(await r.Content.ReadAsStringAsync()));
                        // the API returns newest-first; walk oldest-first so lastId advances correctly
                        for (int i = msgs.Count - 1; i >= 0; i--)
                        {
                            object m = msgs[i];
                            string id = Json.Str(m, "id");
                            if (!string.IsNullOrEmpty(id)) lastId = Max(lastId, id);
                            object author = Json.Get(m, "author");
                            if (Json.Bool(author, "bot")) continue;             // ignore bots (incl. ourselves)
                            if (Json.Str(author, "id") == botId) continue;
                            if (IsCommand(Json.Str(m, "content")))
                                await HandleCommand(c, channel, ct);
                        }
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { Log.Error("DiscordBot poll", ex); }

                try { await Task.Delay(PollMs, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        static bool IsCommand(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            string c = content.Trim().ToLowerInvariant();
            string cmd = (Store.Settings.DiscordBotCmd ?? "!shot").Trim().ToLowerInvariant();
            if (c == cmd || c.StartsWith(cmd + " ")) return true;
            // handy built-in aliases, whatever the custom command is
            return c == "!shot" || c == "!screenshot" || c == "!accounts";
        }

        static async Task HandleCommand(HttpClient c, string channel, CancellationToken ct)
        {
            try
            {
                int count;
                byte[] png = AccountShot.CapturePng(out count);
                if (count == 0 || png == null)
                {
                    await PostText(c, channel, "No Roblox windows are running right now.", ct);
                    Log.Write("Discord bot: screenshot requested, but nothing is running.");
                    return;
                }
                int _c;
                string summary = AccountShot.Summary(out _c);
                await PostImage(c, channel, summary, png, ct);
                Log.Write("Discord bot: sent a screenshot of " + count + " account(s) on request.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Error("DiscordBot command", ex); }
        }

        static async Task PostImage(HttpClient c, string channel, string content, byte[] png, CancellationToken ct)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["content"] = content ?? "";
            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(Json.To(body), Encoding.UTF8), "payload_json");
            ByteArrayContent file = new ByteArrayContent(png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "files[0]", "accounts.png");
            HttpResponseMessage r = await c.PostAsync(Api + "/channels/" + channel + "/messages", form, ct);
            if ((int)r.StatusCode == 429) { await DelayForRetry(r, ct); }
        }

        static async Task PostText(HttpClient c, string channel, string content, CancellationToken ct)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["content"] = content ?? "";
            StringContent sc = new StringContent(Json.To(body), Encoding.UTF8, "application/json");
            HttpResponseMessage r = await c.PostAsync(Api + "/channels/" + channel + "/messages", sc, ct);
            if ((int)r.StatusCode == 429) { await DelayForRetry(r, ct); }
        }

        static async Task<string> LatestId(HttpClient c, string channel, CancellationToken ct)
        {
            HttpResponseMessage r = await c.GetAsync(Api + "/channels/" + channel + "/messages?limit=1", ct);
            if (!r.IsSuccessStatusCode) return "";
            List<object> msgs = Json.Arr(Json.Parse(await r.Content.ReadAsStringAsync()));
            return msgs.Count > 0 ? Json.Str(msgs[0], "id") : "";
        }

        static async Task DelayForRetry(HttpResponseMessage r, CancellationToken ct)
        {
            double sec = 2;
            try
            {
                object j = Json.Parse(await r.Content.ReadAsStringAsync());
                object v = Json.Get(j, "retry_after");
                if (v != null) sec = Math.Max(1, Math.Min(30, Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture)));
            }
            catch (Exception) { }
            try { await Task.Delay((int)(sec * 1000), ct); } catch (OperationCanceledException) { }
        }

        // Discord IDs are snowflakes: numerically ordered but too big for int. Compare as numbers.
        static string Max(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b;
            if (string.IsNullOrEmpty(b)) return a;
            if (a.Length != b.Length) return a.Length > b.Length ? a : b;
            return string.CompareOrdinal(a, b) >= 0 ? a : b;
        }
    }
}
