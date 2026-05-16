# Story 4.1: Tra cuu Thong tin Voucher (Check for Information / Verify)

Status: ready-for-dev

## Story

As a POS system at the counter,
I want to send a voucher code to the Backend WITHOUT a BillNumber,
So that the cashier can quickly check the voucher value without affecting its status.

## Acceptance Criteria

**AC1: Verify Endpoint**
Given a POS system sends a `POST /api/v1/pos/verify` request
With body `{ voucherCode: "...", outletID: "..." }` and valid API Key header
When the Backend receives it
Then it validates the dynamic voucher code signature and expiry
And checks that the voucher belongs to a plan with the outlet in its `SalesRange`
And checks that `UsageStatus = Pending` and current date is within validity window

**AC2: Non-Mutating Response**
Given a valid verify request
When the Backend responds
Then it returns `{ status: "Valid", voucherInfo: { faceValue, expiryDate, brandName } }`
And the voucher's `UsageStatus` remains `Pending`
And no locks or usage records are created

**AC3: Invalid Code Handling**
Given an invalid, expired, or forged voucher code
When verify is called
Then the response is `{ status: "Invalid", reason: "..." }`
And HTTP 200 (to prevent POS error handling confusion) or 400 based on API contract preference

**AC4: Outlet Scope Validation**
Given a valid voucher for Brand A
When the POS at an Outlet belonging to Brand B calls verify
Then the response is `{ status: "Invalid", reason: "OutletNotAuthorized" }`

**AC5: Time Window Validation**
Given a voucher is outside its `ValidFrom-ValidTo` range or past `ExpiryDate`
When verify is called
Then the response indicates the specific time-based invalidity reason

## Tasks / Subtasks

- [ ] Task 1: Implement POS verify service (AC1, AC2, AC3, AC4, AC5)
  - [ ] Subtask 1.1: `IPosService` with `VerifyAsync(string voucherCode, Guid outletId)`
  - [ ] Subtask 1.2: Decode and validate dynamic voucher code (reuse `IVoucherCodeService` from Story 2.2)
  - [ ] Subtask 1.3: Lookup `VoucherPlanDetail` + `VoucherPlanHeader` + `PlanOutlet` join
  - [ ] Subtask 1.4: Validate outlet belongs to plan's SalesRange
  - [ ] Subtask 1.5: Validate time windows and UsageStatus
- [ ] Task 2: API Key middleware (AC1)
  - [ ] Subtask 2.1: `ApiKeyMiddleware` in `NonCash.API/Middleware/`
  - [ ] Subtask 2.2: Read `X-API-Key` header, validate against `Outlet.ApiKey` or a platform secret
  - [ ] Subtask 2.3: Attach Outlet claims to HttpContext for downstream use
- [ ] Task 3: POS API endpoint (AC1, AC2, AC3, AC4, AC5)
  - [ ] Subtask 3.1: `PosController` in `NonCash.API/Controllers/`
  - [ ] Subtask 3.2: `POST /api/v1/pos/verify`
  - [ ] Subtask 3.3: DTOs: `PosVerifyRequest`, `PosVerifyResponse`
- [ ] Task 4: Tests
  - [ ] Subtask 4.1: Unit tests for each validation branch (valid, expired, wrong outlet, used, blacklisted customer)
  - [ ] Subtask 4.2: Integration tests for API Key rejection
  - [ ] Subtask 4.3: Integration tests verifying no DB mutation on verify

## Dev Notes

### Architecture Compliance
- POS endpoints are **API-Key authenticated**, NOT JWT. Implement `ApiKeyMiddleware` before JWT auth in the pipeline.
- The POS flow is stateless. Verify is read-only and must never mutate voucher state.
- Dynamic code validation must use the same secret/key that generated the code (Story 2.2).

### File Structure Requirements
```
src/NonCash.Core/Interfaces/IPosService.cs
src/NonCash.Core/Services/PosService.cs
src/NonCash.API/Controllers/PosController.cs
src/NonCash.API/Middleware/ApiKeyMiddleware.cs
src/NonCash.API/DTOs/PosDtos.cs
```

### Database Schema
- Reads `voucher_plan_details`, `voucher_plan_headers`, `plan_outlets`, `outlets`.
- No writes in this story.

### API Contracts
- `POST /api/v1/pos/verify`
- Header: `X-API-Key: <outlet_api_key>`
- Request: `{ voucherCode: "string", outletID: "guid" }`
- Response (Valid): `{ status: "Valid", voucherInfo: { faceValue: 100000, expiryDate: "2026-12-31", brandName: "..." } }`
- Response (Invalid): `{ status: "Invalid", reason: "Expired|Forged|OutletNotAuthorized|AlreadyUsed|NotYetValid" }`

### Security & NFR
- NFR1: Dynamic code validation prevents static code copy-fraud.
- NFR4: Outlet scope validation enforces multi-tenancy at the POS level.
- API Keys must be rotated periodically. Store hashed API keys, not plaintext.

### Testing Standards
- Use `WebApplicationFactory` with middleware configured.
- Verify that calling verify 100 times does not change `UsageStatus`.

### References
- [Source: docs/api-contracts.md#POS Integration API] — Verify endpoint spec.
- [Source: Key Functionalities.txt#IV] — POS verify flow (no BillNumber).
- [Source: docs/architecture.md#Security Architecture] — API Key and dynamic code requirements.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

