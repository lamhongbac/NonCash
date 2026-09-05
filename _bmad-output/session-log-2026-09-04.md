# Session Log — 2026-09-04

## Context
- Continuation of task-d8b (Customer Model Redesign — Brand-Customer Mapping). Plan implementation was completed in the prior session (UnitTests 77/77, IntegrationTests 91/91, docs + sprint-status updated).
- This session: runtime error diagnosis + deployment of the pending migration + empty-state hardening.

## Work done

### 1. Web error "No connection could be made ... (localhost:7107)"
- Mechanism: Blazor Web calls API at `https://localhost:7107/` (Web appsettings); error = no process listening on 7107.
- Verified causes: (a) API started with `http` launch profile (only opens 5200) or not started; (b) API crash at startup — `Program.cs:227` seeds admin BEFORE `app.Run()`, unhandled DB failure kills the process (local PostgreSQL service is Stopped; non-Development env falls back to `Host=localhost` conn string at Program.cs:38); (c) with Development env + correct content root the API starts fine against remote DB.
- Fix kept (user waived rollback): `src/NonCash.API/Properties/launchSettings.json` reordered so `https` profile (7107+5200) is first → default for `dotnet run` / fresh VS load.

### 2. New working rule (user mandate, stored in memory)
- Before ANY change: present a change-proposal TABLE (DB/tables/migrations, logic, config files, impact/risk) and wait for explicit approval. Read-only diagnostics need no approval. Past unapproved changes are NOT reverted.

### 3. BrandManager Customers page error (500)
- Root cause: migration `20260903100953_AddBrandCustomerMapping` never applied to remote dev DB → table `brand_customers` missing → Postgres 42P01 on brand-scoped query (`CustomerRepository.BuildSearchQuery` EXISTS subquery) → API 500 → Blazor snackbar error.
- Diagnosis evidence (psql, read-only): `to_regclass('public.brand_customers')` = NULL; latest applied migration = `20260822032348_AddUniqueCustomerEmailIndex`.
- psql gotchas: `__EFMigrationsHistory` / `MigrationId` need double quotes (shell eats them — escape with backticks in PowerShell); use `information_schema.tables ... ILIKE` when quoting fails.

### 4. Approved fixes (user: "duyệt tất cả") — all applied
1. DB: `dotnet ef database update --project src\NonCash.Infrastructure --startup-project src\NonCash.API --connection "Host=45.119.87.247;...SSL Mode=Require"` (needs OUTSIDE sandbox: EF tools spawn child processes). Applied; backfill D1 verified: 104/104 customers → brand HighLand (`01a023ec-521a-7e06-bba3-9f28ad53d011`).
2. `src/NonCash.Web/Components/Pages/BrandManager/Customers.razor`: MudTable `NoRecordsContent` = "No customers yet — use Add or Import..."; MudPagination hidden when `_totalCount == 0`; `LoadCustomers` shows API error body (ExtractErrorMessage) instead of raw status text.
3. `src/NonCash.API/Controllers/CustomersController.cs`: `using Npgsql;`; GET list + GET by id wrap brand-scoped reads in `catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)` → `SchemaOutdated()` = 503 + clear message. NOTE: helper must be non-static (`StatusCode()` is an instance method).
4. Verification: API/Web builds 0 errors; UnitTests 77/77; IntegrationTests 91/91; live check with minted BrandManager JWT (HS256, key/issuer/audience from appsettings `Jwt` section; claims `brand_id`, ClaimTypes.Role=BrandManager, sub/nameidentifier): HighLand → HTTP 200 totalCount=104; brand `01a02744-82b5-7efe-aa31-3cfec6b6ef04` (0 mappings) → HTTP 200 totalCount=0. Temp mint script deleted; test API instance killed; port 7107 free.

## State at session end
- ALL changes UNCOMMITTED in working tree: task-d8b code/tests/docs, launchSettings reorder, `appsettings.Development.json` EmailEnabled=false (from 2026-08-22 session), today's controller/razor fixes. Migration IS applied to remote DB (schema ahead of git history is fine).
- User restarting machine. On return: start API (`https` profile) + Web, login as BrandManager → Customers page should list 104 HighLand customers; a brand with no mappings shows the empty-state message, no error.
- No pending implementation work beyond user's own UI verification; commit only if user asks.

