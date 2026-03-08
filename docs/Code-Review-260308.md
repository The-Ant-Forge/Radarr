# Code Review — 2026-03-08

Scope: full codebase sweep covering `frontend/src/`, `src/NzbDrone.Core/`, `src/NzbDrone.Common/`, `src/Radarr.Api.V3/`, and `src/Radarr.Http/`.

> **Process note** — per CLAUDE.md, this document is the review deliverable only.
> No changes should be implemented until findings are reviewed and approved.
> Architecture changes adopted during implementation must be captured in `docs/Transformation-260308.md`.

> **Peer review** — This document was independently reviewed by OpenAI Codex (GPT-5.3).
> Corrections, new findings, and priority adjustments from that review are incorporated below
> and marked with *(Codex)* where they differ from the original analysis.

---

## 1. Summary Table

| # | Category | Description | Action | Impact | Effort | Risk |
|---|----------|-------------|--------|--------|--------|------|
| 1 | Dead dependencies | `react-addons-shallow-compare` unused in code | Remove from package.json | Low | Trivial | None |
| 2 | Dead dependencies | `react-async-script` unused in code | Remove from package.json | Low | Trivial | None |
| 3 | Dead code | `PathsDefaultStatic` in `MediaManagementConfigResource.cs` — orphaned, not mapped | Remove property | Low | Trivial | None |
| 4 | Dead code | Deprecated `SetMovieFile` endpoint in `MovieFileController.cs:108` | ~~Remove~~ Keep until v6 deprecation window *(Codex)* | Low | Low | Medium |
| 5 | Dead code | Deprecated `GetImportListExclusions()` in `ImportListExclusionController.cs:35` | ~~Remove~~ Keep until v6 deprecation window *(Codex)* | Low | Low | Medium |
| 6 | Dead code | Deprecated metadata providers: Kometa, MediaBrowser | Schedule removal for v6 | Low | Medium | Low |
| 7 | Dead code | `MatchesFolderSpecification.cs:31` — "TODO: Actually implement this!!!!" | Implement or remove the spec | Medium | Medium | Medium |
| 8 | Type safety | `urlParams.js` is plain JS in a TypeScript codebase | Convert to `.ts` | Medium | Low | None |
| 9 | Type safety | `createAjaxRequest.js` is plain JS | Convert to `.ts` with `AugmentedPromise<T>` type | Medium | Medium | Low |
| 10 | Naming & consistency | `SlackExeption.cs` — typo in filename | Rename to `SlackException.cs` | Low | Trivial | None |
| 11 | Naming & consistency | `?? ""` vs `?? string.Empty` mixed across notifications | ~~Standardise~~ Low-value churn, deprioritise *(Codex)* | Low | Low | None |
| 12 | Error handling | `CalendarFeedController` tag lookup throws on unknown tag name | Catch and skip unknown tags | Medium | Low | Low |
| 13 | Error handling | `CalendarFeedController.CreateEvent` — `Overview`/`Studio` may be null | Default to `string.Empty` | Low | Trivial | None |
| 14 | Error handling | Inconsistent `xhr.aborted` checking across action creators | Standardise all handlers to check `xhr.aborted` | Medium | Low | None |
| 15 | Error handling | `MediaBrowserProxy` — "TODO: actually check for the error" | Implement error response validation | Medium | Low | Low |
| 16 | Security | `IsInCidrRange` — no CIDR prefix length bounds check | Add 0–32 / 0–128 validation | **High** *(Codex: upgraded)* | Trivial | None |
| 17 | Security | `MovieRepository.cs:288` — SQL string interpolation for WHERE clause | Use parameterised queries | **Medium** *(Codex: downgraded — values are numeric/internal)* | Low | Low |
| 18 | Security | `MoviePathBuilder` — no path traversal validation | ~~Validate with Path.GetFullPath()~~ Low priority — names are sanitised and validated in add/update flows *(Codex)* | Low | Medium | Low |
| 19 | Security | `TorrentFileInfoReader.cs:29` — logs raw file contents on parse failure | Log filename only, not content | Medium | Low | None |
| 20 | Security | Missing input validation on `MovieController.AllMovie` — `languageId` not bounds-checked | Add validation guard | Low | Trivial | None |
| 21 | Robustness | OAuth global `window.onCompleteOauth` callback — concurrent flows collide | Use unique callback per flow | Medium | Medium | Low |
| 22 | Robustness | ~~Movie path collision — identical title/year produces same path~~ | ~~Detect and disambiguate~~ Already handled by `MoviePathValidator` *(Codex)* | ~~High~~ N/A | — | — |
| 23 | Robustness | `CommandQueue.All()` returns internal list reference | Return `new List<>(_items)` copy | Medium | Trivial | None |
| 24 | Robustness | ~~`MovieFileMovingService` TOCTOU — folder exists check then create~~ | ~~Wrap in try/catch~~ `CreateFolder` already handles `IOException` *(Codex)* | ~~Medium~~ N/A | — | — |
| 25 | Robustness | ~~`EventAggregator` lock scope too narrow~~ | ~~Widen lock scope~~ Handlers are built once as read-only arrays; no invalidation path *(Codex)* | ~~Medium~~ N/A | — | — |
| 26 | Performance | `MovieController.cs:147` — `GetAwaiter().GetResult()` blocks async in web handler | Convert to async/await | **Medium** *(Codex: downgraded unless perf data confirms pressure)* | Low | Low |
| 27 | Performance | `CustomFormatService.cs:84-96` — N+1 query: `Get(id)` in loop | Batch load then delete | Medium | Low | None |
| 28 | Performance | Multiple services use `.All().Where()` — full table scan then filter | Push filter to repository query | Medium | Medium | Low |
| 29 | Performance | `RssSyncService.cs:62`, `LocalizationService.cs:131` — blocking async | Convert to async/await | Medium | Low | Low |
| 30 | Duplication | 8 action creator handlers repeat identical request/dispatch/error pattern | Extract shared handler wrapper — deprioritise behind correctness *(Codex)* | High | Medium | Low |
| 31 | Duplication | Discord/Slack/Telegram notifications duplicate embed-building logic | ~~Extract NotificationFieldBuilder~~ Defer — large refactor with regression risk *(Codex)* | High | High | Medium |
| 32 | Duplication | Discord.cs icon URL hardcoded 8 times | Extract to constant | Low | Trivial | None |
| 33 | Duplication | Discord.cs string truncation logic repeated 3+ times | Extract `TruncateString(str, max)` utility | Low | Trivial | None |
| 34 | Duplication | `BytesToString()` in Discord.cs duplicates frontend `formatBytes` | ~~Move to Common~~ Weak ROI — code can't be shared across runtimes *(Codex)* | Low | Low | None |
| 35 | Duplication | `HostConfigResource` mapper mixes `IConfigFileProvider` + `IConfigService` data | Split mappers or create composite service | Medium | Medium | Low |
| 36 | Test gaps | `createAjaxRequest` — zero tests for fetch wrapper | Add unit tests with fetch mock | High | Medium | None |
| 37 | Test gaps | `CalendarFeedController` — no tests | Add integration tests | Medium | Medium | None |
| 38 | Test gaps | `MoviePathBuilder` / `PlaceInRootFolder` — ~17% coverage | Add edge case tests | High | Medium | None |
| 39 | TODO/FIXME audit | `ConfigService.cs:248` — stale TODO | Resolve or remove | Low | Trivial | None |
| 40 | TODO/FIXME audit | `MovieResource.cs:26-28`, `AlternativeTitleResource.cs:10-12` — identical TODOs duplicated | Consolidate to one location or remove | Low | Trivial | None |
| 41 | TODO/FIXME audit | `TransmissionSettings.cs:40` — "Remove this in v6" | Track in docs/TODO.md for v6 | Low | Trivial | None |
| 42 | TODO/FIXME audit | `AlternativeTitleService.cs:122`, `MovieTranslationService.cs:100` — "handle metadata delete" | Implement or document as known debt | Medium | Medium | Medium |
| 43 | Documentation drift | `CONTRIBUTING.md` says .NET 6 — project targets .NET 8 | Update to .NET 8 | Low | Trivial | None |
| 44 | Performance | Magic numbers in Discord.cs (300 overview limit, 256 title limit, `Take(5)`) | Extract to named constants — deprioritise *(Codex)* | Low | Low | None |
| 45 | Robustness | *(Codex)* `CommandQueue.Count` reads `_items.Count` without lock — thread-safety bug | Add lock or use `Interlocked` | Medium | Trivial | None |
| 46 | Robustness | *(Codex)* OAuth popup hang — no timeout/close-detection if user closes popup before callback | Add `window.closed` polling or timeout | Medium | Low | Low |
| 47 | Security | *(Codex)* OAuth query parsing missing `decodeURIComponent` — encoded tokens corrupted | Add URL-decoding to `oAuthActions.js:74` param parsing | Medium | Trivial | None |
| 48 | Security | *(Codex)* Calendar feed `pastDays`/`futureDays` unbounded — enables expensive query ranges | Add reasonable upper bounds (e.g. 365) | Medium | Trivial | None |

