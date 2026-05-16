# Story 3.3: Chuyen nhuong & Dinh danh Quyen So huu (Gifting / Batch Transfer)

Status: ready-for-dev

## Story

As a Member (Individual or Organization),
I want to transfer ownership of one or many vouchers using a list of phone numbers,
So that I can quickly reallocate voucher budgets to employees or customers.

## Acceptance Criteria

**AC1: Transfer Initiation**
Given a Member holds vouchers with `UsageStatus = Pending`
When they provide N target phone numbers and select Transfer
Then the system maps each voucher to one phone number
And creates `VoucherDistribution` records with `Method = Transfer`

**AC2: Two-Way Confirmation (Simplified MVP)**
Given a transfer is initiated
When the system processes it
Then the recipient's MemberID is assigned immediately (MVP simplification)
And a notification placeholder is created for future Inbox/notification epic
**Note:** Full two-way confirmation (recipient explicitly accepts) is deferred to post-MVP. For now, auto-accept with audit trail.

**AC3: Partial Transfer Validation**
Given the Member attempts to transfer more vouchers than they own
When the request is validated
Then the system returns 400 / InsufficientVouchers
And no partial transfers occur

**AC4: Blacklist Check**
Given a recipient phone number belongs to a Blacklisted customer
When transfer is attempted
Then that specific mapping is skipped
And a warning is returned

**AC5: Transfer History**
Given transfers have occurred
When the Member views "My Voucher" or transfer history
Then they see outgoing transfers with recipient phone numbers and dates

## Tasks / Subtasks

- [ ] Task 1: Implement transfer service (AC1, AC3, AC4)
  - [ ] Subtask 1.1: `ITransferService` with `TransferAsync(Guid fromMemberId, List<Guid> voucherIds, List<string> recipientPhones)`
  - [ ] Subtask 1.2: Validate all voucherIds belong to `fromMemberId` and are `Pending`
  - [ ] Subtask 1.3: Match phone numbers to Customers; skip blacklisted; create missing Customers
  - [ ] Subtask 1.4: Atomic update of `VoucherPlanDetail.MemberID` + insert `VoucherDistribution` records
- [ ] Task 2: API endpoint (AC1, AC3, AC4)
  - [ ] Subtask 2.1: `POST /api/v1/member/vouchers/transfer`
  - [ ] Subtask 2.2: Request: `{ voucherIds: [], recipientPhones: [] }`
  - [ ] Subtask 2.3: Response: `{ transferredCount, skippedPhones: [] }`
- [ ] Task 3: Blazor / Member App UI (AC1, AC5)
  - [ ] Subtask 3.1: Transfer modal in "My Voucher" page
  - [ ] Subtask 3.2: Multi-select voucher grid
  - [ ] Subtask 3.3: Phone number list input (comma or line separated)
  - [ ] Subtask 3.4: Transfer history section
- [ ] Task 4: Tests
  - [ ] Subtask 4.1: Unit tests for transfer validation (ownership, status checks)
  - [ ] Subtask 4.2: Integration tests for blacklist skip
  - [ ] Subtask 4.3: Integration tests for atomicity (DB failure mid-transfer -> rollback)

## Dev Notes

### Architecture Compliance
- Transfer is fundamentally a **re-assignment of MemberID** on `VoucherPlanDetail` with an audit trail in `VoucherDistribution`.
- The platform does NOT handle payment during transfer (as per requirements). This is purely ownership reassignment.
- For MVP, auto-accept simplifies the flow while preserving the audit trail. Document this decision clearly for future enhancement.

### File Structure Requirements
```
src/NonCash.Core/Interfaces/ITransferService.cs
src/NonCash.Core/Services/TransferService.cs
src/NonCash.API/Controllers/MemberVouchersController.cs
```

### Database Schema
- Reuses `voucher_plan_details` (update `member_id`) and `voucher_distributions` (insert with `method = Transfer`).

### API Contracts
- `POST /api/v1/member/vouchers/transfer`
- Auth: JWT Bearer (any authenticated member)
- 400 if `voucherIds.Count != recipientPhones.Count` (1-to-1 mapping required)
- 400 if any voucher is not Pending or not owned by caller

### Security & NFR
- NFR4: Members can only transfer vouchers they own (`MemberID == currentUser.MemberId`).
- NFR3: No special role required; any authenticated member can transfer.
- Validate that recipient phone count matches voucher count to prevent ambiguous mappings.

### Testing Standards
- Test 1-to-1 mapping: 3 vouchers + 3 phones = 3 transfers.
- Test mismatch: 3 vouchers + 2 phones = 400 error.
- Test ownership fraud: attempt to transfer a voucher owned by another member = 400.

### References
- [Source: Key Functionalities.txt#III] — Chuyen nhuong, cho tang flow.
- [Source: docs/data-models.md#VoucherPlanDetail] — MemberID and UsageStatus fields.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

