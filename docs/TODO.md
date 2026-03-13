# TODO — Radarr Fork

Tracking deferred items, known debt, and planned work.

## Deferred from Code Review 260313

### High Effort / High Risk (requires careful planning)

- [x] **F1/F2**: Guard root folder deletion in MediaFileDeletionService and DownloadedMovieImportService _(done: b257a81)_
- [ ] **F4**: Handle RelativePath identity case in MovieFileMovingService when paths are equal
- [x] **F7**: Fix bulk delete early exit / partial deletion in MediaFileDeletionService.HandleAsync _(done: b257a81)_
- [x] **F9**: Add cross-movie scope validation in bulk file editor _(done: b257a81)_
- [x] **F13**: ~~PlaceInRootFolder config toggle migration~~ — downgraded: toggle only affects new movies, existing paths stay valid. Mixed layout is cosmetic, not a data integrity issue. All scan/delete guards handle both layouts.
- [ ] **F14**: Path consistency validation in MediaFileTableCleanupService
- [ ] **F15**: Path-level scan locking to prevent concurrent scans of same folder
- [ ] **F18**: Config snapshot reads to prevent mid-operation config changes
- [ ] **F23**: Refactor refresh loop to use TMDb bulk API (SkyHookProxy.GetBulkMovieInfo)
- [ ] **F40**: Integration test matrix for PlaceInRootFolder + RefreshMonitoredOnly combined

### Out of Scope

- Full TODO/FIXME/HACK audit (~1000 markers across codebase)
- Upstream notification embed-building duplication refactor
- N+1 query optimization sprint across all repositories
- React types 18 to 19 migration
- ESLint 8 to 10 migration
- FontAwesome 6 to 7 migration

## Feature Ideas

- PlaceInRootFolder: warn user before toggling if existing movies would be affected
- Scan progress indicator in UI (percentage of movies scanned)
- Bulk API endpoint for TMDb metadata refresh
