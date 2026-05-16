# Story 1.1: Thiet lap Thong tin Thuong hieu (Brand Setup)

Status: review

## Story

As a System Admin,
I want to create and manage Brand (tenant) records,
So that each business partner has an isolated workspace identified by a unique BrandID.

## Acceptance Criteria

**AC1: Brand Creation**
Given a System Admin is on the Business Management screen
When they enter Brand details and save
Then the system successfully initializes a unique BrandID (GUID)
And the Brand appears in the active list

**AC2: Brand Data Model Integrity**
Given the Brand entity is implemented
When persisted to PostgreSQL
Then it stores: `BrandID` (PK, GUID), `Name` (required, max 200), `TaxCode` (unique, max 50), `ContactEmail` (max 255), `Status` (Active | Suspended), `CreatedAt`, `UpdatedAt`

**AC3: Brand List & Retrieval**
Given multiple Brands exist
When the System Admin queries the list
Then the API returns paginated results with filtering by `Status` and `Name`

**AC4: Brand Update & Soft Constraints**
Given an existing Brand
When the Admin updates `Name` or `ContactEmail`
Then changes are persisted and `UpdatedAt` is refreshed
And `TaxCode` cannot be changed if any linked Outlet or Plan exists (business rule enforced in BLL)

**AC5: Multi-tenancy Isolation**
Given the platform is SaaS
When any Brand-scoped operation executes
Then data is strictly isolated by `BrandID` (repository enforces filter)

## Tasks / Subtasks

- [x] Task 1: Define Brand domain model (AC2)
  - [x] Subtask 1.1: Create `Brand.cs` entity in `NonCash.Core/Entities/`
  - [x] Subtask 1.2: Implement `IBrandScoped` if not in BaseEntity
  - [x] Subtask 1.3: Add Brand configuration in `ApplicationDbContext` using FluentAPI
- [x] Task 2: Implement Brand repository & service (AC1, AC3)
  - [x] Subtask 2.1: `IBrandRepository` extending `IRepository<Brand>` if needed
  - [x] Subtask 2.2: `BrandService` in `NonCash.Core/Services/` with `CreateAsync`, `UpdateAsync`, `GetByIdAsync`, `ListAsync`
  - [x] Subtask 2.3: Business rule validation in Service layer (TaxCode immutability check)
- [x] Task 3: Expose API endpoints (AC1, AC3)
  - [x] Subtask 3.1: `BrandsController` in `NonCash.API/Controllers/`
  - [x] Subtask 3.2: DTOs: `CreateBrandRequest`, `BrandResponse`, `UpdateBrandRequest`
  - [x] Subtask 3.3: `GET /api/v1/brands`, `POST /api/v1/brands`, `PUT /api/v1/brands/{id}`, `GET /api/v1/brands/{id}`
- [x] Task 4: Build Blazor management UI (AC1, AC3)
  - [x] Subtask 4.1: `Brands.razor` page under `NonCash.Web/Pages/Admin/`
  - [x] Subtask 4.2: Create/Edit modal component
  - [x] Subtask 4.3: HTTP client service to call API
- [x] Task 5: Database migration (AC2)
  - [x] Subtask 5.1: `dotnet ef migrations add Initial_Brand`
  - [x] Subtask 5.2: Verify migration creates `brands` table with correct constraints
- [x] Task 6: Unit & integration tests
  - [x] Subtask 6.1: `BrandServiceTests` — create, update rules, validation
  - [x] Subtask 6.2: `BrandsControllerTests` — endpoint contracts, 400/404 cases

## Dev Notes

### Architecture Compliance
- Brand is the **root tenant entity**. All other tenant-scoped entities (Outlet, Plan, Voucher) must reference `BrandID`.
- The Service layer MUST enforce the TaxCode immutability rule when linked entities exist. Do not rely on DB constraints alone for this business rule.
- Use `FluentValidation` for input validation on DTOs.

### File Structure Requirements
```
src/NonCash.Core/Entities/Brand.cs
src/NonCash.Core/Interfaces/IBrandRepository.cs
src/NonCash.Core/Services/BrandService.cs
src/NonCash.Infrastructure/Data/Configurations/BrandConfiguration.cs
src/NonCash.API/Controllers/BrandsController.cs
src/NonCash.API/DTOs/BrandDtos.cs
src/NonCash.Web/Pages/Admin/Brands.razor
```

### Database Schema
- Table: `brands`
- Columns: `brand_id` (uuid PK), `name` (varchar 200 not null), `tax_code` (varchar 50 unique), `contact_email` (varchar 255), `status` (varchar 20), `created_at` (timestamptz), `updated_at` (timestamptz)
- Index: `IX_brands_tax_code` (unique), `IX_brands_status`

### API Contracts
- Base path: `/api/v1/brands`
- Authentication: JWT Bearer required (Admin role)
- Pagination: `pageNumber`, `pageSize` query params; response wraps `items`, `totalCount`, `pageNumber`, `pageSize`

### Security & NFR
- NFR4 (Multi-tenancy): Brand table itself is global (System Admin scope), but all other tables filter by `BrandID`. No user-facing endpoint should expose Brands from other tenants unless explicitly Admin-scoped.
- NFR3 (RBAC): Only `System Admin` role can create/modify Brands.

### Testing Standards
- Unit tests for `BrandService` using mocked `IRepository<Brand>`.
- Integration tests must verify unique constraint on `TaxCode` and snake_case column mapping.

### References
- [Source: docs/data-models.md#Brand] — Entity definition.
- [Source: Key Functionalities.txt#V] — Business Management requirements.
- [Source: docs/architecture.md#Security Architecture] — Multi-tenancy and RBAC.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

