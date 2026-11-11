using System;
using System.Collections.Generic;

namespace RobloxMultiHub
{
    // One saved Roblox account. Only plain public fields so the JSON serializer can round-trip it.
    public class Account
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Username = "";
        public long UserId;
        public string DisplayName = "";
        public string Cookie = "";          // the .ROBLOSECURITY value. Treat like a password!
        public string Password = "";        // optional, only stored if you type it in
        public string Alias = "";
        public string Description = "";
        public string Group = "";
        public string SavedPlaceId = "";
        public string SavedJobId = "";
        public int FpsCap;                  // 0 = use the global FPS cap; otherwise this account launches with its own cap
        public string GameLink = "";        // game link / Place ID this account joins by default
        public long TrackerId;              // stable "browser tracker id" for this account
        public long LastUsedTicks;
        public int Order;
        public bool LoginLost;              // Roblox ended this login and you were already told (startup check)
    }

    public class AccountFile
    {
        public List<Account> Accounts = new List<Account>();
    }

    // Runtime-only info about an account (never saved).
    public class LiveInfo
    {
        public int PresenceType = -1;       // -1 unknown, 0 offline, 1 online, 2 in game, 3 studio
        public string Where = "";
        public bool CookieBad;
        public bool Locked;                 // HTTP 403: Roblox locked / blocked this account
        public bool LockedAlerted;          // a Discord alert was already sent for this lock
    }

    public static class AppState
    {
        static readonly Dictionary<string, LiveInfo> live = new Dictionary<string, LiveInfo>();
        public static LiveInfo Live(Account a)
        {
            LiveInfo li;
            if (!live.TryGetValue(a.Id, out li)) { li = new LiveInfo(); live[a.Id] = li; }
            return li;
        }
        public static string Name(Account a)
        {
            if (!string.IsNullOrEmpty(a.Alias)) return a.Alias;
            if (!string.IsNullOrEmpty(a.Username)) return a.Username;
            return "(unnamed)";
        }
    }

    public class LayoutSettings
    {
        public string Mode = "Auto";        // Auto | Grid | FixedSize
        public int Rows = 0;
        public int Cols = 0;
        public string Monitor = "";         // monitor device name, "" = primary
        public bool UseWorkArea = true;     // true = keep clear of the taskbar
        public int Gap = 0;
        public int Margin = 0;
        public int Scale = 100;             // percent of each grid cell the window fills
        public int FixedW = 800;
        public int FixedH = 600;
        public bool StretchLast = true;     // last row stretches to fill leftover space
        public bool Borderless = false;     // optional; turned on automatically only if a window still refuses to shrink
        public bool AlwaysOnTop = false;
        public bool LabelTitles = false;
        public bool AutoCapture = false;    // OFF by default, as requested
        public int AutoCaptureDelayMs = 1500;
        public bool Hotkeys = true;
        public string SortMode = "Manual";     // Manual | Name | Pid - the order the grid is filled in
        public int MinW = 0;                // smallest window size Roblox accepts (measured automatically, 0 = unknown)
        public int MinH = 0;
    }

    // Roblox graphics options that are written into ClientAppSettings.json before each launch.
    public class GfxSettings
    {
        public string Preset = "Default";   // Default | Light | LowCpu | Lowest | Custom
        public int Quality = 0;             // 0 = Roblox default, otherwise graphics quality level 1..21
        public int Texture = -1;            // -1 = Roblox default, 0..3
        public int Msaa = 0;                // 0 = Roblox default, otherwise 1 (off), 2, 4, 8
        public bool NoGrass = false;
        public bool PauseVoxelizer = false;
        public bool GraySky = false;
        public bool LowCsg = false;
        public string Api = "Default";      // Default | D3D11 | Vulkan | OpenGL
        public bool WriteUnlisted = false;  // also write custom flags that are not on Roblox's allowlist
    }

    public class GameEntry
    {
        public long PlaceId;
        public string Name = "";
        public long Ticks;
    }

    public class PinnedRect
    {
        public string Key = "";
        public int X;
        public int Y;
        public int W;
        public int H;
    }

    public class AppSettings
    {
        public bool FirstRunDone = false;
        public bool MultiInstance = false;
        public string Theme = "Dark";
        public bool Animations = true;               // eased UI transitions; off = everything snaps
        public string Language = "en";               // UI language code; "en" = English (default)
        public string LaunchMethod = "Protocol";    // Protocol (roblox-player: link, like Roblox Account Manager) | Direct
        public bool LaunchDefaultsV2 = false;       // set once the launch method was switched to the RAM-style default
        public int LaunchDelaySec = 8;
        public bool AutoCloseLast = true;
        public int MaxInstances = 0;
        public bool RefreshCookieOnLaunch = false;
        public bool StartWithWindows = false;
        public bool ShowPresence = true;
        public int PresenceSec = 30;
        public string CustomClientSettings = "";
        public GfxSettings Gfx = new GfxSettings();
        public string Priority = "Normal";          // Normal | BelowNormal | Idle
        public bool WroteClientFile = false;
        public int FpsCap = 0;                      // 0 = leave Roblox's own setting alone
        public bool WroteFpsCap = false;
        public string View = "List";                // List | Cards
        public int SplitW = 600;
        public bool AutoRelaunch = false;
        public int WatcherSec = 60;
        public bool WatcherUsePresence = false;
        // Crash breaker: stop relaunching an account that keeps dying, so the watcher never loops forever.
        public bool CrashBreaker = true;
        public int CrashBreakerMax = 3;             // give up after this many relaunches...
        public int CrashBreakerWindowMin = 3;       // ...within this many minutes
        public int CrashBreakerCooldownMin = 10;    // then try that account again after this long
        public bool AntiAfk = false;
        public int AntiAfkMin = 12;                 // how often anti-AFK runs; Roblox kicks after ~20 min
        public string AntiAfkAction = "Nudge";      // Nudge (mouse) | Space (jump) | Zoom
        public string PlaceLauncherUrl = "https://assetgame.roblox.com/game/PlaceLauncher.ashx";
        public string LastMode = "Game";
        public string LastPlaceId = "";
        public string LastJobId = "";
        public string LastPrivate = "";
        public string LastFollow = "";
        public bool UseSavedPerAccount = true;
        public List<string> Groups = new List<string>();
        public List<GameEntry> Recent = new List<GameEntry>();
        public List<PinnedRect> Pinned = new List<PinnedRect>();
        public LayoutSettings Layout = new LayoutSettings();
        public List<Preset> Presets = new List<Preset>();
        public string StartupPreset = "";           // name of the preset that runs when the app starts, "" = none
        public bool PresetExactServers = false;     // presets rejoin the saved Job ID instead of any server of that game
        public MacroSettings Macro = new MacroSettings();
        public MemSettings Mem = new MemSettings();
        public bool CheckLoginsAtStartup = true;    // check every saved login when the app starts
        public bool CheckUpdates = true;            // check GitHub for a newer version at startup (notify only)
        public int WinW = 1300;
        public int WinH = 780;

        // Discord webhook alerts (opt-in). The URL is only ever sent to Discord.
        public bool Webhook = false;
        public string WebhookUrl = "";
        public bool WebhookCrash = true;            // a window this app started disappeared unexpectedly
        public bool WebhookRelaunch = true;         // the watcher brought a window back
        public bool WebhookBatch = true;            // a Launch all / preset batch finished
        public bool WebhookLocked = true;           // an account came back locked (HTTP 403)
        public bool WebhookStatus = false;          // keep a live status embed updated in the channel
        public int WebhookStatusSec = 60;           // how often the status embed refreshes
        public string WebhookStatusMsgId = "";      // the Discord message the status keeps editing

        // Discord Rich Presence ("Playing Roblox MultiHub" on your profile). Needs a Discord Application ID.
        public bool DiscordRpc = true;
        public string DiscordAppId = "";

        // "Max alts": throttle the Roblox windows you are NOT playing, so many clients stay light.
        public bool MaxAlts = false;
        public int MaxAltsFps = 10;                 // FPS cap used for new launches while Max alts is on
        public bool MaxAltsIdle = true;             // background clients get Idle CPU priority + efficiency mode
        public bool MaxAltsTrim = true;             // background clients get their memory trimmed every so often
        public bool MaxAltsMinimize = false;        // also minimize background clients

        // Beta: hard working-set cap per Roblox window in MB (0 = off).
        public int MaxRamMb = 0;
        public int RamCapFloorMb = 0;             // measured by the cap probe; 0 = not measured yet
        public bool VeryLowMem = true;            // VeryLow memory priority on every client
        public bool HideTaskbar = false;          // Roblox windows off the taskbar / Alt-Tab
    }

    // RAM cleaner (Performance page). The cleaning itself is done by an admin helper process.
    public class MemSettings
    {
        public bool AutoClean = false;
        public int Threshold = 80;                  // clean when this % of the RAM is in use
        public int IntervalMin = 0;                 // also clean every N minutes (0 = off)
        public bool WorkingSets = true;             // trim the memory of running programs
        public bool FileCache = true;               // system file cache
        public bool ModifiedList = true;            // write modified pages to disk and free them
        public bool StandbyLow = true;              // low-priority part of the standby list
        public bool StandbyFull = false;            // the whole standby list (makes the next file loads slower)
        public bool Combine = false;                // combine identical memory pages (Windows 10/11, uses some CPU)
    }

    // Options of the macro recorder window.
    public class MacroSettings
    {
        public double Speed = 1.0;
        public int Repeats = 1;
        public bool Continuous = false;
        public int PauseMs = 0;
        public bool RecordMoves = true;
        public bool HiRes = true;
        public string RecHotkey = "Ctrl+Shift+Alt+R";
        public string PlayHotkey = "Ctrl+Shift+Alt+P";
        public bool TopMost = false;
        public bool HideWhileRunning = false;
        public bool Compact = false;                // small display: only the buttons
        public string LastFile = "";
    }

    // One account inside a preset: what it joined and where its window was.
    public class PresetEntry
    {
        public string AccountId = "";
        public string Username = "";        // shown if the account was removed in the meantime
        public LaunchSpec Spec;             // null = the account's own game, or the Launch page of the preset
        public int GridIndex = -1;          // position in the window list (which grid cell it got)
        public bool Include = true;         // false = the window was taken out of the grid
        public bool HasRect;
        public int X;
        public int Y;
        public int W;
        public int H;
        public bool Borderless;
        public bool Minimized;
    }

    // Everything on the settings pages that a preset brings back (not the theme, password or window of this app).
    public class PresetSettings
    {
        public GfxSettings Gfx = new GfxSettings();
        public string CustomClientSettings = "";
        public int FpsCap;
        public string Priority = "Normal";
        public LayoutSettings Layout = new LayoutSettings();
        public string LaunchMethod = "Direct";
        public int LaunchDelaySec = 8;
        public bool AutoCloseLast = true;
        public int MaxInstances;
        public bool UseSavedPerAccount = true;
        public bool AutoRelaunch;
        public int WatcherSec = 60;
        public bool WatcherUsePresence;
        public bool AntiAfk;
        public int AntiAfkMin = 12;
        public string AntiAfkAction = "Nudge";
        public string LastMode = "Game";
        public string LastPlaceId = "";
        public string LastJobId = "";
        public string LastPrivate = "";
        public string LastFollow = "";

        public static PresetSettings Capture(AppSettings s)
        {
            PresetSettings p = new PresetSettings();
            p.Gfx = Json.From<GfxSettings>(Json.To(s.Gfx));
            p.CustomClientSettings = s.CustomClientSettings;
            p.FpsCap = s.FpsCap;
            p.Priority = s.Priority;
            p.Layout = Json.From<LayoutSettings>(Json.To(s.Layout));
            p.LaunchMethod = s.LaunchMethod;
            p.LaunchDelaySec = s.LaunchDelaySec;
            p.AutoCloseLast = s.AutoCloseLast;
            p.MaxInstances = s.MaxInstances;
            p.UseSavedPerAccount = s.UseSavedPerAccount;
            p.AutoRelaunch = s.AutoRelaunch;
            p.WatcherSec = s.WatcherSec;
            p.WatcherUsePresence = s.WatcherUsePresence;
            p.AntiAfk = s.AntiAfk;
            p.AntiAfkMin = s.AntiAfkMin;
            p.AntiAfkAction = s.AntiAfkAction;
            p.LastMode = s.LastMode;
            p.LastPlaceId = s.LastPlaceId;
            p.LastJobId = s.LastJobId;
            p.LastPrivate = s.LastPrivate;
            p.LastFollow = s.LastFollow;
            return p;
        }

        public void ApplyTo(AppSettings s)
        {
            if (Gfx != null) s.Gfx = Json.From<GfxSettings>(Json.To(Gfx));
            s.CustomClientSettings = CustomClientSettings ?? "";
            s.FpsCap = FpsCap;
            s.Priority = Priority ?? "Normal";
            if (Layout != null)
            {
                LayoutSettings l = Json.From<LayoutSettings>(Json.To(Layout));
                l.MinW = s.Layout.MinW;               // measured on this PC, not part of the preset
                l.MinH = s.Layout.MinH;
                s.Layout = l;
            }
            s.LaunchDelaySec = LaunchDelaySec;
            s.AutoCloseLast = AutoCloseLast;
            s.MaxInstances = MaxInstances;
            s.UseSavedPerAccount = UseSavedPerAccount;
            s.AutoRelaunch = AutoRelaunch;
            s.WatcherSec = WatcherSec;
            s.WatcherUsePresence = WatcherUsePresence;
            s.AntiAfk = AntiAfk;
            s.AntiAfkMin = AntiAfkMin > 0 ? AntiAfkMin : 12;
            s.AntiAfkAction = string.IsNullOrEmpty(AntiAfkAction) ? "Nudge" : AntiAfkAction;
            s.LastMode = LastMode ?? "Game";
            s.LastPlaceId = LastPlaceId ?? "";
            s.LastJobId = LastJobId ?? "";
            s.LastPrivate = LastPrivate ?? "";
            s.LastFollow = LastFollow ?? "";
        }
    }

    // A saved setup: which accounts ran (in launch order), where they went, their windows and the settings.
    public class Preset
    {
        public string Name = "";
        public long SavedTicks;
        public List<PresetEntry> Entries = new List<PresetEntry>();
        public PresetSettings Settings = new PresetSettings();
    }

    // What to join.
    public class LaunchSpec
    {
        public string Kind = "Game";        // Game | Job | Private | Follow
        public long PlaceId;
        public string JobId = "";
        public string LinkCode = "";
        public long FollowUserId;
        public string FollowName = "";

        public LaunchSpec Copy()
        {
            LaunchSpec s = new LaunchSpec();
            s.Kind = Kind; s.PlaceId = PlaceId; s.JobId = JobId; s.LinkCode = LinkCode;
            s.FollowUserId = FollowUserId; s.FollowName = FollowName;
            return s;
        }

        public string Describe()
        {
            if (Kind == "Job") return "place " + PlaceId + " / server " + JobId;
            if (Kind == "Private") return "private server of place " + PlaceId;
            if (Kind == "Follow") return "following " + (string.IsNullOrEmpty(FollowName) ? FollowUserId.ToString() : FollowName);
            return "place " + PlaceId;
        }
    }

    // A Roblox window/process that this app started.
    public class Instance
    {
        public Account Account;
        public int Pid;
        public DateTime Started;
        public LaunchSpec Spec;
        public bool UserClosed;
        public int Strikes;
    }
}
