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
# Backend (CLI)
dotnet clean src/Radarr.sln -c Debug
dotnet msbuild -restore src/Radarr.sln -p:Configuration=Debug -p:Platform=Posix -t:PublishAllRids
# Run from _output/

# Backend (VS): set startup project to Radarr.Console, framework net6.0

# Frontend
cd frontend && yarn install
yarn start        # dev server with hot reload
yarn build        # production build
```
App runs at http://localhost:7878

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
1. **Dead code** — unused functions, classes, modules, imports, config keys
2. **Dead dependencies** — libraries that are unused or underused relative
   to what we could replace inline
3. **Duplication** — repeated or near-identical logic that should be shared
4. **Naming & consistency** — mixed conventions, unclear names, stale comments
5. **Error handling** — inconsistent patterns, swallowed exceptions, missing
   user-facing messages
6. **Security** — input validation gaps, credential handling, OWASP patterns
7. **Type safety** — missing annotations, `Any` overuse, type errors
8. **Test gaps** — untested code paths, stale tests, missing edge cases
9. **Documentation drift** — specs, docstrings, or README sections that no
   longer match the code
10. **Performance** — unnecessary work, avoidable allocations, slow patterns
11. **Robustness** — race conditions, resource leaks, missing cleanup
12. **TODO/FIXME/HACK audit** — resolve or remove stale markers

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