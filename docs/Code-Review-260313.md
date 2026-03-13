# Code Review — 2026-03-13

**Checklist version**: v3 (invariant-driven, failure-mode grouping)

**Guiding question**: *"Where does the codebase assume a specific data topology,
ownership model, or storage layout — and what breaks when those assumptions
don't hold?"*

**Scope**: Full codebase consolidation review covering all source, tests, build
config, and metadata. Focus on PlaceInRootFolder interactions, safety of
destructive operations, and performance at scale.

**Transformation document**: Architecture changes adopted during implementation
should be captured in `docs/Transformation-260313.md`.

---

## Summary Table

| # | Category | Description | File(s) | Impact | Effort | Risk |
|---|----------|-------------|---------|--------|--------|------|
| 1 | Invariants | Root folder deletion in MediaFileDeletionService | MediaFileDeletionService.cs:129-155 | **HIGH** | Low | High |
| 2 | Invariants | Delete folder missing PlaceInRootFolder guard (import flow) | DownloadedMovieImportService.cs:116-240 | **HIGH** | Low | High |
| 3 | Invariants | RemoveEmptySubfolders on shared root | RenameMovieFileService.cs:132 | Medium | Low | Medium |
| 4 | Invariants | RelativePath calculation when movie.Path == file destination | MovieFileMovingService.cs:120 | Medium | Medium | High |
| 5 | Invariants | DeleteMovieFile uses wrong parent as rootFolder | MediaFileDeletionService.cs:51-89 | Medium | Low | Medium |
| 6 | Safety | Bulk file delete uses wrong movie context | MovieFileController.cs:173-189 | **HIGH** | Medium | High |
| 7 | Safety | Bulk delete early exit on path conflict — partial deletion | MediaFileDeletionService.cs:91-127 | **HIGH** | Medium | High |
| 8 | Safety | PlaceInRootFolder not checked in backend deletion paths | MediaFileDeletionService.cs (entire) | **HIGH** | Medium | Medium |
| 9 | Safety | Bulk movie file editor — no cross-movie scope validation | MovieFileController.cs:193-238 | Medium | Low | High |
| 10 | Safety | No MovieIds validation in bulk movie delete | MovieEditorController.cs:139-143 | Medium | Trivial | Medium |
| 11 | Safety | Missing null check on movieFiles.First() | MovieFileController.cs:180 | Medium | Trivial | Medium |
| 12 | Safety | API key in query string may leak in logs | ApiKeyAuthenticationHandler.cs:41 | Medium | Medium | Medium |
| 13 | Workflow | No migration when PlaceInRootFolder is toggled | Multiple (config change) | Medium | High | High |
| 14 | Workflow | MediaFileTableCleanup assumes movie.Path hasn't changed | MediaFileTableCleanupService.cs:30-55 | Medium | High | High |
| 15 | Workflow | Concurrent scans on same path not serialized | DiskScanService + RefreshMovieService | Medium | High | Medium |
| 16 | Workflow | Already-imported check only looks at current batch | ImportApprovedMovie.cs:81-86 | Medium | Medium | Medium |
| 17 | Workflow | Title-matching lacks Unicode normalization | DiskScanService.cs:137-160 | Medium | Low | Low |
| 18 | Workflow | Config race if PlaceInRootFolder changes mid-operation | ConfigService.cs (overall) | Medium | High | Medium |
| 19 | Workflow | Scan+delete race; double processing with filtering | DiskScanService.cs:130-171 | Low | Medium | Low |
| 20 | Workflow | Rescan queued with potentially invalid MovieId | ImportApprovedMovie.cs:183-189 | Low | Low | Low |
| 21 | Performance | Duplicate file enumeration in PlaceInRootFolder fallback | DiskScanService.cs:147-157 | **HIGH** | Low | Minimal |
| 22 | Performance | Missing media info cache (FFProbe called redundantly) | VideoFileInfoReader.cs:63-74 | **HIGH** | Medium | Low |
| 23 | Performance | Sequential TMDb API calls without batching | RefreshMovieService.cs:287-307 | **HIGH** | High | Medium |
| 24 | Performance | Unbounded all-movies query before filtering | RefreshMovieService.cs:270-275 | Medium | Medium | Low |
| 25 | Performance | Sequential file size polling on NAS/SMB | DiskScanService.cs:173-202 | Medium | Medium | Medium |
| 26 | Performance | RemoveEmptySubfolders called per-movie on shared root | DiskProviderBase.cs:579-598 | Medium | Medium | Medium |
| 27 | Performance | N+1 pattern in MovieRepository.All() | MovieRepository.cs:99-127 | Medium | Medium | Medium |
| 28 | Performance | Logging elapsed time bug (wrong stopwatch) | DiskScanService.cs:202 | Low | Trivial | None |
| 29 | UX | Missing tooltip explaining disabled CreateEmptyMovieFolders | MediaManagement.tsx:191-195 | Medium | Trivial | None |
| 30 | UX | Vague label "Disable Monitoring" for UnmonitorOnCutoffMet | MediaManagement.tsx:362 | Medium | Trivial | None |
| 31 | UX | RefreshMonitoredOnly placed in wrong settings section | MediaManagement.tsx:436 | Low | Trivial | None |
| 32 | UX | PlaceInRootFolder mode has no visual emphasis/warning | MediaManagement.tsx:171 | Low | Trivial | None |
| 33 | Observability | Title-based filtering doesn't log retained vs filtered files | DiskScanService.cs | Low | Low | None |
| 34 | Observability | RefreshMonitoredOnly doesn't log per-movie skip reason | RefreshMovieService.cs | Low | Low | None |
| 35 | Maintainability | RecycleBin cleanup silently swallows permission errors | RecycleBinProvider.cs:187-196 | Medium | Low | Medium |
| 36 | Maintainability | Extra files orphaned if PlaceInRootFolder is toggled | DiskScanService.cs:206-214 | Low | Low | Low |
| 37 | Tests | No tests for RefreshMonitoredOnly filtering logic | RefreshMovieServiceFixture.cs | **HIGH** | Medium | None |
| 38 | Tests | No tests for PlaceInRootFolder path building | MovieFolderPathBuilderFixture.cs | **HIGH** | Low | None |
| 39 | Tests | No tests for PlaceInRootFolder movie addition | AddMovieFixture.cs | **HIGH** | Low | None |
| 40 | Tests | No integration test for PlaceInRootFolder + RefreshMonitoredOnly combined | (missing) | **HIGH** | High | Low |
| 41 | Sustainability | Missing docs/TODO.md tracker | docs/ | Medium | Low | None |
| 42 | Sustainability | Inconsistent TreatWarningsAsErrors in deploy script | deploy-local.sh:54-59 | Low | Trivial | None |
| 43 | Sustainability | No upstream merge strategy documented | CLAUDE.md | Low | Trivial | None |

