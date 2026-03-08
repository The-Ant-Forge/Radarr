# Spec: Do Not Create Movie Folders

## Goal
Add a setting that tells Radarr to place movie files directly in the root folder instead of creating a per-movie subfolder (e.g. `Movies/The Great Adventure (2024)/`). When enabled, movies land as `Movies/The Great Adventure (2024).mkv` instead.

## Motivation
Many users (especially those using flat library layouts or external tools for organisation) find per-movie folders unnecessary. Currently there is no way to disable folder creation without hacking the folder format.

---

## Design

### New config property: `PlaceInRootFolder`
- **Type**: `bool`, default `false`
- **UI label**: "Do Not Create Movie Folders"
- **Help text**: "Have Radarr place the movie in the root folder instead of a subfolder of the same name"
- **Location in UI**: Settings > Media Management > Folders section (alongside the existing `CreateEmptyMovieFolders` and `DeleteEmptyFolders` switches)

### Behaviour when enabled
1. **Adding a movie** — `movie.Path` is set to the root folder itself (no subfolder appended)
2. **Importing/moving a file** — the file is placed directly in the root folder
3. **Folder creation** — `EnsureMovieFolder` skips subfolder creation when the movie path equals the root folder
4. **Folder naming format** — the `MovieFolderFormat` setting is effectively ignored (the file still gets renamed per `StandardMovieFormat`)

### Behaviour when disabled (default)
No change from current behaviour.

---

## Files to modify

### 1. Backend — Config layer

| File | Change |
|---|---|
| `src/NzbDrone.Core/Configuration/IConfigService.cs` | Add `bool PlaceInRootFolder { get; set; }` after `DeleteEmptyFolders` (~line 33) |
| `src/NzbDrone.Core/Configuration/ConfigService.cs` | Add property: `get => GetValueBoolean("PlaceInRootFolder", false)` (after `DeleteEmptyFolders` block, ~line 212) |

### 2. Backend — API layer

| File | Change |
|---|---|
| `src/Radarr.Api.V3/Config/MediaManagementConfigResource.cs` | Add `bool PlaceInRootFolder` property (~line 15, after `DeleteEmptyFolders`) |
| `src/Radarr.Api.V3/Config/MediaManagementConfigResource.cs` | Add mapping in `ToResource()`: `PlaceInRootFolder = model.PlaceInRootFolder` |

### 3. Backend — Core logic

| File | Change |
|---|---|
| `src/NzbDrone.Core/Movies/AddMovieService.cs` (~line 131-137) | In `SetPropertiesAndValidate()`: when `PlaceInRootFolder` is true, set `newMovie.Path = newMovie.RootFolderPath` instead of appending the folder name |
| `src/NzbDrone.Core/Organizer/FileNameBuilder.cs` (~line 161-168) | In `BuildFilePath()`: no change needed — it already uses `movie.Path`, which will be the root when the setting is on |
| `src/NzbDrone.Core/Movies/MoviePathBuilder.cs` (~line 28-42) | In `BuildPath()`: when `PlaceInRootFolder` is true, return `movie.RootFolderPath` directly instead of appending the folder name. Inject `IConfigService`. |
| `src/NzbDrone.Core/MediaFiles/MovieFileMovingService.cs` (~line 167-203) | In `EnsureMovieFolder()`: when `PlaceInRootFolder` is true and `movieFolder == rootFolder`, skip the subfolder creation block (the root already exists) |

### 4. Frontend — UI

| File | Change |
|---|---|
| `frontend/src/Settings/MediaManagement/MediaManagement.tsx` (~line 170-210) | Add a new `FormGroup` in the Folders `FieldSet` for the `placeInRootFolder` checkbox, using `inputTypes.CHECK` |
| `src/NzbDrone.Core/Localization/en.json` | Add keys: `"PlaceInRootFolder": "Do Not Create Movie Folders"` and `"PlaceInRootFolderHelpText": "Have Radarr place the movie in the root folder instead of a subfolder of the same name"` |

### 5. Database migration

| File | Change |
|---|---|
| `src/NzbDrone.Core/Datastore/Migration/###_add_place_in_root_folder.cs` | Config values are key-value in the `Config` table — no schema migration needed. The `ConfigService` will return the default (`false`) if the key doesn't exist yet. **No migration file required.** |

---

## Implementation order

1. Add config property to `IConfigService` + `ConfigService`
2. Add to API resource + mapper
3. Add localization strings to `en.json`
4. Add UI toggle in `MediaManagement.tsx`
5. Wire up `AddMovieService.SetPropertiesAndValidate()`
6. Wire up `MoviePathBuilder.BuildPath()`
7. Guard `MovieFileMovingService.EnsureMovieFolder()`
8. Write unit tests for each modified service

## Edge cases to handle

- **Existing movies with folders**: This setting only affects *new* imports/adds. Existing movies keep their current `movie.Path`. Users would need to manually move existing movies if they want a flat layout.
- **Multiple movies in one root**: File naming must be unique. The `StandardMovieFormat` should include year/quality to avoid collisions (e.g. two movies with the same title).
- **Extra files** (subtitles, nfo): These also go into root instead of a subfolder — same as the movie file.
- **CreateEmptyMovieFolders interaction**: When `PlaceInRootFolder` is true, `CreateEmptyMovieFolders` should be ignored/disabled in the UI (no point creating empty folders if we're not using folders).
- **DeleteEmptyFolders interaction**: Should still function normally — it won't find movie subfolders to delete since none are created.
- **Movie refresh/rename**: `AutoRenameFolders` should be ignored when `PlaceInRootFolder` is true.

## Testing

- Unit test `AddMovieService` with `PlaceInRootFolder=true` — verify `movie.Path == rootFolderPath`
- Unit test `MoviePathBuilder.BuildPath()` with setting on — verify returns root path
- Unit test `EnsureMovieFolder` with setting on — verify no subfolder creation
- Unit test interactions: `PlaceInRootFolder=true` + `CreateEmptyMovieFolders=true` — verify no empty folders created
