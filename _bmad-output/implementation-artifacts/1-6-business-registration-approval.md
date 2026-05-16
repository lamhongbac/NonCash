# Story 1.6: Business Registration Approval (Admin Review)

Status: ready-for-dev

## Story

As a Platform Admin/Manager,
I want to review and approve or reject business registration requests,
So that only legitimate and verified businesses gain access to the NonCash platform.

## Acceptance Criteria

**AC1: Pending Registration List**
Given a Platform Admin is on the Registration Management screen
When they view the list
Then they see all `BrandRegistrationRequest` records with `Status = Submitted`
Ordered by `SubmittedAt` (oldest first)
And each row shows: Company Name, Tax Code, Contact Email, Submitted Date

**AC2: Registration Detail View**
Given the Admin clicks a pending request
When the detail opens
Then they see all submitted information plus any auto-verification flags (e.g., Tax Code format valid, domain check placeholder)

**AC3: Approve Registration**
Given a request is under review
When the Admin clicks Approve
Then the system:
- Updates `BrandRegistrationRequest.Status` to `Approved`
- Updates `Brand.Status` to `Active`
- Updates the linked `UserAccount.Status` to `Active`
- Records `ReviewedAt`, `ReviewedByUserId`, and `ReviewNotes`
- Triggers a notification to the business representative with login instructions

**AC4: Reject Registration**
Given a request is under review
When the Admin clicks Reject
Then the system:
- Updates `BrandRegistrationRequest.Status` to `Rejected`
- Updates `Brand.Status` to `Rejected`
- Updates the linked `UserAccount.Status` to `Rejected` or deletes it (prefer status update for audit)
- Requires the Admin to provide `ReviewNotes` (minimum 10 characters)
- Records `ReviewedAt` and `ReviewedByUserId`
- Triggers a rejection notification to the business representative with the reason

**AC5: Approval Permission Enforcement**
Given a user with Role = BrandManager or Planner
When they attempt to access the registration approval endpoints
Then the system returns 403 Forbidden

**AC6: Audit Trail**
Given any approval or rejection action
When completed
Then the `BrandRegistrationRequest` record becomes immutable for status and review fields
And a history of decisions is queryable by super-admins

## Tasks / Subtasks

- [ ] Task 1: Implement registration review service (AC1, AC3, AC4, AC6)
  - [ ] Subtask 1.1: `IRegistrationReviewService` with `GetPendingListAsync`, `ApproveAsync(Guid requestId, string? notes)`, `RejectAsync(Guid requestId, string notes)`
  - [ ] Subtask 1.2: Transaction: update request + brand + user account atomically
  - [ ] Subtask 1.3: Guard: only Admin role can execute; request must be in `Submitted` status
  - [ ] Subtask 1.4: Reject requires non-empty `ReviewNotes`
- [ ] Task 2: API endpoints (AC1, AC2, AC3, AC4, AC5)
  - [ ] Subtask 2.1: `GET /api/v1/admin/registrations?status=Submitted` — list pending
  - [ ] Subtask 2.2: `GET /api/v1/admin/registrations/{requestId}` — detail view
  - [ ] Subtask 2.3: `POST /api/v1/admin/registrations/{requestId}/approve` — approve
  - [ ] Subtask 2.4: `POST /api/v1/admin/registrations/{requestId}/reject` — reject
  - [ ] Subtask 2.5: DTOs: `RegistrationReviewRequest`, `RegistrationListResponse`, `RegistrationDetailResponse`
- [ ] Task 3: Blazor Admin UI (AC1, AC2, AC3, AC4)
  - [ ] Subtask 3.1: `RegistrationReview.razor` page under `NonCash.Web/Pages/Admin/`
  - [ ] Subtask 3.2: Data grid of pending registrations with sort/filter
  - [ ] Subtask 3.3: Detail drawer/modal with Approve/Reject actions
  - [ ] Subtask 3.4: Confirmation modal for Reject requiring notes input
- [ ] Task 4: Notification integration (AC3, AC4)
  - [ ] Subtask 4.1: Call `INotificationService` on approval/reject
  - [ ] Subtask 4.2: For MVP, log to console or a `notifications` queue table
- [ ] Task 5: Database migration
  - [ ] Subtask 5.1: Ensure `brand_registration_requests` table supports review fields
- [ ] Task 6: Tests
  - [ ] Subtask 6.1: Unit tests for approve/reject business rules and guards
  - [ ] Subtask 6.2: Integration tests for permission enforcement (403 for non-admins)
  - [ ] Subtask 6.3: Integration tests for transaction atomicity (if brand update fails, request stays Submitted)
  - [ ] Subtask 6.4: Integration tests for duplicate approval attempts (409 Conflict)

## Dev Notes

### Architecture Compliance
- This is an **admin-only** workflow. Enforce `[Authorize(Roles = "Admin")]` on all endpoints.
- The approval/rejection is a **state machine** on `BrandRegistrationRequest`. Valid transitions: Submitted -> Approved, Submitted -> Rejected. No reversals.
- Use a **database transaction** to ensure all three records (request, brand, user) update atomically. If any step fails, nothing commits.
- The `UserAccount` created during self-registration (Story 1.5) is activated here. Ensure the auth service allows login only after this activation.

### File Structure Requirements
```
src/NonCash.Core/Interfaces/IRegistrationReviewService.cs
src/NonCash.Core/Services/RegistrationReviewService.cs
src/NonCash.API/Controllers/AdminRegistrationsController.cs
src/NonCash.Web/Pages/Admin/RegistrationReview.razor
```

### Database Schema
- Table: `brand_registration_requests` (extends Story 1.5 schema)
- Additional context:
  - `reviewed_by_user_id` (uuid FK -> user_accounts)
  - `reviewed_at` (timestamptz)
  - `review_notes` (text)
  - Ensure composite status check performance: index on `(status, submitted_at)`

### API Contracts
- `GET /api/v1/admin/registrations?status=Submitted&page=1&pageSize=20`
- `GET /api/v1/admin/registrations/{requestId}`
- `POST /api/v1/admin/registrations/{requestId}/approve` -> `{ reviewNotes?: "string" }`
- `POST /api/v1/admin/registrations/{requestId}/reject` -> `{ reviewNotes: "string" }` (required)
- 403 if role != Admin
- 409 if request status != Submitted
- 400 if reject without reviewNotes

### Security & NFR
- NFR3 (RBAC): Strict Admin-only access. This is a platform-level governance function.
- NFR4: Admins may see all Brands regardless of tenant, but standard Brand scoping still applies to Brand data mutations.
- Immutability of review decisions: once Approved or Rejected, the request record status must not change. If a mistake is made, the business must re-register.

### Testing Standards
- State machine tests: assert that `Rejected -> Approved` and `Approved -> Rejected` both return 409.
- Transaction test: simulate DB failure during brand status update and assert request remains `Submitted`.
- Cross-role test: generate JWTs for each role and verify only Admin gets 200.

### References
- [Source: docs/data-models.md#Brand] — Brand status field.
- [Source: docs/data-models.md#UserAccount] — User account status field.
- [Source: Key Functionalities.txt#V] — Business Management, approval context.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