## Follow-up (same day, after machine restart): root-cause of recurring "connection refused 7107"
- User hypothesis "must edit port in appsettings after every restart" DISPROVED: Web appsettings untouched since commit 7829b38 (20 Aug); connection strings unchanged; only config diff = launchSettings profile reorder (names unchanged, harmless). `.suo` shows multiple startup projects = API + Web both Start, so VS does launch the API.
- Real root cause: `Program.cs` seeded admin BEFORE `app.Run()` with no retry — whenever the DB/network is not ready at boot (machine restart, DB service restart, slow network) the unhandled exception killed the API before it bound 7107, so Web showed connection-refused for the whole session. Secondary: cold-build race (Web ready before API binds) → transient refused, fixed by refresh.
- FIX APPLIED (approved): `Program.cs` seed wrapped in retry loop — 5 attempts, 3s apart, `LogWarning("Database not ready for seeding (attempt {n}/5)...")`; 5th failure still exits but now with a visible log trail.
- Verified: build 0 errors; dead-DB simulation (`ConnectionStrings__DevConnection` → port 59999; NOTE the env key is DevConnection, not DefaultConnection, see Program.cs:27-33) printed attempts 1-4 warnings then clear unhandled exception; normal run printed "Now listening on: https://localhost:7107" with no warnings; Unit 77/77, Integration 91/91. Test instance killed, port 7107 free.
- OPERATIONAL NOTE: after reboot / DB service restart, wait until the API console prints "Now listening on: https://localhost:7107" before using Web; if Web shows connection-refused while API is still building, refresh the page. If the API console shows repeated "Database not ready for seeding" warnings, the DB/network is the problem, not the app.

## Resume cues
- Builds/tests to temp dirs (VS file locks): `dotnet build src\NonCash.API -o build_tmp_api`, `... src\NonCash.Web -o build_tmp_web`, `dotnet test tests\NonCash.UnitTests -o build_tmp_tests`, `dotnet test tests\NonCash.IntegrationTests -o tmp_build_it`.
- Run API from temp build: `$env:ASPNETCORE_CONTENTROOT="d:\GIT PROJECT\NonCash\src\NonCash.API"; dotnet build_tmp_api\NonCash.API.dll --urls "https://localhost:7107;http://localhost:5200"`.
- DB checks: `psql -h 45.119.87.247 -U noncash_app -d noncash` (password in appsettings Development ConnectionStrings).
- Killing a background dotnet API: `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | ? CommandLine -like '*NonCash.API.dll*' | Stop-Process` (requires outside-sandbox permission).

## Follow-up (same day): email kill-switch — log-only in dev, real send when enabled
- Requirement: distributing to "many customers" via the Email channel must NOT send email in the current (dev) environment, but MUST log that "the email step was executed"; flipping email mode on must actually send.
- Root problem found: dev was hard-registered to `ConsoleNotificationService` regardless of the flag, so `Notifications:EmailEnabled` had no effect; and the console line said "delivered", which is misleading.
- FIX APPLIED (approved, 3 code files + 1 test file):
  1. `src/NonCash.API/Program.cs` (124-139): single email toggle `emailEnabled = Configuration.GetValue<bool?>("Notifications:EmailEnabled") ?? !environmentConfig.IsDev` (explicit flag wins everywhere; absent => dev suppressed, other envs enabled). Registers `EmailNotificationService` only when `emailEnabled && Smtp:Host` present, else `ConsoleNotificationService`.
  2. `src/NonCash.API/Controllers/SystemController.cs` (26-32): `EmailDelivery` badge now computed from the SAME toggle (was `!IsDev && smtpHost`), so the UI badge matches the registered sink.
  3. `src/NonCash.Infrastructure/Services/ConsoleNotificationService.cs`: optional `ILogger<ConsoleNotificationService>` injected (parameterless ctor still valid for existing call sites). On the Email channel it now logs `[EMAIL SIMULATED] Email send executed (not delivered - console sink active): to {Email}...` when an email is on file, or `[EMAIL SIMULATED] No email on file - email step skipped...` when not. The old "delivered" console line is unchanged for other channels.
  4. `tests/NonCash.UnitTests/Services/ConsoleNotificationServiceTests.cs` (NEW, 4 tests, CapturingLogger): Email channel => logs simulated-send containing email+phone; Email channel without email => logs skip; non-Email channel (Zalo) => no simulated-send line; parameterless ctor => does not throw.
