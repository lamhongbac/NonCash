# Session Log — 2026-08-22

## 1. Customer Import: Root-Caused the Two Failing Files

- `Top100_CustomerImport.csv`: header was `Mobile` instead of `Phone` → phone column never mapped → every row errored.
- `top100.csv`: file was **tab-separated** (Excel "Text (Tab delimited)" export) → each line parsed as one column `Phone\tFullName\tEmail` → raw CsvHelper exception `No members are mapped...` shown to the user.

## 2. Customer Import Overhaul (template-first, xlsx + hardened CSV)

- Added **ClosedXML 0.104.2** to `NonCash.Infrastructure`.
- `ICustomerImportService.ImportFromCsvAsync` renamed to `ImportAsync`; content sniffing: ZIP magic bytes → read as Excel via ClosedXML, otherwise CSV.
- New `CustomerImportTemplate.BuildXlsx()` — Excel template (`Phone, FullName, Email`, bold header, phone column text-formatted to keep leading zeros, 2 sample rows).
- CSV hardening in `CsvCustomerImportService`:
  - delimiter auto-detect (tab / comma / semicolon),
  - header aliases (`Mobile`, `Phone`, `SĐT`, `Họ tên`, `Name`, … incl. Vietnamese),
  - headerless files → positional mapping,
  - `NULL` / `N/A` / `-` cells treated as empty,
  - fatal parse failures throw new `CustomerImportParseException` (Core) with user-facing "use the template" guidance.
- `CustomersController`: removed the old `.csv`-only extension guard (it blocked `.xlsx` — user hit "Only CSV files are supported"); catches `CustomerImportParseException` → friendly 400.
- `Customers.razor`: "Download Excel template" button, accept `.csv,.xlsx`, snackbar extracts the `error` text from JSON instead of dumping raw body.
- Tests: 5 new (`CustomerImportServiceTests`), suite 61 → 66.
- Verified the user's two real files via throwaway `tools/import-check` console tool: **99 created + 1 error each** (row 45 — `trami2105873@yahoo.com.vn` genuinely duplicated on rows 44/45; logged and continued as designed).

## 3. Batch Promotion: Same Template Treatment

- New `RecipientListTemplate.BuildXlsx()` — single column `Phone / Email` template.
- New `RecipientFileReader.ReadAsync` — xlsx (header alias detected, first column used) + text (previous header heuristic, tab/comma/semicolon).
- `PromotionsController.Promote` now uses the reader; parse failures → friendly 400 (`error: Validation`).
- `PlanVouchers.razor`: "Download template" button next to Choose File, accept `.csv,.txt,.xlsx`, correct media type per extension, friendly error snackbar.
- Tests: 4 new (`RecipientFileReaderTests`), suite 66 → 70.

## 4. Dev-Mode Email Kill-Switch

User fear: test runs sending real emails to real customers.

- Pre-existing but unenforced: `Environment:Name = "dev"` in both API appsettings (bound by `EnvironmentConfig`), plus `Notifications:EmailEnabled` flag; `appsettings.Development.json` had `EmailEnabled: true` **with live SMTP credentials** → dev runs really sent mail.
- Now enforced at two layers:
  1. `Program.cs`: `EnvironmentConfig.IsDev` → always registers `ConsoleNotificationService`, regardless of SMTP/`EmailEnabled`.
  2. `EmailNotificationService.SendAsync`: dev guard suppresses delivery even if registered directly; writes audit row to `email_logs` (`Success=false`, `ErrorMessage="Suppressed: dev mode (Environment:Name=dev)"`).
- Fail-safe: missing `Environment:Name` is treated as dev (no email).
- `appsettings.Development.json`: `EmailEnabled` flipped to `false`.
- Documented in `docs/notification-matrix.md` ("Dev Mode Kill-Switch" section).
- Test: `SendAsync_DevMode_SuppressesDeliveryEvenWithSmtpConfigured`; existing `SendAsync_SkipsWhenSmtpHostEmpty` pinned to `Environment:Name=production` config. Suite 70 → 71.

### Switching modes (operational)

- Real-send test: edit `src\NonCash.API\appsettings.Development.json` → `"Environment": { "Name": "production" }` AND `"Notifications": { "EmailEnabled": true }`, restart API. (Base `appsettings.json` alone won't work under the Development profile; `EmailEnabled=false` alone still routes to console sink.)
- Back to safe: `Name=dev`, `EmailEnabled=false`. Env-var override also works: `Environment__Name=production`.
- While name ≠ dev, ALL notifications (registration, approvals, resets…) send real mail — keep the window short.

## 5. Mode Badge in the Web UI

- New `SystemController` — `GET /api/v1/system/info` (`[AllowAnonymous]`) → `{ environment, emailDelivery }`; `emailDelivery = !isDev && smtpHost set && emailEnabled`.
- `MainLayout.razor` fetches it on init (best-effort, never breaks layout) and shows an app-bar chip:
  - amber **`DEV MODE - emails off`** when dev,
  - red **`PRODUCTION - real emails ON`** otherwise.
- Badge reads the live API config — single source of truth, no second config to forget. Refresh browser after API restart to see the change.

## 6. Build / Test Status

- API + Web build with 0 errors; unit suite **71/71 passing**.
- Builds verified via temp output dirs `build_tmp_api`, `build_tmp_web`, `build_tmp_tests` (VS file-lock workaround; safe to delete, as is `tools/import-check`).

## 7. Open / Next Steps

- User's in-flight plan: switch to `production`, distribute a voucher to their own email to verify real delivery end-to-end, then switch back to `dev` and run the import-file test. Badge should show red during the first phase, amber after.
- Standing instruction honored: session log written only because the user explicitly asked at session end.
