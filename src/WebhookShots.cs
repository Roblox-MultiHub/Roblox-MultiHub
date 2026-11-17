using System;

namespace RobloxMultiHub
{
    // Periodic account screenshot: every N minutes, if turned on, post ONE composite PNG of all
    // running Roblox windows to the webhook. OFF by default. Independent of the live "Log status"
    // embed - this one posts a fresh message each time so you keep a visual history.
    public static class WebhookShots
    {
        static DateTime lastSent = DateTime.MinValue;

        // Called from the main 1-second timer.
        public static void Tick()
        {
            if (!Store.Settings.WebhookShots || !Webhook.Enabled) return;
            int min = Store.Settings.WebhookShotsMin;
            if (min < 1) return;
            if ((DateTime.UtcNow - lastSent).TotalMinutes < min) return;
            lastSent = DateTime.UtcNow;
            SendNow("Account screenshots");
        }

        // Builds and sends the composite now (also used by the "Send one now" button). Returns the
        // number of windows captured (0 = nothing running, nothing sent).
        public static int SendNow(string title)
        {
            try
            {
                int count;
                byte[] png = AccountShot.CapturePng(out count);
                if (count == 0 || png == null) return 0;
                string desc;
                int _c;
                desc = AccountShot.Summary(out _c);
                Webhook.SendImage(title, desc, Webhook.Blurple, png, "accounts.png");
                return count;
            }
            catch (Exception ex) { Log.Error("WebhookShots", ex); return 0; }
        }
    }
}
