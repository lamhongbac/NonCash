# Session Log — 2026-09-07

## CR-10+11+02+14 (Sóng 1) — Customer fill-empty-only + audit trail
- **Status**: Dev-complete, pending user verification
- Migration `20260907054508_AddCustomerAuditLogs` applied to dev DB (Host=45.119.87.247)
- Verification guide delivered to user

## CR-18 Phase 1 — POS app backend foundation
- **Status**: Dev-complete 2026-09-07

### Changes delivered
1. **Entities**: `Outlet.Code` (short store code, unique per brand), `UserRole.StoreStaff`, `UserOutlet` join table (staff↔outlet assignments), `VoucherUsage.PosNo` + `OperatorId` (nullable, CR-18(C) contract extension)
2. **Configurations**: OutletConfiguration (unique composite index `brand_id + code`), UserOutletConfiguration (table `user_outlet_assignments`, unique `user_id + outlet_id`), VoucherUsageConfiguration (pos_no, operator_id columns)
3. **Repositories**: `IUserOutletRepository` + implementation (GetOutletsForUser, GetUsersForOutlet, ReplaceAssignments), `IOutletRepository.GetByCodeAsync` (case-insensitive, Active-only, brand-scoped)
4. **Services**: `StoreStaffService` (CRUD with brand-scope security — callerBrandId from JWT, never trusted from request body), `AuthService.LoginStaffAsync` (generic error message on all failure paths to prevent enumeration)
5. **JWT**: New overload `GenerateToken(UserAccount, Guid outletId)` adds `outlet_id` claim for staff sessions
6. **Controllers**: `StoreStaffController` (CRUD endpoints, Authorize Admin+BrandManager, BrandManager-only create/update/lock/unlock), `AuthController.staff-login` (AllowAnonymous, rate-limited 5 req/min per IP via ASP.NET Core `AddFixedWindowLimiter`)
7. **UI**: `BrandManager/StoreStaff.razor` (list/create/edit/lock/unlock/delete with outlet multi-select), nav item added to MainLayout
8. **Migration**: `20260907074128_AddCR18StoreStaffAndAuditLog` applied to dev DB
9. **Tests**: 127 unit tests (+28 new: StoreStaffServiceTests + AuthService.LoginStaffAsync tests), 126 integration tests — all green

### Migration disaster recovery
- `dotnet ef migrations remove --force` removed wrong migration twice (removed `AddCustomerAuditLogs` and `AddVoucherDistributionBatches` instead of the intended target)
- Recovery: restored deleted files from git, manually edited combined migration to exclude already-applied `customer_audit_logs` CreateTable, fixed HasFilter syntax (`"Code"` → `code` for PostgreSQL lowercase column convention)
- Lesson learned: never use `dotnet ef migrations remove --force` when the target migration isn't the actual last one in the compiled assembly; `--no-build` can reference stale binaries

### Files created/modified (key)
- `src/NonCash.Core/Entities/UserOutlet.cs` (new)
- `src/NonCash.Core/Entities/Outlet.cs` (Code property)
- `src/NonCash.Core/Entities/UserAccount.cs` (StoreStaff enum)
- `src/NonCash.Core/Entities/VoucherUsage.cs` (PosNo, OperatorId)
- `src/NonCash.Core/Services/StoreStaffService.cs` (new, 174 lines)
- `src/NonCash.Core/Services/AuthService.cs` (LoginStaffAsync)
- `src/NonCash.Core/Interfaces/IUserOutletRepository.cs` (new)
- `src/NonCash.API/Controllers/StoreStaffController.cs` (new, 241 lines)
- `src/NonCash.API/Controllers/AuthController.cs` (staff-login endpoint)
- `src/NonCash.API/DTOs/StoreStaffDtos.cs` (new)
- `src/NonCash.API/Program.cs` (rate limiter, DI registrations)
- `src/NonCash.Web/Components/Pages/BrandManager/StoreStaff.razor` (new, 404 lines)
- `src/NonCash.Web/Components/Layout/MainLayout.razor` (nav item)
- `tests/NonCash.UnitTests/Services/StoreStaffServiceTests.cs` (new, 278 lines)
- `tests/NonCash.UnitTests/Services/AuthServiceTests.cs` (+109 lines)

### Phase 2 (pending)
- NonCash.Pos PWA app (Razor Class Library, scan-first UI)
- Shift management, end-of-day reconciliation
- Receipt printing, cash drawer integration (MAUI Blazor Hybrid)