- Verified: API build 0 errors/0 warnings; Unit 85/85 (was 81, +4 new); Integration 91/91. Dev config unchanged (`Notifications:EmailEnabled=false`, `Smtp:Host=smtp.gmail.com` present) => dev resolves to console sink (log-only). To actually send: set `Notifications:EmailEnabled=true` (SMTP already configured) and restart the API.
- OPERATIONAL NOTE: this is a startup-DI decision (sink chosen at host build). Changing the flag requires an API restart to take effect; the `/api/v1/system/info` `emailDelivery` boolean reflects the effective mode.

## Follow-up (same day): Distribute Vouchers made a reusable component + voucher-scope semantics clarified
- User issue: distribution ("Batch Promotion") lived only inside Generate Vouchers, so sending vouchers always required opening that page; also a plan created "for 1 store" appeared to apply to all in View.
- Scope semantics CLARIFIED BY OWNER (corrects an earlier wrong assumption of mine): a plan's applicability is HIERARCHICAL — Business(company) -> Brand -> Outlet. Empty scope at a level = "all under that level" (no outlets => whole brand; no brands => whole company). Cross-brand AND cross-company co-marketing vouchers are legitimate. Target model = 3 NULLABLE collections on the plan header (companies / brands / outlets); scope is set during Create/Draft (Pending), and Approval is approve/reject only (never changes scope).
- Confirmed current-code GAPS (NOT fixed — deferred to a dedicated "voucher scope epic"): `VoucherPlanHeader` has only single `BrandId` + `SponsorBrandId` (no company, no multi-brand); `PosService.VerifyAsync` (~L319) rejects any outlet not in `plan_outlets`, so an EMPTY scope is currently redeemable NOWHERE (opposite of intended). Both dev-DB plans have 0 `plan_outlets` rows.
- IMPLEMENTED NOW (approved, Web frontend only — NO DB/backend/redeem change):
  1. NEW `src/NonCash.Web/Components/Shared/DistributeVouchers.razor` — reusable Batch Promotion card extracted verbatim from PlanVouchers (phone/email field, Email/Zalo/Both radio, template download, InputFile, Distribute Now, result + skipped table; POST api/v1/plans/{PlanId}/promote). Params: `PlanId`, `OnDistributed` (EventCallback), `ShowHeader`; resets state in OnParametersSet when PlanId changes. Uses InputFile (not MudFileUpload — MudBlazor 9.4.0).
  2. `src/NonCash.Web/Components/Pages/BrandManager/PlanVouchers.razor` — replaced the inline Batch Promotion block (~179 lines removed) with `<DistributeVouchers PlanId="@PlanId" OnDistributed="LoadVouchers" />`; dropped now-unused `IJSRuntime`/`NonCash.Infrastructure.Services` + duplicated DTOs/helpers. Generate Vouchers + voucher table unchanged.
  3. NEW `src/NonCash.Web/Components/Pages/BrandManager/Distribute.razor` (route `/brandmanager/distribute`) — pick an Approved plan (MudSelect from GET api/v1/plans?status=Approved) then embed `<DistributeVouchers ShowHeader="false">`.
  4. `src/NonCash.Web/Components/Layout/MainLayout.razor` — added "Distribute Vouchers" nav link (Campaign icon) in the Brand Manager group.
- Result: distribution now reachable in 2 places (inside Generate Vouchers as before + a standalone "Distribute Vouchers" menu), satisfying "don't have to enter Generate to send". A4 (embed in a read-only View) deferred into the scope epic so View is built once with correct scope display.
- Verified: Web build 0 errors (25 pre-existing warnings, none from the new/edited files); Integration 91/91.
- NEXT (when user ready): switch to Plan mode to design the "voucher scope epic" (3 nullable scope collections + migration + cascading company->brand->outlet create/edit UI + hierarchy-aware POS redeem + settlement/permissions). Owner decisions captured in memory (id abd781c5).

