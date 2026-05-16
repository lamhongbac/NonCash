# Story 5.2: Recipient Confirmation of Voucher Transfer

Status: ready-for-dev

## Story

As a voucher transfer recipient (Customer),
I want to accept or reject a pending voucher transfer addressed to me,
So that ownership only changes after my explicit two-way confirmation.

## Acceptance Criteria

**AC1: List Pending Inbound Transfers**
Given the current user has one or more `PendingAcceptance` transfers addressed to them
When they call `GET /api/v1/member/transfers/inbox`
Then the system returns the list with sender display name, voucher info (face value, brand, expiry), note, and `expiresAt`
And the list is sorted by `InitiatedAt` desc

**AC2: Accept Transfer**
Given a recipient has a `PendingAcceptance` transfer
When they call `POST /api/v1/member/transfers/{transferId}/accept`
Then the system atomically:
- transitions transfer `Status` to `Accepted`
- sets `RespondedAt = Now()`
- changes voucher `MemberId` to the recipient
- releases the soft-lock on the voucher
And returns `{ status: "Accepted", voucherId: "..." }`

**AC3: Reject Transfer**
Given a recipient has a `PendingAcceptance` transfer
When they call `POST /api/v1/member/transfers/{transferId}/reject` with `{ reason: "..." }`
Then the system atomically:
- transitions transfer `Status` to `Rejected`
- sets `RespondedAt = Now()` and stores reason
- voucher `MemberId` remains with the sender
- releases the soft-lock
And returns `{ status: "Rejected" }`

**AC4: Authorization**
Given a request to accept/reject a transfer
When the current user is NOT the recorded `RecipientId`
Then the system returns 403/Forbidden
And no state changes are made

**AC5: Idempotency / Already Resolved**
Given a transfer is already `Accepted`, `Rejected`, `Expired`, or `Cancelled`
When the recipient calls accept/reject again
Then the system returns 409 with the current status
And no state changes are made

**AC6: Auto-Expiry**
Given a transfer's `ExpiresAt` has passed
When any read or action touches the transfer
Then the system flags it as `Expired` (lazy) AND a background sweep marks expired transfers and releases voucher locks (e.g., every hour)

## Tasks / Subtasks

- [ ] Task 1: Service layer (AC2, AC3, AC4, AC5)
  - [ ] Subtask 1.1: `IVoucherTransferService.AcceptAsync(transferId, currentUserId)`
  - [ ] Subtask 1.2: `IVoucherTransferService.RejectAsync(transferId, currentUserId, reason)`
  - [ ] Subtask 1.3: Atomic ownership flip via EF transaction (transfer status + voucher.MemberId + lock release)
- [ ] Task 2: Inbox endpoint (AC1)
  - [ ] Subtask 2.1: `GET /api/v1/member/transfers/inbox` with paging & filter by status
  - [ ] Subtask 2.2: DTO joins voucher + brand info
- [ ] Task 3: Background sweeper (AC6)
  - [ ] Subtask 3.1: `TransferExpirySweepService` BackgroundService running every 1h
  - [ ] Subtask 3.2: `ExecuteUpdateAsync` flips PendingAcceptance with `ExpiresAt < now` to `Expired` and releases locks
- [ ] Task 4: API endpoints
  - [ ] Subtask 4.1: `POST .../transfers/{id}/accept`
  - [ ] Subtask 4.2: `POST .../transfers/{id}/reject`
- [ ] Task 5: Tests
  - [ ] Subtask 5.1: Atomicity test for accept (transfer status + ownership flip)
  - [ ] Subtask 5.2: Authorization test for non-recipient
  - [ ] Subtask 5.3: Idempotency test for double-accept

## Dev Notes

### Architecture Compliance
- Accept is the **permanent ownership change**. Wrap transfer-status update + voucher MemberId update in a single DB transaction.
- Reject is reversible only by initiating a new transfer (no "undo reject").
- Sender cannot accept on the recipient's behalf even with admin role (preserves two-way consent semantics per FR5).

### File Structure Requirements
```
src/NonCash.Core/Interfaces/IVoucherTransferService.cs
src/NonCash.Core/Services/VoucherTransferService.cs
src/NonCash.API/Controllers/MemberTransfersController.cs (new)
src/NonCash.API/HostedServices/TransferExpirySweepService.cs (new)
```

### API Contracts
- `GET /api/v1/member/transfers/inbox?status=PendingAcceptance&page=1&pageSize=20`
- `POST /api/v1/member/transfers/{id}/accept`
- `POST /api/v1/member/transfers/{id}/reject`  body: `{ reason?: "..." }`
- All require Customer JWT.

### Security & NFR
- NFR2 Transaction integrity: ownership flip MUST be atomic.
- NFR3 (Authorization): only recipient may accept/reject; only sender may cancel (Story 5-3).
- Rate-limit accept/reject per user to prevent abuse.

### Testing Standards
- Concurrency test: simulate two parallel `Accept` calls; only one succeeds (use `ExecuteUpdateAsync` with conditional `Status = PendingAcceptance`).
- Negative test: recipient phone not yet registered → placeholder Customer must be promotable on first accept (or earlier on Story 5-1).

### References
- [Source: Key Functionalities.txt#Chuyen nhuong, cho tang] — "Thu tuc luon co 2 chieu" (always two-way).
- [Source: planning-artifacts/epics.md#Epic 5]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

