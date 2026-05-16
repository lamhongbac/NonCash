# Story 1.5: Business Self-Registration & Onboarding

Status: review

## Story

As a Business Representative,
I want to register my company on the NonCash platform via a self-service form,
So that I can begin using the voucher production and distribution system without manual admin intervention for the initial signup.

## Acceptance Criteria

**AC1: Registration Form Submission**
Given a Business Representative visits the public registration page
When they submit the form with: Company Name, Tax Code, Contact Email, Phone Number, Business Address, and Representative Full Name
Then the system validates all required fields and creates a `BrandRegistrationRequest` record

**AC2: Tax Code Uniqueness Validation**
Given a registration submission
When the Tax Code already exists in an Active or PendingActivation Brand
Then the system returns 400 / DuplicateTaxCode and prevents duplicate registration

**AC3: Brand Record Creation (Pending)**
Given validation passes
When the request is accepted
Then the system creates a `Brand` record with `Status = PendingActivation`
And creates an initial `UserAccount` for the representative with `Role = BrandManager` and `Status = PendingActivation`
And generates a temporary password hash (user must reset on first login after approval)

**AC4: Registration Request Tracking**
Given a registration is submitted
When stored
Then a `BrandRegistrationRequest` record persists with: `RequestID`, `BrandID` (FK), `SubmittedAt`, `Status` (Submitted | UnderReview | Approved | Rejected), `ReviewNotes` (nullable)

**AC5: Admin Notification**
Given a new registration is submitted
When the record is saved
Then the system triggers a notification (email or in-app) to Platform Admins
So that they can review and approve/reject (Story 1.6)

**AC6: Public Status Check**
Given a registration was submitted
When the representative checks status via a tokenized link or login attempt
Then they see the current status: Submitted, UnderReview, Approved, or Rejected
And if Rejected, they see the reason

## Tasks / Subtasks

- [x] Task 1: Define BrandRegistrationRequest entity (AC4)
  - [x] Subtask 1.1: `BrandRegistrationRequest.cs` in `NonCash.Core/Entities/`
  - [x] Subtask 1.2: `RegistrationStatus` enum: Submitted, UnderReview, Approved, Rejected
  - [x] Subtask 1.3: EF config with FK to Brand and UserAccount
- [x] Task 2: Implement registration service (AC1, AC2, AC3)
  - [x] Subtask 2.1: `IRegistrationService` with `SubmitAsync(RegistrationRequestDto)`
  - [x] Subtask 2.2: Tax code uniqueness check against existing Brands
  - [x] Subtask 2.3: Create Brand (PendingActivation) + UserAccount (BrandManager, PendingActivation) atomically
  - [x] Subtask 2.4: Generate secure temporary password and hash it
- [x] Task 3: Public API endpoint (AC1, AC2, AC6)
  - [x] Subtask 3.1: `POST /api/v1/public/register` — no auth required
  - [x] Subtask 3.2: `GET /api/v1/public/register/{requestId}/status` — open status check
  - [x] Subtask 3.3: DTOs: `BusinessRegistrationRequest`, `RegistrationStatusResponse`
- [x] Task 4: Blazor public page (AC1, AC6)
  - [x] Subtask 4.1: `Register.razor` public page (no auth required)
  - [x] Subtask 4.2: Form validation mirroring backend rules
  - [x] Subtask 4.3: Status check page for applicants
- [x] Task 5: Notification placeholder (AC5)
  - [x] Subtask 5.1: `INotificationService` interface (placeholder for future email/SMS integration)
  - [x] Subtask 5.2: Console notification logging for MVP
- [x] Task 6: Database migration
  - [x] Subtask 6.1: `brand_registration_requests` table migration (AddBrandRegistrationRequest)
  - [x] Subtask 6.2: `PendingActivation` added to `BrandStatus` and `UserStatus` enums
- [x] Task 7: Tests
  - [x] Subtask 7.1: Integration tests for successful registration flow
  - [x] Subtask 7.2: Integration tests for duplicate TaxCode rejection
  - [x] Subtask 7.3: Integration tests for duplicate username rejection
  - [x] Subtask 7.4: Integration tests for PendingActivation login blocking

## Dev Notes

### Architecture Compliance
- This endpoint is **public** (no JWT/API Key required). Apply rate limiting (`AspNetCoreRateLimit` or built-in middleware) to prevent abuse.
- The `UserAccount` created here is **inactive until approval**. Login attempts must return `AccountPendingActivation`.
- Do NOT allow login for `PendingActivation` accounts. Block at the authentication layer.
- The `BrandRegistrationRequest` bridges the gap between self-service signup and admin approval.

