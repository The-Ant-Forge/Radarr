# Dependency Update Specification

**Date:** 2026-03-08
**Scope:** All NuGet (backend) and Yarn (frontend) dependencies

> **Fork note:** Since this is a fork of upstream Radarr, major upgrades increase merge
> conflict risk. Safe patch/minor updates are preferred unless there is a compelling reason
> to diverge.

---

## 1. Safe Updates (patch / minor — low risk)

### NuGet

| Package | Current | Latest | Type | Projects |
|---|---|---|---|---|
| Newtonsoft.Json | 13.0.3 | 13.0.4 | Patch | Core, Common |
| Polly | 8.6.0 | 8.6.6 | Patch | Core |
| Dapper | 2.1.66 | 2.1.72 | Patch | Core, Core.Test |
| Diacritical.Net | 1.0.4 | 1.0.5 | Patch | Core |
| MailKit | 4.13.0 | 4.15.1 | Minor | Core |
| SixLabors.ImageSharp | 3.1.11 | 3.1.12 | Patch | Core |
| IPAddressRange | 6.2.0 | 6.3.0 | Minor | Common |
| Moq | 4.18.4 | 4.20.72 | Minor | Test.Common |
| SourceGear.sqlite3 | 3.50.4.2 | 3.50.4.5 | Patch | Common |
| Microsoft.Data.SqlClient | 6.1.1 | 6.1.4 | Patch | Core |
| NLog.Layouts.ClefJsonLayout | 1.0.3 | 1.0.5 | Patch | Common |

### Yarn

| Package | Current | Latest | Type |
|---|---|---|---|
| @tanstack/react-query | 5.74.3 | 5.90.21 | Minor |
| typescript | 5.7.2 | 5.9.3 | Minor |
| webpack | 5.95.0 | 5.105.4 | Patch |
| postcss | 8.5.6 | 8.5.8 | Patch |
| autoprefixer | 10.4.20 | 10.4.27 | Patch |
| core-js | 3.42.0 | 3.48.0 | Minor |
| qs | 6.13.0 | 6.15.0 | Minor |
| use-debounce | 10.0.4 | 10.1.0 | Minor |
| @typescript-eslint/eslint-plugin | 8.18.1 | 8.56.1 | Minor |
| @typescript-eslint/parser | 8.18.1 | 8.56.1 | Minor |
| eslint-plugin-import | 2.31.0 | 2.32.0 | Minor |
| eslint-plugin-react | 7.37.1 | 7.37.5 | Patch |
| terser-webpack-plugin | 5.3.10 | 5.3.17 | Patch |
| ts-loader | 9.5.1 | 9.5.4 | Patch |
| rimraf | 6.0.1 | 6.1.3 | Minor |
| mini-css-extract-plugin | 2.9.1 | 2.10.0 | Minor |
| html-webpack-plugin | 5.6.0 | 5.6.6 | Patch |
| lodash | 4.17.21 | 4.17.23 | Patch |
| react-focus-lock | 2.9.4 | 2.13.7 | Minor |
| @types/react-virtualized | 9.22.2 | 9.22.3 | Patch |
| typescript-plugin-css-modules | 5.0.1 | 5.2.0 | Minor |

---

## 2. Blocked by .NET Target Framework (net8.0 → net10.0)

These packages track the .NET runtime version. Updating them requires migrating the
entire solution to .NET 10. **Do not update independently.**

| Package | Current | Latest |
|---|---|---|
| Microsoft.Extensions.DependencyInjection | 8.0.1 | 10.0.3 |
| Microsoft.Extensions.Hosting.WindowsServices | 8.0.1 | 10.0.3 |
| Microsoft.Extensions.Configuration | 8.0.0 | 10.0.3 |
| Microsoft.Extensions.Logging | 8.0.1 | 10.0.3 |
| Microsoft.AspNetCore.Cryptography.KeyDerivation | 8.0.17 | 10.0.3 |
| Microsoft.AspNetCore.SignalR.Client | 8.0.17 | 10.0.3 |
| System.Text.Json | 8.0.5 | 10.0.3 |
| System.Drawing.Common | 8.0.20 | 10.0.3 |
| System.Text.Encoding.CodePages | 8.0.0 | 10.0.3 |
| System.Configuration.ConfigurationManager | 8.0.1 | 10.0.3 |
| System.ServiceProcess.ServiceController | 8.0.1 | 10.0.3 |
| @microsoft/signalr (frontend) | 8.0.7 | 10.0.0 |

