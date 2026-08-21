# Story 1.6: Business Registration Approval (Admin Review)

Status: done

## Story

As a Platform Admin/Manager,
I want to review business registration requests, send a contract, collect digital confirmation or a signed hardcopy, and approve or reject the business,
So that only legitimate and verified businesses gain access to the NonCash platform.

## Acceptance Criteria

**AC1: Pending Registration List**
Given a Platform Admin is on the Registration Management screen
When they view the list
Then they see `BusinessRegistrationRequest` records with `Status = Submitted`
Split into two tabs:
- Pending Contract: `Status = Submitted` and `ContractStatus` is `None` or `Sent`
- Pending Review: `Status = Submitted` and `ContractStatus` is `Confirmed` or `Signed`
And each card shows: Business Name, Tax Code, Contact Email, Phone, Address, Representative, First Brand info (if declared), Submitted Date, Contract Status

**AC2: Contract Workflow**
Given a request is in Pending Contract
When the Admin selects a Welcome Policy Template and clicks Send Contract
Then the system:
- Resolves the active default `ContractTemplate` from the database (or uses a built-in fallback)
- Stores `WelcomePolicyTemplateId`, `ContractTemplateId`, sets `ContractStatus = Sent`, records `ContractSentAt`, and generates a `ContractConfirmationToken`
- Generates contract HTML by replacing placeholders (e.g. `{{BusinessName}}`, `{{SubscriptionFeeVnd}}`, `{{MinimumCommitmentMonths}}`) in the template
- Emails the contract to the applicant via `NotifyContractSentAsync`, including a one-click confirmation link and the confirmation key

**AC2a: Editable Contract Templates**
Given a Platform Admin is on the Contract Templates screen
When they create, edit, or set a default template
Then the system persists the template HTML and uses it for all future contract generation

**AC3: Digital Confirmation (Preferred)**
Given a contract has been sent
When the business visits the confirmation page from the email link (or enters the request ID and confirmation key) and checks "I agree"
Then the system validates the token, sets `ContractStatus = Confirmed`, records `ContractConfirmedAt` and `ContractConfirmedByIp`
And the request moves to Pending Review

**AC3a: Signed Contract Upload (Optional)**
Given a contract has been sent
When the Admin selects a PDF/JPG/PNG file in the Upload Signed dialog and clicks Upload
Then the system stores the file via `IDocumentStorageService` (MSA or local), sets `ContractStatus = Signed`, and stores the returned `ContractFileUrl`
And the request moves to Pending Review

**AC4: Print Contract**
Given a contract has been sent
When the Admin clicks Print
Then the system fetches the contract HTML with authentication and opens it in a new browser tab

**AC5: Approve Registration**
Given a request is in Pending Review (contract confirmed or signed)
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

**AC7: Subscription Fee Clause**
Given a contract is generated
Then the contract includes a Subscription Fee section
And the subscription fee and minimum commitment are resolved from the active `SubscriptionFeePolicy` for the contract date
And if no active policy exists, the contract falls back to `CreditConfig.SubscriptionFeeVnd` and `CreditConfig.MinimumCommitmentMonths`

**AC7a: Subscription Fee Policy Management**
Given a Platform Admin is on the Subscription Fee Policies screen
When they create or edit a policy with a date range, amount, and IsFree flag
Then the system persists the policy and uses it to resolve subscription terms for contracts generated within the effective period

**AC8: Approval Guard**
Given a request with `ContractStatus != Signed`
When the Admin attempts to approve
Then the system returns 400 with message "Signed contract must be uploaded before approval."

**AC9: Approval Permission Enforcement**
Given a user with Role != Admin
When they attempt to access the registration review endpoints
Then the system returns 403 Forbidden

**AC10: Audit Trail**
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
- [x] Task 2: Contract templates and generation (AC2, AC2a, AC4, AC7, AC7a)
  - [x] Subtask 2.1: `ContractTemplate` entity with `Name`, `HtmlTemplate`, `IsDefault`, `IsActive`
  - [x] Subtask 2.2: `IContractTemplateService` with CRUD and default-template management
  - [x] Subtask 2.3: `IContractService` renders contract HTML from template + replaces placeholders
  - [x] Subtask 2.4: Contract resolves subscription fee and commitment from active `SubscriptionFeePolicy` (or `CreditConfig` fallback)
  - [x] Subtask 2.5: Contract HTML used in both email and Print button
