# Story 4.4: Huy Quy doi (Rollback Mechanism)

Status: ready-for-dev

## Story

As a POS system at the counter,
I want to send a rollback/cancel command if the bill transaction fails,
So that the voucher is released back to Pending status and can be used again.

## Acceptance Criteria

**AC1: Rollback Endpoint**
Given a voucher is in `InUse` status with a valid `LockID`
When the POS sends `POST /api/v1/pos/rollback` with `{ lockID: "..." }`
Then the system validates the lock exists and is not expired
And atomically updates `UsageStatus` back to `Pending`
And clears `LockID`, `LockedAt`, and `BillNumber`
And does NOT create any `VoucherUsage` record

**AC2: Already Complete Rejection**
Given a voucher is already `Complete`
When rollback is attempted
Then the system returns 409 / AlreadyCompleted
And no changes are made

**AC3: Expired Lock Handling**
Given a lock has already expired and auto-released
When rollback is called with the expired lockID
Then the system returns 200 / AlreadyReleased (or 404)
Since the voucher is already back to Pending

**AC4: Idempotency**
Given the same rollback request is sent twice
When the Backend receives the duplicate
Then it returns success without error
And the voucher remains Pending

**AC5: Audit Trail (Optional)**
Given a rollback occurs
When the operation completes
Then an optional `VoucherDistribution` or audit log record with a special rollback note may be created for traceability
**Note:** For MVP, rollback without audit log is acceptable. Document this gap.

## Tasks / Subtasks

- [ ] Task 1: Implement rollback service (AC1, AC2, AC3, AC4)
  - [ ] Subtask 1.1: `IPosService.RollbackAsync(string lockId)`
  - [ ] Subtask 1.2: Validate lock exists and matches an `InUse` voucher
  - [ ] Subtask 1.3: Atomic update: `UsageStatus = Pending`, clear lock fields
  - [ ] Subtask 1.4: Guard against rollback of `Complete` vouchers -> 409
  - [ ] Subtask 1.5: Handle expired locks gracefully (voucher already Pending)
- [ ] Task 2: API endpoint (AC1, AC2, AC3, AC4)
  - [ ] Subtask 2.1: `POST /api/v1/pos/rollback`
  - [ ] Subtask 2.2: DTOs: `PosRollbackRequest`, `PosRollbackResponse`
- [ ] Task 3: Tests
  - [ ] Subtask 3.1: Unit tests for rollback atomicity
  - [ ] Subtask 3.2: Integration tests for rollback of Complete voucher (409)
  - [ ] Subtask 3.3: Integration tests for idempotency
  - [ ] Subtask 3.4: Integration tests for expired lock rollback (graceful handling)

## Dev Notes

### Architecture Compliance
- Rollback is the **compensating transaction** for the POS flow. It must be as safe as commit.
- The atomic update pattern is identical to lock: `UPDATE ... WHERE lock_id = @lockId AND usage_status = 'InUse'`.
- If row affected = 0, check if voucher is already Pending (expired lock) or Complete (reject).
- Do NOT create `VoucherUsage` records on rollback.

### File Structure Requirements
```
src/NonCash.Core/Interfaces/IPosService.cs
src/NonCash.Core/Services/PosService.cs
src/NonCash.API/Controllers/PosController.cs
```

### Database Schema
- Updates `voucher_plan_details` only (clear lock fields, reset status).
- No new tables.

### API Contracts
- `POST /api/v1/pos/rollback`
- Header: `X-API-Key: <outlet_api_key>`
- Request: `{ lockID: "guid" }`
- Response (Success): `{ status: "Success", message: "Voucher released" }`
- Response (AlreadyComplete): `{ status: "AlreadyCompleted" }` (HTTP 409)
- Response (AlreadyReleased): `{ status: "AlreadyReleased" }` (HTTP 200)

### Security & NFR
- NFR2 (Transaction integrity): Rollback must be atomic. A failed rollback must not leave a voucher stuck in `InUse`.
- The same API Key auth applies. Only the outlet that locked the voucher should be able to rollback (enforced by `lock_id` matching).

### Testing Standards
- Test sequence: Lock -> Rollback -> Verify returns valid -> Lock again -> Commit. This proves full cycle works.
- Test that rollback after commit returns 409 and leaves usage record intact.

### References
- [Source: docs/api-contracts.md#POS Integration API] — Rollback endpoint spec.
- [Source: Key Functionalities.txt#IV] — POS rollback flow (huy su dung -> quay ve pending).
- [Source: docs/architecture.md#BLL] — Usage Service transaction orchestration.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

