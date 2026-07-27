# Story 7.2: Settlement Ledger

Status: backlog

## Story

As a Mall Operator or Admin,
I want every cross-tenant voucher redemption to generate a settlement entry recording who owes whom and how much,
So that financial obligations between sponsoring and redeeming brands are tracked automatically.

## Acceptance Criteria

**AC1: Settlement Entry Creation**
Given a voucher from a cross-tenant plan (SponsorBrandID ≠ BrandID or RedeemBrandID) is redeemed and committed
When the commit succeeds
Then the system creates a `SettlementEntry` record: `SponsorBrandID`, `IssuingBrandID`, `RedeemBrandID`, `RedeemOutletID`, `FaceValue`, `VoucherUsageID`, `SettlementDate`

**AC2: No Settlement for Self-Sponsored**
Given a voucher from a self-sponsored plan (SponsorBrandID is null or equals BrandID)
When redeemed at the same Brand's outlet
Then no settlement entry is created

**AC3: Settlement List**
Given an Admin or Mall Operator views the Settlement Ledger
When they filter by date range, brand, or status (Pending/Settled)
Then they see all settlement entries with full attribution details

**AC4: Manual Settlement Marking**
Given a settlement entry
When an Admin marks it as "Settled" (after off-platform payment between brands)
Then the `SettlementDate` and `SettledBy` are recorded

## Tasks / Subtasks

- [ ] Task 1: Settlement entity and table (AC1, AC2)
  - [ ] Subtask 1.1: `SettlementEntry.cs` entity
  - [ ] Subtask 1.2: `settlement_entries` table migration
  - [ ] Subtask 1.3: Hook into VoucherUsage commit logic to create entry when cross-tenant
- [ ] Task 2: Settlement list and management UI (AC3, AC4)
  - [ ] Subtask 2.1: `SettlementLedger.razor` page
  - [ ] Subtask 2.2: Filter by date, brand, status
  - [ ] Subtask 2.3: "Mark Settled" action button
- [ ] Task 3: API (AC3)
  - [ ] Subtask 3.1: `GET /api/v1/settlements` (Admin/Mall Operator)
- [ ] Task 4: Tests

## Dev Notes

### Schema
- Table: `settlement_entries`
- Columns: `id` (uuid PK), `sponsor_brand_id` (uuid FK), `issuing_brand_id` (uuid FK), `redeem_brand_id` (uuid FK), `redeem_outlet_id` (uuid FK), `voucher_usage_id` (uuid FK), `face_value` (numeric 18,2), `status` (varchar 20: Pending/Settled), `settled_at` (timestamptz nullable), `settled_by` (uuid FK nullable), `created_at` (timestamptz)

### Business Rule
- Settlement only applies when SponsorBrandID ≠ RedeemBrandID (the sponsor funded a voucher that was honored at a different brand's outlet).

### References
- [Source: docs/architecture.md#Cross-tenant settlement]