- [x] Task 2b: Subscription fee policies (AC7, AC7a)
  - [x] Subtask 2b.1: `SubscriptionFeePolicy` entity with `Name`, `AmountVnd`, `IsFree`, `MinimumCommitmentMonths`, `EffectiveFrom`, `EffectiveTo`, `IsActive`
  - [x] Subtask 2b.2: `ISubscriptionFeePolicyService` with CRUD and `GetEffectivePolicyAsync`
  - [x] Subtask 2b.3: Effective policy resolution by date with fallback to `CreditConfig`
- [x] Task 3: Registration review API endpoints (AC1, AC2, AC3, AC5, AC6, AC8, AC9)
  - [x] Subtask 3.1: `GET /api/v1/admin/registration-requests/pending-contract` — list pending contract
  - [x] Subtask 3.2: `GET /api/v1/admin/registration-requests/pending-review` — list pending review
  - [x] Subtask 3.3: `GET /api/v1/admin/registration-requests` — list all
  - [x] Subtask 3.4: `GET /api/v1/admin/registration-requests/{requestId}/contract` — print HTML
  - [x] Subtask 3.5: `POST /api/v1/admin/registration-requests/{requestId}/send-contract` -> `{ welcomePolicyTemplateId }`
  - [x] Subtask 3.6: `POST /api/v1/admin/registration-requests/{requestId}/upload-signed-contract` -> `{ contractFileUrl }`
  - [x] Subtask 3.7: `POST /api/v1/admin/registration-requests/{requestId}/approve` -> `{ reviewNotes?: "string" }`
  - [x] Subtask 3.8: `POST /api/v1/admin/registration-requests/{requestId}/reject` -> `{ reviewNotes: "string" }` (required)
- [x] Task 3b: Contract template API endpoints (AC2a)
  - [x] Subtask 3b.1: `GET /api/v1/contract-templates` — list templates
  - [x] Subtask 3b.2: `GET /api/v1/contract-templates/{id}` — single template
  - [x] Subtask 3b.3: `GET /api/v1/contract-templates/default` — current default
  - [x] Subtask 3b.4: `POST /api/v1/contract-templates` — create
  - [x] Subtask 3b.5: `PUT /api/v1/contract-templates/{id}` — update
  - [x] Subtask 3b.6: `POST /api/v1/contract-templates/{id}/set-default` — set default
- [x] Task 4: Blazor Admin UI (AC1, AC2, AC3, AC4, AC5, AC6)
  - [x] Subtask 4.1: `RegistrationRequests.razor` page under `NonCash.Web/Components/Pages/Admin/`
  - [x] Subtask 4.2: Pending Contract / Pending Review / All Requests tabs
  - [x] Subtask 4.3: Send Contract dialog with Welcome Policy Template selector
  - [x] Subtask 4.4: Upload Signed Contract dialog
  - [x] Subtask 4.5: Print button that opens contract in new tab
  - [x] Subtask 4.6: Approve / Reject buttons with review notes input
- [x] Task 4b: Contract Templates Admin UI (AC2a)
  - [x] Subtask 4b.1: `ContractTemplates.razor` page under `NonCash.Web/Components/Pages/Admin/`
  - [x] Subtask 4b.2: List templates with default/active indicators
  - [x] Subtask 4b.3: HTML editor with placeholder reference
