# Story 1.5: Business Self-Registration & Onboarding

Status: done

## Story

As a Business Representative,
I want to register my company on the NonCash platform via a self-service form,
So that I can begin the onboarding process and later operate under my first brand once approved.

## Acceptance Criteria

**AC1: Registration Form Submission**
Given a Business Representative visits the public registration page
When they submit the form with: Company Name, Tax Code, Contact Email, Phone Number, Business Address, and Representative Full Name
Then the system validates all required fields and creates a `BusinessRegistrationRequest` record only

**AC2: Optional First Brand Declaration**
Given a registration submission
When the applicant fills the optional First Brand section (Brand Name, Manager Username, Manager Password)
Then the system stores these values on the request for automatic brand/user creation on approval
And if the section is skipped, no brand or user is created at submission time

**AC3: Tax Code Uniqueness Validation**
Given a registration submission
When the Tax Code already exists in an Active Business or a pending `BusinessRegistrationRequest`
Then the system returns 400 / DuplicateTaxCode and prevents duplicate registration

**AC4: Username Uniqueness Validation**
Given a first brand is declared
When the Manager Username already exists in `UserAccount` or another pending request
Then the system returns 400 / Username already exists

**AC5: No Business/Brand/User Created at Submission**
Given validation passes
When the request is stored
Then no `Business`, `Brand`, or `UserAccount` record is created yet
And the request status is `Submitted`

**AC6: Applicant Welcome Page**
Given a registration is submitted
When the system saves the request
Then the applicant is redirected to a welcome page that explains the next steps and shows the request ID

**AC7: Public Status Check**
Given a registration was submitted
When the representative checks status via the public status page
Then they see the current status: Submitted, UnderReview, Approved, or Rejected
And whether a first brand was declared
And if Rejected, they see the reason

**AC8: Admin Notification**
Given a new registration is submitted
When the record is saved
Then the system triggers an admin notification (email or in-app)
So that they can review and start the contract workflow (Story 1.6)

## Tasks / Subtasks

- [x] Task 1: Define BusinessRegistrationRequest entity (AC1, AC2, AC5)
  - [x] Subtask 1.1: `BusinessRegistrationRequest.cs` in `NonCash.Core/Entities/`
  - [x] Subtask 1.2: `RegistrationStatus` enum: Submitted, UnderReview, Approved, Rejected
  - [x] Subtask 1.3: Business info fields: BusinessName, TaxCode, ContactEmail, PhoneNumber, Address, RepresentativeName
  - [x] Subtask 1.4: Optional first-brand fields: FirstBrandName, ManagerUsername, ManagerPasswordHash
  - [x] Subtask 1.5: EF config with optional FKs to Business, Brand, and UserAccount (populated on approval)
- [x] Task 2: Implement registration service (AC1, AC2, AC3, AC4, AC5)
  - [x] Subtask 2.1: `IRegistrationService` with `SubmitAsync(RegistrationRequestDto)`
  - [x] Subtask 2.2: Tax code uniqueness check against existing Businesses and pending requests
  - [x] Subtask 2.3: Username uniqueness check when first brand is declared
  - [x] Subtask 2.4: Hash manager password when first brand is declared
  - [x] Subtask 2.5: `GetStatusAsync(Guid requestId)` returning status + HasFirstBrandDeclaration
- [x] Task 3: Public API endpoint (AC1, AC7)
  - [x] Subtask 3.1: `POST /api/v1/public/register` — no auth required
  - [x] Subtask 3.2: `GET /api/v1/public/register/{requestId}/status` — open status check
  - [x] Subtask 3.3: DTOs: `SubmitBusinessRegistrationRequest`, `BusinessRegistrationResponse`, `RegistrationStatusResponse`
- [x] Task 4: Blazor public pages (AC1, AC6, AC7)
  - [x] Subtask 4.1: `Register.razor` public page with optional First Brand section
  - [x] Subtask 4.2: Form validation mirroring backend rules
  - [x] Subtask 4.3: `RegistrationWelcome.razor` page shown after submission
  - [x] Subtask 4.4: `RegistrationStatus.razor` status check page for applicants
- [x] Task 5: Notification system (AC8)
  - [x] Subtask 5.1: `NotifyAdminNewRegistrationAsync` on submission
  - [x] Subtask 5.2: `NotifyApplicantRegistrationSubmittedAsync` with welcome-page link
- [x] Task 6: Database migration
  - [x] Subtask 6.1: `BusinessRegistrationRequestFirstBrandInfo` migration renaming table from `brand_registration_requests` to `business_registration_requests` and adding first-brand columns
- [x] Task 7: Tests
  - [x] Subtask 7.1: Integration tests for successful registration request creation
  - [x] Subtask 7.2: Integration tests for duplicate TaxCode rejection
  - [x] Subtask 7.3: Integration tests for duplicate username rejection
  - [x] Subtask 7.4: Integration tests for approval creating Business/Brand/User when first brand is declared
  - [x] Subtask 7.5: Integration tests for approval creating Business only when no first brand is declared

