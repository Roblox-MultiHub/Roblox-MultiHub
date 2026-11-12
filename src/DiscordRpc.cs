using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace RobloxMultiHub
{
    // Discord Rich Presence: shows "Playing Roblox MultiHub" on your Discord profile while the app runs,
    // with how many accounts are running, a timer, and Lang.T("Get Roblox MultiHub") / Lang.T("Join Discord") buttons.
    // It talks to the Discord app already on your PC over its local IPC pipe - no bot token, no library,
    // and nothing leaves your PC except to Discord itself. Needs a Discord Application ID (Settings).
    public static class DiscordRpc
    {
        const string DownloadUrl = "https://github.com/Roblox-MultiHub/Roblox-MultiHub/raw/main/download/RobloxMultiHub.zip";
        const string DiscordInvite = "https://discord.gg/TCrVyjYhHr";

        static NamedPipeClientStream pipe;
        static bool ready;
        static long startEpoch;
        static string lastPayload = "";
        static DateTime lastAttempt = DateTime.MinValue;
        static bool busy;

        public static bool Enabled
        {
            get { return Store.Settings.DiscordRpc && !string.IsNullOrEmpty(Store.Settings.DiscordAppId); }
        }

        static long UnixNow()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static void Begin()
        {
            if (startEpoch == 0) startEpoch = UnixNow();
        }

        // Called from the UI timer. Does the pipe work on a background thread so it never blocks the UI.
        public static void Update(int running, int saved)
        {
            if (!Enabled) { if (ready || pipe != null) Stop(); return; }
            Begin();
            if (busy) return;
            busy = true;
            int run = running, sav = saved;
            Task.Run(delegate
            {
                try { DoUpdate(run, sav); }
                catch (Exception) { Close(); }
                finally { busy = false; }
            });
        }

        static void DoUpdate(int running, int saved)
        {
            if (!Ensure()) return;
            string details = running > 0 ? ("Running " + running + (running == 1 ? " alt" : " alts")) : Lang.T("Multi-account launcher");
            string state = saved > 0 ? (saved + (saved == 1 ? " account saved" : " accounts saved")) : "Free & open source";

            Dictionary<string, object> ts = new Dictionary<string, object>(); ts["start"] = startEpoch;
            Dictionary<string, object> assets = new Dictionary<string, object>(); assets["large_image"] = "logo"; assets["large_text"] = "Roblox MultiHub";
            Dictionary<string, object> activity = new Dictionary<string, object>();
            activity["details"] = details;
            activity["state"] = state;
            activity["timestamps"] = ts;
            activity["assets"] = assets;
            activity["buttons"] = new object[]
            {
                new Dictionary<string, object> { { "label", "Get Roblox MultiHub" }, { "url", DownloadUrl } },
                new Dictionary<string, object> { { "label", "Join Discord" }, { "url", DiscordInvite } }
            };
            Dictionary<string, object> args = new Dictionary<string, object>();
            args["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id;
            args["activity"] = activity;
            Dictionary<string, object> frame = new Dictionary<string, object>();
            frame["cmd"] = "SET_ACTIVITY"; frame["args"] = args; frame["nonce"] = Guid.NewGuid().ToString();

            string json = Json.To(frame);
            if (json == lastPayload) return;     // nothing changed since the last update
            WriteFrame(1, json);
            ReadFrame();                         // drain Discord's reply so the pipe never backs up
            lastPayload = json;
        }

        static bool Ensure()
        {
            if (ready && pipe != null && pipe.IsConnected) return true;
            if ((DateTime.UtcNow - lastAttempt).TotalSeconds < 10) return false;   // Discord may be closed - don't hammer it
            lastAttempt = DateTime.UtcNow;
            Close();
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    NamedPipeClientStream p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.None);
                    p.Connect(400);
                    pipe = p;
                    Dictionary<string, object> h = new Dictionary<string, object>(); h["v"] = 1; h["client_id"] = Store.Settings.DiscordAppId;
                    WriteFrame(0, Json.To(h));   // handshake
                    ReadFrame();                 // READY
                    ready = true;
                    lastPayload = "";
                    return true;
                }
                catch (Exception)
                {
                    try { if (pipe != null) pipe.Dispose(); } catch (Exception) { }
                    pipe = null;
                }
            }
            return false;
        }

        static void WriteFrame(int op, string json)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] buf = new byte[8 + data.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(op), 0, buf, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(data.Length), 0, buf, 4, 4);
            Buffer.BlockCopy(data, 0, buf, 8, data.Length);
            pipe.Write(buf, 0, buf.Length);
            pipe.Flush();
        }

        static void ReadFrame()
        {
            byte[] header = ReadExact(8);
            if (header == null) return;
            int len = BitConverter.ToInt32(header, 4);
            if (len > 0 && len < 1024 * 1024) ReadExact(len);
        }

        static byte[] ReadExact(int n)
        {
            byte[] b = new byte[n];
            int off = 0;
            while (off < n)
            {
                int r = pipe.Read(b, off, n - off);
                if (r <= 0) return null;
                off += r;
            }
            return b;
        }

        // Clears the presence and closes the pipe (called when the app closes or the feature is turned off).
        public static void Stop()
        {
            try
            {
                if (ready && pipe != null && pipe.IsConnected)
                {
                    Dictionary<string, object> args = new Dictionary<string, object>();
                    args["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id;
                    args["activity"] = null;
                    Dictionary<string, object> frame = new Dictionary<string, object>();
                    frame["cmd"] = "SET_ACTIVITY"; frame["args"] = args; frame["nonce"] = Guid.NewGuid().ToString();
                    WriteFrame(1, Json.To(frame));
                }
            }
            catch (Exception) { }
            Close();
        }

        static void Close()
        {
            ready = false;
            lastPayload = "";
            try { if (pipe != null) pipe.Dispose(); } catch (Exception) { }
            pipe = null;
        }
    }
}
