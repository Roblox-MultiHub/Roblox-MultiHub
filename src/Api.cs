using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RobloxMultiHub
{
    public class ApiResult
    {
        public HttpStatusCode Status;
        public string Body = "";
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool Ok { get { return (int)Status >= 200 && (int)Status < 300; } }
        public string Header(string name)
        {
            string v;
            return Headers.TryGetValue(name, out v) ? v : "";
        }
    }

    public class UserInfo
    {
        public long Id;
        public string Name = "";
        public string Display = "";
        public string Error = "";
        public bool Valid;
        public bool Locked;             // HTTP 403: account is locked / blocked / moderated by Roblox
    }

    public class TicketResult
    {
        public string Ticket;
        public string Error = "";
        public bool Unauthorized;
        public bool Locked;             // HTTP 403: account is locked / blocked / moderated by Roblox
    }

    public class PresenceInfo
    {
        public int Type;
        public string Location = "";
        public long PlaceId;
        public string GameId = "";
    }

    public class ServerInfo
    {
        public string Id = "";
        public int Playing;
        public int Max;
        public int Ping;
        public double Fps;
    }

    public class ServerPage
    {
        public List<ServerInfo> Servers = new List<ServerInfo>();
        public string Next = "";
        public string Error = "";
    }

    public class GameInfo
    {
        public string Name = "";
        public long PlaceId;
        public long Players;
    }

    public static class Api
    {
        static readonly HttpClient Http;

        static Api()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            HttpClientHandler h = new HttpClientHandler();
            h.UseCookies = false;
            h.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            Http = new HttpClient(h);
            Http.Timeout = TimeSpan.FromSeconds(25);
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        }

        static async Task<ApiResult> Req(HttpMethod method, string url, string cookie, string jsonBody, string csrf, Dictionary<string, string> extra)
        {
            using (HttpRequestMessage req = new HttpRequestMessage(method, url))
            {
                if (!string.IsNullOrEmpty(cookie)) req.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
                if (csrf != null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
                if (extra == null || !extra.ContainsKey("Referer")) req.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");
                if (extra == null || !extra.ContainsKey("Origin")) req.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
                if (extra != null)
                    foreach (KeyValuePair<string, string> kv in extra) if (!string.IsNullOrEmpty(kv.Value)) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                if (jsonBody != null) req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                using (HttpResponseMessage resp = await Http.SendAsync(req))
                {
                    ApiResult r = new ApiResult();
                    r.Status = resp.StatusCode;
                    r.Body = await resp.Content.ReadAsStringAsync();
                    foreach (KeyValuePair<string, IEnumerable<string>> h in resp.Headers)
                    {
                        string sep = h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ? "\n" : ",";
                        r.Headers[h.Key] = string.Join(sep, h.Value.ToArray());
                    }
                    return r;
                }
            }
        }

        // POST that transparently handles Roblox's CSRF handshake (first try -> 403 + token, second try works).
        static async Task<ApiResult> PostCsrf(string url, string cookie, string body, Dictionary<string, string> extra)
        {
            ApiResult r = await Req(HttpMethod.Post, url, cookie, body, null, extra);
            if ((int)r.Status == 403)
            {
                string token = r.Header("x-csrf-token");
                if (!string.IsNullOrEmpty(token)) r = await Req(HttpMethod.Post, url, cookie, body, token, extra);
            }
            return r;
        }

        public static async Task<bool> InternetOk()
        {
            try
            {
                ApiResult r = await Req(HttpMethod.Get, "https://users.roblox.com/v1/users/1", null, null, null, null);
                return (int)r.Status > 0;
            }
            catch (Exception) { return false; }
        }

        public static async Task<UserInfo> Authenticated(string cookie)
        {
            UserInfo u = new UserInfo();
            try
            {
                ApiResult r = await Req(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated", cookie, null, null, null);
                if (r.Status == HttpStatusCode.Unauthorized) { u.Error = "Cookie expired or invalid"; return u; }
                if ((int)r.Status == 403) { u.Locked = true; u.Error = "Account locked or blocked by Roblox (HTTP 403)"; return u; }
                if ((int)r.Status == 429) { u.Error = "Rate limited (HTTP 429) - try again in a minute"; return u; }
                if (!r.Ok) { u.Error = "HTTP " + (int)r.Status; return u; }
                object j = Json.Parse(r.Body);
                u.Id = Json.Long(j, "id");
                u.Name = Json.Str(j, "name");
                u.Display = Json.Str(j, "displayName");
                u.Valid = u.Id > 0;
                if (!u.Valid) u.Error = "Unexpected response";
            }
            catch (Exception ex) { u.Error = ex.Message; }
            return u;
        }

        public static async Task<TicketResult> GetTicket(string cookie)
        {
            TicketResult t = new TicketResult();
            try
            {
                Dictionary<string, string> hdr = new Dictionary<string, string>();
                hdr["Referer"] = "https://www.roblox.com/games/4924922222/Brookhaven-RP";
                hdr["Origin"] = "";                         // like Roblox Account Manager: no Origin header on the ticket request
                ApiResult r = await PostCsrf("https://auth.roblox.com/v1/authentication-ticket/", cookie, "", hdr);
                if (r.Status == HttpStatusCode.Unauthorized) { t.Unauthorized = true; t.Error = "cookie expired or invalid (HTTP 401)"; return t; }
                if ((int)r.Status == 403) { t.Locked = true; t.Error = "account locked or blocked by Roblox (HTTP 403)"; return t; }
                if ((int)r.Status == 429) { t.Error = "rate limited by Roblox (HTTP 429) - wait a bit and retry"; return t; }
                string ticket = r.Header("rbx-authentication-ticket");
                if (!r.Ok || string.IsNullOrEmpty(ticket)) { t.Error = "no authentication ticket received (HTTP " + (int)r.Status + ")"; return t; }
                t.Ticket = ticket;
            }
            catch (Exception ex) { t.Error = ex.Message; }
            return t;
        }

        // Exchanges the cookie for a brand-new one (this signs the account out of its other sessions).
        public static async Task<string> RotateCookie(string cookie, Action<string> log)
        {
            try
            {
                TicketResult t = await GetTicket(cookie);
                if (t.Ticket == null) { log("cookie refresh failed: " + t.Error); return null; }
                Dictionary<string, object> body = new Dictionary<string, object>();
                body["authenticationTicket"] = t.Ticket;
                Dictionary<string, string> h = new Dictionary<string, string>();
                h["RBXAuthenticationNegotiation"] = "1";
                ApiResult r = await PostCsrf("https://auth.roblox.com/v1/authentication-ticket/redeem", null, Json.To(body), h);
                Match m = Regex.Match(r.Header("Set-Cookie"), @"\.ROBLOSECURITY=([^;\s]+)");
                if (!m.Success) { log("cookie refresh failed: Roblox did not return a new cookie (HTTP " + (int)r.Status + ")"); return null; }
                string nc = m.Groups[1].Value;
                UserInfo u = await Authenticated(nc);
                if (!u.Valid) { log("cookie refresh failed: the new cookie did not validate (" + u.Error + ")"); return null; }
                return nc;
            }
            catch (Exception ex) { log("cookie refresh error: " + ex.Message); return null; }
        }

        public static async Task<Dictionary<long, PresenceInfo>> Presence(string cookie, List<long> ids)
        {
            Dictionary<long, PresenceInfo> res = new Dictionary<long, PresenceInfo>();
            if (ids.Count == 0) return res;
            try
            {
                for (int i = 0; i < ids.Count; i += 100)
                {
                    List<long> chunk = ids.Skip(i).Take(100).ToList();
                    Dictionary<string, object> body = new Dictionary<string, object>();
                    body["userIds"] = chunk.ToArray();
                    ApiResult r = await PostCsrf("https://presence.roblox.com/v1/presence/users", cookie, Json.To(body), null);
                    if (!r.Ok) continue;
                    object j = Json.Parse(r.Body);
                    foreach (object p in Json.Arr(Json.Get(j, "userPresences")))
                    {
                        PresenceInfo pi = new PresenceInfo();
                        pi.Type = (int)Json.Long(p, "userPresenceType");
                        pi.Location = Json.Str(p, "lastLocation");
                        pi.PlaceId = Json.Long(p, "placeId");
                        pi.GameId = Json.Str(p, "gameId");
                        res[Json.Long(p, "userId")] = pi;
                    }
                }
            }
            catch (Exception ex) { Log.Write("Presence lookup failed: " + ex.Message); }
            return res;
        }

        public static async Task<UserInfo> UserByName(string name)
        {
            UserInfo u = new UserInfo();
            try
            {
                Dictionary<string, object> body = new Dictionary<string, object>();
                body["usernames"] = new string[] { name };
                body["excludeBannedUsers"] = false;
                ApiResult r = await PostCsrf("https://users.roblox.com/v1/usernames/users", null, Json.To(body), null);
                if (!r.Ok) { u.Error = "HTTP " + (int)r.Status; return u; }
                foreach (object d in Json.Arr(Json.Get(Json.Parse(r.Body), "data")))
                {
                    u.Id = Json.Long(d, "id");
                    u.Name = Json.Str(d, "name");
                    u.Valid = u.Id > 0;
                    return u;
                }
                u.Error = "User not found";
            }
            catch (Exception ex) { u.Error = ex.Message; }
            return u;
        }

        public static async Task<ServerPage> Servers(long placeId, bool ascending, string cursor, string cookie)
        {
            ServerPage sp = new ServerPage();
            try
            {
                string url = "https://games.roblox.com/v1/games/" + placeId + "/servers/Public?sortOrder=" + (ascending ? "Asc" : "Desc") + "&excludeFullGames=false&limit=100";
                if (!string.IsNullOrEmpty(cursor)) url += "&cursor=" + Uri.EscapeDataString(cursor);
                ApiResult r = await Req(HttpMethod.Get, url, cookie, null, null, null);
                if ((int)r.Status == 429) { sp.Error = "Rate limited by Roblox (HTTP 429). Wait a minute and try again."; return sp; }
                if (!r.Ok) { sp.Error = "HTTP " + (int)r.Status + " " + Util.Trunc(r.Body, 100); return sp; }
                object j = Json.Parse(r.Body);
                sp.Next = Json.Str(j, "nextPageCursor");
                foreach (object s in Json.Arr(Json.Get(j, "data")))
                {
                    ServerInfo si = new ServerInfo();
                    si.Id = Json.Str(s, "id");
                    si.Playing = (int)Json.Long(s, "playing");
                    si.Max = (int)Json.Long(s, "maxPlayers");
                    si.Ping = (int)Json.Long(s, "ping");
                    object f = Json.Get(s, "fps");
                    if (f != null) { try { si.Fps = Convert.ToDouble(f, CultureInfo.InvariantCulture); } catch (Exception) { } }
                    if (si.Id.Length > 0) sp.Servers.Add(si);
                }
            }
            catch (Exception ex) { sp.Error = ex.Message; }
            return sp;
        }

        // Profile picture (head shot) URLs for up to 100 users at once.
        public static async Task<Dictionary<long, string>> AvatarUrls(List<long> ids)
        {
            Dictionary<long, string> res = new Dictionary<long, string>();
            if (ids.Count == 0) return res;
            try
            {
                string url = "https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds="
                    + string.Join(",", ids.Select(i => i.ToString()).ToArray()) + "&size=150x150&format=Png&isCircular=false";
                ApiResult r = await Req(HttpMethod.Get, url, null, null, null, null);
                if (!r.Ok) return res;
                foreach (object d in Json.Arr(Json.Get(Json.Parse(r.Body), "data")))
                {
                    if (Json.Str(d, "state") != "Completed") continue;
                    string u = Json.Str(d, "imageUrl");
                    if (u.Length > 0) res[Json.Long(d, "targetId")] = u;
                }
            }
            catch (Exception ex) { Log.Write("Avatar lookup failed: " + ex.Message); }
            return res;
        }

        public static async Task<byte[]> Download(string url)
        {
            try
            {
                using (HttpResponseMessage resp = await Http.GetAsync(url))
                {
                    if (!resp.IsSuccessStatusCode) return null;
                    return await resp.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception) { return null; }
        }

        public static async Task<string> PlaceName(long placeId)
        {
            try
            {
                ApiResult r = await Req(HttpMethod.Get, "https://apis.roblox.com/universes/v1/places/" + placeId + "/universe", null, null, null, null);
                if (!r.Ok) return "";
                long uni = Json.Long(Json.Parse(r.Body), "universeId");
                if (uni == 0) return "";
                ApiResult g = await Req(HttpMethod.Get, "https://games.roblox.com/v1/games?universeIds=" + uni, null, null, null, null);
                if (!g.Ok) return "";
                foreach (object d in Json.Arr(Json.Get(Json.Parse(g.Body), "data")))
                    return Json.Str(d, "name");
            }
            catch (Exception) { }
            return "";
        }

        public static async Task<List<GameInfo>> SearchGames(string query, string cookie)
        {
            List<GameInfo> list = new List<GameInfo>();
            try
            {
                string url = "https://apis.roblox.com/search-api/omni-search?searchQuery=" + Uri.EscapeDataString(query) + "&sessionId=" + Guid.NewGuid().ToString() + "&pageType=all";
                ApiResult r = await Req(HttpMethod.Get, url, cookie, null, null, null);
                if (r.Ok) CollectGames(Json.Parse(r.Body), list, 0);
                else Log.Write("Game search returned HTTP " + (int)r.Status);
            }
            catch (Exception ex) { Log.Write("Game search failed: " + ex.Message); }
            return list;
        }

        // The search response is nested; walk it and pick up anything that looks like a game.
        static void CollectGames(object node, List<GameInfo> acc, int depth)
        {
            if (node == null || depth > 8) return;
            IDictionary<string, object> d = Json.Obj(node);
            if (d != null)
            {
                if (d.ContainsKey("rootPlaceId") && d.ContainsKey("name"))
                {
                    GameInfo g = new GameInfo();
                    g.Name = Json.Str(d, "name");
                    g.PlaceId = Json.Long(d, "rootPlaceId");
                    g.Players = Json.Long(d, "playerCount");
                    long pid = g.PlaceId;
                    if (pid > 0 && !acc.Any(x => x.PlaceId == pid)) acc.Add(g);
                    return;
                }
                foreach (KeyValuePair<string, object> kv in d) CollectGames(kv.Value, acc, depth + 1);
                return;
            }
            foreach (object x in Json.Arr(node)) CollectGames(x, acc, depth + 1);
        }

        // New style private-server links: https://www.roblox.com/share?code=XXXX&type=Server
        public static async Task<LaunchSpec> ResolveShare(string code, string cookie)
        {
            try
            {
                string clean = Regex.Replace(code, "[^A-Za-z0-9]", "");
                Dictionary<string, object> body = new Dictionary<string, object>();
                body["linkId"] = clean;
                body["linkType"] = "Server";
                ApiResult r = await PostCsrf("https://apis.roblox.com/sharelinks/v1/resolve-link", cookie, Json.To(body), null);
                if (!r.Ok) return null;
                object d = Json.Get(Json.Parse(r.Body), "privateServerInviteData");
                LaunchSpec s = new LaunchSpec();
                s.Kind = "Private";
                s.PlaceId = Json.Long(d, "placeId");
                s.LinkCode = Json.Str(d, "linkCode");
                if (s.PlaceId > 0 && s.LinkCode.Length > 0) return s;
            }
            catch (Exception ex) { Log.Write("Share link resolve failed: " + ex.Message); }
            return null;
        }

        public static async Task<string> Details(Account a)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Username:      " + a.Username);
            sb.AppendLine("Display name:  " + a.DisplayName);
            sb.AppendLine("User ID:       " + a.UserId);
            try
            {
                ApiResult u = await Req(HttpMethod.Get, "https://users.roblox.com/v1/users/" + a.UserId, null, null, null, null);
                if (u.Ok)
                {
                    object j = Json.Parse(u.Body);
                    DateTime created;
                    if (DateTime.TryParse(Json.Str(j, "created"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out created))
                        sb.AppendLine("Account age:   " + (int)(DateTime.UtcNow - created).TotalDays + " days (created " + created.ToString("yyyy-MM-dd") + ")");
                    sb.AppendLine("Banned:        " + (Json.Bool(j, "isBanned") ? "yes" : "no"));
                }
                ApiResult e = await Req(HttpMethod.Get, "https://economy.roblox.com/v1/users/" + a.UserId + "/currency", a.Cookie, null, null, null);
                if (e.Ok) sb.AppendLine("Robux:         " + Json.Long(Json.Parse(e.Body), "robux"));
                else sb.AppendLine("Robux:         (could not load, HTTP " + (int)e.Status + ")");
                ApiResult p = await Req(HttpMethod.Get, "https://premiumfeatures.roblox.com/v1/users/" + a.UserId + "/validate-membership", a.Cookie, null, null, null);
                if (p.Ok) sb.AppendLine("Premium:       " + (p.Body.Trim().ToLowerInvariant() == "true" ? "yes" : "no"));
            }
            catch (Exception ex) { sb.AppendLine("(some details failed: " + ex.Message + ")"); }
            return sb.ToString();
        }
    }
}