---

## 3. Major Upgrades — Breaking API Changes

These require code changes and carry significant risk. Each would be its own work item.

| Package | Current | Latest | Effort | Notes |
|---|---|---|---|---|
| NLog | 5.4.0 | 6.1.1 | Very High | 435 files use logging; pervasive |
| FluentValidation | 9.5.4 | 12.1.1 | Very High | 253 files; architectural |
| FluentMigrator.Runner.* | 6.2.0 | 8.0.1 | Very High | 148 migration files |
| Sentry | 4.0.2 | 6.1.0 | High | 16 files; integrated with NLog |
| Npgsql | 9.0.3 | 10.0.1 | Medium | Database driver |
| Swashbuckle.AspNetCore.* | 8.1.4 | 10.1.4 | Low | 3 files; surface-level |
| Ical.Net | 4.3.1 | 5.2.1 | Low | 1 file |
| NUnit | 3.14.0 | 4.5.1 | High | All test projects |
| FluentAssertions | 6.12.1 | 8.8.0 | High | Test.Common; all tests |
| RestSharp | 106.15.0 | 114.0.0 | Medium | 15 files; test-only |
| React | 18.3.1 | 19.2.4 | Very High | Entire frontend |
| react-router / react-router-dom | 5.2.0 | 7.13.1 | Very High | Complete API rewrite |
| redux / react-redux | 4.2.1 / 7.2.4 | 5.0.1 / 9.2.0 | Very High | State management layer |
| @fortawesome/* | 6.7.x | 7.2.0 | Medium | Icon library |
| eslint | 8.57.1 | 10.0.3 | Medium | Flat config migration |
| prettier | 2.8.8 | 3.8.1 | Low | Formatting only |
| stylelint | 15.6.1 | 17.4.0 | Low | CSS linting config |

---

## 4. Dependency Usage Audit

### 4.1 Dead Dependencies (zero imports — remove immediately)

| Package | Listed Version | Evidence |
|---|---|---|
| react-tabs | 4.3.0 | No imports found anywhere in `frontend/src/` |

**Action:** Remove from `package.json`, run `yarn install`.

### 4.2 Deeply Integrated — Keep

These are load-bearing. Replacing them would be a rewrite, not a refactor.

| Package | Files | Role | Verdict |
|---|---|---|---|
| Newtonsoft.Json | 113 | Primary JSON serializer, custom converters | Keep |
| FluentValidation | 253 | Validation across all settings/entities | Keep |
| FluentMigrator | 148 | All 242 database migrations | Keep |
| NLog | 435 | Logging in nearly every class | Keep |
| Dapper | 75 | All database access via repositories | Keep |
| lodash | 61 | Utilities (debounce, reduce, filter, merge, etc.) | Keep |
| redux-actions | 51 | `createAction()` in all Redux action files | Keep (see §4.4) |
| react-dnd + backends | 9 | Drag-and-drop for profiles, table columns | Keep |
| react-window | 4 | Movie index table, posters, overviews, import picker | Keep |
| react-virtualized | 3 | Discover movie posters/overviews | Keep |

### 4.3 Moderate Integration — Keep (not worth replacing)

| Package | Files | Role | Why Keep |
|---|---|---|---|
| Sentry | 16 | Error reporting, NLog integration | Important operational feature |
| MailKit | 2 | Email notifications via SMTP | `System.Net.Mail.SmtpClient` is deprecated |
| SixLabors.ImageSharp | 4 | Image resizing for covers | Cross-platform; `System.Drawing` is Windows-only |
| react-measure | 7 | DOM measurement with debouncing | Could use ResizeObserver but 7 call sites |
| react-popper | 5 | Tooltip/dropdown positioning | Positioning math is fiddly |
| react-slider | 4 | Quality profile range sliders | 3-thumb range slider is non-trivial |
| jquery | 7 | AJAX layer (`$.ajax`, `$.param`) | Moderate refactor to `fetch`; 7 files |
| react-focus-lock | 1 | Focus trap in Modal.tsx | Accessibility concern; keep for now |
| react-google-recaptcha | 1 | Captcha input component | Thin wrapper but handles callback lifecycle |

### 4.4 Light Usage — Candidates for Inlining

These dependencies are used in very few places and provide functionality that can be
written in a handful of lines. Removing them reduces supply-chain surface area.

#### Diacritical.Net → inline (~15 lines)

- **Used in:** 3 files (primarily `FileNameBuilder.cs`)
- **Purpose:** Strip diacritical marks (accents) from strings
- **Replacement:**
  ```csharp
  // Using System.Globalization
  static string RemoveDiacritics(string text)
  {
      var normalized = text.Normalize(NormalizationForm.FormD);
      var sb = new StringBuilder(normalized.Length);
      foreach (var c in normalized)
      {
          if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
              sb.Append(c);
      }
      return sb.ToString().Normalize(NormalizationForm.FormC);
  }
  ```
- **Risk:** Low
- **Recommendation:** **REMOVE** — standard .NET globalization handles this

#### IPAddressRange → inline (~25 lines)

- **Used in:** 2 files (`HttpProxySettingsProvider.cs`)
- **Purpose:** Parse CIDR notation and check if IP is in range
- **Replacement:** `System.Net.IPAddress` + manual CIDR mask comparison
  ```csharp
  static bool IsInRange(IPAddress address, string cidr)
  {
      var parts = cidr.Split('/');
      var network = IPAddress.Parse(parts[0]);
      var prefixLen = int.Parse(parts[1]);
      var networkBytes = network.GetAddressBytes();
      var addressBytes = address.GetAddressBytes();
      // Compare prefix bits...
  }
  ```
- **Risk:** Low
- **Recommendation:** **REMOVE** — very narrow usage for a whole package

#### filesize → inline (~15 lines)

- **Used in:** 2 files (`formatBytes.ts`, `formatBitrate.ts`)
- **Purpose:** Format byte counts for display (e.g., "1.5 GB")
- **Replacement:**
  ```typescript
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  function formatBytes(bytes: number, decimals = 2): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${(bytes / Math.pow(k, i)).toFixed(decimals)} ${units[i]}`;
  }
  ```
- **Risk:** Low
- **Recommendation:** **REMOVE** — trivial utility

#### qs → inline (~3 lines)

- **Used in:** 1 file (`parseUrl.js`)
- **Purpose:** Parse query string into object
- **Replacement:**
  ```typescript
  const params = Object.fromEntries(new URLSearchParams(search));
  ```
- **Risk:** Very low
- **Recommendation:** **REMOVE** — native API replacement

#### Polly → inline (~30 lines)

- **Used in:** 3 files (`DownloadClientBase.cs`)
- **Purpose:** HTTP retry with exponential backoff
- **Replacement:**
  ```csharp
  async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
  {
      for (var i = 0; i <= maxRetries; i++)
      {
          try { return await action(); }
          catch when (i < maxRetries)
          {
              await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
          }
      }
      throw new InvalidOperationException(); // unreachable
  }
  ```
- **Risk:** Low
- **Recommendation:** **OPTIONAL** — Polly is well-maintained and lightweight, but
  the usage here is narrow enough to inline

#### redux-actions → inline (~10 lines utility)

- **Used in:** 51 files, but only for `createAction(type, payloadCreator?)`
- **Purpose:** Returns `(payload) => ({ type, payload })` — a trivial wrapper
- **Replacement:**
  ```typescript
  function createAction(type: string, payloadCreator?: Function) {
    const creator = (...args: any[]) => ({
      type,
      payload: payloadCreator ? payloadCreator(...args) : args[0]
    });
    creator.toString = () => type;
    return creator;
  }
  ```
- **Risk:** Medium (51 files import it, but no API change needed if drop-in)
- **Recommendation:** **DEFER** — high touch count; do when Redux layer is refactored

#### history → inline

- **Used in:** 1 file (`Store/Actions/index.js`)
- **Purpose:** Browser history for React Router
- **Replacement:** React Router v5 `useHistory()` or v6 `useNavigate()`.
  Removing `history` package alone is trivial but couples to router upgrade.
- **Recommendation:** **DEFER** — remove when upgrading react-router

#### RestSharp (test-only) → HttpClient

- **Used in:** 15 files (integration tests only)
- **Purpose:** API client for integration test HTTP calls
- **Replacement:** `System.Net.Http.HttpClient` (built-in)
- **Risk:** Very low (tests only)
- **Recommendation:** **OPTIONAL** — low priority, no production impact

#### Swashbuckle → evaluate need

- **Used in:** 3 files (API project + Host)
- **Purpose:** Auto-generates Swagger/OpenAPI documentation
- **Replacement:** Could remove entirely if API docs aren't needed, or use
  `Microsoft.AspNetCore.OpenApi` (built-in from .NET 9+)
- **Recommendation:** **DEFER** — useful feature, revisit on .NET 10 migration

---

## 5. Recommendations Summary

### Immediate Actions (low risk, high value)

| # | Action | Effort | Status |
|---|---|---|---|
| 1 | Remove `react-tabs` from package.json (dead) | Trivial | **Done** |
| 2 | Inline `qs` → `URLSearchParams` (1 file) | Trivial | **Done** |
| 3 | Inline `filesize` → custom formatter (2 files) | Trivial | **Done** |
| 4 | Inline `Diacritical.Net` → `StringExtensions.RemoveAccent()` (2 files) | Low | **Done** |
| 5 | Inline `IPAddressRange` → manual CIDR check (1 file) | Low | **Done** |
| 6 | Apply all safe NuGet patch updates (§1) | Low | **Done** |
| 7 | Apply all safe Yarn patch/minor updates (§1) | Low | **Done** |

### Near-term (moderate effort)

| # | Action | Effort | Status |
|---|---|---|---|
| 8 | Inline `Polly` → custom retry helper (3 files) | Low | **Skipped** — kept; version bumped to 8.6.6 |
| 9 | Replace `jquery` → `fetch` API (7 files) | Medium | **Done** — code migrated to `fetch`; `jquery` kept in package.json for plugin extensibility |
| 10 | Update Swashbuckle 8.x → 10.x (3 files) | Medium | **Blocked** — incompatible with net8.0 |
| 11 | Update Ical.Net 4.x → 5.x (1 file) | Low | **Done** |

### Deferred (do alongside larger migrations)

| # | Action | Trigger |
|---|---|---|
| 12 | Remove `history` | When upgrading react-router |
| 13 | Inline `redux-actions` | When refactoring Redux layer |
| 14 | Remove `RestSharp` | When overhauling integration tests |
| 15 | Upgrade .NET-bound packages (§2) | When migrating to .NET 10 |
| 16 | Upgrade NLog 5→6, FluentValidation 9→12 | Dedicated migration sprint |
| 17 | Upgrade React 18→19, react-router 5→7 | Major frontend overhaul |

---

## 6. Supply-Chain Reduction Summary

Items 1–7, 9, and 11 completed. Removed **5 packages** (`react-tabs`, `qs`, `filesize`,
`Diacritical.Net`, `IPAddressRange`) from code and replaced with framework-native
implementations. Migrated all jQuery AJAX/param/Deferred usage to native `fetch`,
`URLSearchParams`, and `Promise` (jQuery kept in package.json for plugin extensibility).
Updated Ical.Net 4→5. Applied safe patch/minor updates to 11 NuGet and 21 Yarn packages.

Item 8 (Polly) skipped — kept as-is with version bump to 8.6.6; usage is narrow but the
retry strategies involve complex match predicates better served by the library.
Item 10 (Swashbuckle) blocked — v10 requires OpenAPI.NET v2 which is incompatible with
.NET 8; defer to .NET 10 migration.
