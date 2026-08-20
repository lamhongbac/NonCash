# Story 1.6: Business Registration Approval (Admin Review)

Status: done

## Story

As a Platform Admin/Manager,
I want to review business registration requests, send a contract, collect the signed hardcopy, and approve or reject the business,
So that only legitimate and verified businesses gain access to the NonCash platform.

## Acceptance Criteria

**AC1: Pending Registration List**
Given a Platform Admin is on the Registration Management screen
When they view the list
Then they see `BusinessRegistrationRequest` records with `Status = Submitted`
Split into two tabs:
- Pending Contract: `Status = Submitted` and `ContractStatus != Signed`
- Pending Review: `Status = Submitted` and `ContractStatus = Signed`
And each card shows: Business Name, Tax Code, Contact Email, Phone, Address, Representative, First Brand info (if declared), Submitted Date

**AC2: Contract Workflow**
Given a request is in Pending Contract
When the Admin selects a Welcome Policy Template and clicks Send Contract
Then the system:
- Stores `WelcomePolicyTemplateId`, sets `ContractStatus = Sent`, and records `ContractSentAt`
- Generates contract HTML including business info, welcome policy terms, and pricing appendix
- Emails the contract to the applicant via `NotifyContractSentAsync`

**AC3: Signed Contract Upload**
Given a contract has been sent
When the Admin enters the signed contract file URL and clicks Upload Signed
Then the system sets `ContractStatus = Signed` and stores `ContractFileUrl`
And the request moves to Pending Review

**AC4: Print Contract**
Given a contract has been sent
When the Admin clicks Print
Then the system fetches the contract HTML with authentication and opens it in a new browser tab

**AC5: Approve Registration**
Given a request is in Pending Review (contract signed)
When the Admin clicks Approve
Then the system:
- Creates the `Business` record
- If first brand was declared, creates the `Brand` and `UserAccount` (BrandManager, Active)
- If no first brand was declared, creates only the Business
- Assigns the selected Welcome Policy Template to the Business as its active `WelcomeGrantPolicy`
- Grants welcome credits to the newly created brand (if any)
- Updates `BusinessRegistrationRequest.Status` to `Approved`
- Records `ReviewedAt`, `ReviewedByUserId`, and `ReviewNotes`
- Triggers a welcome notification to the business contact

**AC6: Reject Registration**
Given a request is under review
When the Admin clicks Reject
Then the system:
- Updates `BusinessRegistrationRequest.Status` to `Rejected`
- Records `ReviewedAt`, `ReviewedByUserId`, and `ReviewNotes`
- Requires the Admin to provide `ReviewNotes`
- Triggers a rejection notification to the business contact with the reason
- Does not create any Business, Brand, or UserAccount records

**AC7: Approval Guard**
Given a request with `ContractStatus != Signed`
When the Admin attempts to approve
Then the system returns 400 with message "Signed contract must be uploaded before approval."

**AC8: Approval Permission Enforcement**
Given a user with Role != Admin
When they attempt to access the registration review endpoints
Then the system returns 403 Forbidden

**AC9: Audit Trail**
Given any approval or rejection action
When completed
Then the `BusinessRegistrationRequest` record becomes immutable for status and review fields
And a history of decisions is queryable by super-admins

## Tasks / Subtasks

- [x] Task 1: Implement registration review service (AC1, AC5, AC6, AC7, AC9)
  - [x] Subtask 1.1: `IRegistrationService` with `GetPendingContractRequestsAsync`, `GetPendingReviewRequestsAsync`, `GetAllRequestsAsync`
  - [x] Subtask 1.2: `SendContractAsync(Guid requestId, Guid welcomePolicyTemplateId, Guid senderUserId)`
  - [x] Subtask 1.3: `UploadSignedContractAsync(Guid requestId, string contractFileUrl, Guid adminUserId)`
  - [x] Subtask 1.4: `ReviewAsync(Guid requestId, Guid reviewerUserId, bool approve, string? reviewNotes)`
  - [x] Subtask 1.5: Guard: only Admin role can execute; request must be in `Submitted` status
  - [x] Subtask 1.6: Approve requires `ContractStatus = Signed`
  - [x] Subtask 1.7: Reject requires non-empty `ReviewNotes`
