using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RobloxMultiHub
{
    // Opens a clean, throw-away Edge/Chrome profile on roblox.com/login. You log in normally (captcha, 2-step,
    // passkeys all work). As soon as the browser has a valid .ROBLOSECURITY cookie we read it through the
    // browser's own debugging port, close the browser and delete the temporary profile.
    public static class BrowserLogin
    {
        public static string FindBrowser()
        {
            string pf = Environment.GetEnvironmentVariable("ProgramFiles");
            string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            List<string> c = new List<string>();
            if (pf86 != null) c.Add(Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"));
            if (pf != null) c.Add(Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"));
            if (pf != null) c.Add(Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"));
            if (pf86 != null) c.Add(Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"));
            if (lad != null) c.Add(Path.Combine(lad, @"Google\Chrome\Application\chrome.exe"));
            foreach (string p in c)
                if (File.Exists(p)) return p;
            return null;
        }

        static int FreePort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        // Returns the cookie once validate(cookie) says it works, or null if cancelled / browser closed.
        // When prefillUser is given, the login form's username and password are filled in automatically;
        // the user still presses Lang.T("Log In") themselves (and solves any captcha / 2-step).
        public static async Task<string> GetCookie(Action<string> log, CancellationToken ct, Func<string, Task<bool>> validate,
            string prefillUser = null, string prefillPass = null)
        {
            string exe = FindBrowser();
            if (exe == null) { log(Lang.T("No Microsoft Edge or Google Chrome was found on this PC.")); return null; }

            string profile = Path.Combine(Path.GetTempPath(), "rmh_login_" + Guid.NewGuid().ToString("N"));
            int port = FreePort();
            string cookie = null;
            try
            {
                string args = "--user-data-dir=\"" + profile + "\" --remote-debugging-port=" + port
                    + " --remote-allow-origins=* --no-first-run --no-default-browser-check --new-window \"https://www.roblox.com/login\"";
                Process.Start(exe, args);
                if (string.IsNullOrEmpty(prefillUser))
                    log("Browser opened. Log in to the account you want to add - this window will notice automatically.");
                else
                {
                    log("Browser opened for " + prefillUser + ". The username and password are filled in - press 'Log In' (solve any captcha), it is added automatically.");
                    FillLoginLater(port, prefillUser, prefillPass ?? "", ct);
                }

                string wsUrl = null;
                using (HttpClient hc = new HttpClient())
                {
                    hc.Timeout = TimeSpan.FromSeconds(5);
                    for (int i = 0; i < 60 && wsUrl == null && !ct.IsCancellationRequested; i++)
                    {
                        try
                        {
                            string js = await hc.GetStringAsync("http://127.0.0.1:" + port + "/json/version");
                            string u = Json.Str(Json.Parse(js), "webSocketDebuggerUrl");
                            if (u.Length > 0) wsUrl = u;
                        }
                        catch (Exception) { }
                        if (wsUrl == null) await Task.Delay(500, ct);
                    }
                }
                if (wsUrl == null) { log("Could not talk to the browser (debugging port did not open)."); return null; }

                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    await ws.ConnectAsync(new Uri(wsUrl), ct);
                    int id = 0;
                    DateTime started = DateTime.Now;
                    while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open && (DateTime.Now - started).TotalMinutes < 15)
                    {
                        id++;
                        string reply = await Cdp(ws, id, "Storage.getCookies", ct);
                        if (reply == null) break;
                        string found = FindCookie(reply);
                        if (found != null && await validate(found)) { cookie = found; break; }
                        await Task.Delay(1500, ct);
                    }
                    if (cookie != null && ws.State == WebSocketState.Open)
                    {
                        try
                        {
                            id++;
                            await Cdp(ws, id, "Browser.close", ct);
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (OperationCanceledException) { log(Lang.T("Browser login cancelled.")); }
            catch (Exception ex)
            {
                Log.Error("BrowserLogin", ex);
                log("Browser login ended: " + ex.Message);
            }
            CleanupLater(profile);
            return cookie;
        }

        static void CleanupLater(string dir)
        {
            Task.Run(async delegate
            {
                for (int i = 0; i < 6; i++)
                {
                    await Task.Delay(4000);
                    try { if (Directory.Exists(dir)) Directory.Delete(dir, true); return; }
                    catch (Exception) { }
                }
            });
        }

        // Fills the Roblox login form through the browser's debugging port, retrying until the inputs exist.
        // The form is only filled - the user presses "Log In" and solves any captcha / 2-step themselves.
        static void FillLoginLater(int port, string user, string pass, CancellationToken ct)
        {
            Task.Run(async delegate
            {
                try
                {
                    string pageWs = null;
                    using (HttpClient hc = new HttpClient())
                    {
                        hc.Timeout = TimeSpan.FromSeconds(5);
                        for (int i = 0; i < 40 && pageWs == null && !ct.IsCancellationRequested; i++)
                        {
                            try
                            {
                                string js = await hc.GetStringAsync("http://127.0.0.1:" + port + "/json/list");
                                foreach (object t in Json.Arr(Json.Parse(js)))
                                {
                                    if (Json.Str(t, "type") == "page" && Json.Str(t, "url").IndexOf("roblox.com", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        string u = Json.Str(t, "webSocketDebuggerUrl");
                                        if (u.Length > 0) { pageWs = u; break; }
                                    }
                                }
                            }
                            catch (Exception) { }
                            if (pageWs == null) await Task.Delay(500, ct);
                        }
                    }
                    if (pageWs == null) return;

                    string expr = "(function(){var u=document.querySelector('#login-username,input[name=\\\"username\\\"]');"
                        + "var p=document.querySelector('#login-password,input[type=\\\"password\\\"]');"
                        + "if(!u||!p)return 'wait';"
                        + "var S=Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,'value').set;"
                        + "function f(e,v){S.call(e,v);e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));}"
                        + "f(u," + JsStr(user) + ");f(p," + JsStr(pass) + ");return 'done';})()";
                    string paramsJson = "\"params\":{\"expression\":" + JsStr(expr) + ",\"returnByValue\":true}";

                    using (ClientWebSocket ws = new ClientWebSocket())
                    {
                        await ws.ConnectAsync(new Uri(pageWs), ct);
                        int id = 0;
                        for (int i = 0; i < 40 && !ct.IsCancellationRequested && ws.State == WebSocketState.Open; i++)
                        {
                            id++;
                            string reply = await Cdp(ws, id, "Runtime.evaluate", ct, paramsJson);
                            if (reply != null && reply.Contains("\"done\"")) break;   // filled once; the user takes over
                            await Task.Delay(700, ct);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Log.Error("FillLogin", ex); }
            });
        }

        // A JSON/JS string literal (quotes included) for safe embedding.
        static string JsStr(string s)
        {
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '\\' || c == '"') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        static async Task<string> Cdp(ClientWebSocket ws, int id, string method, CancellationToken ct, string paramsJson = null)
        {
            string body = "{\"id\":" + id + ",\"method\":\"" + method + "\"" + (paramsJson != null ? "," + paramsJson : "") + "}";
            byte[] data = Encoding.UTF8.GetBytes(body);
            await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, ct);
            byte[] buf = new byte[65536];
            string a = "\"id\":" + id + ",";
            string b = "\"id\":" + id + "}";
            while (true)
            {
                StringBuilder sb = new StringBuilder();
                WebSocketReceiveResult res;
                do
                {
                    res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                    if (res.MessageType == WebSocketMessageType.Close) return null;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
                } while (!res.EndOfMessage);
                string text = sb.ToString();
                if (text.Contains(a) || text.Contains(b)) return text;
            }
        }

        static string FindCookie(string reply)
        {
            try
            {
                object j = Json.Parse(reply);
                object result = Json.Get(j, "result");
                foreach (object c in Json.Arr(Json.Get(result, "cookies")))
                {
                    if (Json.Str(c, "name") == ".ROBLOSECURITY" && Json.Str(c, "domain").IndexOf("roblox.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string v = Json.Str(c, "value");
                        if (v.Length > 40) return v;
                    }
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
