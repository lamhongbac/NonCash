# Story 1.2: Cau hinh Diem ban hang (Outlet Configuration)

Status: review

## Story

As a Brand Manager,
I want to create and manage Outlets (stores) belonging to my Brand,
So that I can accurately configure the physical locations where vouchers will be accepted.

## Acceptance Criteria

**AC1: Outlet Creation**
Given a Brand Manager is operating within their BrandID scope
When they add a new Outlet
Then the system stores the Outlet tied to the current `BrandID`
And generates a preview API Key for POS integration

**AC2: Outlet Data Model**
Given the Outlet entity is implemented
When persisted
Then it stores: `OutletID` (PK, GUID), `BrandID` (FK, GUID, indexed), `Name` (required), `Address` (nullable), `Status` (Active | Closed), `ApiKeyPrefix` (string, nullable until full key generation in later story)

**AC3: Outlet List Scoped by Brand**
Given a Brand Manager is logged in
When they view the Outlet list
Then only Outlets with matching `BrandID` are returned
And the list supports pagination and filtering by `Status`

**AC4: Outlet Update / Close**
Given an existing Outlet
When the Brand Manager updates details or sets Status to Closed
Then changes persist
And existing VoucherPlanHeader `SalesRange` references to this Outlet remain intact (historical reference, not cascade delete)

**AC5: API Key Placeholder**
Given a new Outlet is created
When the record is saved
Then the system generates an `ApiKeyPrefix` (first 8 chars of a GUID or random hex) as a placeholder for future full API Key provisioning

## Tasks / Subtasks

- [x] Task 1: Define Outlet domain model (AC2)
  - [x] Subtask 1.1: `Outlet.cs` entity in `NonCash.Core/Entities/`
  - [x] Subtask 1.2: `Brand -> Outlet` one-to-many navigation
  - [x] Subtask 1.3: EF FluentAPI configuration with `BrandID` index
- [x] Task 2: Implement Outlet service & repository (AC1, AC3, AC4)
  - [x] Subtask 2.1: `IOutletRepository` with `ListByBrandAsync(Guid brandId, ...)`
  - [x] Subtask 2.2: `OutletService` enforcing BrandID scoping from current user context
  - [x] Subtask 2.3: Soft-close logic (status change, not physical delete)
- [x] Task 3: API endpoints (AC1, AC3)
  - [x] Subtask 3.1: `OutletsController` under `NonCash.API/Controllers/`
  - [x] Subtask 3.2: DTOs: `CreateOutletRequest`, `OutletResponse`, `UpdateOutletRequest`
  - [x] Subtask 3.3: Routes: `GET /api/v1/outlets`, `POST /api/v1/outlets`, `PUT /api/v1/outlets/{id}`, `GET /api/v1/outlets/{id}`
- [x] Task 4: Blazor UI (AC1, AC3)
  - [x] Subtask 4.1: `Outlets.razor` under `NonCash.Web/Pages/BrandManager/`
  - [x] Subtask 4.2: Outlet form component with validation
- [x] Task 5: Database migration
  - [x] Subtask 5.1: Add `outlets` table migration
  - [ ] Subtask 5.2: Seed sample outlet data for local development (optional)
- [x] Task 6: Tests
  - [x] Subtask 6.1: Unit tests for `OutletService` — Brand scoping, soft-close
  - [x] Subtask 6.2: Integration tests for controller — 403 when accessing other Brand's outlets

## Dev Notes

### Architecture Compliance
- **Multi-tenancy is CRITICAL here**: Every Outlet query MUST include `.Where(o => o.BrandID == currentBrandId)`.
- The current user's `BrandID` comes from JWT claims. The Service layer receives it as a parameter or from an `ICurrentUserService` abstraction (prefer `ICurrentUserService` in Core, implemented in API/Web).
- Do NOT allow physical deletion of Outlets; use `Status = Closed` to preserve historical plan references.

### File Structure Requirements
```
src/NonCash.Core/Entities/Outlet.cs
src/NonCash.Core/Interfaces/IOutletRepository.cs
src/NonCash.Core/Interfaces/ICurrentUserService.cs
src/NonCash.Core/Services/OutletService.cs
src/NonCash.Infrastructure/Data/Configurations/OutletConfiguration.cs
src/NonCash.API/Services/CurrentUserService.cs
src/NonCash.API/Controllers/OutletsController.cs
src/NonCash.Web/Pages/BrandManager/Outlets.razor
```

### Database Schema
- Table: `outlets`
- Columns: `outlet_id` (uuid PK), `brand_id` (uuid FK not null), `name` (varchar 200 not null), `address` (text), `status` (varchar 20), `api_key_prefix` (varchar 16), `created_at` (timestamptz), `updated_at` (timestamptz)
- Index: `IX_outlets_brand_id`, `IX_outlets_status`
- FK: `FK_outlets_brands_brand_id` (no cascade delete on Brand — restrict or set null depending on business rule; default to RESTRICT)

### API Contracts
- Base path: `/api/v1/outlets`
- Auth: JWT Bearer (BrandManager, Admin roles)
- The controller MUST extract `BrandID` from the user's JWT claim and pass it to the service. Never trust a `BrandID` from the request body for creation.

### Security & NFR
- NFR4 (Multi-tenancy): Strict Brand isolation. Integration tests must verify a BrandManager cannot read/write another Brand's outlets.
- NFR3 (RBAC): `BrandManager` and `System Admin` roles allowed. `Planner` and `Approver` roles do NOT manage outlets.

### Testing Standards
- Mock `ICurrentUserService` in unit tests to return a fixed BrandID.
- Integration test: create two Brands, two Outlets, assert cross-Brand access returns 403 or empty list.

### References
- [Source: docs/data-models.md#Outlet] — Entity definition.
- [Source: Key Functionalities.txt#I] — Pham vi ban hang (Sales Range) context.
- [Source: docs/architecture.md#Security Architecture] — Multi-tenancy via BrandID.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