### File Structure Requirements
```
src/NonCash.Core/Entities/BrandRegistrationRequest.cs
src/NonCash.Core/Enums/RegistrationStatus.cs
src/NonCash.Core/Interfaces/IRegistrationService.cs
src/NonCash.Core/Services/RegistrationService.cs
src/NonCash.Core/Interfaces/INotificationService.cs
src/NonCash.Infrastructure/Data/Configurations/BrandRegistrationRequestConfiguration.cs
src/NonCash.API/Controllers/PublicRegistrationController.cs
src/NonCash.Web/Pages/Public/Register.razor
src/NonCash.Web/Pages/Public/RegistrationStatus.razor
```

### Database Schema
- Table: `brand_registration_requests`
- Columns: `request_id` (uuid PK), `brand_id` (uuid FK not null), `submitted_by_user_id` (uuid FK not null), `submitted_at` (timestamptz not null), `status` (varchar 20), `review_notes` (text), `reviewed_at` (timestamptz nullable), `reviewed_by_user_id` (uuid FK nullable)
- Index: `IX_brand_registration_requests_brand_id`, `IX_brand_registration_requests_status`

### API Contracts
- `POST /api/v1/public/register` -> `{ companyName, taxCode, contactEmail, phoneNumber, address, representativeName }` => `{ requestId, brandId, status: "Submitted" }`
- `GET /api/v1/public/register/{requestId}/status` => `{ status, submittedAt, reviewedAt, reviewNotes }`
- 400 on duplicate TaxCode

### Security & NFR
- NFR3 (RBAC): Public endpoint. No role required.
- NFR4: The created Brand is isolated even while pending. No cross-tenant leakage.
- Rate limit: max 5 registration attempts per IP per hour.
- Do NOT return the temporary password in any API response. It is only used internally and reset on first post-approval login.

### Testing Standards
- Test concurrent registrations with the same TaxCode. Only one should succeed.
- Test that `PendingActivation` accounts cannot authenticate.

### References
- [Source: docs/data-models.md#Brand] — Brand entity fields.
- [Source: Key Functionalities.txt#V] — Business Management context.

## Dev Agent Record

### Agent Model Used

Qoder AI Assistant

### Debug Log References

- `RegistrationResult` positional record caused CS7036 errors — converted to explicit class with multiple constructors
- `RegistrationStatusResponse` naming conflict between Core and API DTOs — renamed Core type to `RegistrationStatusInfo`
- `IBrandRepository` did not have `SearchAsync` — used existing `GetByTaxCodeAsync` instead
- `ErrorMessage:` named parameter failed due to constructor parameter being `errorMessage` (lowercase) — fixed by using positional parameters

### Completion Notes List

- `PendingActivation` added to both `BrandStatus` and `UserStatus` enums
- AuthService blocks login for `PendingActivation` accounts with "Account is pending activation." message
- Registration flow: submit -> create Brand (PendingActivation) + UserAccount (BrandManager, PendingActivation) + RegistrationRequest (Submitted)
- Tax code uniqueness enforced against Active and PendingActivation brands (Suspended brands can be re-registered)
- ConsoleNotificationService logs admin notifications to stdout for MVP placeholder
- All 77 tests pass (66 previous + 11 new registration tests)
- Migration `AddBrandRegistrationRequest` applied to PostgreSQL

### File List

- src/NonCash.Core/Entities/BrandRegistrationRequest.cs (entity + RegistrationStatus enum)
- src/NonCash.Core/Entities/Brand.cs (added PendingActivation to BrandStatus)
- src/NonCash.Core/Entities/UserAccount.cs (added PendingActivation to UserStatus)
- src/NonCash.Core/Interfaces/IRegistrationService.cs (via RegistrationService.cs)
- src/NonCash.Core/Interfaces/INotificationService.cs
- src/NonCash.Core/Interfaces/IBrandRegistrationRequestRepository.cs
- src/NonCash.Core/Services/RegistrationService.cs (SubmitAsync + GetStatusAsync)
- src/NonCash.Core/Services/AuthService.cs (blocked PendingActivation login)
- src/NonCash.Infrastructure/Data/Configurations/BrandRegistrationRequestConfiguration.cs
- src/NonCash.Infrastructure/Data/ApplicationDbContext.cs (DbSet<BrandRegistrationRequest>)
- src/NonCash.Infrastructure/Repositories/BrandRegistrationRequestRepository.cs
- src/NonCash.Infrastructure/Services/ConsoleNotificationService.cs
- src/NonCash.API/Controllers/PublicRegistrationController.cs
- src/NonCash.API/DTOs/RegistrationDtos.cs
- src/NonCash.API/Program.cs (DI registrations)
- src/NonCash.Web/Components/Pages/Public/Register.razor
- src/NonCash.Web/Components/Pages/Public/RegistrationStatus.razor
- tests/NonCash.IntegrationTests/Controllers/PublicRegistrationControllerTests.cs (11 tests)

