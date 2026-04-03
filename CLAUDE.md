# Radarr - Project Guide

## What is Radarr?
Movie collection manager for Usenet/BitTorrent. Users select movies via web UI (or Overseerr/Jellyseerr), Radarr searches indexers, sends to download clients, and organizes files for Plex/media servers.

## Architecture
- **Backend**: C# .NET (solution at `src/Radarr.sln`)
- **Frontend**: React/TypeScript with webpack (`frontend/src/`)
- **API**: REST API v3 (`src/Radarr.Api.V3/`)
- **Database**: SQLite (migrations in `src/NzbDrone.Core/Datastore/`)

## Key Backend Projects
| Directory | Purpose |
|---|---|
| `src/NzbDrone.Core/` | Core business logic (movies, indexers, download clients, etc.) |
| `src/NzbDrone.Common/` | Shared utilities, disk, HTTP, environment |
| `src/NzbDrone.Host/` | Application host, startup, web server |
| `src/Radarr.Api.V3/` | API controllers |
| `src/Radarr.Http/` | HTTP framework, routing, authentication |
| `src/NzbDrone.SignalR/` | Real-time push notifications |

**Note**: Namespace is `NzbDrone.*` (legacy from Sonarr fork), project files are `Radarr.*.csproj`.

## Code Style
- **C#**: 4-space indent, `var` everywhere, `_camelCase` for private fields, no `this.` qualifier
- **Frontend**: 2-space indent (JS/TS/CSS)
- StyleCop + .editorconfig enforced; `TreatWarningsAsErrors=true`
- Tests use NUnit; test projects mirror source (`NzbDrone.Core.Test/` tests `NzbDrone.Core/`)

