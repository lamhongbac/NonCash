# Story 5.3: Sender Cancellation & Transfer History UI

Status: ready-for-dev

## Story

As a voucher owner who initiated a transfer,
I want to cancel a still-pending transfer and view my outgoing/incoming transfer history,
So that I retain control over my voucher until the recipient acts and have full visibility into past gifts.

## Acceptance Criteria

**AC1: Cancel Pending Transfer**
Given the current user is the `SenderId` of a `PendingAcceptance` transfer
When they call `POST /api/v1/member/transfers/{transferId}/cancel`
Then the system atomically transitions transfer `Status` to `Cancelled`, sets `RespondedAt = Now()`, releases the voucher's soft-lock
And returns `{ status: "Cancelled" }`

**AC2: Cancel Already Resolved**
Given the transfer is already `Accepted`, `Rejected`, `Expired`, or `Cancelled`
When the sender attempts to cancel
Then the system returns 409 with the current status

**AC3: Outgoing History**
Given the current user has past transfers as sender
When they call `GET /api/v1/member/transfers/outbox?status=...&page=...`
Then the system returns all transfers initiated by the user with status, recipient display, voucher info, dates

**AC4: Authorization**
Given a cancel request
When the current user is NOT the `SenderId`
Then 403 / Forbidden

**AC5: My Transfers UI (Customer/Member portal)**
Given the customer logs into the member portal
When they navigate to `/member/transfers`
Then they see two tabs: Inbox (incoming pending) + History (outgoing + incoming all)
And each row offers Accept/Reject for inbox-pending and Cancel for outbox-pending
And clicking a row shows voucher details and transfer note

## Tasks / Subtasks

- [ ] Task 1: Cancel service (AC1, AC2, AC4)
  - [ ] Subtask 1.1: `IVoucherTransferService.CancelAsync(transferId, currentUserId)`
  - [ ] Subtask 1.2: Atomic update: status flip + lock release in transaction
- [ ] Task 2: Outbox endpoint (AC3)
  - [ ] Subtask 2.1: `GET /api/v1/member/transfers/outbox`
- [ ] Task 3: Cancel endpoint (AC1)
  - [ ] Subtask 3.1: `POST /api/v1/member/transfers/{id}/cancel`
- [ ] Task 4: Member portal UI (AC5)
  - [ ] Subtask 4.1: Blazor page `/member/transfers` with MudTable + tabs
  - [ ] Subtask 4.2: Action buttons wired to accept/reject/cancel APIs
  - [ ] Subtask 4.3: Empty-state copy and loading skeletons
- [ ] Task 5: Tests
  - [ ] Subtask 5.1: Authorization tests (non-sender cancel rejected)
  - [ ] Subtask 5.2: UI smoke test for tab switching

## Dev Notes

### Architecture Compliance
- Cancel is symmetric to Reject from a state-machine perspective but is invoked by the sender.
- The Blazor page should reuse `MemberAuthService` patterns from Story 1-5 for token handling.
- Use the same DTOs as Story 5-2 inbox endpoint to minimize divergence.

### File Structure Requirements
```
src/NonCash.Core/Services/VoucherTransferService.cs (extend)
src/NonCash.Core/Interfaces/IVoucherTransferService.cs (extend)
src/NonCash.API/Controllers/MemberTransfersController.cs (extend)
src/NonCash.Web/Components/Pages/Member/Transfers.razor (new)
src/NonCash.Web/Components/Pages/Member/Transfers.razor.cs (optional code-behind)
```

### API Contracts
- `POST /api/v1/member/transfers/{id}/cancel`
- `GET /api/v1/member/transfers/outbox?status=PendingAcceptance|Accepted|Rejected|Expired|Cancelled`

### UI Requirements
- Reuse MudBlazor 9.x components (MudTabs, MudTable, MudButton, MudDialog).
- Display per-row chip for status (color-coded: warning=Pending, success=Accepted, error=Rejected, default=Cancelled/Expired).
- Confirmation dialog before destructive actions (Reject/Cancel).

### Security & NFR
- NFR3: Sender-only cancel; recipient-only accept/reject (cross-checked at service layer).
- All actions audit via existing `BaseEntity.UpdatedAt` plus `RespondedAt` field on transfer.

### Testing Standards
- bUnit smoke test for tab/button rendering.
- Integration test for full happy path: initiate → recipient accept → both lists reflect new state.

### References
- [Source: Key Functionalities.txt#Chuyen nhuong, cho tang]
- [Source: planning-artifacts/ux-design-specification.md#Member Portal]
- [Source: implementation-artifacts/3-3-gifting-batch-transfer.md] — phone-normalization patterns reused.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

