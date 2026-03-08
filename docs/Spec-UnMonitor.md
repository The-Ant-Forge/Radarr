# Spec: Unmonitor on Cutoff Met

## Problem

Radarr continues monitoring movies after download, searching indexers for upgrades even when the imported file already meets the quality profile's cutoff. This wastes indexer API calls, bandwidth, and download client resources.

## Solution

A new global setting **Disable Monitoring** (default: off) in Settings > Media Management > File Management. When enabled, movies are automatically unmonitored after a file import once the imported file's quality meets or exceeds the quality profile's cutoff.

## Behaviour

- **Trigger**: `MovieFileAddedEvent` — fires after every successful import (new download, quality upgrade, or manual import)
- **Check**: Uses `IUpgradableSpecification.QualityCutoffNotMet()` to compare the imported file's quality against the movie's quality profile cutoff
- **Action**: If cutoff is met and the setting is enabled, sets `movie.Monitored = false` before persisting the update
- **Scope**: Quality cutoff only (not custom format score cutoff). Custom format cutoff can be added later if needed

### When it fires

| Scenario | Cutoff met? | Result |
|----------|-------------|--------|
| New download at or above cutoff | Yes | Unmonitored |
| New download below cutoff | No | Stays monitored |
| Upgrade that reaches cutoff | Yes | Unmonitored |
| Upgrade still below cutoff | No | Stays monitored |
| Manual import at cutoff | Yes | Unmonitored |
| Setting is off | N/A | No change (existing behaviour) |

### Relationship to existing settings

- **Unmonitor Deleted Movies** (`AutoUnmonitorPreviouslyDownloadedMovies`): Unmonitors when a file is *deleted* (non-upgrade). Complementary — one fires on file add, the other on file delete.
- **Download Propers and Repacks**: Still applies while the movie is monitored. Once unmonitored by this setting, no further searching occurs.

## Implementation

### Config layer

| File | Change |
|------|--------|
| `IConfigService.cs` | `bool UnmonitorOnCutoffMet { get; set; }` |
| `ConfigService.cs` | Property with `GetValueBoolean("UnmonitorOnCutoffMet")`, default `false` |

### API layer

| File | Change |
|------|--------|
| `MediaManagementConfigResource.cs` | Property + mapper line |

### Business logic

| File | Change |
|------|--------|
| `MovieService.cs` | Inject `IUpgradableSpecification`, check cutoff in `Handle(MovieFileAddedEvent)` |

### Frontend

| File | Change |
|------|--------|
| `MediaManagement.ts` | Add `unmonitorOnCutoffMet: boolean` to interface |
| `MediaManagement.tsx` | Add checkbox in File Management section |

### Localization

| File | Change |
|------|--------|
| `en.json` | `UnmonitorOnCutoffMet` and `UnmonitorOnCutoffMetHelpText` keys |

## Testing

- All 19 existing MovieService tests pass
- Backend build: 0 errors, 0 warnings
- Frontend build: compiles successfully
- Manual: toggle appears in Settings > Media Management > File Management
