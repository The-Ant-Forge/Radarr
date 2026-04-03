# Code Review Specification

Periodically we do a consolidation review covering all source, tests, build config, and metadata.

## Review Checklist

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

## Deliverable
A review document in `docs/Code-Review-YYMMDD.md` (or similar) with:
- Summary table: Category, Description, Action, Impact, Effort, Risk
- Detailed findings grouped by category, ordered by impact then effort
- Out-of-scope items noted for `docs/TODO.md`
- Transformation Document — during execution of recommended items any architecture changes are captured in `docs/Transformation-YYMMDD.md` for future refactors of the upstream code base.

## Process
1. Produce the review document — do NOT implement during review
2. Review and approve findings with the user
3. Implement approved items in focused commits
4. Re-run tests after each change
5. On completion of review items update the code review doc to reflect tasks done, deferred or ignored.
