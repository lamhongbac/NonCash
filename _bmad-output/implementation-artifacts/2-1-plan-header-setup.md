# Story 2.1: Khai bao Cau hinh Ke hoach & Voucher (Plan Header Setup)

Status: ready-for-dev

## Story

As a Brand Manager,
I want to declare all campaign information and voucher properties into a VoucherPlanHeader record,
So that all budget, voucher parameters, and applicable outlet ranges are managed centrally in one place.

## Acceptance Criteria

**AC1: Plan Header Creation**
Given a Brand Manager is on the Create New Plan screen
When they enter information matching the VoucherPlanHeader structure
Then the system saves the Header record successfully with Timestamp
And auto-assigns `CreatorID` (from JWT), `BrandID` (from JWT), and sets `ApprovalStatus = Pending`

**AC2: Complete Data Model Coverage**
Given the Plan Header entity
When persisted
Then it includes ALL fields:
- `ID` (PK, GUID), `PlanDate` (DateTime), `CreatorID` (FK), `ApproverID` (nullable FK)
- `BrandID` (FK, indexed), `VoucherType` (Complimentary | Gift)
- `ImageURL`, `IconURL` (strings)
- `ValueType` (Value | Percentage), `FaceValue` (Decimal), `NetValue` (Decimal)
- `ExpiryDate` (DateTime), `PublishDate` (DateTime)
- `SalesRange` (list of OutletIDs — implemented as a separate join table or JSONB array; recommend join table `plan_outlets` for referential integrity)
- `TimeRange` (ValidFrom, ValidTo — nullable DateTimes)
- `TargetQuantity` (Int), `Budget` (Decimal)
- `TargetDistributed` (Int), `TargetUsed` (Int)
- `ApprovalStatus` (Pending | Approved | Rejected)

**AC3: Validation Rules**
Given a Plan Header form submission
When validation runs
Then the following rules are enforced:
- `FaceValue` > 0
- `NetValue` <= `FaceValue`
- `ExpiryDate` >= `PublishDate`
- `TargetQuantity` > 0
- `SalesRange` contains only OutletIDs belonging to the current Brand
- `ValidFrom` < `ValidTo` if both provided

**AC4: Plan List & Detail View**
Given multiple plans exist
When the Brand Manager views the plan list
Then only plans for their Brand are shown
And the list supports filtering by `ApprovalStatus` and `VoucherType`

**AC5: Edit Draft Plan**
Given a plan with `ApprovalStatus = Pending`
When the Planner edits it
Then changes are persisted and `UpdatedAt` is refreshed
And `ApprovalStatus` remains Pending

## Tasks / Subtasks

- [ ] Task 1: Define VoucherPlanHeader entity (AC2)
  - [ ] Subtask 1.1: `VoucherPlanHeader.cs` in `NonCash.Core/Entities/`
  - [ ] Subtask 1.2: `VoucherType` and `ValueType` enums in `NonCash.Core/Enums/`
  - [ ] Subtask 1.3: `ApprovalStatus` enum in `NonCash.Core/Enums/`
  - [ ] Subtask 1.4: `PlanOutlet` join entity for SalesRange many-to-many
  - [ ] Subtask 1.5: EF FluentAPI configuration
- [ ] Task 2: Implement Plan service (AC1, AC3, AC5)
  - [ ] Subtask 2.1: `VoucherPlanService` with `CreateAsync`, `UpdateDraftAsync`, `GetByIdAsync`, `ListAsync`
  - [ ] Subtask 2.2: Validation logic for business rules (FaceValue, NetValue, dates, SalesRange Brand validation)
  - [ ] Subtask 2.3: Auto-set CreatorID and BrandID from `ICurrentUserService`
- [ ] Task 3: API endpoints (AC1, AC4, AC5)
  - [ ] Subtask 3.1: `VoucherPlansController`
  - [ ] Subtask 3.2: `POST /api/v1/plans`, `PUT /api/v1/plans/{id}`, `GET /api/v1/plans`, `GET /api/v1/plans/{id}`
  - [ ] Subtask 3.3: DTOs: `CreatePlanRequest`, `UpdatePlanRequest`, `PlanResponse`