---

## Detailed Findings

### 1. Invariants & Boundaries

#### F1: Root Folder Deletion in MediaFileDeletionService.Handle() [HIGH]

**File**: `src/NzbDrone.Core/MediaFiles/MediaFileDeletionService.cs:129-155`

When PlaceInRootFolder is enabled, `movie.Path == rootFolder`. The
`Handle(MovieFileDeletedEvent)` method attempts to delete empty folders,
including the movie folder itself at line 152. If all movies' files are removed,
this could delete the entire shared root folder.

**Action**: Add `if (!_configService.PlaceInRootFolder)` guard around the
folder cleanup block (lines 132-154).

---

#### F2: Delete Folder Missing PlaceInRootFolder Guard (Import Flow) [HIGH]

**File**: `src/NzbDrone.Core/MediaFiles/DownloadedMovieImportService.cs:116-240`

`ShouldDeleteFolder()` and `ProcessFolder()` do not check PlaceInRootFolder.
If triggered with PlaceInRootFolder=true, they could attempt to delete the
entire library root after import. The frontend hides the option (b32d9aff5),
but the backend has no safeguard.

**Action**: Add config check in `ProcessFolder()` before calling
`_diskProvider.DeleteFolder()`.

---

#### F3: RemoveEmptySubfolders on Shared Root [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/RenameMovieFileService.cs:132`

