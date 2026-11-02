# Roblox MultiHub

**Manage your accounts. Launch your sessions. Organize your workspace.**

A Windows application for managing multiple Roblox accounts and sessions from one place.

[Downloads & release notes](../../releases) · [Report a bug](../../issues) · [Suggest a feature](../../issues)

> **Project setup in progress:** the application source code and first Windows build have not been uploaded yet. There is no app download available at this stage.

## Downloads

Published versions will be available on the [Releases page](../../releases). Each release will include a version number, a short changelog, and downloadable files under **Assets**.

Once a Windows build is published:

1. Open the latest release and read its release notes.
2. Download the Windows package listed under **Assets**.
3. Extract the ZIP if the release uses a portable package, then follow the included instructions.

GitHub-generated **Source code** archives contain project files; a runnable Windows package will be listed separately.

## Feature overview

The following describes the intended product scope. Check individual release notes for features available in a published build.

| Area | Features |
| --- | --- |
| Accounts | Browser sign-in, cookie imports, groups, aliases, notes, account details, live status, bulk editing, list and card views. |
| Launching | Games, specific servers, private servers, player following, launch delays, instance limits, and automatic relaunching. |
| Window layouts | Multiple sessions, automatic grids, custom dimensions, spacing, borderless windows, pinning, hotkeys, and multiple monitors. |
| Performance | FPS limits, graphics presets, CPU priority controls, and manual or automatic RAM cleaning. |
| Presets | Save and restore accounts, games, layouts, and settings. |
| Extras | Anti-AFK, mouse and keyboard macros, dark and light themes, and optional startup with Windows. |

## FAQ

**What is Roblox MultiHub?**  
A tool for managing Roblox accounts, sessions, and window layouts.

**Where will I download it?**  
From this repository's [Releases page](../../releases), under **Assets**. No build has been published yet.

**Which operating system is it for?**  
Windows. Exact requirements will be included with the first release.

**Can I report a problem or request a feature?**  
Yes. Open an [issue](../../issues) and include the app version, steps to reproduce, and expected behavior. Remove account details and other private information from screenshots and logs.

## Working with the team

Each developer uses their own GitHub account. Request project access from the organization owner.

1. Clone the repository.
2. Create a branch for your change, such as `feature/server-browser` or `fix/window-layout`.
3. Commit and push your work to that branch.
4. Open a pull request and have another team member review it.
5. Merge the reviewed change into `main`.

Never commit Roblox cookies, passwords, API tokens, account databases, personal backups, or recovery codes.

## Publishing updates

Use version tags such as `v1.0.0`, with release titles such as **Roblox MultiHub 1.0.0**.

For each release, include:

- **Added:** new features.
- **Changed:** improvements and behavior changes.
- **Fixed:** resolved bugs.
- **Known issues:** remaining limitations.
- **Installation / update:** requirements and upgrade instructions.
- **Assets:** the tested Windows package and, when available, a SHA-256 checksum file.

Prepare releases as drafts. Publish after the matching source code and Windows package have been reviewed and tested.

## Account data and security

Account cookies are sensitive credentials. Never share them in issues, pull requests, or release attachments. Storage, encryption, backup, and network behavior will be documented after the application source has been added and reviewed.

Roblox MultiHub is an independent project and is not affiliated with or endorsed by Roblox.