## Follow-up (same day): Epic 3 — voucher scope STORAGE (jsonb) implemented
- Owner scoped the epic to 2 tasks: (1) change table structure to store 3 scope attributes (companies/brands/outlets); (2) create-plan derives 1 company + 1 brand + outlet list (may be empty). REDEEM hierarchy logic + View display (A4) explicitly DEFERRED.
- Design evolution (owner-driven, 3 rounds): rejected "3 relational tables" -> rejected "1 relational plan_scope table" -> FINAL = single **jsonb `scope` column on `voucher_plan_headers`** (1 plan = 1 row). Referential integrity enforced in application logic, NOT PK-FK. API DTO contract (`OutletIds`) and Web UI UNCHANGED.
- Model: NEW `src/NonCash.Core/Entities/VoucherScope.cs` POCO = `List<Guid> Companies/Brands/Outlets` (all default `new()`). SEMANTIC REFINEMENT vs the earlier "3 nullable collections" note: collections are NON-null; an **empty list = "all under that level"** (empty Outlets = whole brand). `VoucherPlanHeader.Scope` replaces the removed `PlanOutlet` class + `PlanOutlets` nav.
- KEY TECHNICAL DECISION (self-caught before applying migration): EF Core owned-entity `.ToJson()` is RELATIONAL-ONLY (SQL Server/PostgreSQL) and would break the mixed-provider integration tests (SQLite: Credits/Transfer/BrandCustomerEnforcement; InMemory: Auth/Brands/Outlets/Customers/CreditService). Switched to provider-agnostic `ValueConverter<VoucherScope,string>` + `ValueComparer` + `.HasColumnType("jsonb")` in `VoucherPlanHeaderConfiguration`. Npgsql stores real jsonb; SQLite stores TEXT (lenient affinity); InMemory ignores column type. PascalCase keys (`JsonSerializerDefaults.General`) — backfill SQL must match.
- Create/Update derive scope server-side via `VoucherPlanService.BuildScope(brand, brandId, dto.OutletIds)`: `Companies=[brand.BusinessId]`, `Brands=[brandId]`, `Outlets=dto.OutletIds`. Injected `IBrandRepository`; Create now single SaveChanges. `PosService` (~L319) + `PlanCloneService` compile-touched only (redeem still checks explicit `Scope.Outlets`; behavior preserved). `VoucherPlanRepository` dropped 3x `.Include(PlanOutlets)`. `VoucherPlansController.MapToResponse` -> `p.Scope.Outlets`. `ApplicationDbContext` dropped `DbSet<PlanOutlet>` (snake_case loop auto-renames `Scope`->`scope`).
- Migration `20260904100644_AddVoucherScopeJson` — auto-generated version had 2 bugs, HAND-EDITED: (a) it dropped `plan_outlets` BEFORE backfill (data loss) -> reordered AddColumn -> backfill -> DropTable; (b) `defaultValue: ""` is INVALID jsonb (empty string fails the NOT NULL ALTER) -> `defaultValueSql: '{"Companies":[],"Brands":[],"Outlets":[]}'::jsonb`, then `ALTER COLUMN scope DROP DEFAULT` after backfill so the column matches the EF model. `Down()` recreates plan_outlets + restores outlets from `scope->'Outlets'` (jsonb_array_elements_text) before dropping the column.
- Applied to remote dev DB (`dotnet ef` needs `NONCASH_CONNECTION_STRING` env var — the design-time factory defaults to localhost, and `migrations remove`/`database update` DO connect to check history). Verified via psql: 2 plans backfilled `{Companies:[brand.business_id], Brands:[brand_id], Outlets:[]}` (plan_outlets had 0 rows), column jsonb NOT NULL no default, plan_outlets table gone.
- Verified: Infrastructure + API build 0 errors/0 warnings; Unit 88/88 (+3 NEW `VoucherPlanServiceTests`: create-with-outlets, create-empty-outlets, update-refreshes-scope); Integration 91/91. `dotnet ef` built the model against Npgsql OK = the string+jsonb+converter mapping is valid on Postgres.
- STATE: ALL Epic 3 changes UNCOMMITTED in working tree; migration IS applied to remote dev DB (schema ahead of git history is fine). NOT committed (user hasn't asked). Deferred: hierarchy-aware POS redeem + View scope display (A4) + cascading company->brand->outlet create/edit UI.

## Follow-up (same day, wrap-up): committed everything + Direction A proposed (PENDING approval)

### Commit — first since `43031be` (21 Aug)
- `ea3f4d2` "Work since Aug 21: voucher scope jsonb (Epic 3), brand-customer mapping, email kill-switch, Distribute component, media upload, pricing docs; untrack build-output dirs". Working tree CLEAN. NOT pushed (user hasn't asked).
- Same commit did housekeeping: `.gitignore` += `tmp_build_*/`, `build_tmp_*/`; `git rm -r --cached tmp_build_api tmp_build_web` (untracked the previously-committed build binaries; files kept on disk). This SUPERSEDES the "ALL UNCOMMITTED" notes above (L29, L79): task-d8b, email kill-switch, Distribute, Epic 3 scope + migrations 20260822/20260903/20260904 + the cumulative snapshot are all committed together (the 3 migrations share ONE `ApplicationDbContextModelSnapshot.cs`, so they could NOT be split into separate commits).
- GOTCHA (verified): git WRITE ops (`rm`/`add`/`commit`) are DENIED inside the sandbox ("Access is denied"); git READS (`status`/`log`) work. Commits must run OUTSIDE the sandbox (required_permissions=all).

### Direction A — hierarchy-aware POS redeem: INVESTIGATED, proposal PENDING approval (NOT implemented)
- BUG at `src/NonCash.Core/Services/PosService.cs` `ValidateCoreAsync` (~L319-325): `if (!plan.Scope.Outlets.Contains(outletId)) -> "OutletNotAuthorized"`. `BuildScope` gives whole-brand plans EMPTY `Outlets`, so such plans are currently redeemable NOWHERE. Check #1 just above (`outlet.BrandId != plan.BrandId -> reject`) already pins redemption to the plan's OWN brand.
- Invariants: `VoucherPlanService.BuildScope` always sets `Companies=[brand.BusinessId]`, `Brands=[brandId]`, `Outlets=dto.OutletIds(may be empty)` -> Brands/Companies are always the owner, so their cascade levels are redundant WHILE check #1 stays. `VoucherPlanRepository.GetByIdWithOutletsAsync` loads the header incl. `Scope` (legacy misnomer, functionally fine). STALE doc: `VoucherScope.cs` L9 still says "owned entity mapped with ToJson" (should be ValueConverter).
- Test gap: only redeem tests live in `tests/NonCash.IntegrationTests/Controllers/CreditsControllerTests.cs` (`SeedPlan` sets `Outlets=[_outletId]`, NON-empty) -> pass today, won't break, but the EMPTY-Outlets (whole-brand) path is UNCOVERED. No dedicated POS test file exists.
- PROPOSED CHANGE (await user go — DO NOT implement until approved):
  1. `Core/Entities/VoucherScope.cs`: add pure `bool CoversOutlet(outletId, outletBrandId, companyBusinessId)` = cascade Outlets->Brands->Companies (empty level = all under it); fix the stale ToJson doc.
  2. `Core/Services/PosService.cs`: move the EXISTING brand load above the scope check (reuse, no extra query); replace the buggy line with `if (!plan.Scope.CoversOutlet(outletId, outlet.BrandId, brand.BusinessId)) reject`; drop the now-duplicate brand load below; KEEP check #1.
  3. NEW `tests/NonCash.UnitTests/Entities/VoucherScopeTests.cs`: 7 cases (membership per level + fall-through when empty + all-empty).
  4. NEW `tests/NonCash.IntegrationTests/Controllers/PosRedeemScopeTests.cs`: empty-Outlets plan -> LockAsync succeeds; Outlets=[other] -> mismatch rejected; other-brand outlet -> rejected by check #1.
  - NO DB migration (pure logic + tests). Then build + run Unit & Integration (expect green).
- DECISIONS recommended (pending confirm): D1 KEEP check #1 (same-brand only) — relaxing is inert until Direction B authoring UI and drags in cross-tenant settlement; D2 extracted `CoversOutlet` helper (A2) over one-line inline fix (A1). Assumptions: all-empty scope => whole owning brand (still gated by check #1); dedicated `PosRedeemScopeTests.cs` over bloating CreditsControllerTests.

### Resume tomorrow
1. Get approval on the Direction A proposal (or adjustments) -> implement rows 1-4 -> build -> run Unit + Integration.
2. This log update + Direction A code will be UNCOMMITTED -> commit when the user asks (remember: git writes run OUTSIDE the sandbox).
3. Other directions still open: B (scope-authoring UI + View display + relax check #1 for true multi-brand), C ("approve with policy" business activation), D (backlog epics: 7 settlement/sponsorship, 8 display, pricing analytics).
