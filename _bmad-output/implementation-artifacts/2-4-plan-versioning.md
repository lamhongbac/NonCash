# Story 2.4: Dieu chinh & Phien ban hoa Ke hoach (Plan Adjustments / Versioning)

Status: ready-for-dev

## Story

As a Brand Manager,
I want to clone or create a new version from a Rejected plan,
So that I can adjust and resubmit for approval while preserving the history of the original review process.

## Acceptance Criteria

**AC1: Clone from Rejected Plan**
Given a plan with `ApprovalStatus = Rejected`
When the Brand Manager selects Clone / Create New Version
Then the system creates a new `VoucherPlanHeader` record
And copies all fields from the original except: `ID`, `ApprovalStatus` (set to Pending), `ApproverID` (null), `CreatedAt`, `UpdatedAt`
And links the new plan to the original via a `PreviousVersionId` field

**AC2: Version History Traceability**
Given a cloned plan exists
When viewing its detail
Then the UI displays a link to the original plan version
And the original plan remains static in the database with its Rejected status intact

**AC3: Prevent Clone from Approved Plans**
Given a plan with `ApprovalStatus = Approved`
When a user attempts to clone it
Then the system returns 400 / CannotCloneApprovedPlan
And suggests creating a new plan instead

**AC4: Cascade Clone of SalesRange**
Given a plan is cloned
When the new version is saved
Then all `PlanOutlet` relationships are copied to the new plan

**AC5: Version List**
Given multiple versions of the same plan lineage exist
When the user views the plan family
Then they see a chronological list of all versions with their statuses

## Tasks / Subtasks

- [ ] Task 1: Extend VoucherPlanHeader for versioning (AC2)
  - [ ] Subtask 1.1: Add `PreviousVersionId` (nullable FK to self) to `VoucherPlanHeader`
  - [ ] Subtask 1.2: Add `VersionNumber` (int, default 1) to `VoucherPlanHeader`
  - [ ] Subtask 1.3: EF config for self-referencing FK
- [ ] Task 2: Implement Clone service (AC1, AC3, AC4)
  - [ ] Subtask 2.1: `IPlanCloneService` with `CloneAsync(Guid rejectedPlanId)`
  - [ ] Subtask 2.2: Deep clone logic: copy scalar properties, copy PlanOutlet collection, nullify approval fields
  - [ ] Subtask 2.3: Guard: only Rejected plans can be cloned
  - [ ] Subtask 2.4: Auto-increment `VersionNumber` based on max version in lineage
- [ ] Task 3: API endpoint (AC1)
  - [ ] Subtask 3.1: `POST /api/v1/plans/{planId}/clone`
  - [ ] Subtask 3.2: Return 201 Created with new plan ID and version number
- [ ] Task 4: Blazor UI (AC1, AC2, AC5)
  - [ ] Subtask 4.1: "Create New Version" button on rejected plan detail
  - [ ] Subtask 4.2: Version history timeline component
  - [ ] Subtask 4.3: Navigation between versions
- [ ] Task 5: Database migration
  - [ ] Subtask 5.1: Add `previous_version_id` and `version_number` columns
- [ ] Task 6: Tests
  - [ ] Subtask 6.1: Unit tests for clone logic (all fields copied correctly)
  - [ ] Subtask 6.2: Integration tests for guard clauses (approved plan clone blocked)
  - [ ] Subtask 6.3: Integration tests verifying original plan remains unchanged

## Dev Notes

### Architecture Compliance
- Versioning is **shallow copy** of plan headers. Do NOT copy `VoucherPlanDetail` records — the new version starts with zero generated vouchers until approved and batch-generated (Story 2.2).
- The self-referencing FK enables a linked list of versions. For tree-like histories, this is sufficient because we only clone the latest rejected version.
- `VersionNumber` is sequential within a lineage (1, 2, 3...). Query: `MAX(VersionNumber) WHERE root plan lineage`.

### File Structure Requirements
```
src/NonCash.Core/Interfaces/IPlanCloneService.cs
src/NonCash.Core/Services/PlanCloneService.cs
src/NonCash.API/Controllers/PlanVersionsController.cs
src/NonCash.Web/Components/PlanVersionTimeline.razor
```

### Database Schema
- Alter `voucher_plan_headers`:
  - Add `previous_version_id` (uuid FK nullable -> voucher_plan_headers.id)
  - Add `version_number` (int not null default 1)
- Index: `IX_voucher_plan_headers_previous_version_id`

### API Contracts
- `POST /api/v1/plans/{planId}/clone` => `{ newPlanId: "guid", versionNumber: 2 }`
- `GET /api/v1/plans/{planId}/versions` => list of all versions in this lineage

### Security & NFR
- NFR3 (RBAC): Only Planner and Admin can clone.
- NFR4 (Multi-tenancy): Can only clone plans within user's Brand.
- Original plan data must be preserved exactly for audit; clone must not modify source.

### Testing Standards
- Assert that `CreatedAt` of original != `CreatedAt` of clone.
- Assert that `PlanOutlet` count matches after clone.
- Assert that `ApprovalStatus` of clone is always Pending.

### References
- [Source: Key Functionalities.txt#II] — Plan rejection, adjustment, and traceability requirements.
- [Source: docs/data-models.md#VoucherPlanHeader] — Plan entity fields.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

