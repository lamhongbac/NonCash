# Story 2.3: Trinh duyet & Quan ly Phe duyet (Approval Workflow)

Status: ready-for-dev

## Story

As a Manager/Approver,
I want to review submitted plan parameters and execute approve/reject actions,
So that budget and strategic checkpoints are verified before vouchers go live.

## Acceptance Criteria

**AC1: Approval Action**
Given a Plan Header with `ApprovalStatus = Pending`
When an Approver performs Approve
Then `ApprovalStatus` updates to `Approved`
And `ApproverID` is recorded from the current user's JWT
And `PublishDate` can be adjusted by the Approver during approval

**AC2: Reject Action**
Given a Plan Header with `ApprovalStatus = Pending`
When an Approver performs Reject
Then `ApprovalStatus` updates to `Rejected`
And `ApproverID` is recorded
And the Approver must provide `ReviewNotes` (required on reject)

**AC3: Approval History Tracking**
Given any approval or rejection
When the action is completed
Then a record is created in `VoucherReview` table with:
- `ReviewID`, `PlanID` (FK), `ApproverID`, `ReviewDate`, `ReviewNotes`, `Decision` (Approved | Rejected), `PublishDate` (if adjusted)

**AC4: Single-Level Approval Only**
Given the system design
When a plan is Approved or Rejected
Then no further approval actions are permitted on that plan version
And the plan becomes immutable for approval status (edits require versioning — Story 2.4)

**AC5: Approver Permission Enforcement**
Given a user with Role = Planner
When they attempt to approve a plan
Then the system returns 403 Forbidden

**AC6: Notification / Status Visibility**
Given a plan's approval status changes
When the Planner views the plan
Then the updated status and approver name are visible

## Tasks / Subtasks

- [ ] Task 1: Define VoucherReview entity (AC3)
  - [ ] Subtask 1.1: `VoucherReview.cs` in `NonCash.Core/Entities/`
  - [ ] Subtask 1.2: `ReviewDecision` enum: Approved, Rejected
  - [ ] Subtask 1.3: EF config with FKs to `VoucherPlanHeader` and `UserAccount`
- [ ] Task 2: Implement Approval service (AC1, AC2, AC4, AC5)
  - [ ] Subtask 2.1: `IApprovalService` with `ApproveAsync(Guid planId, DateTime? publishDate)`, `RejectAsync(Guid planId, string reviewNotes)`
  - [ ] Subtask 2.2: Guard clauses: plan must be Pending, user must have Approver/Admin role, plan must belong to user's Brand
  - [ ] Subtask 2.3: Transaction: update plan header + insert review record atomically
  - [ ] Subtask 2.4: If Rejected, require non-empty ReviewNotes (min 10 chars)
- [ ] Task 3: API endpoints (AC1, AC2, AC5)
  - [ ] Subtask 3.1: `ApprovalsController` or actions on `VoucherPlansController`
  - [ ] Subtask 3.2: `POST /api/v1/plans/{planId}/approve`, `POST /api/v1/plans/{planId}/reject`
  - [ ] Subtask 3.3: DTOs: `ApproveRequest` (optional `publishDate`), `RejectRequest` (`reviewNotes` required)
- [ ] Task 4: Blazor UI (AC1, AC2, AC6)
  - [ ] Subtask 4.1: `PlanReview.razor` page for Approvers
  - [ ] Subtask 4.2: Display plan summary (budget, targets, outlets) in read-only form
  - [ ] Subtask 4.3: Approve/Reject buttons with confirmation modal
  - [ ] Subtask 4.4: Review history panel showing past decisions
- [ ] Task 5: Database migration
  - [ ] Subtask 5.1: `voucher_reviews` table migration
- [ ] Task 6: Tests
  - [ ] Subtask 6.1: Unit tests for all guard clauses (wrong role, wrong status, wrong Brand)
  - [ ] Subtask 6.2: Integration tests for approve/reject happy path
  - [ ] Subtask 6.3: Integration test for idempotency — double-approve should return 409

## Dev Notes

### Architecture Compliance
- Approval is a **state machine** on `VoucherPlanHeader`. Valid transitions: Pending -> Approved, Pending -> Rejected. No other transitions allowed.
- The `VoucherReview` table provides an immutable audit trail. Never update or delete review records.
- `PublishDate` on the Plan Header may be adjusted by the Approver. If not provided, keep the Planner's original value.

### File Structure Requirements
```
src/NonCash.Core/Entities/VoucherReview.cs
src/NonCash.Core/Enums/ReviewDecision.cs
src/NonCash.Core/Interfaces/IApprovalService.cs
src/NonCash.Core/Services/ApprovalService.cs
src/NonCash.Infrastructure/Data/Configurations/VoucherReviewConfiguration.cs
src/NonCash.API/Controllers/ApprovalsController.cs
src/NonCash.Web/Pages/Approver/PlanReview.razor
```

### Database Schema
- Table: `voucher_reviews`
- Columns: `review_id` (uuid PK), `plan_id` (uuid FK not null), `approver_id` (uuid FK not null), `review_date` (timestamptz not null), `review_notes` (text), `decision` (varchar 20 not null), `publish_date` (timestamptz nullable)
- Index: `IX_voucher_reviews_plan_id`, `IX_voucher_reviews_approver_id`

### API Contracts
- `POST /api/v1/plans/{planId}/approve` -> `{ publishDate?: "2026-05-01T00:00:00Z" }` => 200 + updated plan
- `POST /api/v1/plans/{planId}/reject` -> `{ reviewNotes: "Budget exceeds Q2 limit." }` => 200 + updated plan
- 403 if role != Approver/Admin
- 409 if plan status != Pending
- 400 if reject without reviewNotes

### Security & NFR
- NFR3 (RBAC): Enforce `[Authorize(Roles = "Approver,Admin")]` on approval endpoints.
- NFR4 (Multi-tenancy): Approver can only act on plans within their Brand.
- Immutability of review records supports audit and compliance requirements.

### Testing Standards
- State machine tests: attempt invalid transitions (Approved -> Rejected, etc.) and assert 409.
- Transaction tests: verify that if review insert fails, plan header is NOT updated.

### References
- [Source: docs/data-models.md#VoucherPlanHeader] — ApprovalStatus field.
- [Source: Key Functionalities.txt#II] — Approval workflow details, review attributes.
- [Source: docs/architecture.md#BLL] — Approval Service microservice.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