- [x] Task 2: Contract generation (AC2, AC4)
  - [x] Subtask 2.1: `IContractService` with `GenerateContractHtmlAsync`
  - [x] Subtask 2.2: Contract includes business info, welcome policy, pricing appendix, signature blocks
  - [x] Subtask 2.3: Contract HTML used in both email and Print button
- [x] Task 3: API endpoints (AC1, AC2, AC3, AC5, AC6, AC7, AC8)
  - [x] Subtask 3.1: `GET /api/v1/admin/registration-requests/pending-contract` — list pending contract
  - [x] Subtask 3.2: `GET /api/v1/admin/registration-requests/pending-review` — list pending review
  - [x] Subtask 3.3: `GET /api/v1/admin/registration-requests` — list all
  - [x] Subtask 3.4: `GET /api/v1/admin/registration-requests/{requestId}/contract` — print HTML
  - [x] Subtask 3.5: `POST /api/v1/admin/registration-requests/{requestId}/send-contract` -> `{ welcomePolicyTemplateId }`
  - [x] Subtask 3.6: `POST /api/v1/admin/registration-requests/{requestId}/upload-signed-contract` -> `{ contractFileUrl }`
  - [x] Subtask 3.7: `POST /api/v1/admin/registration-requests/{requestId}/approve` -> `{ reviewNotes?: "string" }`
  - [x] Subtask 3.8: `POST /api/v1/admin/registration-requests/{requestId}/reject` -> `{ reviewNotes: "string" }` (required)
- [x] Task 4: Blazor Admin UI (AC1, AC2, AC3, AC4, AC5, AC6)
  - [x] Subtask 4.1: `RegistrationRequests.razor` page under `NonCash.Web/Components/Pages/Admin/`
  - [x] Subtask 4.2: Pending Contract / Pending Review / All Requests tabs
  - [x] Subtask 4.3: Send Contract dialog with Welcome Policy Template selector
  - [x] Subtask 4.4: Upload Signed Contract dialog
  - [x] Subtask 4.5: Print button that opens contract in new tab
  - [x] Subtask 4.6: Approve / Reject buttons with review notes input
- [x] Task 5: Notification integration (AC2, AC5, AC6)
  - [x] Subtask 5.1: `NotifyContractSentAsync` with contract HTML
  - [x] Subtask 5.2: `NotifyBusinessActivatedAsync` on approval with conditional brand/credit info
  - [x] Subtask 5.3: `NotifyRegistrationRejectedAsync` on rejection with reason
- [x] Task 6: Database migration
  - [x] Subtask 6.1: `BusinessRegistrationRequestContractWorkflow` migration adding contract columns
  - [x] Subtask 6.2: `BusinessRegistrationRequestFirstBrandInfo` migration adding business info and first-brand columns
- [x] Task 7: Tests
  - [x] Subtask 7.1: Unit tests for approve/reject business rules and guards
  - [x] Subtask 7.2: Integration tests for permission enforcement (403 for non-admins)
  - [x] Subtask 7.3: Integration tests for contract-required-before-approval guard
  - [x] Subtask 7.4: Integration tests for approval creating Business/Brand/User when first brand is declared
  - [x] Subtask 7.5: Integration tests for approval creating Business only when no first brand is declared

## Dev Notes

### Architecture Compliance
- This is an **admin-only** workflow. Enforce `[Authorize(Roles = "Admin")]` on all endpoints.
- The approval/rejection is a **state machine** on `BusinessRegistrationRequest`. Valid transitions: Submitted -> Approved, Submitted -> Rejected. No reversals.
- Contract workflow: Sent -> Signed -> Approved/Rejected.
- Use a **database transaction** to ensure all created records (Business, Brand, User, WelcomeGrantPolicy) update atomically. If any step fails, nothing commits.

### File Structure Requirements
```
src/NonCash.Core/Interfaces/IRegistrationService.cs
src/NonCash.Core/Services/RegistrationService.cs
src/NonCash.Core/Interfaces/IContractService.cs
src/NonCash.Infrastructure/Services/ContractService.cs
src/NonCash.API/Controllers/RegistrationReviewController.cs
src/NonCash.Web/Components/Pages/Admin/RegistrationRequests.razor
src/NonCash.Web/wwwroot/js/contract-print.js
```