---

## 2. Detailed Findings

### Dead Dependencies

**#1 — `react-addons-shallow-compare`**
Listed in `package.json` but never imported anywhere. Deprecated by React — functionality moved to `React.PureComponent` / `React.memo`.
- **Action**: Remove from `package.json`.

**#2 — `react-async-script`**
Listed in `package.json` but no imports found. Leftover from a removed feature.
- **Action**: Remove from `package.json`.

### Dead Code

**#3 — `PathsDefaultStatic` in `MediaManagementConfigResource.cs`**
Property exists on the resource but is not mapped from `IConfigService` or used in any API response serialisation.
- **Action**: Remove the property. Verify no frontend code reads it.

**#4 — Deprecated `SetMovieFile` endpoint**
[MovieFileController.cs:108](src/Radarr.Api.V3/MovieFiles/MovieFileController.cs#L108) — marked `[Obsolete("Use bulk endpoint instead")]`. The bulk endpoint already exists.
- **Action**: *(Codex)* Keep through a major-version deprecation window to avoid breaking API consumers. Schedule removal for v6.

**#5 — Deprecated `GetImportListExclusions()`**
[ImportListExclusionController.cs:35](src/Radarr.Api.V3/ImportLists/ImportListExclusionController.cs#L35) — marked `[Obsolete("Deprecated")]`. The paged endpoint at line 48 replaces it.
- **Action**: *(Codex)* Same as #4 — keep until v6 deprecation window.

**#6 — Deprecated metadata providers**
`KometaMetadata` (settings: `Deprecated = true`) and `MediaBrowserMetadata` (marked deprecated for v6). Both are fully deprecated with warnings shown to users.
- **Action**: Schedule removal for v6 release. Add to `docs/TODO.md`.

**#7 — Unimplemented `MatchesFolderSpecification`**
[MatchesFolderSpecification.cs:31](src/NzbDrone.Core/MediaFiles/MovieImport/Specifications/MatchesFolderSpecification.cs#L31) — contains `// TODO: Actually implement this!!!!`. A core import specification that currently does nothing.
- **Action**: Either implement the folder matching logic or remove the specification entirely if it's not needed.

### Type Safety

**#8 — `urlParams.js` should be TypeScript**
All other utility files in `frontend/src/Utilities/` are `.ts`. Straightforward to type.
- **Action**: Rename to `.ts`, add `(obj: Record<string, unknown>): string` signature.

**#9 — `createAjaxRequest.js` should be TypeScript**
Core AJAX layer, used by 40+ files. The `augmentPromise` wrapper adds non-standard methods to a native Promise. Without types, callers have no autocomplete and TS can't catch misuse.
- **Action**: Convert to `.ts`. Define `AugmentedPromise<T>` interface extending `Promise<T>` with `.done()`, `.fail()`, `.always()`, `.promise()`.

### Naming & Consistency

**#10 — `SlackExeption.cs` typo**
[SlackExeption.cs](src/NzbDrone.Core/Notifications/Slack/SlackExeption.cs) — filename misspells "Exception".
- **Action**: Rename to `SlackException.cs`, update class name and all references.

**#11 — Mixed empty string patterns**
Discord.cs uses `?? ""` while CustomScript.cs uses `?? string.Empty`. Both appear across notifications.
- **Action**: *(Codex)* Low-value churn. Deprioritise unless touching these files for another reason.

### Error Handling

**#12 — Tag lookup in `CalendarFeedController`**
[CalendarFeedController.cs:37](src/Radarr.Api.V3/Calendar/CalendarFeedController.cs#L37):
```csharp
parsedTags.AddRange(tags.Split(',').Select(_tagService.GetTag).Select(t => t.Id));
```
If a user passes a non-existent tag name, `GetTag` throws → 500 on the iCal feed.
- **Action**: Catch and skip unknown tags with a log warning.

**#13 — Null fields in calendar events**
[CalendarFeedController.cs:109-110](src/Radarr.Api.V3/Calendar/CalendarFeedController.cs#L109-L110) — `Overview` and `Studio` can be null for newly-added movies with incomplete metadata.
- **Action**: Default to `string.Empty`.

**#14 — Inconsistent `xhr.aborted` checking**
Some action creators check `xhr.aborted` to suppress error state on cancellation (e.g. `createFetchHandler.js:38`, `createTestProviderHandler.js:66`), while others don't (`createBulkEditItemHandler.js:42`, `createRemoveItemHandler.js:34`, `createSaveHandler.js:33`). Users see spurious error toasts when requests are cancelled.
- **Action**: Standardise all handlers to check `xhr.aborted` and set error to null when true.

**#15 — `MediaBrowserProxy` skips error checking**
Contains comment "TODO: actually check for the error" — error responses from the Emby/Jellyfin API are silently ignored.
- **Action**: Parse response and throw on error status.

### Security

**#16 — CIDR prefix bounds check** *(Codex: upgraded to High)*
The inline `IsInCidrRange` method parses prefix length from user-supplied proxy bypass config but doesn't validate the range. A prefix of `-1` or `999` would cause incorrect bit-shift behaviour or runtime exceptions.
- **Action**: Clamp to `0–32` for IPv4, `0–128` for IPv6, or reject with a warning log.

**#17 — SQL string interpolation** *(Codex: downgraded to Medium)*
[MovieRepository.cs:288](src/NzbDrone.Core/Movies/MovieRepository.cs#L288):
```csharp
clauses.Add(string.Format($"(\"QualityProfileId\" = {profile.ProfileId} AND ...  {belowCutoff} ...)"));
```
Values are integers from internal profile objects — not raw user input. Still a hygiene issue for defence-in-depth, but not an immediate injection risk.
- **Action**: Use parameterised queries when convenient. Lower priority than originally assessed.

**#18 — Path traversal in `MoviePathBuilder`** *(Codex: downgraded to Low)*
[MoviePathBuilder.cs](src/NzbDrone.Core/Movies/MoviePathBuilder.cs) — `Path.Combine()` with user-controllable paths. However, Codex verified that folder names are generated via naming/sanitisation and paths are validated through `MoviePathValidator` in add/update flows.
- **Action**: Low priority. Add `GetFullPath()` validation only if path building is refactored for other reasons.

**#19 — Raw content logging in `TorrentFileInfoReader`**
[TorrentFileInfoReader.cs:29](src/NzbDrone.Core/MediaFiles/TorrentInfo/TorrentFileInfoReader.cs#L29) — logs raw file contents as ASCII on parse failure. Could expose sensitive data.
- **Action**: Log filename/size only, not content.

**#20 — Missing input validation on `MovieController.AllMovie`**
[MovieController.cs:117](src/Radarr.Api.V3/Movies/MovieController.cs#L117) — `languageId` parameter hits `Language.All.Single(l => l.Id == languageId.Value)` which throws `InvalidOperationException` if not found.
- **Action**: Use `FirstOrDefault` with 400 response on invalid ID.

**#47 — OAuth query parsing missing URL-decoding** *(Codex: new finding)*
[oAuthActions.js:74](frontend/src/Store/Actions/oAuthActions.js#L74) — splits query params on `=` and `&` but never calls `decodeURIComponent()`. OAuth tokens containing encoded characters (e.g. `+`, `%3D`) will be corrupted.
- **Action**: Add `decodeURIComponent()` to both key and value in the parsing loop.

**#48 — Calendar feed unbounded date range** *(Codex: new finding)*
[CalendarFeedController.cs:27](src/Radarr.Api.V3/Calendar/CalendarFeedController.cs#L27) — `pastDays` and `futureDays` query parameters have no upper bound. A request with `futureDays=999999` would generate an expensive query and huge iCal response.
- **Action**: Clamp to a reasonable maximum (e.g. 365 days each).

### Robustness

**#21 — OAuth global callback collision**
[oAuthActions.js:70](frontend/src/Store/Actions/oAuthActions.js#L70) — `window.onCompleteOauth` is overwritten if two OAuth flows run concurrently. First flow silently hangs.
- **Action**: Use unique callback name per flow (e.g. `onCompleteOauth_${nonce}`).

**#22 — ~~Movie path collision~~** *(Codex: invalidated)*
~~`MoviePathBuilder.BuildPath` constructs `{RootFolder}/{Title} ({Year})`. Two movies with identical title and year get the same path.~~
Codex verified that `AddMovieValidator` already runs `MoviePathValidator` which checks for unique paths before insert. This is already handled.
- **Action**: None required. Removed from active findings.

**#23 — `CommandQueue.All()` returns internal reference**
[CommandQueue.cs:48-57](src/NzbDrone.Core/Messaging/Commands/CommandQueue.cs#L48-L57) — returns `_items` directly. Callers can modify the internal list outside the lock.
- **Action**: Return `new List<CommandModel>(_items)`.

**#24 — ~~TOCTOU in `MovieFileMovingService`~~** *(Codex: invalidated)*
~~Checks `FolderExists()` then calls `CreateFolder()`.~~
Codex verified that `CreateFolder` already handles `IOException` internally and continues. The TOCTOU is a non-issue.
- **Action**: None required. Removed from active findings.

**#25 — ~~`EventAggregator` lock scope too narrow~~** *(Codex: invalidated)*
~~Subscribers accessed outside lock after lookup.~~
Codex verified that handlers are built once as read-only arrays with no cache invalidation path. The access outside lock is safe as-is.
- **Action**: None required. Removed from active findings.

**#45 — `CommandQueue.Count` unsynchronised** *(Codex: new finding)*
[CommandQueue.cs:19](src/NzbDrone.Core/Messaging/Commands/CommandQueue.cs#L19) — `public int Count => _items.Count;` reads shared state without acquiring `_mutex`. While `List<T>.Count` is a simple property, concurrent modifications could produce stale or torn reads.
- **Action**: Wrap in lock or use `Volatile.Read` pattern.

**#46 — OAuth popup hang on close** *(Codex: new finding)*
[oAuthActions.js:69](frontend/src/Store/Actions/oAuthActions.js#L69) — if the user closes the OAuth popup window before the callback fires, the promise hangs forever with no timeout or `window.closed` detection.
- **Action**: Add a polling interval that checks `popup.closed` and rejects the promise with a user-friendly error after detecting close.

### Performance

**#26 — Blocking async in `MovieController`** *(Codex: downgraded to Medium)*
[MovieController.cs:147](src/Radarr.Api.V3/Movies/MovieController.cs#L147) — `movieTask.GetAwaiter().GetResult()` blocks a thread-pool thread. Also seen in `RssSyncService.cs:62` and `LocalizationService.cs:131`.
- **Action**: Convert to proper async/await. *(Codex notes: lower priority unless perf profiling confirms thread-pool pressure. Blocking async is pervasive in this codebase — HttpClient.cs alone has 7 instances.)*

**#27 — N+1 query in `CustomFormatService`**
[CustomFormatService.cs:84-96](src/NzbDrone.Core/CustomFormats/CustomFormatService.cs#L84-L96) — `Delete(List<int>)` calls `_formatRepository.Get(id)` per ID in a loop.
- **Action**: Batch load all IDs, then loop through results.

**#28 — `.All().Where()` full table scans**
Multiple services load entire tables then filter in memory:
- `ReleaseProfileService.cs:40-45`
- `PendingReleaseService.cs:149`
- `ProviderStatusServiceBase.cs:45`
- `IndexerDownloadClientCheck.cs:30-32`
- **Action**: Push WHERE clause to repository queries where feasible.

**#29 — Blocking async in sync services**
`RssSyncService.cs:62` and `MoviesSearchService.cs:48,68,88` use `.GetAwaiter().GetResult()`.
- **Action**: Convert to async where the call chain supports it.

### Duplication

**#30 — Action creator handler duplication**
Eight handler creators in `frontend/src/Store/Actions/Creators/` repeat the same pattern: dispatch loading state → create AJAX request → on success dispatch batch → on fail dispatch error. ~40% code reduction possible.
- **Action**: Extract shared `createRequestHandler(section, ajaxOptions, { onSuccess, onFail })` wrapper. *(Codex: deprioritise behind correctness/security fixes.)*

**#31 — Notification provider embed duplication**
Discord.cs (~550 lines), Slack.cs (~143 lines), Telegram.cs (~110 lines) all build similar message payloads with duplicate field extraction, truncation, and formatting logic.
- **Action**: *(Codex)* Defer to a future sprint. Large cross-provider refactor with regression risk — not ideal for near-term cleanup.

**#32 — Discord icon URL repeated 8 times**
`"https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"` hardcoded throughout Discord.cs.
- **Action**: Extract to `private const string RadarrLogoUrl`.

**#33 — String truncation logic repeated**
`overview.Length <= 300 ? overview : $"{overview.AsSpan(0, 300)}..."` appears 3+ times in Discord.cs. Title truncation at 256 chars also repeated.
- **Action**: Extract `TruncateString(string input, int maxLength)` utility.

**#34 — `BytesToString()` duplicates frontend `formatBytes`** *(Codex: weak ROI)*
Discord.cs has its own byte formatting method. The same logic exists in `frontend/src/Utilities/Number/formatBytes.ts`.
- **Action**: *(Codex)* Weak ROI since C# and JS code cannot literally be shared. Keep as-is, or extract to `NzbDrone.Common` only if touching Discord.cs for another reason.

**#35 — `HostConfigResource` mapper mixes concerns**
[HostConfigResource.cs](src/Radarr.Api.V3/Config/HostConfigResource.cs#L51-L96) — mapper pulls from both `IConfigFileProvider` and `IConfigService` in a single method. Comments out `Username`/`Password` fields.
- **Action**: Split into separate mappers or create composite service. Note for `docs/Transformation-260308.md`.

### Test Gaps

**#36 — No tests for `createAjaxRequest`**
The fetch wrapper is the single most critical piece of frontend infrastructure — every API call flows through it. Zero test coverage.
- **Action**: Add tests covering: success JSON, non-JSON, HTTP error with/without JSON body, abort/cancel, network failure.

**#37 — No tests for `CalendarFeedController`**
Tag parsing, date filtering, event creation all untested.
- **Action**: Add integration tests.

**#38 — Low coverage on `MoviePathBuilder` / `PlaceInRootFolder`**
Path building is critical (wrong path = files in wrong location). ~17% coverage.
- **Action**: Add tests for special chars, long paths, duplicate title/year, missing year, non-ASCII.

### TODO/FIXME Audit

**#39 — Stale TODO in `ConfigService.cs:248`**
- **Action**: Resolve or remove.

**#40 — Duplicated TODOs in resource classes**
`MovieResource.cs:26-28` and `AlternativeTitleResource.cs:10-12` contain identical TODO comments about sorters and profiles.
- **Action**: Consolidate to one location, or remove if no longer relevant.

**#41 — "Remove this in v6" markers**
`TransmissionSettings.cs:40`, `Sabnzbd.cs:559` — legacy compatibility code scheduled for v6.
- **Action**: Track in `docs/TODO.md` under v6 milestone.

**#42 — Incomplete metadata delete refactor**
`AlternativeTitleService.cs:122` and `MovieTranslationService.cs:100` both say "TODO handle metadata delete instead of movie delete".
- **Action**: Implement properly or document as known debt with a tracking issue.

### Documentation Drift

**#43 — CONTRIBUTING.md references .NET 6**
Project targets `net8.0` but `CONTRIBUTING.md` still says .NET 6.
- **Action**: Update to .NET 8.

### Magic Numbers

**#44 — Hardcoded values in Discord.cs**
Overview limit (300), title limit (256/253), genre limit (`Take(5)`), tag limit (`Take(5)`), timestamp format string — all repeated without named constants.
- **Action**: Extract to named constants at the top of the class. *(Codex: deprioritise behind correctness.)*

---

## 3. Out-of-Scope Items (for `docs/TODO.md`)

- Full TODO/FIXME triage across the ~1003 markers in the codebase
- Pre-existing SA1200 StyleCop errors in `NzbDrone.Common/TPL/` (~492 warnings)
- jQuery full removal (kept intentionally for plugin/extension extensibility)
- Swashbuckle upgrade to v10 (blocked until .NET 10 migration)
- .NET 10 migration (scheduled for after .NET 10 GA, November 2026)
- Deprecated metadata provider removal (Kometa, MediaBrowser) — v6 milestone
- Deprecated API endpoint removal (#4, #5) — v6 deprecation window *(Codex)*
- v6 legacy code removal (Sabnzbd compat, Transmission settings)
- `HostConfigResource` mapper separation (#35) — architectural change
- Notification provider base class refactor (#31) — large scope, deferred *(Codex)*
- Full `.All().Where()` audit across all repositories (#28)
- Rate limiting / request size validation on API endpoints
- HttpClient cache lifecycle management in `ManagedHttpDispatcher`
- Pervasive blocking-async cleanup (HttpClient.cs has 7 instances alone)

---

## 4. Transformation Document

Any architecture changes made during implementation of approved items should be captured in `docs/Transformation-260308.md` for future upstream reconciliation.

---

## 5. Prioritised Implementation Order

Ordered by: ease of implementation (trivial → hard), grouped so related changes are adjacent, dependencies respected. Each item can be a single focused commit.

### Batch A — One-liner fixes ✅ DONE

All 10 items implemented and verified with `dotnet build` + `yarn build`.

### Batch B — Small targeted fixes ✅ DONE

All 9 items implemented (including Discord.cs constants extraction) and verified.

### Batch C — Frontend correctness ✅ DONE

`xhr.aborted` standardised across 6 Creator handlers, OAuth popup close-detection added, `urlParams.js` converted to TypeScript.

### Batch D — Backend correctness ✅ DONE

SQL parameterised in `MovieRepository`, `MediaBrowserProxy.CheckForError` cleaned up, `CustomFormatService.Delete(List)` batch-loads with `DeleteMany`.

### Batch E — TypeScript conversion ✅ DONE

`createAjaxRequest.js` → `.ts` with exported `AugmentedPromise<T>`, `AjaxOptions`, `XhrErrorObject`, `AjaxRequestResult<T>` interfaces. Also removed unused `using System.Text` from `TorrentFileInfoReader.cs`.

### Batch F — Test coverage ✅ DONE

- **#36**: SKIP — no frontend test framework exists (no jest/vitest/mocha installed)
- **#38**: 4 new edge-case tests added to `MovieFolderPathBuilderFixture.cs` (null/empty root folder, non-parent path fallback, empty path with useExisting)
- **#37**: 9 new unit tests in `CalendarFeedControllerFixture.cs` (content type, events, bounds clamping, negative days, unknown tags, null fields, no-date skip, release type filter)

### Batch G — Medium-effort improvements ✅ DONE

- **#26**: `MovieController.AllMovie` converted to `async Task<>`, `GetAwaiter().GetResult()` replaced with `await`
- **#29**: SKIP — `IExecute<T>` interface only supports `void Execute()`; converting requires command pipeline infrastructure changes (deferred)
- **#21**: OAuth callbacks now use per-flow nonce via `window._oauthCallbacks[nonce]`; `oauth.html` updated to look up callback by `window.name`; backwards-compatible fallback retained
- **#30**: SKIP — critical abort-null-check fix already done in Batch B; remaining structural extraction is low ROI with regression risk across 8 core Redux files
- **#7**: `MatchesFolderSpecification` removed — dead Sonarr-fork code that always returned Accept with commented-out episode-matching logic; test fixture also removed
- **#42**: Documented as known debt — both `AlternativeTitleService` and `MovieTranslationService` TODO comments replaced with explanation of needed `MovieMetadataDeletedEvent`

### Deferred to v6 (no action now)

| # | What | Reason |
|---|------|--------|
| #4 | Deprecated `SetMovieFile` endpoint | API compatibility — keep through deprecation window |
| #5 | Deprecated `GetImportListExclusions()` | API compatibility — keep through deprecation window |
| #6 | Deprecated metadata providers | v6 milestone |
| #41 | Legacy SABnzbd/Transmission compat code | v6 milestone |
| #31 | Notification provider refactor | Large scope, regression risk |
| #35 | `HostConfigResource` mapper separation | Architectural — needs transformation doc |
| #28 | Full `.All().Where()` audit | Broad scope, needs profiling first |

### Deprioritised (do only if touching file for another reason)

| # | What | Reason |
|---|------|--------|
| #11 | `""` vs `string.Empty` standardisation | Low-value churn *(Codex)* |
| #18 | Path traversal validation in `MoviePathBuilder` | Already validated in add/update flows *(Codex)* |
| #34 | `BytesToString` dedup backend/frontend | Can't share code across runtimes *(Codex)* |

### Invalidated (no action needed)

| # | What | Reason |
|---|------|--------|
| #22 | Movie path collision | Already handled by `MoviePathValidator` |
| #24 | TOCTOU folder create | `CreateFolder` already handles `IOException` |
| #25 | EventAggregator lock scope | Handlers are read-only arrays, safe as-is |