After rename, `RemoveEmptySubfolders(movie.Path)` is called. When
PlaceInRootFolder=true, this operates on the shared root. Could
accidentally clean up subfolders belonging to other movies.

**Action**: Skip when PlaceInRootFolder is enabled.

---

#### F4: RelativePath Calculation Breaks When Paths Are Equal [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/MovieFileMovingService.cs:120`

`movie.Path.GetRelativePath(destinationFilePath)` throws
`NotParentException` when both paths are equal (shared root folder case).
`GetRelativePath()` requires the first path to be a proper parent.

**Action**: Handle identity case before calling `GetRelativePath()`.

---

#### F5: DeleteMovieFile Uses Wrong Parent as RootFolder [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/MediaFileDeletionService.cs:51-89`

Calculates `rootFolder` via `GetParentFolder(movie.Path)`. When
PlaceInRootFolder=true, this returns the parent of the configured root
folder — an unexpected and potentially dangerous path reference.

**Action**: Use configured root folder instead of deriving from movie.Path.

---

### 2. Safety & Resilience

#### F6: Bulk File Delete Uses Wrong Movie Context [HIGH]

**File**: `src/Radarr.Api.V3/MovieFiles/MovieFileController.cs:173-189`

`DeleteMovieFiles()` retrieves `movie` from `movieFiles.First().MovieId`
then uses that single movie for ALL file deletions. If files span multiple
movies, deletion logic uses wrong context — bypassing ownership checks.

**Action**: Group movieFiles by MovieId and delete each group with its
correct movie context.

---

#### F7: Bulk Delete Early Exit — Partial Deletion [HIGH]

**File**: `src/NzbDrone.Core/MediaFiles/MediaFileDeletionService.cs:91-127`

When deleting multiple movies with files, if ANY movie's path overlaps
another, the handler `return`s silently. Movies already processed get
files deleted; remaining movies are skipped. DB records for ALL movies
are already deleted (happens before this event fires).

**Action**: Process per-movie independently; log and skip conflicting
movies instead of aborting the entire batch.

---

#### F8: PlaceInRootFolder Not Checked in Backend Deletion [HIGH]

**File**: `src/NzbDrone.Core/MediaFiles/MediaFileDeletionService.cs`

No reference to `_configService.PlaceInRootFolder` anywhere in this
service. The frontend hides delete-files UI, but API clients can still
trigger deletion. In shared-root mode, this could delete the entire library.

**Action**: Add backend guard in all deletion paths: refuse file/folder
deletion when PlaceInRootFolder=true and path matches the root folder.

---

#### F9: Bulk File Editor — No Cross-Movie Scope Validation [MEDIUM]

**File**: `src/Radarr.Api.V3/MovieFiles/MovieFileController.cs:193-238`

`SetPropertiesBulk` allows editing files from different movies in one
request but maps all responses using the first movie's context. No
ownership validation.

**Action**: Validate all files belong to same movie, or group by movie.

---

#### F10: No MovieIds Validation in Bulk Movie Delete [MEDIUM]

**File**: `src/Radarr.Api.V3/Movies/MovieEditorController.cs:139-143`

`DeleteMovies` accepts `MovieIds` with no null/empty check. Silently
succeeds with no-op. Contrast with other endpoints that validate input.

**Action**: Add `BadRequestException` when MovieIds is null or empty.

---

#### F11: Missing Null Check on movieFiles.First() [MEDIUM]

**File**: `src/Radarr.Api.V3/MovieFiles/MovieFileController.cs:180`

If provided IDs don't exist, `GetMovies()` returns empty list and
`First()` throws `InvalidOperationException` → HTTP 500 instead of 400.

**Action**: Add empty check before `First()`.

---

#### F12: API Key in Query String May Leak in Logs [MEDIUM]

**File**: `src/Radarr.Http/Authentication/ApiKeyAuthenticationHandler.cs:41`

API key can be passed via query string. If request fails, the full URL
(with key) may appear in error logs or access logs.

**Action**: Prefer header-based auth; consider redacting query strings in
error logging.

---

### 3. Workflow Correctness

#### F13: No Migration When PlaceInRootFolder Is Toggled [MEDIUM]

