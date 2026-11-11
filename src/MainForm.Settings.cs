using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RobloxMultiHub
{
    public partial class MainForm
    {
        // ===== Links =====  The Support section buttons open these.
        const string DonateUrl = "https://buymeacoffee.com/multihub";
        const string DiscordUrl = "https://discord.gg/TCrVyjYhHr";

        CheckBox chkMulti;
        ComboBox cbMethod;
        CheckBox chkAutoClose, chkRefreshLaunch;
        NumericUpDown numMax, numPresence, numWatcher;
        RoundTextBox tbLauncherUrl;
        CheckBox chkPresence, chkRelaunch, chkWatcherPresence, chkStartup, chkAntiAfk, chkCheckLogins;
        CheckBox chkCheckUpdates;
        CheckBox chkCrashBreaker;
        CheckBox chkAnimations;
        NumericUpDown numCrashMax;
        CheckBox chkWebhook, chkWebhookCrash, chkWebhookRelaunch, chkWebhookBatch, chkWebhookLocked, chkWebhookStatus;
        NumericUpDown numStatusSec;
        RoundTextBox tbWebhook;
        CheckBox chkDiscordRpc;
        RoundTextBox tbDiscordAppId;
        NumericUpDown numAntiAfk;
        ComboBox cbAntiAfkAction;
        DateTime lastAntiAfk = DateTime.Now;
        ComboBox cbTheme;
        ComboBox cbLang;
        ComboBox cbLogHours;
        Label lblStorage;
        bool loadingSettings;

        void BuildSettingsPage()
        {
            loadingSettings = true;
            AppSettings S = Store.Settings;
            Panel page = NewPage("Settings");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Settings", "Changes are saved immediately.");

            p.Section("Interface");
            chkAnimations = Ui.Chk("Smooth animations (pages, lists, buttons)", S.Animations, null);
            p.Opt("Turn off to switch instantly.",
                "Pages slide in, re-sorted lists settle into place and buttons appear with a short eased motion. "
                + "Turning this off makes every change immediate; nothing else about the app changes.",
                chkAnimations);
            chkAnimations.CheckedChanged += delegate
            {
                if (loadingSettings) return;
                Store.Settings.Animations = chkAnimations.Checked;
                Store.SaveSettings();
            };
            cbLogHours = Ui.Cmb(140, "12 hours", "24 hours");
            cbLogHours.SelectedIndex = S.LogHours >= 24 ? 1 : 0;
            p.Opt("Keep the activity log for this long.",
                "The activity log file (log.txt in the data folder) is cleared when it is older than this. 12 hours keeps it short; 24 hours keeps a full day of history.",
                Ui.Fld("Keep logs", 100), cbLogHours);
            cbLogHours.SelectedIndexChanged += delegate { if (loadingSettings) return; Store.Settings.LogHours = cbLogHours.SelectedIndex == 1 ? 24 : 12; Store.SaveSettings(); };

            p.Section("Multi-instance");
            chkMulti = Ui.Chk("Enable multi-instance", S.MultiInstance, null);
            p.Opt("Lets several Roblox windows run at once.",
                "Roblox normally closes its other window when you start a second one. While this is ON, this app holds the two Windows lock objects Roblox checks (ROBLOX_singletonMutex and ROBLOX_singletonEvent), so every client believes it is allowed to run. Nothing is injected into Roblox and no Roblox file or memory is touched.\n\nIt only works for Roblox windows started AFTER this app, and only while this app stays open.\n\nRisk: Roblox's anti-cheat team has said running several clients may be treated as abusive, and Roblox can change its client at any time and break this. Use it with your own accounts, at your own risk.",
                chkMulti);
            p.Opt("Fixes the 'WAITING' status.",
                "If the status at the top right says WAITING, a Roblox process that was already running (often a background one that Windows starts for you) owns the lock. Close all Roblox processes, then start your accounts from this app.\n\nTip: enable 'Start with Windows' below so this app is always first.",
                Ui.Btn("Close all Roblox processes", 210, delegate { CloseAllForLock(); }, "danger"));

            p.Section("Launching");
            cbMethod = Ui.Cmb(330, "roblox-player link  (recommended, like Account Manager)", "Direct  (starts RobloxPlayerBeta.exe)");
            p.Opt("How Roblox is started.",
                "roblox-player link: hands a roblox-player: link to Windows, exactly like Roblox Account Manager does (and like clicking Play on the website). Accounts launched this way are not asked for human verification when they join.\n\nDirect: starts RobloxPlayerBeta.exe itself with the login ticket. Roblox can ask accounts launched like that to prove they are human, especially with many accounts. Only use it if the link method does not start Roblox on your PC, or if launching one account closes your other Roblox windows.",
                Ui.Fld("Launch method", 120), cbMethod);
            chkAutoClose = Ui.Chk("Close an account's previous window when it is launched again", S.AutoCloseLast, null);
            p.Opt("Never runs the same account twice.",
                "Only closes windows that this app started for that same account. Windows you opened yourself are never touched.",
                chkAutoClose);
            numMax = Ui.Num(0, 100, S.MaxInstances, 70);
            p.Opt("Max windows (0 = no limit).",
                "A safety net for weak PCs: launches are refused once this many Roblox windows are running.",
                Ui.Fld("Maximum windows", 150), numMax);
            chkRefreshLaunch = Ui.Chk("Refresh the cookie before every launch", S.RefreshCookieOnLaunch, null);
            p.Opt("Gets a fresh login cookie before each launch.",
                "OFF by default. Rotating keeps cookies fresh, but it signs the account out of its other sessions (for example the website in your browser).",
                chkRefreshLaunch);

            p.Section("Account status");
            chkPresence = Ui.Chk("Show online / in-game status", S.ShowPresence, null);
            p.Opt("Status dot per account.",
                "Dot colors: green = in a game or running, blue = online but not in a game, purple = Roblox Studio, grey = offline, red = cookie expired.",
                chkPresence);
            numPresence = Ui.Num(10, 3600, S.PresenceSec, 80);
            p.Opt("How often the status is refreshed.",
                "Lower values show changes faster but use more requests; Roblox may rate-limit very frequent checks.",
                Ui.Fld("Refresh every (seconds)", 190), numPresence);
            chkCheckLogins = Ui.Chk("Check all saved logins when MultiHub starts", S.CheckLoginsAtStartup, null);
            p.Opt("Warns which accounts got signed out.",
                "A few seconds after start-up every saved login is tested once (a short pause between accounts, so Roblox does not rate-limit you). Accounts whose login stopped working since the previous check - password changed, logged out everywhere, or locked / reset by Roblox - are listed in one popup, so you know which ones to log in with again. Accounts you were already told about only appear in the activity log.",
                chkCheckLogins);

            p.Section(Lang.T("Automation");
            chkRelaunch = Ui.Chk("Auto-relaunch (watcher)", S.AutoRelaunch, null);
            p.Opt("Relaunches accounts whose window disappeared.",
                "OFF by default. If a Roblox window that this app started disappears (crash, kick, closed), the same account is launched again into the same game. Use the Close / Stop buttons of this app to close windows on purpose.",
                chkRelaunch);
            numWatcher = Ui.Num(10, 3600, S.WatcherSec, 80);
            p.Opt("How often the watcher checks.",
                "The watcher only looks at windows that this app started.",
                Ui.Fld("Check every (seconds)", 190), numWatcher);
            chkWatcherPresence = Ui.Chk("Also relaunch when the account left the game", S.WatcherUsePresence, null);
            p.Opt("Catches windows open after a disconnect.",
                "Uses Roblox's presence information: if an account is no longer in a game for two checks in a row, its window is closed and the account is launched again.",
                chkWatcherPresence);
            chkCrashBreaker = Ui.Chk("Crash breaker", S.CrashBreaker, null);
            numCrashMax = Ui.Num(1, 20, S.CrashBreakerMax, 70);
            p.Opt("Stops relaunching an account that keeps dying.",
                "ON by default. Without it, auto-relaunch would keep restarting an account forever if it crashes or is locked straight away - spamming launches and Discord alerts. With it on, if the watcher has to relaunch the same account this many times within a few minutes, it gives up on that one account and leaves it alone for about " + S.CrashBreakerCooldownMin + " minutes (or until you launch it yourself). The other accounts keep running normally.",
                chkCrashBreaker, Ui.Fld("Give up after", 90), numCrashMax);

            chkAntiAfk = Ui.Chk("Anti-AFK", S.AntiAfk, null);
            numAntiAfk = Ui.Num(1, 120, S.AntiAfkMin, 70);
            p.Opt("How often the anti-AFK input runs.",
                "Roblox disconnects an account after about 20 minutes without input. With this ON, every X minutes each Roblox window that this app started is brought to the front for about a third of a second, gets one small input (exactly like your own mouse / keyboard) and then the window you were using is put back. You see a short flash. The window you are using yourself is skipped, and nothing in Roblox is changed.\n\n12 minutes is the recommended value (comfortably under Roblox's ~20 minute limit).\n\nRisk: like multi-instance, this automates input, which Roblox may not like. Use it with your own accounts.",
                chkAntiAfk, Ui.Fld("Every (minutes)", 110), numAntiAfk);
            cbAntiAfkAction = Ui.Cmb(330, AntiAfk.ActionNames);
            cbAntiAfkAction.SelectedIndex = Math.Max(0, Array.IndexOf(AntiAfk.ActionIds, S.AntiAfkAction));
            p.Opt("Which input is sent.",
                "Mouse nudge (recommended): moves the mouse a few pixels and back. It resets Roblox's idle timer but does NOT move your character, so auto-hatch, auto-farm and AFK setups keep running.\n\nJump (Space): the older behaviour - the character jumps once. Simple, but a jump can briefly move you off an egg / spot and interrupt auto-hatch.\n\nZoom camera (I then O): zooms in and back out. No character movement; use it if mouse nudge does not work in a game.",
                Ui.Fld("Anti-AFK action", 110), cbAntiAfkAction);

            p.Section(Lang.T("Discord alerts");
            chkWebhook = Ui.Chk("Send alerts to a Discord webhook", S.Webhook, null);
            p.Opt("Posts short messages to a channel you choose.",
                "OFF by default. Create a webhook in your Discord server (Server settings - Integrations - Webhooks - New Webhook - Copy URL) and paste it below. MultiHub then posts short alerts to that channel. The URL is only ever sent to Discord and is stored locally; treat it like a password - anyone with it can post to that channel.",
                chkWebhook);
            tbWebhook = Ui.Txt(330, S.WebhookUrl);
            tbWebhook.UseSystemPasswordChar = true;
            p.Opt("Paste your webhook URL.",
                "Looks like https://discord.com/api/webhooks/123.../abc... . Use 'Send a test message' to check it works.",
                Ui.Fld("Webhook URL", 120), tbWebhook,
                Ui.Btn("Send a test message", 170, delegate { SendWebhookTest(); }, ""));
            chkWebhookCrash = Ui.Chk("A client closed unexpectedly (crash / kick)", S.WebhookCrash, null);
            p.Opt("A window disappeared on its own.",
                "Posted when a Roblox window that MultiHub launched closes without you asking - a crash, a kick, or a disconnect. Windows you close yourself are never reported.",
                chkWebhookCrash);
            chkWebhookRelaunch = Ui.Chk("The watcher brought a client back", S.WebhookRelaunch, null);
            p.Opt("When auto-relaunch starts an account again.",
                "Posted each time the watcher relaunches an account whose window had disappeared. Needs auto-relaunch (above) to be on.",
                chkWebhookRelaunch);
            chkWebhookBatch = Ui.Chk("A launch batch finished", S.WebhookBatch, null);
            p.Opt("A launch batch finished.",
                "Posted once, with how many accounts were started, when a whole batch has launched.",
                chkWebhookBatch);
            chkWebhookLocked = Ui.Chk("An account came back locked (HTTP 403)", S.WebhookLocked, null);
            p.Opt("When Roblox blocks one of your accounts.",
                "Posted when a check or launch finds that Roblox has locked or blocked an account (HTTP 403), so you notice straight away which account needs attention.",
                chkWebhookLocked);

            chkWebhookStatus = Ui.Chk("Log status", S.WebhookStatus, null);
            p.Opt("One live status message in the channel.",
                "ON: MultiHub posts one message listing every running account - username (linked to its profile), uptime as a live Discord timestamp, the game it is in (linked) and average CPU / RAM since the last update - and EDITS that same message on every refresh instead of posting again. Needs the webhook above. Minimum 15 seconds.",
                chkWebhookStatus);
            numStatusSec = Ui.Num(15, 600, Math.Max(15, Math.Min(600, S.WebhookStatusSec)), 70);
            numStatusSec.ValueChanged += delegate
            {
                if (loadingSettings) return;
                Store.Settings.WebhookStatusSec = (int)numStatusSec.Value;
                Store.SaveSettings();
            };
            chkWebhookStatus.CheckedChanged += delegate
            {
                if (loadingSettings) return;
                Store.Settings.WebhookStatus = chkWebhookStatus.Checked;
                Store.SaveSettings();
            };
            p.Opt("How often the status message refreshes.",
                "Seconds between refreshes (15 - 600). Smaller = livelier, but every refresh is a Discord API call.",
                Ui.Fld("Refresh every (seconds)", 190), numStatusSec);

            p.Section("Discord status (Rich Presence)");
            chkDiscordRpc = Ui.Chk("Show \"Playing Roblox MultiHub\" on my Discord profile", S.DiscordRpc, null);
            p.Opt("Shows the app on your Discord profile.",
                "Like other apps that show \"Playing ...\" on your profile. It needs a Discord Application ID (below). While it is on, your Discord shows Playing Roblox MultiHub, how many accounts are running, how long it has been open, and two buttons (Get Roblox MultiHub / Join Discord) that other people can click. It talks only to the Discord app on your own PC - no bot token, nothing is uploaded. Needs the Discord desktop app to be running.",
                chkDiscordRpc);
            tbDiscordAppId = Ui.Txt(230, S.DiscordAppId);
            p.Opt("Your Discord Application ID.",
                "Create one for free: open the Discord Developer Portal (discord.com/developers/applications), click New Application, name it exactly \"Roblox MultiHub\" (that name is what shows after \"Playing\"), open it and copy the Application ID. Paste it here. For the little logo image, upload a picture under Rich Presence > Art Assets named exactly \"logo\". The ID is public - it is safe to share, unlike a bot token.",
                Ui.Fld("Application ID", 120), tbDiscordAppId);

            p.Section("Security");
            lblStorage = Ui.Lbl("", "muted");
            p.Row(lblStorage);
            p.Opt("Protect your accounts with your own password.",
                "Accounts (including cookies) are always stored encrypted, in your user profile - not next to this program - so you can share the folder with the .exe without leaking logins.\n\nBy default Windows encrypts them for your Windows user. With a password they are protected by AES-256 and can be moved to another PC. If you forget the password, the accounts cannot be recovered.",
                Ui.Btn("Set / change password", 190, delegate { SetPassword(); }, ""),
                Ui.Btn("Use Windows encryption", 190, delegate { RemovePassword(); }, ""));
            p.Opt("Backups are made automatically.",
                "A backup of your accounts is made every time the app starts (the last 10 are kept). 'Open data folder' shows where settings, log and backups are stored.",
                Ui.Btn("Back up now", 120, delegate { Store.Backup(); AddLog("Backup saved in " + Store.BackupDir); }, ""),
                Ui.Btn("Open data folder", 150, delegate { OpenDataFolder(); }, ""));

            p.Section("Lang.T(Lang.T(Appearance & startup");
            cbTheme = Ui.Cmb(140, "Dark", "Light");
            cbTheme.SelectedIndex = S.Theme == "Light" ? 1 : 0;
            p.Opt("Dark or light look.", "Switches the whole window immediately.", Ui.Fld("Theme", 120), cbTheme);
            List<string[]> langs = Lang.Available();
            string[] langNames = new string[langs.Count];
            for (int i = 0; i < langs.Count; i++) langNames[i] = langs[i][1];
            cbLang = Ui.Cmb(220, langNames);
            int li = 0;
            for (int i = 0; i < langs.Count; i++) if (langs[i][0] == S.Language) li = i;
            cbLang.SelectedIndex = li;
            p.Opt("Interface language.",
                "English and Dutch are built in; anything not translated stays in English. You can add more languages by dropping a file  lang\\<code>.json  (a JSON object of { \"English text\": \"translation\" }) into the data folder - use 'Open data folder' above. Changing the language restarts the app.",
                Ui.Fld("Language", 120), cbLang);
            chkStartup = Ui.Chk("Start MultiHub with Windows (minimized)", S.StartWithWindows, null);
            p.Opt("Starts with Windows.",
                "Useful with multi-instance: if this app starts before Roblox does, it always wins the lock. Adds a normal Windows startup entry that you can remove here again.",
                chkStartup);

            p.Section("Updates");
            chkCheckUpdates = Ui.Chk("Check for a new version when MultiHub starts", S.CheckUpdates, null);
            p.Opt("Notifies about updates; never downloads.",
                "When the app starts it asks GitHub once what the latest released version is. If that is newer than the version you have, you get a short notice with a button to open the download page. It only reads a version number; it never downloads or replaces the app on its own, so it is completely safe. Turn it off to never check.",
                chkCheckUpdates);
            p.Opt("Check right now.",
                "Asks GitHub for the latest version immediately and tells you whether you are up to date.",
                Ui.Btn("Check for updates now", 200, delegate { CheckUpdatesNow(); }, ""));

            p.Section(Lang.T("Advanced");
            tbLauncherUrl = Ui.Txt(290, S.PlaceLauncherUrl);
            p.Opt("Only change if Roblox moves it.",
                "The address the app uses to ask Roblox for a game session. Only change it if launching stops working after a Roblox change and you know the new address. 'Reset' restores the default.",
                Ui.Fld("Place launcher URL", 130), tbLauncherUrl,
                Ui.Btn("Reset", 70, delegate { tbLauncherUrl.Text = "https://assetgame.roblox.com/game/PlaceLauncher.ashx"; }, ""));

            p.Section(Lang.T("Support this project");
            p.Opt("Free - donations keep it going.",
                "This tool is built by our team in a lot of free time and stays completely free. If it saves you time and you want to say thanks, a small donation is hugely appreciated - it is never required and nothing is locked behind it. The button opens the donation page in your browser.",
                Ui.Btn("Buy us a coffee", 170, delegate { OpenDonate(); }, "primary"));
            p.Opt("Need help? Send us a debug log.",
                "Writes a detailed text file next to the app data: your settings (webhook URL masked), Windows, CPU, GPU, motherboard, BIOS, every disk and where the pagefile is, the Roblox install and its flags, a count of accounts, every running client with its live overhead, hub overhead, and the recent log.",
                Ui.Btn("Create debug log", 170, delegate
                {
                    try
                    {
                        string f = SupportBundle.Create();
                        AddLog("Debug log written: " + f);
                        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + f + "\"");
                    }
                    catch (Exception ex) { AddLog("Debug log failed: " + ex.Message); }
                }, ""));
            p.Opt("News, help and tickets.",
                "Opens the Roblox MultiHub Discord server in your browser: announcements, update logs, a general chat and a support ticket system if you need help.",
                Ui.Btn("Join our Discord", 150, delegate { OpenLink(DiscordUrl, "Discord"); }, ""));

            p.Section("About");
            p.Hint("Independent project - not affiliated with Roblox.",
                "Roblox MultiHub is an independent project. It is not affiliated with Roblox Corporation, Roblox Account Manager or OpenMultiBoxing. The feature set is modelled on Roblox Account Manager (multi-account launching, groups, presence, server browser...) and the window layout on OpenMultiBoxing; no code from either project is used.");

            // ---------- wiring ----------
            EventHandler changed = delegate { OnSettingChanged(); };
            chkMulti.CheckedChanged += delegate { OnMultiToggled(); };
            cbMethod.SelectedIndexChanged += changed;
            chkAutoClose.CheckedChanged += changed;
            numMax.ValueChanged += changed;
            chkRefreshLaunch.CheckedChanged += changed;
            chkPresence.CheckedChanged += changed;
            numPresence.ValueChanged += changed;
            chkCheckLogins.CheckedChanged += changed;
            chkCheckUpdates.CheckedChanged += changed;
            chkRelaunch.CheckedChanged += changed;
            numWatcher.ValueChanged += changed;
            chkWatcherPresence.CheckedChanged += changed;
            chkCrashBreaker.CheckedChanged += changed;
            numCrashMax.ValueChanged += changed;
            tbLauncherUrl.Leave += changed;
            chkAntiAfk.CheckedChanged += delegate
            {
                if (loadingSettings) return;
                OnSettingChanged();
                lastAntiAfk = DateTime.Now;
                AddLog(chkAntiAfk.Checked ? "Anti-AFK ON: every " + Store.Settings.AntiAfkMin + " min each Roblox window briefly comes to the front and jumps once." : "Anti-AFK OFF.");
            };
            numAntiAfk.ValueChanged += delegate { if (loadingSettings) return; OnSettingChanged(); lastAntiAfk = DateTime.Now; };
            cbAntiAfkAction.SelectedIndexChanged += delegate { if (loadingSettings) return; OnSettingChanged(); };
            cbTheme.SelectedIndexChanged += delegate { OnThemeChanged(); };
            cbLang.SelectedIndexChanged += delegate { OnLanguageChanged(); };
            chkStartup.CheckedChanged += delegate { if (loadingSettings) return; OnSettingChanged(); SetStartup(chkStartup.Checked); };
            chkWebhook.CheckedChanged += changed;
            tbWebhook.Leave += changed;
            chkWebhookCrash.CheckedChanged += changed;
            chkWebhookRelaunch.CheckedChanged += changed;
            chkWebhookBatch.CheckedChanged += changed;
            chkWebhookLocked.CheckedChanged += changed;
            chkDiscordRpc.CheckedChanged += changed;
            tbDiscordAppId.Leave += changed;

            loadingSettings = false;
            SyncSettingsControls();
        }

        // Pushes the saved settings into the controls (used at startup and after the first-run question).
        void SyncSettingsControls()
        {
            if (chkMulti == null) return;
            bool was = loadingSettings;
            loadingSettings = true;
            AppSettings S = Store.Settings;
            chkMulti.Checked = S.MultiInstance;
            cbMethod.SelectedIndex = S.LaunchMethod == "Direct" ? 1 : 0;
            chkAutoClose.Checked = S.AutoCloseLast;
            numMax.Value = Math.Max(numMax.Minimum, Math.Min(numMax.Maximum, S.MaxInstances));
            chkRefreshLaunch.Checked = S.RefreshCookieOnLaunch;
            chkPresence.Checked = S.ShowPresence;
            chkCheckLogins.Checked = S.CheckLoginsAtStartup;
            if (chkCheckUpdates != null) chkCheckUpdates.Checked = S.CheckUpdates;
            chkRelaunch.Checked = S.AutoRelaunch;
            chkWatcherPresence.Checked = S.WatcherUsePresence;
            if (chkCrashBreaker != null)
            {
                chkCrashBreaker.Checked = S.CrashBreaker;
                numCrashMax.Value = Math.Max(numCrashMax.Minimum, Math.Min(numCrashMax.Maximum, S.CrashBreakerMax));
            }
            chkStartup.Checked = S.StartWithWindows;
            chkAntiAfk.Checked = S.AntiAfk;
            numAntiAfk.Value = Math.Max(numAntiAfk.Minimum, Math.Min(numAntiAfk.Maximum, S.AntiAfkMin));
            cbAntiAfkAction.SelectedIndex = Math.Max(0, Array.IndexOf(AntiAfk.ActionIds, S.AntiAfkAction));
            if (chkWebhook != null)
            {
                chkWebhook.Checked = S.Webhook;
                tbWebhook.Text = S.WebhookUrl;
                chkWebhookCrash.Checked = S.WebhookCrash;
                chkWebhookRelaunch.Checked = S.WebhookRelaunch;
                chkWebhookBatch.Checked = S.WebhookBatch;
                chkWebhookLocked.Checked = S.WebhookLocked;
            }
            if (chkDiscordRpc != null)
            {
                chkDiscordRpc.Checked = S.DiscordRpc;
                tbDiscordAppId.Text = S.DiscordAppId;
            }
            UpdateStorageLabel();
            loadingSettings = was;
            if (S.MultiInstance) MultiInstance.Start();
        }

        void OnSettingChanged()
        {
            if (loadingSettings) return;
            AppSettings S = Store.Settings;
            S.LaunchMethod = cbMethod.SelectedIndex == 1 ? "Direct" : "Protocol";
            S.AutoCloseLast = chkAutoClose.Checked;
            S.MaxInstances = (int)numMax.Value;
            S.RefreshCookieOnLaunch = chkRefreshLaunch.Checked;
            S.ShowPresence = chkPresence.Checked;
            S.PresenceSec = (int)numPresence.Value;
            S.CheckLoginsAtStartup = chkCheckLogins.Checked;
            S.CheckUpdates = chkCheckUpdates.Checked;
            S.AutoRelaunch = chkRelaunch.Checked;
            S.WatcherSec = (int)numWatcher.Value;
            S.WatcherUsePresence = chkWatcherPresence.Checked;
            S.CrashBreaker = chkCrashBreaker.Checked;
            S.CrashBreakerMax = (int)numCrashMax.Value;
            S.StartWithWindows = chkStartup.Checked;
            S.AntiAfk = chkAntiAfk.Checked;
            S.AntiAfkMin = (int)numAntiAfk.Value;
            S.AntiAfkAction = AntiAfk.ActionIds[Math.Max(0, cbAntiAfkAction.SelectedIndex)];
            string url = tbLauncherUrl.Text.Trim();
            S.PlaceLauncherUrl = url.Length == 0 ? "https://assetgame.roblox.com/game/PlaceLauncher.ashx" : url;
            S.Webhook = chkWebhook.Checked;
            S.WebhookUrl = tbWebhook.Text.Trim();
            S.WebhookCrash = chkWebhookCrash.Checked;
            S.WebhookRelaunch = chkWebhookRelaunch.Checked;
            S.WebhookBatch = chkWebhookBatch.Checked;
            S.WebhookLocked = chkWebhookLocked.Checked;
            S.DiscordRpc = chkDiscordRpc.Checked;
            S.DiscordAppId = tbDiscordAppId.Text.Trim();
            Store.SaveSettings();
            if (!S.DiscordRpc) DiscordRpc.Stop();
        }

        void SendWebhookTest()
        {
            string url = tbWebhook.Text.Trim();
            if (!Webhook.LooksLikeWebhook(url))
            {
                MessageBox.Show(this, "That does not look like a Discord webhook URL.\r\nIt should start with https://discord.com/api/webhooks/",
                    "Discord alerts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Webhook.SendTo(url, "Test message", "Roblox MultiHub is connected to this channel. \U0001F680", Webhook.Blurple);
            AddLog("Sent a test message to your Discord webhook. Check the channel.");
        }

        void OnMultiToggled()
        {
            if (loadingSettings) return;
            Store.Settings.MultiInstance = chkMulti.Checked;
            Store.SaveSettings();
            if (chkMulti.Checked)
            {
                MultiInstance.Start();
                AddLog("Multi-instance turned ON.");
                if (Launcher.RobloxPids().Count > 0)
                    AddLog("Roblox is already running, so it may own the lock. If the status stays WAITING, use 'Close all Roblox processes'.");
            }
            else
            {
                MultiInstance.Stop();
                AddLog("Multi-instance turned OFF. Only one Roblox window can run at a time.");
            }
        }

        async void RunAntiAfk()
        {
            lastAntiAfk = DateTime.Now;
            await AntiAfk.Pulse(AddLog);
        }

        void CloseAllForLock()
        {
            if (MessageBox.Show(this, "This closes every running Roblox window and background process.\r\nContinue?", "Close all Roblox processes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            int n = Launcher.KillAllRoblox();
            AddLog("Closed " + n + " Roblox process(es). Now launch your accounts from this app.");
            UpdateStatusCells();
        }

        void OnThemeChanged()
        {
            if (loadingSettings) return;
            bool light = cbTheme.SelectedIndex == 1;
            Store.Settings.Theme = light ? "Light" : "Dark";
            Store.SaveSettings();
            Theme.Set(!light);
            ApplyTheme();
            OnAccountSelectionChanged();
            UpdateMultiChip();
        }

        void OnLanguageChanged()
        {
            if (loadingSettings) return;
            List<string[]> langs = Lang.Available();
            int i = cbLang.SelectedIndex;
            if (i < 0 || i >= langs.Count) return;
            string code = langs[i][0];
            if (code == Store.Settings.Language) return;
            Store.Settings.Language = code;
            Store.SaveSettings();
            // applied in place: the whole UI is rebuilt with the new language, no restart
            // (a restart also raced the single-instance mutex -> 'already running')
            Lang.Load(code);
            RightToLeft = (code == "ar" || code == "he") ? RightToLeft.Yes : RightToLeft.No;
            currentPage = "Settings";
            BuildUi();
            AddLog("Language: " + code);
        }

        void SetStartup(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("RobloxMultiHub", "\"" + Application.ExecutablePath + "\" --minimized");
                    else k.DeleteValue("RobloxMultiHub", false);
                }
            }
            catch (Exception ex) { AddLog("Could not change the startup entry: " + ex.Message); }
        }

        void UpdateStorageLabel()
        {
            if (lblStorage == null) return;
            lblStorage.Text = Store.Password == null
                ? "Accounts file: encrypted with your Windows account (no password needed)."
                : "Accounts file: protected with your own password (AES-256).";
        }

        void SetPassword()
        {
            string pw = Dialogs.AskPassword(this, "Set a password", "Choose a password for your accounts file.\r\nIf you forget it, the accounts CANNOT be recovered!", true);
            if (pw == null) return;
            Store.Password = pw;
            Store.SaveAccounts();
            UpdateStorageLabel();
            AddLog("Password set. You will be asked for it every time the app starts.");
        }

        void RemovePassword()
        {
            if (Store.Password == null) { AddLog("No password is set - already using Windows encryption."); return; }
            if (MessageBox.Show(this, "Remove the password and encrypt with your Windows account instead?", "Use Windows encryption", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Store.Password = null;
            Store.SaveAccounts();
            UpdateStorageLabel();
            AddLog("Password removed. The accounts file is now tied to this Windows user.");
        }

        void OpenDataFolder()
        {
            try { Process.Start(new ProcessStartInfo(Store.Dir) { UseShellExecute = true }); }
            catch (Exception ex) { AddLog("Could not open the folder: " + ex.Message); }
        }

        void OpenDonate()
        {
            if (DonateUrl.Contains("YOUR_NAME_HERE"))
            {
                MessageBox.Show(this,
                    "No donation link is set yet.\r\n\r\nOpen src\\MainForm.Settings.cs, replace the DonateUrl value at the top with your own Buy Me a Coffee / Ko-fi / PayPal link, and rebuild.",
                    "Support this project", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenLink(DonateUrl, "donation page");
        }

        void OpenLink(string url, string what)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { AddLog("Could not open the " + what + ": " + ex.Message); }
        }

        // Quiet check at startup: only says something when a newer version exists.
        async void CheckUpdatesOnStartup()
        {
            try
            {
                UpdateInfo u = await Updater.Check();
                if (IsDisposed || !u.Available) return;
                AddLog("Update available: v" + u.Latest + " (you have v" + u.Current + ").");
                if (MessageBox.Show(this,
                        "A newer version of Roblox MultiHub is available:\r\n\r\n" +
                        "    v" + u.Latest + "   (you have v" + u.Current + ")\r\n\r\n" +
                        "Open the download page now? Nothing is downloaded automatically.",
                        "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    OpenLink(u.Url, "download page");
            }
            catch (Exception ex) { Log.Error("CheckUpdatesOnStartup", ex); }
        }

        // Manual check from the button: always tells you the result.
        async void CheckUpdatesNow()
        {
            AddLog("Checking GitHub for a new version...");
            UpdateInfo u = await Updater.Check();
            if (IsDisposed) return;
            if (!string.IsNullOrEmpty(u.Error))
            {
                MessageBox.Show(this, "Could not check for updates right now:\r\n" + u.Error, "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (u.Available)
            {
                AddLog("Update available: v" + u.Latest + " (you have v" + u.Current + ").");
                if (MessageBox.Show(this,
                        "A newer version is available:\r\n\r\n    v" + u.Latest + "   (you have v" + u.Current + ")\r\n\r\nOpen the download page now?",
                        "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    OpenLink(u.Url, "download page");
            }
            else
            {
                AddLog("You are up to date (v" + u.Current + ").");
                MessageBox.Show(this, "You already have the latest version (v" + u.Current + ").", "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

    }
}
