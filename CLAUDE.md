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
- Source strings: `src/NzbDrone.Core/Localization/en.json`
- Backend: `_localizationService.GetLocalizedString("KeyName")`
- Frontend: `import translate from 'Utilities/String/translate'` → `translate('KeyName')`

## Working style

### Keep diffs focused
- One logical change per commit
- Avoid unrelated reformatting

### Planning sessions → write a spec
Whenever we do a planning session (plan mode), always write the finalised specification into `docs/` as a named document. This ensures we have a durable reference if context is lost or the session is interrupted.

### Update docs before committing
Before committing, check if `docs/Parrot spec.md`, `README.md` and `CLAUDE.md` need updating to reflect the changes (new sites, new features, architectural changes, etc.) and check whether a `docs/TODO.md` can be checked. If an entire TODO section is completed then move the section to Completed.md in the same folder.

### Compile/test locally after changes
1. Make a small, targeted change
2. Run tests/linting after each change
3. Only then commit/push

### Documentation or commentary
Never use real movie or tv show names. Always make up example ones.


## Code Review Phases

Periodically we do a consolidation review covering all source, tests, build config, and metadata.

### Review Checklist

**Guiding question:** *"Where does the codebase assume a specific data topology,
ownership model, or storage layout — and what breaks when those assumptions
don't hold?"*

**1. Invariants & Boundaries**
1. **Domain invariants** — what assumptions about identity, ownership,
   uniqueness, and path topology exist? Are they valid in every supported
   mode (e.g., per-folder vs shared-root)?
2. **Cross-feature & configuration invariants** — when a setting changes a
   fundamental assumption, do all dependent code paths respect it? Identify
   config flags that alter domain invariants, then trace all read/write/scan/
   delete paths affected
3. **Ownership boundaries** — does every destructive or mutating action
   operate only on resources the system can prove belong to the target
   entity? What does a record/job/path "own" vs what is merely adjacent?
4. **API & serialization contracts** — do endpoints accept all valid inputs
   and reject all invalid ones under every valid data topology? Check
   validators, serialization, and error responses
5. **Persistence & migration integrity** — schema consistency, orphaned
   records, config file handling across upgrades, backward compatibility
   with existing databases and stored paths

**2. Workflow Correctness**
6. **Lifecycle transitions** — do end-to-end flows remain correct across
   import, scan, rename/move, delete, upgrade, metadata refresh, and config
   changes on existing libraries?
7. **Concurrency & idempotency** — are background jobs, retries, and
   duplicate events safe? Can two scans run at once? What happens during
   scan + delete? Are partially completed actions recoverable?
8. **Filesystem semantics** — path normalization, case sensitivity,
   symlinks/hardlinks, UNC/network shares, long paths, cross-volume moves,
   permissions/ACL failures

**3. Safety & Resilience**
9. **Destructive action safeguards** — are dangerous operations (delete,
   overwrite, bulk modify) guarded with confirmation, scope checks,
   ownership proof, and feature-flag awareness?
10. **Security** — input validation, credential handling, injection vectors
    (SQL, command, XSS), auth gaps, secret exposure in logs or API responses
11. **Error handling & recovery** — swallowed exceptions, missing user-facing
    messages, inconsistent patterns, error states that leave the system
    broken or data corrupted

**4. Performance & Scale**
12. **Scan & enumeration efficiency** — file system walks, database queries,
    and API calls that scale with collection size; deduplication of repeated
    work; complexity blowups from shared roots
13. **Resource management** — leaks, missing disposal, unbounded collections,
    connection pool exhaustion

**5. UX & Operability**
14. **UI/UX coherence** — labels, tooltips, and help text that are ambiguous
    or misleading; features visible when they shouldn't be; missing feedback
    for user actions
15. **Observability** — if a scan, move, or delete goes wrong, do logs and
    UI state make root cause and scope obvious? Are dangerous operations
    auditable? Are logs structured, actionable, and non-spammy?

**6. Maintainability**
16. **Dead code & dependencies** — unused functions, classes, modules,
    imports, config keys; unused or obsolete/high-risk dependencies
17. **Duplication** — repeated or near-identical logic that should be shared
18. **Static safety** — missing type annotations, `Any`/`object` overuse,
    unsafe casts, nullability gaps, DTO/domain mismatches (C# and TypeScript)
19. **Naming & consistency** — mixed conventions, unclear names, stale
    comments, code style drift
20. **Test gaps** — untested code paths, stale tests; prioritize matrix
    coverage across config x workflow x topology
21. **TODO/FIXME/HACK audit** — resolve or remove stale markers

**7. Sustainability**
22. **Documentation drift** — specs, READMEs, inline docs that no longer
    match the code
23. **Build & deploy integrity** — build configurations, deploy scripts,
    output structure; does a partial deploy leave the system broken?
24. **Upstream divergence** — changes that conflict with upstream, patches
    that need re-applying after merges, features that upstream might break

### Deliverable
A review document in `docs/Code-Review-YYMMDD.md` (or similar) with:
- Summary table: Category, Description, Action, Impact, Effort, Risk
- Detailed findings grouped by category, ordered by impact then effort
- Out-of-scope items noted for `docs/TODO.md`
- Transformation Document - The deliverable shoudl specificy that during the execution of recommended items any architecture changes are captured in `docs/Transformation-YYMMD.md` for future refactors of te upstream code base.

### Process
1. Produce the review document — do NOT implement during review
2. Review and approve findings with the user
3. Implement approved items in focused commits
4. Re-run tests after each change

## Releases
Before doing a release check that all primary document are updated and current with respect to what you know of the changes made. This includes TODO.md, completed.md, parrot spec.md and readme.md (in the root). Then do a commit and push to capture those changes int he remote before starting the normal release procedure.

All releases should have a thorough description in markdown format. Descriptions should start with an intro paragraph giving a broad summary of changes, improvements and fixes then list in order:
1. New Features: What they are, how they work and what benefit they bring
2. Code improvements: What changes to existing feature or code was made and why.
3. Bug fixes: What bugs were fixed and how
4. Anything else we want to say about this release
