# Story 5.1: Initiate Peer-to-Peer Voucher Transfer

Status: ready-for-dev

## Story

As a voucher owner (Customer),
I want to initiate a transfer/gift of one of my vouchers to another person via phone number or MemberID,
So that I can share unused vouchers with family, friends, or partners.

## Acceptance Criteria

**AC1: Initiate Transfer Endpoint**
Given a customer owns a voucher in `Pending` status (UsageStatus = Pending, MemberId = current user)
When the customer calls `POST /api/v1/member/vouchers/{voucherId}/initiate-transfer` with `{ recipientPhone: "...", recipientMemberId: "...", note: "..." }`
Then the system creates a `VoucherTransfer` record with status `PendingAcceptance`
And the voucher's `UsageStatus` remains `Pending` but is locked (cannot be used at POS until accepted/rejected)
And a transfer expiry of 7 days is set

**AC2: Recipient Resolution**
Given the request includes a phone number or MemberID
When the system processes the request
Then it resolves recipient by phone first (creating a placeholder Customer record if not yet registered, per FR5 batch promo pattern)
And falls back to MemberID lookup if phone is missing
And rejects the request with 400 if neither resolves to a Customer

**AC3: Ownership & Eligibility Validation**
Given the request is processed
When validation runs
Then the voucher MUST belong to the current user (`MemberId == currentUser.Id`)
And the voucher MUST be in `Pending` status
And there MUST NOT be an existing `PendingAcceptance` transfer for the same voucher
And the recipient MUST NOT be the same as the sender (no self-transfer)
And on any failure return 400/403/409 with a specific reason

**AC4: No Payment**
Given platform policy forbids payment within transfer (per Key Functionalities)
When the transfer is created
Then no payment record/charge is generated
And the transfer is recorded as a free gift (`TransferType = Gift`)

**AC5: Audit Trail**
Given a transfer is initiated
When it is persisted
Then `VoucherTransfer` records: `SenderId`, `RecipientId`, `VoucherId`, `Status`, `InitiatedAt`, `ExpiresAt`, `Note`

## Tasks / Subtasks

- [ ] Task 1: Domain entity (AC1, AC5)
  - [ ] Subtask 1.1: Create `VoucherTransfer` entity with `Status` enum (PendingAcceptance / Accepted / Rejected / Expired / Cancelled)
  - [ ] Subtask 1.2: Add `VoucherTransferConfiguration` (indexes on VoucherId, SenderId, RecipientId, Status)
  - [ ] Subtask 1.3: Add `DbSet<VoucherTransfer>` to ApplicationDbContext
- [ ] Task 2: Service layer (AC2, AC3, AC4)
  - [ ] Subtask 2.1: `IVoucherTransferService.InitiateAsync(...)`
  - [ ] Subtask 2.2: Recipient resolver (phone → Customer; create placeholder if absent)
  - [ ] Subtask 2.3: Ownership + status + duplicate-pending guards
- [ ] Task 3: API endpoint
  - [ ] Subtask 3.1: `POST /api/v1/member/vouchers/{voucherId}/initiate-transfer`
  - [ ] Subtask 3.2: DTOs: `InitiateTransferRequest`, `InitiateTransferResponse`
- [ ] Task 4: EF migration
  - [ ] Subtask 4.1: `AddVoucherTransfers` migration

## Dev Notes

### Architecture Compliance
- The initiation does NOT change `MemberId` on the voucher detail. Ownership transfers only on recipient acceptance (Story 5-2).
- The voucher SHOULD be soft-locked from POS use during pending transfer to avoid race with redemption. Reuse of lock columns is acceptable, OR add a separate `TransferLockedAt` flag.
- TransferType is fixed to `Gift` for MVP (no monetization in transfer flow per platform policy).

### File Structure Requirements
```
src/NonCash.Core/Entities/VoucherTransfer.cs
src/NonCash.Core/Interfaces/IVoucherTransferService.cs
src/NonCash.Core/Services/VoucherTransferService.cs
src/NonCash.Infrastructure/Data/Configurations/VoucherTransferConfiguration.cs
src/NonCash.API/Controllers/MemberVouchersController.cs (extend existing)
```

### Database Schema
- Table: `voucher_transfers`
- Columns: `id` (uuid PK), `voucher_id` (uuid FK), `sender_id` (uuid FK to customers), `recipient_id` (uuid FK to customers), `status` (varchar 30), `transfer_type` (varchar 20), `note` (varchar 500), `initiated_at` (timestamptz), `expires_at` (timestamptz), `responded_at` (timestamptz nullable)
- Indexes: `IX_voucher_transfers_voucher_id`, `IX_voucher_transfers_sender_id`, `IX_voucher_transfers_recipient_id`, `IX_voucher_transfers_status`

### API Contracts
- `POST /api/v1/member/vouchers/{voucherId}/initiate-transfer`
- Auth: `[Authorize]` (Customer JWT)
- Request: `{ recipientPhone: "0901...", recipientMemberId: null, note: "Happy birthday!" }`
- Response (Success): `{ transferId: "guid", status: "PendingAcceptance", expiresAt: "2026-..." }`
- Response (Failure): `{ status: "Invalid", reason: "VoucherNotOwned|AlreadyPending|RecipientNotFound|SelfTransfer" }`

### Security & NFR
- NFR1 (Multi-tenancy): A transfer can cross brand boundaries (vouchers from Brand A can be gifted to a Customer who has only ever bought from Brand B). Customer entity is global.
- NFR2 (Atomicity): Transfer-initiate + soft-lock MUST be atomic.
- Rate-limit per sender (e.g., max 50 active pending transfers).

### Testing Standards
- Unit tests for ownership/status/duplicate guards.
- Integration tests for phone-resolution placeholder creation and self-transfer rejection.

### References
- [Source: Key Functionalities.txt#Chuyen nhuong, cho tang] — Two-way transfer protocol, no payment.
- [Source: docs/data-models.md#Customer] — Phone normalization rules from Story 3-3.
- [Source: planning-artifacts/epics.md#Epic 5] — Social Engagement & Gifting.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