## Dev Notes

### Architecture Compliance
- This endpoint is **public** (no JWT/API Key required).
- A registration request is just a request until admin approval. No tenant records exist before approval.
- Optional first-brand data is stored on the request and used only on approval.

### File Structure Requirements
```
src/NonCash.Core/Entities/BusinessRegistrationRequest.cs
src/NonCash.Core/Enums/RegistrationStatus.cs
src/NonCash.Core/Interfaces/IRegistrationService.cs
src/NonCash.Core/Services/RegistrationService.cs
src/NonCash.Core/Interfaces/INotificationService.cs
src/NonCash.Infrastructure/Data/Configurations/BusinessRegistrationRequestConfiguration.cs
src/NonCash.API/Controllers/PublicRegistrationController.cs
src/NonCash.API/DTOs/RegistrationDtos.cs
src/NonCash.Web/Components/Pages/Public/Register.razor
src/NonCash.Web/Components/Pages/Public/RegistrationWelcome.razor
src/NonCash.Web/Components/Pages/Public/RegistrationStatus.razor
```

### Database Schema
- Table: `business_registration_requests`
- Columns:
  - `id` (uuid PK)
  - `business_name` (varchar 200, not null)
  - `tax_code` (varchar 50, not null)
  - `contact_email` (varchar 255)
  - `phone_number` (varchar 50)
  - `address` (varchar 500)
  - `representative_name` (varchar 200, not null)
  - `first_brand_name` (varchar 200)
  - `manager_username` (varchar 100)
  - `manager_password_hash` (varchar 500)
  - `business_id` (uuid FK -> businesses, nullable)
  - `brand_id` (uuid FK -> brands, nullable)
  - `submitted_by_user_id` (uuid FK -> user_accounts, nullable)
  - `submitted_at` (timestamptz not null)
  - `status` (varchar 20)
  - `review_notes` (text)
  - `reviewed_at` (timestamptz nullable)
  - `reviewed_by_user_id` (uuid FK nullable)
  - Contract workflow columns: `contract_status`, `contract_sent_at`, `contract_file_url`, `welcome_policy_template_id`
- Index: `IX_business_registration_requests_status`, `IX_business_registration_requests_contract_status`

### API Contracts
- `POST /api/v1/public/register` -> `{ companyName, taxCode, contactEmail, phoneNumber, address, representativeName, firstBrandName?, managerUsername?, managerPassword? }` => `{ requestId, status: "Submitted" }`
- `GET /api/v1/public/register/{requestId}/status` => `{ status, submittedAt, reviewedAt, reviewNotes, hasFirstBrandDeclaration }`
- 400 on duplicate TaxCode or duplicate username

### Security & NFR
- NFR3 (RBAC): Public endpoint. No role required.
- Rate limit: max 5 registration attempts per IP per hour.
- Do NOT return the manager password in any API response.

### Testing Standards
- Test concurrent registrations with the same TaxCode. Only one should succeed.
- Test approval creates the correct records based on whether first brand was declared.

## Dev Agent Record

### Agent Model Used

Qoder AI Assistant

### Completion Notes List

- 2026-08-20: Refactored from early Brand/User creation to request-only model.
- Business, Brand, and UserAccount are created on admin approval, not submission.
- Added optional First Brand declaration to the public registration form.
- Added `RegistrationWelcome.razor` page shown immediately after submission.
- Added welcome-page URL to submission acknowledgment email via `WebBaseUrl` configuration.
- Migration `BusinessRegistrationRequestFirstBrandInfo` applied to PostgreSQL with backfill CTE for old data.
- All 137 tests pass (76 integration + 61 unit).

### File List

- src/NonCash.Core/Entities/BusinessRegistrationRequest.cs
- src/NonCash.Core/Services/RegistrationService.cs
- src/NonCash.Core/Interfaces/INotificationService.cs
- src/NonCash.Infrastructure/Data/Configurations/BusinessRegistrationRequestConfiguration.cs
- src/NonCash.Infrastructure/Migrations/20260820084928_BusinessRegistrationRequestFirstBrandInfo.cs
- src/NonCash.Infrastructure/EmailTemplates/ApplicantRegistrationSubmitted.html
- src/NonCash.Infrastructure/EmailTemplates/AdminNewRegistration.html
- src/NonCash.API/Controllers/PublicRegistrationController.cs
- src/NonCash.API/DTOs/RegistrationDtos.cs
- src/NonCash.Web/Components/Pages/Public/Register.razor
- src/NonCash.Web/Components/Pages/Public/RegistrationWelcome.razor
- src/NonCash.Web/Components/Pages/Public/RegistrationStatus.razor
- tests/NonCash.IntegrationTests/Controllers/PublicRegistrationControllerTests.cs
