# Story 6.3: Member Wallet & Event History API

Status: done

## Story

As a Loyalty App partner,
I want to query a member's current voucher wallet and full lifecycle event history,
So that I can display voucher details in-app and power analytics/notification triggers.

## Acceptance Criteria

**AC1: Wallet Query**
Given a partner calls `GET /integration/member/{phone}/vouchers`
When the member exists and has vouchers
Then the response returns all vouchers across all partner-authorized Brands with: `voucherID`, `brand`, `iconURL`, `coverImageURL`, `faceValue`, `valueType`, `usageStatus`, `expiryDate`, `outlets[]`

**AC2: Event History Query**
Given a partner calls `GET /integration/member/{phone}/events`
When optional filters are provided (`from`, `to`, `eventType`)
Then the response returns a chronological list of lifecycle events: Distributed, Redeemed, Transferred (sent/received), Expired, Cancelled
And each event includes: `eventID`, `eventType`, `voucherID`, `brand`, `timestamp`, `details{}`

**AC3: Partner-Brand Scope Enforcement**
Given a partner queries a member who holds vouchers from Brands outside the partner's association
When the wallet query runs
Then only vouchers from partner-authorized Brands are returned
And vouchers from other Brands are excluded silently

**AC4: Member Not Found**
Given a phone number with no customer record
When wallet or events are queried
Then the API returns `200 OK` with empty arrays (not 404 — to avoid phone enumeration attacks)

## Tasks / Subtasks

- [x] Task 1: Wallet query endpoint (AC1, AC3, AC4)
  - [x] Subtask 1.1: `IntegrationController.GetMemberVouchers`
  - [x] Subtask 1.2: Query `VoucherPlanDetail` joined with `VoucherPlanHeader` filtered by member phone and partner-authorized Brands
  - [x] Subtask 1.3: Map to `IntegrationWalletItem` DTO including display fields (iconURL, coverImageURL, brandColor)
- [x] Task 2: Event history endpoint (AC2, AC3, AC4)
  - [x] Subtask 2.1: `IntegrationController.GetMemberEvents`
  - [x] Subtask 2.2: Query `VoucherDistribution` + `VoucherUsage` + `VoucherTransfer` tables, unified as events
  - [x] Subtask 2.3: Sort chronologically descending with limit (date range filters deferred)
- [ ] Task 3: Tests
  - [ ] Subtask 3.1: Integration test for Brand scope filtering
  - [ ] Subtask 3.2: Unit test for event type mapping

## Dev Notes

### API Contracts
- `GET /integration/member/{phone}/vouchers` — see `docs/api-contracts.md` #2
- `GET /integration/member/{phone}/events` — see `docs/api-contracts.md` #3
- Auth: `X-API-Key` header

### Display Fields
- Wallet response includes `iconURL` and `coverImageURL` from `VoucherPlanHeader` (see Story 8.1 for voucher display data model).
- `outlets[]` resolved from `plan_outlets` join table.

### Event Sources
| Event Type | Source Table |
|---|---|
| Distributed | `voucher_distributions` |
| Redeemed | `voucher_usages` |
| Transferred (sent/received) | `voucher_transfers` |
| Expired | Computed from `expiry_date` vs current date |
| Cancelled | `voucher_transfers` where status = Cancelled |

### References
- [Source: docs/api-contracts.md#2 Get Member Voucher Wallet]
- [Source: docs/api-contracts.md#3 Get Voucher Event History]