- [ ] Task 4: Blazor UI (AC1, AC4, AC5)
  - [ ] Subtask 4.1: `PlanCreate.razor` and `PlanEdit.razor` pages
  - [ ] Subtask 4.2: Multi-select Outlet picker component (filtered by current Brand)
  - [ ] Subtask 4.3: Form validation mirroring backend rules
- [ ] Task 5: Database migration
  - [ ] Subtask 5.1: `voucher_plan_headers` and `plan_outlets` migrations
- [ ] Task 6: Tests
  - [ ] Subtask 6.1: Unit tests for validation rules (NetValue > FaceValue should fail, etc.)
  - [ ] Subtask 6.2: Integration tests for SalesRange outlet ownership validation

## Dev Notes

### Architecture Compliance
- **SalesRange implementation**: Use a join table `plan_outlets` (`plan_id`, `outlet_id`) rather than JSONB. This enforces referential integrity and makes querying plans by outlet efficient.
- The `CreatorID` is a GUID FK to `UserAccount`. Do not enforce cascade delete.
- `ApprovalStatus` transitions are managed in Story 2.3. For this story, status is always `Pending` on create and stays `Pending` on edit.

### File Structure Requirements
```
src/NonCash.Core/Entities/VoucherPlanHeader.cs
src/NonCash.Core/Entities/PlanOutlet.cs
src/NonCash.Core/Enums/VoucherType.cs
src/NonCash.Core/Enums/ValueType.cs
src/NonCash.Core/Enums/ApprovalStatus.cs
src/NonCash.Core/Interfaces/IVoucherPlanRepository.cs
src/NonCash.Core/Services/VoucherPlanService.cs
src/NonCash.Infrastructure/Data/Configurations/VoucherPlanHeaderConfiguration.cs
src/NonCash.API/Controllers/VoucherPlansController.cs
src/NonCash.Web/Pages/Planner/PlanCreate.razor
src/NonCash.Web/Pages/Planner/PlanEdit.razor
```

### Database Schema
- Table: `voucher_plan_headers`
- Columns: `id` (uuid PK), `plan_date` (timestamptz), `creator_id` (uuid FK), `approver_id` (uuid FK nullable), `brand_id` (uuid FK not null), `voucher_type` (varchar 20), `image_url` (text), `icon_url` (text), `value_type` (varchar 20), `face_value` (numeric(18,2)), `net_value` (numeric(18,2)), `expiry_date` (timestamptz), `publish_date` (timestamptz), `valid_from` (timestamptz nullable), `valid_to` (timestamptz nullable), `target_quantity` (int), `budget` (numeric(18,2)), `target_distributed` (int), `target_used` (int), `approval_status` (varchar 20), `created_at` (timestamptz), `updated_at` (timestamptz)
- Table: `plan_outlets` (`plan_id` uuid FK, `outlet_id` uuid FK, PK composite)
- Index: `IX_voucher_plan_headers_brand_id`, `IX_voucher_plan_headers_approval_status`

### API Contracts
- Base path: `/api/v1/plans`
- Auth: JWT Bearer (Planner, Admin)
- `POST` auto-sets CreatorID and BrandID from claims. Reject body-provided BrandID.
- `PUT` only allowed when `ApprovalStatus = Pending`. Return 409 Conflict if not.

### Security & NFR
- NFR4 (Multi-tenancy): Strict Brand filtering. A Planner must never see another Brand's plans.
- NFR3 (RBAC): Only `Planner` and `Admin` can create/edit plans. `Approver` has read-only here. `BrandManager` has no access.

### Testing Standards
- Test boundary conditions: `NetValue` exactly equals `FaceValue` (pass), `NetValue` > `FaceValue` (fail).
- Test `SalesRange` validation: attempt to link an Outlet from a different Brand -> 400 Bad Request.

### References
- [Source: docs/data-models.md#VoucherPlanHeader] — Full field specification.
- [Source: Key Functionalities.txt#I] — Plan Header attributes and business rules.
- [Source: docs/architecture.md#BLL] — Planning Service microservice context.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

