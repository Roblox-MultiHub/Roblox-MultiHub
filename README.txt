ROBLOX MULTIHUB
===============
Roblox account manager + multi-instance + automatic window grid, in ONE program.

HOW TO GET THE .EXE (no installs needed, takes ~10 seconds)
-----------------------------------------------------------
1. Extract this whole zip folder somewhere (for example your Desktop).
   Tip: if Windows shows a "protected your PC" warning on the .bat file, right-click the
   ZIP first > Properties > tick "Unblock" > OK, then extract again.
2. Double-click:   START HERE - Build and Run.bat
   It compiles RobloxMultiHub.exe with the C# compiler that is already built into
   Windows 10/11 (nothing to download) and starts it.
3. From now on just double-click  RobloxMultiHub.exe  (you can move it anywhere).

If the build prints errors, copy ALL the text from the black window and send it back.
(The source is in the "src" folder; "Rebuild" re-compiles after any change.)

FIRST STEPS
-----------
 - '+ Add account' > 'Log in with a browser' (Edge or Chrome opens, you log in, done).
 - Say YES to multi-instance on first start (top right must show ACTIVE).
 - Select accounts > 'Launch / Join' page > 'Launch selected'.
 - 'Windows & Layout' page > 'Arrange now'. Auto-capture is OFF by default.
 - The 'Help' page inside the app explains every button and option.

WHAT IS INCLUDED
----------------
Accounts : browser login, add several with username:password (prefilled browser logins), paste/import/drag-drop cookies (bulk, shows which failed), groups,
           aliases, notes, saved passwords, profile pictures, a game per account, search,
           sorting, manual order, cookie check, cookie refresh, copy user/pass/cookie/ID,
           account details (age, Robux, Premium), live presence (in game / online / offline).
           Two views: compact List and expanded Cards (drag the divider to widen them).
           Rows show the profile picture, status, group, last used and live CPU / RAM.
Usage    : live CPU and memory of all Roblox windows in the top bar (with the average per
           account), and the numbers for every single account.
Graphics : Performance page with an FPS cap, presets (Light / Low CPU-GPU / Lowest) and single options
           (quality level, textures, anti-aliasing, grass, lighting, sky, graphics API) that
           apply to every account you launch until you change them. Shows exactly what is
           written. Only flags on Roblox's official allowlist are written (see below).
           Plus CPU priority for Roblox windows and 'minimize all'.
Launching: join game, specific server (Job ID), private server (old + new links), follow a
           player, search a game by name, pick a server sorted by players / ping / FPS, random server, delay between accounts, auto-close previous window per
           account, max windows limit, recent games, game search, server browser (join the
           same server with many accounts), direct or protocol launch, auto-relaunch
           watcher, Launch all / Stop all.
Layout   : automatic grid (default), custom rows/columns, fixed pixel size, scale %, gap,
           margin, stretch last row, monitor choice, borderless, always-on-top, rename windows
           to account names, pin one window to an exact spot, include/exclude/reorder, live
           preview, auto-capture (default OFF), global hotkeys (Ctrl+Alt+F9/F10/F11).
Presets  : save the running accounts (launch order), their games, every window's position and size
           and all launch / layout / performance settings as a preset; 'Run' brings it all back.
           Optionally runs a preset automatically when the app starts.
Macro    : 'Macro' button opens a TinyTask-style recorder in its own window: record mouse + keyboard,
           save / open recordings, play once, N times or until stopped, at any speed, global hotkeys,
           stay on top (only the macro window), hide while running.
Anti-AFK : optional (off by default): every 10 minutes each Roblox window jumps once.
RAM      : Performance page > Memory: live RAM use, 'Clean memory now' and auto-clean when RAM use goes
           over a limit (default 80%), like Mem Reduct. A small helper runs as administrator for that.
Logins   : at start-up every saved login is checked; accounts Roblox signed out are listed in a popup.
Security : accounts always encrypted (Windows DPAPI, or your own password with AES-256),
           stored in %APPDATA%\RobloxMultiHub (not next to the exe), automatic backups.
Other    : dark/light theme, start with Windows.
Support  : Settings > 'Support this project' has 'Buy us a coffee' and 'Join our Discord' buttons.
           The tool is and stays free. Links: buymeacoffee.com/multihub  and  discord.gg/TCrVyjYhHr

NOT INCLUDED (compared with Roblox Account Manager)
---------------------------------------------------
Nexus / Account Control scripting, the local web-server API, the built-in Puppeteer browser
and extensions, captcha-solving services, username+password auto-login, quick-login by code,
avatar/outfit/group tools, Windows Store (UWP) Roblox, auto-update.

SMALL WINDOWS
-------------
Roblox clamps its window to about 816 x 638 while it processes a normal resize. The grid resizes
windows with SetWindowPos + SWP_NOSENDCHANGING, so the window never gets that message and can be
made much smaller. If a window still refuses, its border is removed automatically ("Restore
borders" brings it back); only if even that fails are the windows stacked with overlap.

FPS CAP
-------
Since 29 September 2025 Roblox ignores the old FPS-cap "Fast Flag". The app therefore edits
Roblox's own settings file instead (the FramerateCap value in GlobalBasicSettings_13.xml, the
same thing you can change by hand in Notepad), right before every launch. It applies to every
account you launch until you change it; windows that are already open keep their old cap until
you relaunch them. 0 gives control back to Roblox. The graphics options on the Performance
page only use flags from Roblox's official allowlist. An account can also have its own FPS cap
(Edit account); it is used for that account's launch only, then the global cap is put back.

IMPORTANT
---------
 - A cookie is a login. Anyone who has it can use the account. Never share them.
 - Multi-instance relies on how Roblox's Windows client works today. Roblox can change this
   at any time, and Roblox's anti-cheat team has said multiple clients may be treated as
   abusive. Use your own accounts and accept that risk.
 - Not affiliated with Roblox, Roblox Account Manager or OpenMultiBoxing. No code from
   those projects is used; this was written from scratch.