Enabling PlaceInRootFolder after movies exist leaves them with individual
paths. Disabling it leaves all movies pointing to root. No code migrates
paths or moves files on toggle.

**Action**: Document as known limitation. Consider adding a "reprocess
library" task that updates paths after toggle.

---

#### F14: MediaFileTableCleanup Assumes movie.Path Hasn't Changed [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/MediaFileTableCleanupService.cs:30-55`

Constructs expected file path via `Path.Combine(movie.Path, movieFile.RelativePath)`.
If PlaceInRootFolder was toggled, movie.Path and actual file location diverge.
Files are incorrectly marked missing and deleted from DB.

**Action**: Validate path consistency before cleanup. Skip cleanup when
path inconsistency is detected and log a warning.

---

#### F15: Concurrent Scans on Same Path Not Serialized [MEDIUM]

Path dedup works within a single refresh cycle, but manual refresh during
background refresh creates two independent HashSet instances. Both scan
the same path concurrently — double I/O and potential DB race conditions.

**Action**: Document limitation. Consider path-level locking for scans.

---

#### F16: Already-Imported Check Only Looks at Current Batch [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/MovieImport/ImportApprovedMovie.cs:81-86`

Duplicate check uses in-memory list, not database. If same movie is
imported via two separate commands (restart, queue replay), second import
may overwrite the first.

**Action**: Add database check before upgrade.

---

#### F17: Title-Matching Lacks Unicode Normalization [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/DiskScanService.cs:137-160`

`StartsWith` with `OrdinalIgnoreCase` doesn't handle decomposed Unicode
(NFD vs NFC). Files with accented characters in titles may silently fail
to match on Linux filesystems.

**Action**: Normalize both title and filename to NFC before comparison.

---

#### F18: Config Race If PlaceInRootFolder Changes Mid-Operation [MEDIUM]

No transaction guard ensures consistent config reads during a scan or
refresh cycle. If config changes mid-operation, some paths use old logic
and some use new.

**Action**: Snapshot config at start of each operation.

---

#### F19: Scan+Delete Race Condition [LOW]

**File**: `src/NzbDrone.Core/MediaFiles/DiskScanService.cs:130-171`

Gap between file enumeration and processing. If a file is deleted during
scan, cleanup may orphan DB records or trigger incorrect events.

**Action**: Low priority; add defensive file-existence checks.

---

#### F20: Rescan Queued with Potentially Invalid MovieId [LOW]

**File**: `src/NzbDrone.Core/MediaFiles/MovieImport/ImportApprovedMovie.cs:183-189`

`DestinationAlreadyExistsException` handler queues rescan, but
`localMovie.Movie.Id` may be 0 for new imports. Rescan silently dropped.

**Action**: Validate MovieId before queueing.

---

### 4. Performance & Scale

#### F21: Duplicate File Enumeration in PlaceInRootFolder Fallback [HIGH]

**File**: `src/NzbDrone.Core/MediaFiles/DiskScanService.cs:147-157`

When year-based title match fails, `GetVideoFiles(movie.Path)` is called
again — re-enumerating the entire shared root. With thousands of files
and variable match rates, this doubles I/O for failed matches.

**Action**: Cache the initial `GetVideoFiles()` result and reuse for
fallback filtering.

---

#### F22: Missing Media Info Cache (FFProbe Called Redundantly) [HIGH]

**File**: `src/NzbDrone.Core/MediaFiles/MediaInfo/VideoFileInfoReader.cs:63-74`

Code has a TODO comment: "Cache media info by path, mtime and length."
FFProbe spawns 1-3 processes per file (0.5-2s each). No caching means
rescans repeat expensive analysis.

**Action**: Implement hash-based cache keyed by path+mtime+size.

---

#### F23: Sequential TMDb API Calls Without Batching [HIGH]

**File**: `src/NzbDrone.Core/Movies/RefreshMovieService.cs:287-307`

Refresh loop calls `RefreshMovieInfo()` once per movie (one HTTP request
each). `SkyHookProxy.GetBulkMovieInfo()` exists but is never used in
the refresh flow. 100 movies × 500ms = 50s wall-clock time.

