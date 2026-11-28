# Changelog

All notable changes to Roblox MultiHub. Newest first. Versions link to their release.

## [v1.4.6](https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/tag/v1.4.6)

- **New: control your whole farm from Discord — even from your phone.** The Discord bot is now a full remote-control console, not just screenshots. Type `!status` (who's running), `!launch <name|all>`, `!stop <name|all>`, `!relaunch <name|all>`, `!cap <MB|off>`, `!shot` or `!help` in the channel the bot watches, and MultiHub does it and replies. Unlike other tools' remote control, it drives MultiHub itself (nothing is injected into Roblox) and never exposes your cookies. Use a **private** channel — anyone who can post there can launch/stop your accounts. Settings → Discord bot (remote control).

## [v1.4.5](https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/tag/v1.4.5)

- **Much better behaviour when Roblox auto-updates.** When Roblox installs an update it closes the old clients and has no window for a while, which used to look like a crash: the watcher relaunched into the half-installed update, launches "failed" after 60s, and the crash breaker gave up. Now MultiHub detects an update in progress and (1) pauses the watcher until it finishes, then brings your accounts back, (2) keeps waiting for a launching client instead of failing at 60s, and (3) holds the next account in a batch until the update is done — so one update no longer takes your whole farm down.
- **New (Beta): set the in-game volume on every launch.** Works like the FPS cap — MultiHub writes Roblox's master volume into its settings file right before each account starts, so every window opens at the volume you pick (0 = muted, great for a wall of alts). Find it under Beta → In-game volume.

## [v1.4.4](https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/tag/v1.4.4)

- **Fixed per-account private servers with new Roblox share links.** Roblox now hands out new-style share links (`roblox.com/share?code=...&type=Server`) that contain no place ID. A share link saved on an account is now resolved with that account's own login at launch, converted to the classic link and saved, so the account joins its own private server instead of silently falling back to the Launch page's game.
- If an account's share link can't be opened (the account isn't the owner and wasn't invited), that account is now skipped with a clear reason in the log instead of being sent to the wrong game.
- An account's own saved game / private server now always takes priority over the Launch page choice (when "use saved game per account" is on), including when the page is set to Private server or Follow player.
- The account card no longer warns "No Place ID found" for a valid share link.

## [v1.4.3](https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/tag/v1.4.3)

- Updated the "Buy us a coffee" / donate link to the project's new Buy Me a Coffee page.

## [v1.4.2](https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/tag/v1.4.2)

- **Run lighter when minimized** — while minimized to the taskbar MultiHub lowers its own process priority, releases unused memory and skips on-screen refreshes. The watcher/auto-relaunch, Discord alerts, live status and screenshots keep running. On by default (Settings → Interface).
- **Account screenshots to Discord** — post a labelled grid of all running account windows:
  - **Every X minutes** (off by default), with a *Send one now* button.
  - **On demand via a Discord bot command** — type `!shot` (or `!screenshot` / `!accounts`) in a channel and the bot replies with the screenshot. Polls the REST API; no "Message Content" intent needed.
- **Privacy fix** — the support bundle now correctly masks the webhook URL (an old check never matched) and the new bot token.

## [v1.4.0](https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/tag/v1.4.2)

- Merged community features (PR #1 by curv3ball): **Beta tab**, **per-window RAM cap** with an auto-prober that measures the lowest safe cap, **GPU metering**, **live Discord status embed** with a CPU/RAM chart, **one-click support bundle**, layout sorting and a borderless draggable window.
- Clearer messages: the "could not start Roblox" error now hints to install Roblox; the multi-instance chip suggests restarting the PC.
- The support bundle reports the real app version.

## v1.3.8

- Simpler UI: vertical sidebar with Settings/Help pinned at the bottom, collapsible activity log. Fixed the accounts filter/footer clipping and Home card sizing.

## v1.3.7

- Anti-AFK moves the cursor to the window centre before the nudge, then restores it.

## v1.3.6

- Identify the Roblox client by its account tracker id (command line); added launch diagnostics.

## v1.3.5

- Re-attach to a reused background Roblox process, so a relaunch after you close a window manually registers correctly.

## v1.3.4

- Fixed an instance-tracking race (the new Roblox PID is now polled on the UI thread).

## v1.3.3

- Discord Rich Presence, a welcome Home tab, smooth button animations and a wrapping tab bar.

## v1.1.4

- Launch PID detection runs fully off the UI thread — fixes preset/launch stalls.

## v1.1.3

- Update check (notify-only), at startup and on demand.

## v1.1.2

- Presets no longer overwrite your settings. Fixed a launch pump stall, a wheel-scroll trap, the RAM-clean prompt and toolbar overflow.

## [v1.1.0](https://github.com/Roblox-MultiHub/Roblox-MultiHub/releases/tag/v1.1.0)

- First public release line: Discord alerts, Max alts, Monitor, crash breaker, installer, translations (i18n) and a CI build.
