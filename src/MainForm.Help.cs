using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RobloxMultiHub
{
    // Help page: one foldable section per topic. All sections start closed; click a title to open it.
    public partial class MainForm
    {
        class HelpSection
        {
            public Panel Box;
            public Label Head;
            public Label Body;
            public string Title;
            public bool Open;
        }

        FlowLayoutPanel helpFlow;
        Label helpIntro;
        readonly List<HelpSection> helpSections = new List<HelpSection>();
        static readonly Font HelpHeadFont = new Font("Segoe UI Semibold", 10.5F);
        static readonly Font HelpBodyFont = new Font("Segoe UI", 9.5F);

        void BuildHelpPage()
        {
            Panel page = NewPage("Help");
            helpFlow = new FlowLayoutPanel();
            helpFlow.Dock = DockStyle.Fill;
            helpFlow.FlowDirection = FlowDirection.TopDown;
            helpFlow.WrapContents = false;
            helpFlow.AutoScroll = true;
            helpFlow.Padding = new Padding(16, 14, 8, 14);
            helpFlow.Tag = "card";
            page.Controls.Add(helpFlow);

            Label title = Ui.Lbl("Help", "title");
            title.Margin = new Padding(0, 0, 0, 2);
            helpFlow.Controls.Add(title);
            helpIntro = Ui.Lbl("Click a topic to open it. Every option in the app also has a short line under it, and the round (i) next to it shows the full explanation.", "muted");
            helpIntro.Margin = new Padding(0, 0, 0, 6);
            helpFlow.Controls.Add(helpIntro);
            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.AutoSize = true;
            tools.Tag = "card";
            tools.Margin = new Padding(0, 0, 0, 10);
            tools.Controls.Add(Ui.Btn("Open all", 80, delegate { SetAllHelp(true); }, ""));
            tools.Controls.Add(Ui.Btn("Close all", 80, delegate { SetAllHelp(false); }, ""));
            helpFlow.Controls.Add(tools);

            foreach (string[] topic in HelpTopics())
            {
                string[] paras = new string[topic.Length - 1];
                Array.Copy(topic, 1, paras, 0, paras.Length);
                AddHelpSection(topic[0], string.Join("\r\n\r\n", paras));
            }
            helpFlow.ClientSizeChanged += delegate { LayoutHelp(); };
            LayoutHelp();
        }

        void AddHelpSection(string title, string text)
        {
            HelpSection s = new HelpSection();
            s.Title = title;
            s.Box = new Panel();
            s.Box.Tag = "panel2";
            s.Box.Margin = new Padding(0, 0, 0, 6);
            s.Head = new Label();
            s.Head.AutoSize = false;
            s.Head.Height = 34;
            s.Head.Location = new Point(0, 0);
            s.Head.Font = HelpHeadFont;
            s.Head.TextAlign = ContentAlignment.MiddleLeft;
            s.Head.Padding = new Padding(10, 0, 0, 0);
            s.Head.Cursor = Cursors.Hand;
            s.Head.Tag = "";
            s.Body = new Label();
            s.Body.AutoSize = true;
            s.Body.Font = HelpBodyFont;
            s.Body.Text = text;
            s.Body.Location = new Point(14, 36);
            s.Body.Tag = "";
            s.Body.Visible = false;
            s.Box.Controls.Add(s.Head);
            s.Box.Controls.Add(s.Body);
            s.Head.Click += delegate { s.Open = !s.Open; LayoutHelp(); };
            helpSections.Add(s);
            helpFlow.Controls.Add(s.Box);
        }

        void SetAllHelp(bool open)
        {
            foreach (HelpSection s in helpSections) s.Open = open;
            LayoutHelp();
        }

        void LayoutHelp()
        {
            if (helpFlow == null) return;
            int w = Math.Max(300, helpFlow.ClientSize.Width - helpFlow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            helpFlow.SuspendLayout();
            helpIntro.MaximumSize = new Size(w, 0);
            foreach (HelpSection s in helpSections)
            {
                s.Head.Text = (s.Open ? "-   " : "+   ") + s.Title;
                s.Head.Width = w;
                s.Body.MaximumSize = new Size(w - 28, 0);
                s.Body.Visible = s.Open;
                s.Box.Size = new Size(w, s.Open ? s.Body.Bottom + 12 : s.Head.Height);
            }
            helpFlow.ResumeLayout(true);
        }

        // { title, paragraph, paragraph, ... }
        static string[][] HelpTopics()
        {
            return new string[][]
            {
                new string[] { "Quick start",
                    "1. Add accounts: '+ Add account' > 'Log in with a browser'. A clean Edge / Chrome window opens on roblox.com - log in normally (captcha and 2-step verification work). The app notices the login, stores it encrypted and closes that browser. You can also paste cookies or drop a text file onto the account list.",
                    "2. Turn multi-instance on: the top right must show ACTIVE.",
                    "3. Click one or more accounts (Ctrl / Shift for several), choose a game on the Launch page and press 'Launch selected'.",
                    "4. Open the Layout page and press 'Arrange now' - all windows are spread evenly over the screen.",
                    "5. Happy with the setup? Save it on the Presets page, so next time one click brings everything back." },

                new string[] { "The main window and the account list",
                    "Top bar - Running = accounts this app started / accounts saved. Roblox CPU and Roblox memory = what all Roblox windows use together (CPU as a percentage of your whole PC, like Task Manager); the small line is the average per account.",
                    "List / Cards - the buttons next to the search box switch the account view. List = compact rows with profile picture and status dot. Cards = big profile picture, status, a game field for that account and a Launch / Stop button. Drag the divider between the accounts and the pages to make the cards wider (two or more columns).",
                    "Status dot - green = in a game or running, blue = online, purple = Roblox Studio, grey = offline, red = login no longer works.",
                    "Sorting and order - More > 'Sort accounts by' (name, group, status, CPU, last used). 'Up' / 'Down' set your own order. Search filters by name, alias, notes or group; the Group box shows one group.",
                    "Shortcuts - Ctrl+A selects all accounts, Delete removes the selected ones, a double-click opens Edit." },

                new string[] { "Adding and editing accounts",
                    "+ Add account - browser login (recommended), add several with username:password, paste cookie(s), or import a file. The app finds cookies inside any text, so exports from other account managers, notes or JSON all work. Adding an account that is already in the list just updates its login.",
                    "Add several with username:password - paste a list of 'username:password' lines (one per account, like Roblox Account Manager). A browser window opens for each account with the username and password already filled in; you press 'Log In' yourself and solve any captcha / 2-step, then the account is added automatically. Passwords are only typed into Roblox's own login page and are not saved.",
                    "Edit - alias, notes, group, password (only for copying), the game of that account, a saved Job ID, an FPS cap just for that account, or replace a login that stopped working.",
                    "Editing several accounts at once - select them and press Edit. Fields they share show their value, fields that differ stay empty, and only the fields you change are saved to every selected account. The window title lists every account you are editing.",
                    "Game per account - type a game link, private server link or Place ID in the card's game field or in Edit. That account then always joins its own game, whether you press its Launch button, 'Launch selected' or 'Launch all'. Accounts without a game use the Launch page.",
                    "Check - tests whether the logins of the selected accounts (or all) still work.",
                    "More (or right-click) - copy username / password / user:pass / cookie / user ID, show account details (age, Robux, Premium - for several accounts at once if you select several), copy a login ticket, open the profile, refresh the cookie, move to a group." },

                new string[] { "Launching and joining games",
                    "Launch page - join a game (Roblox picks a server), a specific server (Job ID), a private server (old and new share links) or follow a player. 'Search game...' finds a game by name, 'Pick server...' lists its servers sorted by players, ping or FPS, 'Random' picks an open public server.",
                    "Accounts start one after another with the delay you set (6-10 seconds is safe). 'Use each account's own game' lets every account go to its own game.",
                    "Launch method (Settings) - 'roblox-player link' (default) starts Roblox exactly like Roblox Account Manager and like the Play button on the website. Accounts launched this way are not asked for human verification when they join. 'Direct' starts RobloxPlayerBeta.exe itself; only use it if the link does not start Roblox on your PC.",
                    "Servers page - search games, browse their servers (players / ping / FPS) and press 'Join with selected accounts' to put several accounts into the SAME server.",
                    "Launch all / Stop all - under the account list. Stop all closes only the Roblox windows this app started." },

                new string[] { "Windows and layout",
                    "Arrange now spreads the Roblox windows over the monitor you choose. Roblox normally refuses to get smaller than about 816 x 638, but the app resizes windows in a way that skips that limit, so even 25 windows fit on one screen. If a window still refuses, its border is removed automatically; only if even that fails are the windows stacked with overlap (the preview turns orange).",
                    "Automatic grid (default) - rows and columns follow the number of windows and your screen shape (2 = side by side, 4 = 2x2, 6 = 3x2, 9 = 3x3). Custom grid - your own rows / columns. Fixed size - every window gets the exact pixel size you choose.",
                    "Window scale, gap and margin shrink the windows or add space between them. 'Stretch the last row' fills a hole in the last row.",
                    "Window list - the order decides which window gets which cell (Move up / down). Include / exclude takes a window out of the grid; 'Pin size/pos' places one window at an exact spot and remembers it for that account.",
                    "Auto-capture (off by default) re-arranges a moment after a Roblox window opens or closes. Hotkeys: Ctrl+Alt+F9 arrange, Ctrl+Alt+F10 / F11 focus the previous / next Roblox window.",
                    "Tip: Roblox must not be in fullscreen (press F11 in the game or choose Windowed in Roblox's settings), otherwise it cannot be moved." },

                new string[] { "Presets",
                    "A preset saves your whole setup: the accounts that are running (in the order you launched them), the game / server each one is in, the position and size of every Roblox window, and the settings of the Launch, Layout, Performance and Settings pages.",
                    "Run puts it all back: settings restored, accounts launched one after another, windows moved to where they were. Accounts that are already running are only moved.",
                    "'Run at startup' together with 'Start MultiHub with Windows' brings everything back after a PC restart without clicking anything.",
                    "Servers close when they are empty or old, so by default accounts rejoin the same game on any server. Tick 'Rejoin the exact same servers' to try the saved server (useful shortly after saving). Private servers and followed players are always kept." },

                new string[] { "Performance: CPU and graphics",
                    "FPS cap - fewer frames per second means much less CPU and GPU; 30 is a good value for many accounts. The app writes it into Roblox's own settings file right before every launch (Roblox ignores the old FPS flag since September 2025). Windows that are already open keep their cap until relaunched. An account can have its own cap (Edit).",
                    "Graphics presets and options - quality level, texture quality, anti-aliasing, grass, lighting, sky and graphics API, applied to every account you launch. Only flags on Roblox's official allowlist are written; the box on the page shows exactly what is written.",
                    "CPU priority - Below normal / Idle keeps your PC responsive with many windows open. 'Minimize all' minimizes every Roblox window (minimized windows use less GPU)." },

                new string[] { "Performance: memory (RAM) cleaner",
                    "Works like Mem Reduct. 'Clean memory now' frees RAM right away: programs (including every Roblox window) give back memory they are not using, and Windows' caches are emptied. Windows only allows that with administrator rights, so you are asked for permission. If you say No, only the Roblox windows started by this app are trimmed.",
                    "Auto-clean - tick 'Clean automatically when RAM use goes over' and choose a limit (default 80%). A tiny helper then runs as administrator in the background, checks the RAM every second and cleans as soon as the limit is passed, at most once a minute. You are asked for permission when you turn it on and each time MultiHub starts; the helper stops when you close MultiHub. Optionally it also cleans every N minutes.",
                    "What is cleaned - the ticked areas: running programs, the system file cache, modified pages and the low-priority standby list are recommended. 'Whole standby list' frees the most but makes programs load a bit slower afterwards; 'Combine identical memory pages' (Windows 10 / 11) merges duplicate memory, which many Roblox windows have a lot of, but costs some CPU for a moment.",
                    "Right after a clean a window can hitch for a moment while it takes back what it needs - that is normal." },

                new string[] { "Automation: anti-AFK and auto-relaunch",
                    "Anti-AFK (Settings, off by default) - Roblox kicks accounts after about 20 minutes without input. When ON, every X minutes (default 12, you set it) each Roblox window started by this app comes to the front for about a third of a second, gets one small input and the window you were using is put back. The window you are using yourself is skipped. Pick the action: 'Mouse nudge' (default) resets the idle timer without moving your character, so auto-hatch / auto-farm keep running; 'Jump' presses Space; 'Zoom' presses I then O.",
                    "Auto-relaunch (Settings) - relaunches an account whose window disappeared (crash, kick, closed) into the same game. 'Also relaunch when the account left the game' catches windows that stay open after a disconnect." },

                new string[] { Lang.T("Macro recorder"),
                    "The Macro button (next to Help) opens a TinyTask-style recorder in its own window. Record (Ctrl+Shift+Alt+R) records your mouse and keyboard, Play (Ctrl+Shift+Alt+P) plays it back; press the same hotkey or button again to stop. The hotkeys also work while a game has the focus.",
                    "Save / Open keep recordings as .rmacro files. Options: playback speed, play N times or until stopped, a pause between repeats, record mouse movement (high resolution or not), your own hotkeys, stay on top (only the macro window, not the hub) and hide the window while it runs.",
                    "'Small' shrinks the window to just the buttons; 'Large' shows all options again. The window remembers which one you used last." },

                new string[] { "Settings, security and your data",
                    "Multi-instance - lets several Roblox windows run at once. It only works for Roblox windows started after this app, and only while this app stays open. 'Start MultiHub with Windows' makes sure the app is first.",
                    "Encryption - accounts (including logins) are always stored encrypted in your user profile, not next to the program. By default Windows protects them for your Windows user; with your own password they are protected with AES-256 and can be moved to another PC. A forgotten password cannot be recovered.",
                    "Backups - a backup of your accounts is made every time the app starts (the last 10 are kept). 'Open data folder' shows settings, log and backups.",
                    "Login check at start-up - every saved login is tested once when MultiHub starts. Accounts that Roblox signed out since the previous check are shown in one popup, so you know which ones to log in with again (+ Add account > Log in with a browser - the existing entry is updated)." },

                new string[] { "Keeping your accounts safe",
                    "A cookie is a login: anyone who has it can use the account. Never share cookies, never paste them into websites, and keep this app's data folder private.",
                    "Add a verified email address (and 2-step verification) to every account. Roblox can lock or reset accounts when it sees many accounts used from the same PC (players report it from about 8 accounts). With a verified email you can always get the account back.",
                    "Running several clients and automating input (multi-instance, anti-AFK, macros) is something Roblox may not like and can change at any time. Use your own accounts and accept that risk." },

                new string[] { "If something goes wrong",
                    "Status says WAITING - Roblox was already running (often a background process). Settings > 'Close all Roblox processes', then launch from this app.",
                    "A second window closes the first - multi-instance is off or not ACTIVE, or Roblox changed its client. Try the other launch method in Settings. Roblox updates can break multi-instance; that is outside this app's control.",
                    "Human verification when joining - make sure the launch method is 'roblox-player link' (Settings) and keep the delay between accounts at 6+ seconds.",
                    "'No new Roblox process appeared' - Roblox may be updating or is not installed for this Windows user. Start Roblox once normally, close it and try again.",
                    "Red dot / 'Cookie expired' - the login was ended (password change, logout, lock). Log in again with '+ Add account' > 'Log in with a browser', or Edit > Replace cookie.",
                    "Auto-clean says the helper is not running - you clicked No on the permission question. Turn auto-clean off and on again.",
                    "Game search or servers empty - Roblox rate-limits and sometimes changes things. You can always paste a Place ID directly.",
                    "Everything the app does is in the activity log; details of errors are in log.txt (Settings > Open data folder)." }
            };
        }
    }
}
