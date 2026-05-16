# Story 4.2: Kiem tra & Khoa can tru (Prepare & Lock for Application)

Status: ready-for-dev

## Story

As a POS system at the counter,
I want to send a voucher code WITH a BillNumber,
So that the Backend returns the value AND immediately Locks the voucher to prevent double-spending.

## Acceptance Criteria

**AC1: Lock Endpoint**
Given a POS sends `POST /api/v1/pos/lock`
With `{ voucherCode: "...", outletID: "...", billNumber: "..." }` and valid API Key
When the Backend processes it
Then it performs all verify validations (signature, outlet scope, time, status)
And if valid, atomically updates `UsageStatus` from `Pending` to `InUse`
And records `BillNumber` in a transient lock store or on the Detail record
And returns `{ status: "Locked", lockID: "guid" }`

**AC2: Lock Uniqueness**
Given a voucher is already `InUse`
When another lock request arrives for the same voucher
Then the response is `{ status: "AlreadyInUse", lockID: "existing-guid" }` or error
And no second lock is granted

**AC3: Lock Expiry (Optional but Recommended)**
Given a voucher is locked
When 10 minutes pass without commit or rollback
Then the system auto-releases the lock (background job or query-time check)
And `UsageStatus` returns to `Pending`

**AC4: Idempotency**
Given the same POS sends the same lock request twice (network retry)
When the Backend receives the duplicate
Then it returns the same `lockID` without error
And the voucher remains `InUse`

## Tasks / Subtasks

- [ ] Task 1: Implement lock service (AC1, AC2, AC4)
  - [ ] Subtask 1.1: `IPosService.LockAsync(...)` extending verify logic
  - [ ] Subtask 1.2: Atomic update using `UPDATE ... WHERE UsageStatus = Pending` with row-level locking
  - [ ] Subtask 1.3: Generate `LockID` (GUID) stored in a `VoucherLock` table or on the Detail record
  - [ ] Subtask 1.4: Idempotency: check existing lock by `(voucherId, outletId, billNumber)` tuple
- [ ] Task 2: Lock expiry mechanism (AC3)
  - [ ] Subtask 2.1: `VoucherLock` entity with `CreatedAt`, `ExpiresAt`
  - [ ] Subtask 2.2: Background service `LockCleanupService` running every minute to release expired locks
  - [ ] Subtask 2.3: Or use query-time filter: treat locks older than X minutes as expired
- [ ] Task 3: API endpoint (AC1, AC2, AC4)
  - [ ] Subtask 3.1: `POST /api/v1/pos/lock`
  - [ ] Subtask 3.2: DTOs: `PosLockRequest`, `PosLockResponse`
- [ ] Task 4: Database migration
  - [ ] Subtask 4.1: `voucher_locks` table or extend `voucher_plan_details` with `lock_id`, `locked_at`, `bill_number`
- [ ] Task 5: Tests
  - [ ] Subtask 5.1: Unit tests for atomic lock (concurrent lock attempts)
  - [ ] Subtask 5.2: Integration tests for idempotency
  - [ ] Subtask 5.3: Integration tests for lock expiry

## Dev Notes

### Architecture Compliance
- Locking is the **most critical concurrency point** in the system. Use database-level pessimistic locking or atomic conditional updates.
- Recommended pattern: `UPDATE voucher_plan_details SET usage_status = 'InUse', lock_id = @lockId, locked_at = @now, bill_number = @bill WHERE id = @id AND usage_status = 'Pending'`.
- If row affected = 0, another process got the lock first. Return conflict.
- The `LockID` acts as a distributed transaction token for commit/rollback.

### File Structure Requirements
```
src/NonCash.Core/Entities/VoucherLock.cs (if separate table)
src/NonCash.Core/Interfaces/IPosService.cs
src/NonCash.Core/Services/PosService.cs
src/NonCash.API/Controllers/PosController.cs
src/NonCash.API/HostedServices/LockCleanupService.cs
```

### Database Schema
- Option A (separate table): `voucher_locks` (`lock_id` uuid PK, `voucher_id` uuid FK unique, `outlet_id` uuid, `bill_number` varchar 100, `created_at` timestamptz, `expires_at` timestamptz)
- Option B (extend detail): add columns to `voucher_plan_details`: `lock_id`, `locked_at`, `bill_number`
- **Recommendation:** Option B is simpler and avoids joins during the hot path. Use Option B.

### API Contracts
- `POST /api/v1/pos/lock`
- Header: `X-API-Key: <outlet_api_key>`
- Request: `{ voucherCode: "string", outletID: "guid", billNumber: "string" }`
- Response (Locked): `{ status: "Locked", lockID: "guid" }`
- Response (InUse): `{ status: "AlreadyInUse" }` (HTTP 409)
- Response (Invalid): `{ status: "Invalid", reason: "..." }`

### Security & NFR
- NFR2 (Transaction integrity): Lock is the BEGIN of the POS transaction. It must be atomic.
- NFR1: Dynamic code validation applies here too.
- Race condition protection is paramount. Test with concurrent load.

### Testing Standards
- Load test: 100 parallel lock requests on the same voucher. Exactly 1 should succeed, 99 should get AlreadyInUse or Invalid.
- Verify idempotency by sending identical requests with same billNumber within seconds.

### References
- [Source: docs/api-contracts.md#POS Integration API] — Lock endpoint spec.
- [Source: Key Functionalities.txt#IV] — POS lock flow (khoa VC).
- [Source: docs/architecture.md#BLL] — Usage Service orchestration.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