**Action**: Refactor to use bulk API. High effort but high payoff.

---

#### F24: Unbounded All-Movies Query Before Filtering [MEDIUM]

**File**: `src/NzbDrone.Core/Movies/RefreshMovieService.cs:270-275`

`GetAllMovies()` loads every movie with joins (AlternativeTitles,
Translations, QualityProfiles) before RefreshMonitoredOnly filtering
discards unmonitored ones.

**Action**: Add `GetMonitoredMovies()` repository method to push
filtering to SQL.

---

#### F25: Sequential File Size Polling on NAS/SMB [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/DiskScanService.cs:173-202`

Sequential `GetFileSize()` calls in a tight loop. On SMB (~50ms RTT),
100 files = 5 seconds of blocking I/O.

**Action**: Consider batch stat or parallel I/O with rate limiting.

---

#### F26: RemoveEmptySubfolders Called Per-Movie on Shared Root [MEDIUM]

**File**: `src/NzbDrone.Common/Disk/DiskProviderBase.cs:579-598`

When PlaceInRootFolder=true, called N times on the same root folder.
On NAS: 1000 movies × 100ms = 100 seconds of cleanup.

**Action**: Centralize to single call after all scans complete.

---

#### F27: N+1 Pattern in MovieRepository.All() [MEDIUM]

**File**: `src/NzbDrone.Core/Movies/MovieRepository.cs:99-127`

Loads ALL alternative titles and translations globally, even when only
some movies are queried. 5000 movies with 50k alt titles = wasteful.

**Action**: Selectively load related data for queried movies only.

---

#### F28: Logging Elapsed Time Bug (Wrong Stopwatch) [LOW]

**File**: `src/NzbDrone.Core/MediaFiles/DiskScanService.cs:202`

Logs `decisionsStopwatch.Elapsed` instead of `fileInfoStopwatch.Elapsed`
for the file reprocessing timing. One-line fix.

**Action**: Fix stopwatch reference.

---

### 5. UX & Operability

#### F29: Missing Tooltip for Disabled CreateEmptyMovieFolders [MEDIUM]

**File**: `frontend/src/Settings/MediaManagement/MediaManagement.tsx:191-195`

When PlaceInRootFolder disables this checkbox, no explanation is shown.

**Action**: Add help text explaining the dependency.

---

#### F30: Vague Label for UnmonitorOnCutoffMet [MEDIUM]

**File**: `frontend/src/Settings/MediaManagement/MediaManagement.tsx:362`

"Disable Monitoring" sounds like a global toggle. Actual behavior is
"unmonitor after quality cutoff is met."

**Action**: Change label to "Unmonitor After Quality Cutoff" or similar.

---

#### F31: RefreshMonitoredOnly in Wrong Settings Section [LOW]

**File**: `frontend/src/Settings/MediaManagement/MediaManagement.tsx:436`

Placed in "File Management" but logically belongs near RescanAfterRefresh.

**Action**: Move to appropriate section.

---

#### F32: PlaceInRootFolder Lacks Visual Emphasis [LOW]

**File**: `frontend/src/Settings/MediaManagement/MediaManagement.tsx:171`

This setting fundamentally changes library behavior but looks like any
other checkbox. No warning or emphasis.

**Action**: Add warning help text about implications.

---

#### F33-34: Observability Gaps in Filtering [LOW]

DiskScanService title filtering and RefreshMonitoredOnly don't log
per-file/per-movie decisions at DEBUG level, making diagnosis difficult.

**Action**: Add structured DEBUG logging.

---

### 6. Maintainability

#### F35: RecycleBin Cleanup Swallows Permission Errors [MEDIUM]

**File**: `src/NzbDrone.Core/MediaFiles/RecycleBinProvider.cs:187-196`

`UnauthorizedAccessException` caught and logged but not surfaced to user.
Bin fills silently over time.

**Action**: Surface warning to health check system.

---

#### F36: Extra Files Orphaned on Config Toggle [LOW]

**File**: `src/NzbDrone.Core/MediaFiles/DiskScanService.cs:206-214`

Stale extra file records remain in DB after PlaceInRootFolder is toggled.

