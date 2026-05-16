# Story 4.3: Ghi nhan Giao dich Thanh cong (Commit & Log)

Status: ready-for-dev

## Story

As a POS system at the counter,
I want to send a commit command after the bill is successfully paid,
So that the voucher is permanently marked as used and the Backend records the transaction.

## Acceptance Criteria

**AC1: Commit Endpoint**
Given a voucher is in `InUse` status with a valid `LockID`
When the POS sends `POST /api/v1/pos/commit` with `{ lockID: "...", transactionID: "...", amountUsed: 123.45 }`
Then the system validates the lock exists and is not expired
And atomically updates `UsageStatus` to `Complete`
And sets `UsedDate = Now()`
And creates a `VoucherUsage` record

**AC2: VoucherUsage Record**
Given a commit succeeds
When the record is persisted
Then it contains: `ID`, `VoucherID`, `POSID` (from API Key context), `TransactionID`, `UsageDate`, `AmountUsed`

**AC3: Lock Invalidation**
Given a commit succeeds
When the transaction completes
Then the `LockID` is cleared/nullified
And the voucher cannot be committed again

**AC4: Expired Lock Rejection**
Given a lock has expired (auto-released by cleanup)
When commit is called with the expired lockID
Then the system returns 409 / LockExpired
And advises the POS to re-verify and re-lock

**AC5: Idempotency**
Given the same commit request is sent twice (network retry)
When the Backend receives the duplicate
Then it returns success without creating duplicate `VoucherUsage` records

## Tasks / Subtasks

- [ ] Task 1: Implement commit service (AC1, AC3, AC4, AC5)
  - [ ] Subtask 1.1: `IPosService.CommitAsync(string lockId, string transactionId, decimal amountUsed)`
  - [ ] Subtask 1.2: Validate lock exists, matches voucher, and is not expired
  - [ ] Subtask 1.3: Atomic update: `UsageStatus = Complete`, `UsedDate = now`, clear `LockID`
  - [ ] Subtask 1.4: Insert `VoucherUsage` record in same transaction
  - [ ] Subtask 1.5: Idempotency: check existing `VoucherUsage` by `TransactionID` before insert
- [ ] Task 2: API endpoint (AC1, AC4, AC5)
  - [ ] Subtask 2.1: `POST /api/v1/pos/commit`
  - [ ] Subtask 2.2: DTOs: `PosCommitRequest`, `PosCommitResponse`
- [ ] Task 3: Database migration
  - [ ] Subtask 3.1: `voucher_usages` table migration
- [ ] Task 4: Tests
  - [ ] Subtask 4.1: Unit tests for commit transaction atomicity
  - [ ] Subtask 4.2: Integration tests for expired lock rejection
  - [ ] Subtask 4.3: Integration tests for idempotency (duplicate transactionID)
  - [ ] Subtask 4.4: Integration tests for double-commit prevention

## Dev Notes

### Architecture Compliance
- Commit is the **permanent state change**. Wrap everything in a database transaction.
- Transaction boundaries: begin when lock is validated, end after `VoucherUsage` insert and detail update.
- The POS ID comes from the API Key context (middleware sets it on HttpContext).
- `AmountUsed` may be less than `FaceValue` (e.g., partial redemption). Store actual amount used.

### File Structure Requirements
```
src/NonCash.Core/Entities/VoucherUsage.cs
src/NonCash.Core/Interfaces/IPosService.cs
src/NonCash.Core/Services/PosService.cs
src/NonCash.Infrastructure/Data/Configurations/VoucherUsageConfiguration.cs
src/NonCash.API/Controllers/PosController.cs
```

### Database Schema
- Table: `voucher_usages`
- Columns: `id` (uuid PK), `voucher_id` (uuid FK not null), `pos_id` (uuid FK not null), `transaction_id` (varchar 100 not null), `usage_date` (timestamptz not null), `amount_used` (numeric(18,2))
- Index: `IX_voucher_usages_voucher_id`, `IX_voucher_usages_transaction_id` (unique), `IX_voucher_usages_pos_id`

### API Contracts
- `POST /api/v1/pos/commit`
- Header: `X-API-Key: <outlet_api_key>`
- Request: `{ lockID: "guid", transactionID: "string", amountUsed: 123.45 }`
- Response (Success): `{ status: "Success", message: "Voucher completed" }`
- Response (Expired): `{ status: "LockExpired" }` (HTTP 409)
- Response (AlreadyComplete): `{ status: "AlreadyComplete" }` (HTTP 200 for idempotency)

### Security & NFR
- NFR2 (Transaction integrity): Commit MUST be atomic. If `VoucherUsage` insert fails, voucher must NOT be marked Complete.
- Use `TransactionScope` or EF `SaveChangesAsync` within an explicit transaction.
- `TransactionID` uniqueness constraint prevents duplicate logging.

### Testing Standards
- Simulate DB failure during `VoucherUsage` insert and assert voucher remains `InUse`.
- Test idempotency by sending same commit twice. Second should return success without new usage record.

### References
- [Source: docs/api-contracts.md#POS Integration API] — Redeem (Commit) endpoint.
- [Source: docs/data-models.md#VoucherUsage] — Usage entity fields.
- [Source: Key Functionalities.txt#IV] — POS commit flow and transaction integrity.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

