# Story 2.2: Sinh lo chi tiet Voucher (Generate Plan Details)

Status: ready-for-dev

## Story

As a System/Worker,
I want to support two mechanisms for generating VoucherPlanDetail entities (pre-generated batch or on-demand),
So that the system can flexibly provision vouchers while strictly protecting the plan's approved state.

## Acceptance Criteria

**AC1: Approval Gate**
Given a request to generate VoucherPlanDetails
When the parent plan's `ApprovalStatus` is NOT `Approved`
Then the system blocks immediately and returns error 400 / PlanNotApproved

**AC2: Batch Generation**
Given an Approved plan with `TargetQuantity = N`
When the system triggers batch generation
Then it creates exactly N `VoucherPlanDetail` records
Each with: `ID` (GUID), `ParentID` (FK to Plan), `SerialNo` (unique string), `VoucherCode` (dynamic/JWT-like secure token), `UsageStatus = Pending`, `MemberID = null`, `UsedDate = null`

**AC3: On-Demand Generation**
Given an Approved plan
When an authorized process requests additional vouchers (e.g., for a large sale order exceeding pre-generated stock)
Then the system generates new Details on demand
And updates the plan's actual quantity tracking

**AC4: SerialNo Uniqueness**
Given any generated Detail
When persisted
Then `SerialNo` is globally unique (format suggestion: `VC-{BrandCode}-{YYYY}-{sequential}` or UUID-based)
And `VoucherCode` is a time-rotating dynamic code (JWT-like: signed payload with expiry, regenerate every 60 seconds or on each fetch)

**AC5: Security of VoucherCode**
Given a VoucherCode is generated
When inspected
Then it MUST be a short-lived signed token (e.g., JWT with `voucherId`, `iat`, `exp` of 2 minutes)
And the POS verification endpoint will validate signature and expiry
And the Member App will display the current code by fetching from the API (not storing static codes)

## Tasks / Subtasks

- [ ] Task 1: Define VoucherPlanDetail entity (AC2, AC4)
  - [ ] Subtask 1.1: `VoucherPlanDetail.cs` in `NonCash.Core/Entities/`
  - [ ] Subtask 1.2: `UsageStatus` enum: Pending, InUse, Complete
  - [ ] Subtask 1.3: EF config with FK to `VoucherPlanHeader`, unique index on `SerialNo`
- [ ] Task 2: Dynamic Voucher Code service (AC4, AC5)
  - [ ] Subtask 2.1: `IVoucherCodeService` with `GenerateCodeAsync(Guid voucherDetailId)`, `ValidateCodeAsync(string code)`
  - [ ] Subtask 2.2: Implement JWT-style token: payload `{ vid: voucherDetailId, iat, exp }`, signed with HMAC-SHA256
  - [ ] Subtask 2.3: Store a `CodeSecret` per Detail (or per Brand) to enable revocation
- [ ] Task 3: Batch generation service (AC1, AC2)
  - [ ] Subtask 3.1: `IVoucherGenerationService` with `GenerateBatchAsync(Guid planId, int quantity)`
  - [ ] Subtask 3.2: Transaction wrapper: all N records inserted atomically
  - [ ] Subtask 3.3: Update `VoucherPlanHeader` with generated count (if tracking field exists)
- [ ] Task 4: On-demand generation endpoint (AC3)
  - [ ] Subtask 4.1: `POST /api/v1/plans/{planId}/generate` (Admin/Service role)
  - [ ] Subtask 4.2: Request body `{ quantity: int }`
  - [ ] Subtask 4.3: Validation: total generated <= some max limit or unlimited based on business rule
- [ ] Task 5: Background worker (optional but recommended)
  - [ ] Subtask 5.1: `IHostedService` or `BackgroundService` to auto-generate batch when plan transitions to Approved
- [ ] Task 6: Blazor UI
  - [ ] Subtask 6.1: On Plan approval, show "Generate Vouchers" button
  - [ ] Subtask 6.2: Progress indicator for batch generation
- [ ] Task 7: Database migration
  - [ ] Subtask 7.1: `voucher_plan_details` table migration
- [ ] Task 8: Tests
  - [ ] Subtask 8.1: Unit tests for `VoucherCodeService` — signature valid/invalid, expiry
  - [ ] Subtask 8.2: Integration tests for generation blocked on non-approved plan
  - [ ] Subtask 8.3: Integration tests for SerialNo uniqueness under concurrent generation

## Dev Notes

### Architecture Compliance
- This story implements **NFR1 (Dynamic Voucher Code)**. The code must NOT be a static string stored in the database. Instead, store a `SecretKey` per voucher and generate signed tokens on the fly.
- The `VoucherCode` displayed to the user is ephemeral. The Member App calls `GET /api/v1/member/vouchers` and receives the current valid code computed at request time.
- Use `System.IdentityModel.Tokens.Jwt` or manual HMAC-SHA256 for the token. Keep it simple: no need for full OIDC.

### File Structure Requirements
```
src/NonCash.Core/Entities/VoucherPlanDetail.cs
src/NonCash.Core/Enums/UsageStatus.cs
src/NonCash.Core/Interfaces/IVoucherCodeService.cs
src/NonCash.Core/Interfaces/IVoucherGenerationService.cs
src/NonCash.Core/Services/VoucherCodeService.cs
src/NonCash.Core/Services/VoucherGenerationService.cs
src/NonCash.Infrastructure/Data/Configurations/VoucherPlanDetailConfiguration.cs
src/NonCash.API/Controllers/VoucherGenerationController.cs
```

### Database Schema
- Table: `voucher_plan_details`
- Columns: `id` (uuid PK), `parent_id` (uuid FK not null), `serial_no` (varchar 50 unique not null), `voucher_code_secret` (varchar 255 not null), `member_id` (uuid FK nullable), `usage_status` (varchar 20), `used_date` (timestamptz nullable), `created_at` (timestamptz)
- Index: `IX_voucher_plan_details_parent_id`, `IX_voucher_plan_details_serial_no` (unique), `IX_voucher_plan_details_member_id`

### API Contracts
- `POST /api/v1/plans/{planId}/generate` -> `{ quantity: 100 }` => `{ generatedCount: 100 }`
- Token payload for VoucherCode: `{ "vid": "guid", "iat": 1234567890, "exp": 1234567950 }`
- Token signing key should be the `voucher_code_secret` stored per detail (or a platform secret + detail salt).

### Security & NFR
- NFR1: Dynamic Voucher Code is the core deliverable. A stolen screenshot of a code is useless after 60 seconds.
- Concurrent batch generation must use DB-level uniqueness constraint on `SerialNo` to prevent race-condition duplicates.
- Do not return the `voucher_code_secret` in any API response.

### Testing Standards
- Simulate time travel in unit tests to verify token expiry rejection.
- Use `Parallel.ForEach` in integration tests to stress-test SerialNo uniqueness.

### References
- [Source: docs/data-models.md#VoucherPlanDetail] — Entity fields.
- [Source: Key Functionalities.txt#I] — Plan detail attributes, SerialNo, VoucherCode logic.
- [Source: docs/architecture.md#Security Architecture] — Dynamic security requirement.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

