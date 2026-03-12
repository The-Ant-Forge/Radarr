# Spec: Refresh Monitored Movies Only + Scan Deduplication

## Goal
Add a setting to skip unmonitored movies during refresh/scan cycles, and deduplicate disk scans when multiple movies share the same path (as with PlaceInRootFolder).

## Motivation
With `PlaceInRootFolder` enabled, every movie's `Path` is the root folder. The daily refresh task calls `DiskScanService.Scan()` once per movie, scanning the entire root folder N times (once per movie in the collection). For a library of 200 movies, this means 200 redundant full-folder scans. Combined with scanning unmonitored movies that will never be acted upon, this creates significant unnecessary I/O.

---

## Design

### Feature 1: `RefreshMonitoredOnly` config toggle

- **Type**: `bool`, default `false`
- **UI label**: "Refresh Monitored Movies Only"
- **Help text**: "Only refresh metadata and scan disk for monitored movies. Unmonitored movies will be skipped during both automatic and manual updates."
- **Location in UI**: Settings > Media Management, near the existing `RescanAfterRefresh` dropdown

#### Behaviour when enabled
1. **Automatic refresh** (daily scheduled task) -- only processes monitored movies
2. **Manual refresh** (UI button / API call with no specific MovieIds) -- only processes monitored movies
3. **Specific movie refresh** (API call with MovieIds) -- always processes the requested movies regardless of setting (explicit intent)
4. **Standalone rescan** (`RescanMovieCommand` with no MovieId) -- only scans monitored movies

#### Behaviour when disabled (default)
No change from current behaviour -- all movies are refreshed and scanned.

### Feature 2: Scan path deduplication

When multiple movies share the same `Path` (as with PlaceInRootFolder), the disk scan should only run once per unique path per refresh cycle. This is always active -- no config toggle needed.

- Uses a `HashSet<string>` with case-insensitive comparison (Windows) or ordinal (Linux)
- Applies in both `RefreshMovieService.Execute()` and `DiskScanService.Execute(RescanMovieCommand)`
- Logged as: "Skipping scan of {movie}. Reason: Path already scanned this cycle"

---

## Files to modify

### 1. Backend -- Config layer

| File | Change |
|---|---|
| `src/NzbDrone.Core/Configuration/IConfigService.cs` | Add `bool RefreshMonitoredOnly { get; set; }` after `RescanAfterRefresh` (~line 46) |
| `src/NzbDrone.Core/Configuration/ConfigService.cs` | Add property: `get => GetValueBoolean("RefreshMonitoredOnly", false)` after `RescanAfterRefresh` block (~line 323) |

### 2. Backend -- API layer

| File | Change |
|---|---|
| `src/Radarr.Api.V3/Config/MediaManagementConfigResource.cs` | Add `bool RefreshMonitoredOnly` property and mapping in `ToResource()` |

### 3. Backend -- Core logic

| File | Change |
|---|---|
| `src/NzbDrone.Core/Movies/RefreshMovieService.cs` (line 259) | After `GetAllMovies()`: filter with `Where(m => m.Monitored)` when `RefreshMonitoredOnly` is enabled. Add `HashSet<string> scannedPaths` before loop; skip `RescanMovie()` if path already in set. |
| `src/NzbDrone.Core/MediaFiles/DiskScanService.cs` (line 281) | In `Execute(RescanMovieCommand)`: filter to monitored only when config is on. Add same path dedup with `HashSet`. |

### 4. Frontend -- UI

| File | Change |
|---|---|
| `frontend/src/Settings/MediaManagement/MediaManagement.tsx` | Add checkbox FormGroup near the `RescanAfterRefresh` select |
| `src/NzbDrone.Core/Localization/Core/en.json` | Add `RefreshMonitoredOnly` and `RefreshMonitoredOnlyHelpText` keys |

### 5. Database migration

No migration required. Config values are key-value in the `Config` table. `ConfigService` returns the default (`false`) if the key doesn't exist.

---

## Implementation order

1. Add config property to `IConfigService` + `ConfigService`
2. Add to API resource + mapper
3. Add localization strings to `en.json`
4. Add UI toggle in `MediaManagement.tsx`
5. Wire up monitored filter in `RefreshMovieService.Execute()`
6. Add path dedup `HashSet` in `RefreshMovieService.Execute()`
7. Wire up monitored filter + path dedup in `DiskScanService.Execute(RescanMovieCommand)`
8. Write unit tests

## Edge cases

- **Specific MovieId refresh**: Always processes regardless of setting (explicit user/API intent for that specific movie)
- **Movie becomes monitored**: Next refresh cycle picks it up automatically
- **All movies unmonitored**: Refresh cycle completes instantly with nothing to process (logged)
- **Path dedup with mixed monitored/unmonitored**: Only monitored movies' paths enter the set; unmonitored are filtered out before dedup applies
- **Non-PlaceInRootFolder users**: Dedup has no effect since each movie has a unique path; monitored-only filter still works as expected

## Testing

- Unit test `RefreshMovieService` with `RefreshMonitoredOnly=true` -- verify only monitored movies are processed
- Unit test `RefreshMovieService` with specific MovieIds -- verify all requested movies processed regardless of setting
- Unit test path dedup -- verify `Scan()` called once per unique path, not once per movie
- Unit test `DiskScanService.Execute(RescanMovieCommand)` -- verify monitored filter + dedup
