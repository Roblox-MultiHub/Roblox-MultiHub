# Team guide

General development and release instructions for Roblox MultiHub maintainers. This file is public; keep confidential information in the team's private workspace.

## Development workflow

Each developer uses their own GitHub account. The organization owner manages repository access.

1. Clone the repository.
2. Create a branch such as `feature/server-browser` or `fix/window-layout`.
3. Commit and push your changes to that branch.
4. Open a pull request and request review from another team member.
5. Merge the reviewed change into `main`.

Never commit Roblox cookies, passwords, API tokens, account databases, personal backups, or recovery codes. The `.gitignore` file helps exclude common local data; review every commit before pushing.

## Publishing updates

Use version tags such as `v1.0.0` and titles such as **Roblox MultiHub 1.0.0**.

Prepare each release as a draft with:

- **Added:** new features.
- **Changed:** improvements and behavior changes.
- **Fixed:** resolved bugs.
- **Known issues:** remaining limitations.
- **Installation / update:** requirements and upgrade instructions.
- **Assets:** the tested Windows package and, when available, a SHA-256 checksum file.

Publish after the matching application source and Windows package have been reviewed and tested. Update the public README with the final download link when the first version is ready.