- [x] Task 4c: Subscription Fee Policies Admin UI (AC7a)
  - [x] Subtask 4c.1: `SubscriptionFeePolicies.razor` page under `NonCash.Web/Components/Pages/Admin/`
  - [x] Subtask 4c.2: List policies with fee, commitment, effective range, status
  - [x] Subtask 4c.3: Editor with IsFree toggle, amount, commitment months, date pickers
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
src/NonCash.Core/Interfaces/IContractTemplateService.cs
src/NonCash.Core/Interfaces/ISubscriptionFeePolicyService.cs
src/NonCash.Core/Entities/ContractTemplate.cs
src/NonCash.Core/Entities/SubscriptionFeePolicy.cs
src/NonCash.Infrastructure/Services/ContractService.cs
src/NonCash.Infrastructure/Services/ContractTemplateService.cs
src/NonCash.Infrastructure/Services/SubscriptionFeePolicyService.cs
src/NonCash.API/Controllers/RegistrationReviewController.cs
src/NonCash.API/Controllers/ContractTemplatesController.cs
src/NonCash.API/Controllers/SubscriptionFeePoliciesController.cs
src/NonCash.Web/Components/Pages/Admin/RegistrationRequests.razor
src/NonCash.Web/Components/Pages/Admin/ContractTemplates.razor
src/NonCash.Web/Components/Pages/Admin/SubscriptionFeePolicies.razor
src/NonCash.Web/wwwroot/js/contract-print.js
```

### Database Schema
- Table: `business_registration_requests` (extends Story 1.5 schema)
- Additional columns:
  - `contract_status` (varchar 20): None | Sent | Signed
  - `contract_sent_at` (timestamptz nullable)
  - `contract_file_url` (varchar 500)
  - `contract_template_id` (uuid FK -> contract_templates)
  - `welcome_policy_template_id` (uuid FK -> welcome_grant_policy_templates)
  - `business_id` (uuid FK -> businesses, populated on approval)
  - `brand_id` (uuid FK -> brands, populated on approval if first brand declared)
  - `submitted_by_user_id` (uuid FK -> user_accounts, populated on approval if first brand declared)
- Index: `IX_business_registration_requests_contract_status`, `IX_business_registration_requests_welcome_policy_template_id`, `IX_business_registration_requests_contract_template_id`

- Table: `contract_templates`
- Columns: `id` (uuid PK), `name` (varchar 200), `html_template` (text), `is_active` (bool), `is_default` (bool), `created_at`, `updated_at`, `created_by`, `updated_by`
- Index: `IX_contract_templates_is_default` (unique filtered index where `is_default = true`)

- Table: `subscription_fee_policies`
- Columns: `id` (uuid PK), `name` (varchar 200), `amount_vnd` (numeric 18,2), `is_free` (bool), `minimum_commitment_months` (int), `effective_from` (timestamptz), `effective_to` (timestamptz nullable), `is_active` (bool), `created_at`, `updated_at`, `created_by`, `updated_by`
- Index: `IX_subscription_fee_policies_effective` on `(is_active, effective_from, effective_to)`

### API Contracts
- `GET /api/v1/admin/registration-requests/pending-contract`
- `GET /api/v1/admin/registration-requests/pending-review`
- `GET /api/v1/admin/registration-requests`
- `GET /api/v1/admin/registration-requests/{requestId}/contract` -> HTML
- `POST /api/v1/admin/registration-requests/{requestId}/send-contract` -> `{ welcomePolicyTemplateId: Guid }`
- `POST /api/v1/admin/registration-requests/{requestId}/upload-signed-contract` -> `{ contractFileUrl: string }`
- `POST /api/v1/admin/registration-requests/{requestId}/approve` -> `{ reviewNotes?: "string" }`
- `POST /api/v1/admin/registration-requests/{requestId}/reject` -> `{ reviewNotes: "string" }` (required)
- `GET /api/v1/contract-templates?includeInactive=false`
- `GET /api/v1/contract-templates/{id}`
- `GET /api/v1/contract-templates/default`
- `POST /api/v1/contract-templates` -> `{ name: string, htmlTemplate: string, isActive: bool, isDefault: bool }`
- `PUT /api/v1/contract-templates/{id}` -> `{ name: string, htmlTemplate: string, isActive: bool, isDefault: bool }`
- `POST /api/v1/contract-templates/{id}/set-default`
- `GET /api/v1/subscription-fee-policies?includeInactive=false`
- `GET /api/v1/subscription-fee-policies/effective`
- `GET /api/v1/subscription-fee-policies/{id}`
- `POST /api/v1/subscription-fee-policies` -> `{ name, amountVnd, isFree, minimumCommitmentMonths, effectiveFrom, effectiveTo, isActive }`
- `PUT /api/v1/subscription-fee-policies/{id}` -> same body
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
- 2026-08-21: Added editable contract templates and subscription fee clause:
  - `ContractTemplate` entity and `IContractTemplateService` for CRUD + default management.
  - Admin Contract Templates page (`/admin/contract-templates`) with HTML editor and placeholder reference.
  - `ContractService` renders contract from active template (or built-in fallback) and replaces placeholders.
  - Subscription Fee section added to default contract: waived during MVP, post-MVP fee from `CreditConfig.SubscriptionFeeVnd`.
  - Minimum commitment period from `CreditConfig.MinimumCommitmentMonths` (default 12 months).
  - Contract template ID stored on `BusinessRegistrationRequest` so the same template is used for reprints.
- 2026-08-21: Added date-ranged `SubscriptionFeePolicy`:
  - `SubscriptionFeePolicy` entity and `ISubscriptionFeePolicyService` with CRUD + effective-date resolution.
  - Admin Subscription Fee Policies page (`/admin/subscription-fee-policies`) with IsFree toggle, amount, commitment months, date pickers.
  - `ContractService` resolves subscription fee and commitment from the active policy for the contract date.
  - `RegistrationService.SendContractAsync` enforces that at least one active `SubscriptionFeePolicy` exists; otherwise contract cannot be sent.
  - Overlap guard: creating/updating an active policy that overlaps with another active policy is rejected, and the admin sees the overlapping policy name and date range.
  - Admin UI shows warnings in `RegistrationRequests.razor` and `SubscriptionFeePolicies.razor` when no active policy covers today.
- 2026-08-21: Added digital contract confirmation as an alternative to signed upload:
  - `ContractStatus` extended with `Confirmed` value.
  - `BusinessRegistrationRequest` stores `ContractConfirmationToken`, `ContractConfirmedAt`, and `ContractConfirmedByIp`.
  - `SendContractAsync` generates an 8-character confirmation token and emails a confirmation link + key to the business.
  - New public Blazor page `/confirm-contract` lets the business enter the key and check "I agree" before confirming.
  - New API endpoint `POST api/v1/public/register/{requestId}/confirm-contract`.
  - `ReviewAsync` allows approval when `ContractStatus` is `Confirmed` or `Signed`; admin UI shows both states in Pending Review.
  - Signed contract upload remains available as an optional alternative.
- 2026-08-21: Replaced signed-contract URL entry with real file upload:
  - New `IDocumentStorageService` with `MsaDocumentStorageService` and `LocalStorageDocumentService` (pdf, jpg, png; 10 MB).
  - New `POST api/v1/upload/document` endpoint (admin-only).
  - `RegistrationRequests.razor` Upload Signed dialog now uses `MudFileUpload`; uploads the file, then attaches the returned URL to the request.
- Contract HTML includes Pricing section and Appendix A for brand-level pricing.
- Print button opens contract in new tab using `contract-print.js` helper.
- All 137 tests pass (76 integration + 61 unit).

### File List

- src/NonCash.Core/Services/RegistrationService.cs
- src/NonCash.Core/Interfaces/IContractService.cs
- src/NonCash.Core/Interfaces/IContractTemplateService.cs
- src/NonCash.Core/Interfaces/ISubscriptionFeePolicyService.cs
- src/NonCash.Core/Entities/ContractTemplate.cs
- src/NonCash.Core/Entities/SubscriptionFeePolicy.cs
- src/NonCash.Core/Configuration/CreditConfig.cs
- src/NonCash.Infrastructure/Services/ContractService.cs
- src/NonCash.Infrastructure/Services/ContractTemplateService.cs
- src/NonCash.Infrastructure/Services/SubscriptionFeePolicyService.cs
- src/NonCash.Infrastructure/EmailTemplates/ContractSent.html
- src/NonCash.Infrastructure/EmailTemplates/ActiveBusiness.html
- src/NonCash.Infrastructure/EmailTemplates/RegistrationRejected.html
- src/NonCash.API/Controllers/RegistrationReviewController.cs
- src/NonCash.API/Controllers/ContractTemplatesController.cs
- src/NonCash.API/Controllers/SubscriptionFeePoliciesController.cs
- src/NonCash.API/DTOs/RegistrationDtos.cs
- src/NonCash.Web/Components/Pages/Admin/RegistrationRequests.razor
- src/NonCash.Web/Components/Pages/Admin/ContractTemplates.razor
- src/NonCash.Web/Components/Pages/Admin/SubscriptionFeePolicies.razor
- src/NonCash.Web/Components/Layout/MainLayout.razor
- src/NonCash.Web/wwwroot/js/contract-print.js
- src/NonCash.Web/Services/ClientAuthService.cs
