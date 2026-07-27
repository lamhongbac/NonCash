# Story 6.2: Segment Distribution API (Loyalty App Push)

Status: backlog

## Story

As a Loyalty App partner,
I want to push a target member segment (list of phone numbers + external member IDs) to NonCash for batch voucher distribution,
So that I can trigger targeted campaigns from my own segmentation engine without manual CSV uploads.

## Acceptance Criteria

**AC1: Segment Distribution Request**
Given a Loyalty App partner calls `POST /integration/distribute`
When the request includes: `planID`, `members[]` (phone + optional `externalMemberID`), optional `callbackURL`
Then NonCash matches phones to existing Customers (or creates placeholders)
And distributes one voucher per member from the plan's available stock
And returns a `distributionID` with counts (requested, distributed, skipped)

**AC2: Idempotency**
Given the same `planID` and `members[]` payload is sent twice
When the second call arrives
Then already-distributed members are reported in `skipped[]` with reason `"already_distributed"`
And no duplicate vouchers are assigned

**AC3: Stock Validation**
Given a plan with fewer remaining vouchers than requested members
When the distribution is triggered
Then the request fails entirely (all-or-nothing) with error `"insufficient_stock"`
And no vouchers are assigned

**AC4: Blacklist Enforcement**
Given a member phone that is blacklisted
When included in a distribution request
Then that member is skipped and reported in `skipped[]` with reason `"blacklisted"`

**AC5: External Member ID Mapping**
Given a member payload includes `externalMemberID`
When the distribution succeeds
Then the `externalMemberID` is stored on the `VoucherDistribution` record
So the Loyalty App can correlate vouchers back to its own member system

## Tasks / Subtasks

- [ ] Task 1: Integration distribution endpoint (AC1, AC3)
  - [ ] Subtask 1.1: `IntegrationController.DistributeAsync`
  - [ ] Subtask 1.2: `IntegrationDistributionService` reusing batch promotion logic from Story 3.1
  - [ ] Subtask 1.3: All-or-nothing stock check
- [ ] Task 2: Idempotency and blacklist logic (AC2, AC4)
  - [ ] Subtask 2.1: Check existing `VoucherDistribution` for duplicate member+plan combos
  - [ ] Subtask 2.2: Blacklist check per member phone
- [ ] Task 3: External member ID storage (AC5)
  - [ ] Subtask 3.1: Add `external_member_id` (varchar nullable) column to `voucher_distributions`
  - [ ] Subtask 3.2: Migration
- [ ] Task 4: Tests
  - [ ] Subtask 4.1: Integration test for idempotent re-submit
  - [ ] Subtask 4.2: Integration test for insufficient stock rollback

## Dev Notes

### Architecture Compliance
- This endpoint reuses the core distribution logic from `VoucherDistributionService` (Story 3.1). The integration layer is a thin API adapter.
- Partner-Brand scope is enforced by middleware (from Story 6.1): the partner can only distribute for Brands in their `partner_brands` association.

### API Contract
- `POST /integration/distribute` — see `docs/api-contracts.md` Section "Loyalty App Integration API #1"
- Auth: `X-API-Key` header

### Schema Change
- Add `external_member_id` (varchar 100 nullable) to `voucher_distributions` table.

### References
- [Source: docs/api-contracts.md#1 Distribute to Segment]
- [Source: Story 3.1 batch promotion logic]