**Action**: Add cleanup in config change handler.

---

### 7. Test Gaps

#### F37: No Tests for RefreshMonitoredOnly [HIGH]

**File**: `src/NzbDrone.Core.Test/MovieTests/RefreshMovieServiceFixture.cs`

Only 4 tests; none cover the new filtering logic.

**Action**: Add tests for monitored-only on/off, mixed states, path dedup.

---

#### F38: No Tests for PlaceInRootFolder Path Building [HIGH]

**File**: `src/NzbDrone.Core.Test/MovieTests/MovieFolderPathBuilderFixture.cs`

No test that `BuildPath()` returns root folder when PlaceInRootFolder=true.

**Action**: Add test case.

---

#### F39: No Tests for PlaceInRootFolder Movie Addition [HIGH]

**File**: `src/NzbDrone.Core.Test/MovieTests/AddMovieFixture.cs`

Mock setup hardcodes `PlaceInRootFolder=false`. No test for true case.

**Action**: Add test case.

---

#### F40: No Integration Test for Combined Features [HIGH]

No test covers PlaceInRootFolder + RefreshMonitoredOnly + title filtering
+ path dedup working together.

**Action**: Create integration test with config × workflow × topology
matrix.

---

### 8. Sustainability

#### F41: Missing docs/TODO.md [MEDIUM]

CLAUDE.md references this file but it doesn't exist. Deferred items have
no central tracker.

**Action**: Create docs/TODO.md from review findings.

---

#### F42: Inconsistent TreatWarningsAsErrors [LOW]

**File**: `scripts/deploy-local.sh:54-59`

Backend build uses `true`, tray app uses `false`. Inconsistent standards.

**Action**: Align or document exception.

---

#### F43: No Upstream Merge Strategy Documented [LOW]

Fork is 10+ commits ahead with no documented merge approach.

**Action**: Add merge strategy to CLAUDE.md.

---

## Implementation Priority

### Tier 1 — Critical Safety (implement immediately)

| # | Finding | Effort |
|---|---------|--------|
| F1 | Guard root folder deletion in MediaFileDeletionService | Low |
| F2 | Guard delete folder in import flow | Low |
| F8 | Backend PlaceInRootFolder guard on all deletion paths | Medium |
| F6 | Fix bulk file delete cross-movie context | Medium |
| F7 | Fix bulk delete early exit / partial deletion | Medium |

### Tier 2 — High Value, Low Effort (quick wins)

| # | Finding | Effort |
|---|---------|--------|
| F3 | Guard RemoveEmptySubfolders | Low |
| F5 | Fix rootFolder derivation in DeleteMovieFile | Low |
| F10 | Validate MovieIds in bulk delete | Trivial |
| F11 | Null check on movieFiles.First() | Trivial |
| F21 | Cache GetVideoFiles result for fallback | Low |
| F28 | Fix stopwatch logging bug | Trivial |
| F29 | Add tooltip for disabled setting | Trivial |
| F30 | Improve UnmonitorOnCutoffMet label | Trivial |

### Tier 3 — Important, Medium Effort (next cycle)

| # | Finding | Effort |
|---|---------|--------|
| F4 | Handle RelativePath identity case | Medium |
| F9 | Scope validation in bulk file editor | Low |
| F12 | API key query string leakage | Medium |
| F17 | Unicode normalization in title matching | Low |
| F22 | Media info cache | Medium |
| F24 | Push monitored filtering to repository | Medium |
| F37-40 | Test coverage for new features | Medium-High |

### Tier 4 — Architecture / Deferred

| # | Finding | Effort |
|---|---------|--------|
| F13 | Config toggle migration strategy | High |
| F14 | Path consistency validation | High |
| F15 | Path-level scan locking | High |
| F18 | Config snapshot reads | High |
| F23 | TMDb bulk API integration | High |
| F25-27 | Performance optimizations (SMB, cleanup, N+1) | Medium-High |

### Out of Scope (for docs/TODO.md)

- Full TODO/FIXME audit across codebase (~1000 markers)
- Upstream notification code duplication refactor
- N+1 query optimization sprint
- React types 18→19 migration
- ESLint 8→10 migration