## Prerequisites
- **.NET 8 SDK** (project targets `net8.0`; CONTRIBUTING.md says .NET6 but that's outdated)
- **Visual Studio 2022** v17.8+ or Rider (or VS Code with C# Dev Kit)
- **Node.js 20.x** (not 18, not 21 — only 20)
- **Yarn** via corepack: `corepack enable`

## Build & Run
```bash
# Backend — win-x64 self-contained (for local testing)
dotnet msbuild -restore src/Radarr.sln -p:Configuration=Release -p:Platform=Posix -p:RuntimeIdentifiers=win-x64 -p:SelfContained=true -t:PublishAllRids

# Backend — all platforms (for releases)
dotnet msbuild -restore src/Radarr.sln -p:Configuration=Debug -p:Platform=Posix -t:PublishAllRids

# Backend (VS): set startup project to Radarr.Console, framework net8.0

# Frontend
yarn install
yarn start        # dev server with hot reload
yarn build        # production build
```
App runs at http://localhost:7878

## Production Deployment
Radarr runs natively on this Windows machine at `D:\Apps\Radarr` (port 9871).
It accesses media on a Synology NAS via SMB (`/volume1/video/Movies/`).
The build is **self-contained** (bundles .NET runtime + all dependencies), so the
entire `bin/` folder is replaced on each deploy — no version-mismatch issues.

| Build output | Live install |
|---|---|
| `_output/net8.0/win-x64/*` (self-contained) | `D:\Apps\Radarr\bin\` (replaced entirely) |
| `_output/UI/` | `D:\Apps\Radarr\bin\UI\` |

### Deploy script
```bash
# Upgrade deploy (preserves config.xml, radarr.db, logs, MediaCover, Backups)
./scripts/deploy-local.sh            # build + deploy backend + frontend
./scripts/deploy-local.sh ui         # build + deploy frontend only
./scripts/deploy-local.sh backend    # build + deploy backend only
./scripts/deploy-local.sh --no-build all  # deploy only (skip build)

# Clean deploy (wipes EVERYTHING — fresh install with setup wizard)
./scripts/deploy-local.sh --clean    # requires 'yes' confirmation
```
The script builds self-contained win-x64 for backend and webpack for frontend,
stops Radarr if running, replaces `bin/` with fresh build output, deploys UI,
and offers to restart. Data files (config.xml, radarr.db, logs) live in
`D:\Apps\Radarr\` (outside `bin/`) and are preserved across upgrade deploys.

**Important**: The exe must be launched with `--data="D:\Apps\Radarr"` (Windows-style
path via `cygpath -w`) to use the existing config/database. Without it, Radarr
defaults to `C:\ProgramData\Radarr` and port 7878.

**Always use the tray app** (`Radarr.exe`), not the console app (`Radarr.Console.exe`).
The tray app puts an icon in the system tray for easy access and closing — no need
to remember the port. The deploy script launches the tray app by default.

A Windows shortcut is set up to launch the tray app with the correct data path:
```
D:\Apps\Radarr\bin\Radarr.exe --data="D:\Apps\Radarr"
```

## Linting (required before committing frontend changes)
```bash
yarn lint --fix
yarn stylelint-windows --fix   # for CSS changes
```

## Testing
- NUnit for unit/integration/automation tests
- Test projects mirror source: `NzbDrone.Core.Test/` → `NzbDrone.Core/`
- Run via VS Test Explorer or `./test.sh <PLATFORM> <TYPE> <COVERAGE>`
- 80% coverage required on new code

## Localization
- Source strings: `src/NzbDrone.Core/Localization/Core/en.json`
- Backend: `_localizationService.GetLocalizedString("KeyName")`
- Frontend: `import translate from 'Utilities/String/translate'` → `translate('KeyName')`

## Working style

### Keep diffs focused
- One logical change per commit
- Avoid unrelated reformatting

### Planning sessions → write a spec
Whenever we do a planning session (plan mode), always write the finalised specification into `docs/` as a named document. This ensures we have a durable reference if context is lost or the session is interrupted.

### Update docs before committing
Before committing, check if `README.md` and `CLAUDE.md` need updating to reflect the changes (new features, architectural changes, etc.) and check whether `docs/TODO.md` items can be ticked off.

### Compile/test locally after changes
1. Make a small, targeted change
2. Run tests/linting after each change
3. Only then commit/push

### Documentation or commentary
Never use real movie or tv show names. Always make up example ones.


## Code Reviews
See `docs/Spec-CodeReview.md` for the full checklist, deliverable format, and process.

## Git & Upstream

### Repository status
This is a **standalone repository** (detached from the GitHub fork of Radarr/Radarr). The `upstream` git remote is retained for cherry-picking useful commits. PRs are not sent upstream.

### Remotes
- `origin` → `The-Ant-Forge/Radarr` (standalone)
- `upstream` → `Radarr/Radarr` (reference for cherry-picks)

### Incorporating upstream changes
Cherry-pick individual commits rather than merging/rebasing, as the histories have diverged significantly.

```bash
git fetch upstream
git log develop..upstream/develop --oneline   # review new commits
git cherry-pick <sha>                         # pick what we need
```

Conflict hotspots: validators, DiskScanService, RefreshMovieService, frontend Settings/MediaManagement.

### Feature strategy
Custom features (PlaceInRootFolder, RefreshMonitoredOnly, UnmonitorOnCutoffMet) are
implemented as config toggles disabled by default. UpdateMechanism is set to External
in production to prevent upstream auto-updates from overwriting our changes.

## Releases
Before doing a release check that all primary documents are updated and current with respect to the changes made. This includes `docs/TODO.md` and `README.md`. Then commit and push to capture those changes in the remote before starting the normal release procedure.

All releases should have a thorough description in markdown format. Descriptions should start with an intro paragraph giving a broad summary of changes, improvements and fixes then list in order:
1. New Features: What they are, how they work and what benefit they bring
2. Code improvements: What changes to existing feature or code was made and why.
3. Bug fixes: What bugs were fixed and how
4. Anything else we want to say about this release


### Versioning convention

Tags follow the pattern `v{upstream}-antforge.{N}` where `N` is the total
number of fork commits (counted from the first fork commit via `git log --oneline <first-fork-sha>^..HEAD | wc -l`):

| Tag | Meaning |
|---|---|
| `v6.1.1.10360-antforge.41` | 41 commits on top of upstream v6.1.1.10360 |
| `v6.1.1.10360-antforge.55` | 55 commits, still rooted in v6.1.1.10360 |
| `v6.1.2.10380-antforge.60` | 60 commits, after cherry-picking from upstream v6.1.2.10380 |

The upstream version part reflects which source base we are aligned with.
When we cherry-pick from a new upstream release, bump the upstream part.