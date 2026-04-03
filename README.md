# Radarr (The Ant Forge)

A fork of [Radarr](https://github.com/Radarr/Radarr) — movie collection manager for Usenet and BitTorrent users.

This fork adds features, performance improvements, and bug fixes on top of upstream Radarr. It is a standalone repository; the upstream remote is retained for cherry-picking useful commits.

## Features

**Place in Root Folder** — Place movie files directly in the root folder instead of creating per-movie subfolders. Useful for flat library layouts or external organisation tools. Includes title-based file filtering with year validation and path deduplication. See [Spec-No-Folders.md](docs/Spec-No-Folders.md).

**Refresh Monitored Movies Only** — Skip unmonitored movies during refresh and disk scan cycles, with bulk TMDb API support for faster metadata updates. See [Spec-Refresh-Monitored-Only.md](docs/Spec-Refresh-Monitored-Only.md).

**Unmonitor on Cutoff Met** — Automatically unmonitor movies after import once quality meets the profile cutoff. Also unmonitors newly added movies (e.g. from Overseerr) if a file already exists on disk, preventing unnecessary searches and overwrites. See [Spec-UnMonitor.md](docs/Spec-UnMonitor.md).

**Unlink Movie Files** — New UI button (broken chain icon) in the movie file list to detach a file from a movie record without deleting it from disk.

**Scan Locking and Change Detection** — Path-level scan locking prevents concurrent scans of the same folder. Folder LastWriteTime detection skips unchanged folders for faster rescans.

**Bulk TMDb Refresh** — Metadata refresh uses the bulk SkyHook API for multi-movie updates with automatic per-movie fallback, skipping credits in bulk mode for efficiency.

## Bug Fixes

- **Plex/Emby path mapping** — Fixed inverted MapFrom/MapTo logic that caused OsPath platform mismatch when Radarr (Windows) notifies Plex (Linux/NAS)
- **Import path rebase** — Fixed legacy movies importing to wrong folder when PlaceInRootFolder is enabled
- **Year-aware title matching** — Fixed same-title different-year movies (e.g. two versions of the same film) being confused in PlaceInRootFolder mode
- **Root folder deletion guards** — Added PlaceInRootFolder guards on all deletion paths to prevent accidental deletion of shared root folders
- **Bulk operation safety** — Fixed cross-movie context in bulk file delete and early exit on path conflicts
- **OS-aware path comparison** — Fixed RelativePath identity check for movie file moves
- **Backup downloads** — Fixed trailing slash in backup folder path (upstream cherry-pick)
- **URL parsing locale** — Fixed URL parsing on non-English systems (upstream cherry-pick)

## Performance

- **Media info cache** — Hash-based FFProbe cache keyed by path+mtime+size avoids redundant analysis on rescan
- **Monitored-only SQL filtering** — `GetMonitoredMovies()` pushes filtering to the database instead of loading all movies then discarding
- **Scan early exit** — Folders unchanged since last scan are skipped entirely via LastDiskScanTime tracking

## Code Quality

Two rounds of code review (43 findings, 40 resolved). See [Code-Review-260313.md](docs/Code-Review-260313.md) for the full review with resolution status.

- Root folder deletion and bulk operation safety guards
- UX improvements: tooltips, labels, settings placement
- Observability: structured debug/trace logging for scan and refresh decisions
- Test coverage: PlaceInRootFolder, RefreshMonitoredOnly, bulk API, and path dedup test suites
- Dead dependency removal (jQuery, Twitter notification, Plex legacy XML)
- Safe dependency updates across frontend and backend

## Upstream Radarr

Based on upstream v6.1.2. Cherry-picks are taken individually rather than merging, as the histories have diverged. See [CLAUDE.md](CLAUDE.md) for the merge strategy.

For upstream documentation:
- [Wiki](https://wiki.servarr.com/radarr)
- [API Documentation](https://radarr.video/docs/api/)

## Development

See [CLAUDE.md](CLAUDE.md) for full build instructions, project structure, and deployment setup.

```bash
# Prerequisites: .NET 8 SDK, Node.js 20.x, Yarn (corepack enable)

# Backend (self-contained, win-x64)
dotnet msbuild -restore src/Radarr.sln -p:Configuration=Release -p:Platform=Posix \
  -p:RuntimeIdentifiers=win-x64 -p:SelfContained=true -t:PublishAllRids

# Frontend
yarn install && yarn build

# Deploy to local instance
./scripts/deploy-local.sh
```

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2010-2026
