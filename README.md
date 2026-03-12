# Radarr (The Ant Forge)

A fork of [Radarr](https://github.com/Radarr/Radarr) — movie collection manager for Usenet and BitTorrent users.

This fork adds new features, code quality improvements, and bug fixes on top of the upstream Radarr project. Changes are developed with the goal of eventual contribution back to the upstream project via pull requests but divergence may happen due to migrating many of the dependencies and removal and inlining of lightly used dependencies.

## Fork Changes

### New Features

**Place in Root Folder** — A new setting in Media Management that tells Radarr to place movie files directly in the root folder instead of creating a per-movie subfolder. Useful for flat library layouts or users who rely on external tools for organisation. See [Spec-No-Folders.md](docs/Spec-No-Folders.md).

**Refresh Monitored Movies Only** — A new setting in Media Management that skips unmonitored movies during refresh and disk scan cycles. Combined with scan path deduplication and title-based file filtering, this dramatically improves performance for PlaceInRootFolder users with large libraries. See [Spec-Refresh-Monitored-Only.md](docs/Spec-Refresh-Monitored-Only.md).

**Unmonitor on Cutoff Met** — A new setting in Media Management > File Management that automatically unmonitors movies after import once the file quality meets or exceeds the quality profile's cutoff. Saves indexer API calls and bandwidth by stopping searches for upgrades that aren't needed. See [Spec-UnMonitor.md](docs/Spec-UnMonitor.md).

### Bug Fixes

- **createAjaxRequest GET params** — Fixed jQuery-to-fetch migration bug where GET request query parameters were not being appended to the URL correctly.
- **HttpProxySettingsProvider** — Fixed CA1846 (AsSpan over Substring) and SA1513 (StyleCop formatting) build errors.

### Code Quality

Two rounds of code review covering security, correctness, and quality improvements across the codebase. See [Code-Review-260308.md](docs/Code-Review-260308.md) for the full review document.

Highlights:
- Async controller improvements and dead code removal
- OAuth flow isolation and security hardening
- Input validation and error handling improvements
- Dependency updates and dead dependency removal

## Upstream Radarr

This fork is based on [Radarr/Radarr](https://github.com/Radarr/Radarr) and tracks the upstream `develop` branch. The upstream remote is configured as `upstream`.

For upstream documentation, features, and support:
- [Wiki](https://wiki.servarr.com/radarr)
- [Discord](https://radarr.video/discord)
- [API Documentation](https://radarr.video/docs/api/)
- [Contributing Guide](CONTRIBUTING.md)

## Development

See [CLAUDE.md](CLAUDE.md) for full build instructions, project structure, and local testing deployment setup.

Quick start:
```bash
# Prerequisites: .NET 8 SDK, Node.js 20+, Yarn (corepack enable)

# Backend (self-contained, win-x64)
dotnet msbuild -restore src/Radarr.sln -p:Configuration=Release -p:Platform=Posix -p:RuntimeIdentifiers=win-x64 -p:SelfContained=true -t:PublishAllRids

# Frontend
yarn install
yarn build

# Deploy to local test instance
./scripts/deploy-local.sh
```

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2010-2026