### Database Schema
- Table: `business_registration_requests` (extends Story 1.5 schema)
- Additional columns:
  - `contract_status` (varchar 20): None | Sent | Signed
  - `contract_sent_at` (timestamptz nullable)
  - `contract_file_url` (varchar 500)
  - `welcome_policy_template_id` (uuid FK -> welcome_grant_policy_templates)
  - `business_id` (uuid FK -> businesses, populated on approval)
  - `brand_id` (uuid FK -> brands, populated on approval if first brand declared)
  - `submitted_by_user_id` (uuid FK -> user_accounts, populated on approval if first brand declared)
- Index: `IX_business_registration_requests_contract_status`, `IX_business_registration_requests_welcome_policy_template_id`

### API Contracts
- `GET /api/v1/admin/registration-requests/pending-contract`
- `GET /api/v1/admin/registration-requests/pending-review`
- `GET /api/v1/admin/registration-requests`
- `GET /api/v1/admin/registration-requests/{requestId}/contract` -> HTML
- `POST /api/v1/admin/registration-requests/{requestId}/send-contract` -> `{ welcomePolicyTemplateId: Guid }`
- `POST /api/v1/admin/registration-requests/{requestId}/upload-signed-contract` -> `{ contractFileUrl: string }`
- `POST /api/v1/admin/registration-requests/{requestId}/approve` -> `{ reviewNotes?: "string" }`
- `POST /api/v1/admin/registration-requests/{requestId}/reject` -> `{ reviewNotes: "string" }` (required)
- 403 if role != Admin
- 400 if request status != Submitted or contract not signed before approval
- 400 if reject without reviewNotes

### Security & NFR
- NFR3 (RBAC): Strict Admin-only access. This is a platform-level governance function.
- NFR4: Admins may see all requests regardless of tenant, but standard Brand scoping still applies to Brand data mutations.
- Immutability of review decisions: once Approved or Rejected, the request record status must not change. If a mistake is made, the business must re-register.

### Testing Standards
- State machine tests: assert that `Rejected -> Approved` and `Approved -> Rejected` both fail.
- Contract guard test: approve before signed contract returns 400.
- Cross-role test: generate JWTs for each role and verify only Admin gets 200.

## Dev Agent Record

### Agent Model Used

Qoder AI Assistant

### Debug Log References

- DateTime Kind=Unspecified bug when approving voucher plans — fixed with `DateTime.SpecifyKind(value, DateTimeKind.Utc)` in ApprovalService.cs
- SMTP transient errors (ServiceNotAvailable, ServiceClosingTransmissionChannel) — handled with 3-retry exponential backoff policy
- Admin page refresh threw `NavigationException` during prerender — fixed with `ClientAuthService.NavigateToLogin()` helper catching `NavigationException` and `InvalidOperationException`
- Print button returned 401 because `AuthHttpHandler` failed to attach token in Blazor Server — fixed by manually attaching token in `PrintContractAsync`

### Completion Notes List

- 2026-08-18: Initial contract workflow implemented (send contract, upload signed, approve/reject).
- 2026-08-20: Refactored to request-only registration model:
  - Business, Brand, and UserAccount created on approval instead of submission.
  - First-brand declaration shown on request card.
  - Welcome policy template assigned to Business on approval.
- Contract HTML includes Pricing section and Appendix A for brand-level pricing.
- Print button opens contract in new tab using `contract-print.js` helper.
- All 137 tests pass (76 integration + 61 unit).

### File List

- src/NonCash.Core/Services/RegistrationService.cs
- src/NonCash.Core/Interfaces/IContractService.cs
- src/NonCash.Infrastructure/Services/ContractService.cs
- src/NonCash.Infrastructure/EmailTemplates/ContractSent.html
- src/NonCash.Infrastructure/EmailTemplates/ActiveBusiness.html
- src/NonCash.Infrastructure/EmailTemplates/RegistrationRejected.html
- src/NonCash.API/Controllers/RegistrationReviewController.cs
- src/NonCash.API/DTOs/RegistrationDtos.cs
- src/NonCash.Web/Components/Pages/Admin/RegistrationRequests.razor
- src/NonCash.Web/wwwroot/js/contract-print.js
- src/NonCash.Web/Services/ClientAuthService.cs
