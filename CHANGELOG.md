# Changelog

All notable changes to Roblox MultiHub. Newest first. Versions link to their release.

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
