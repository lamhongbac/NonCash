# Story 7.1: Plan Sponsor & Redeem Brand Tracking

Status: backlog

## Story

As a Brand Manager or Mall Operator,
I want to record which Brand sponsored a cross-tenant campaign (SponsorBrandID) separate from the Brand that issued the voucher (BrandID),
So that financial responsibility and campaign attribution are clear when vouchers are redeemed at a different brand's outlet.

## Acceptance Criteria

**AC1: SponsorBrandID on Plan Header**
Given a Planner creates a plan
When they optionally set `SponsorBrandID` (nullable, defaults to null = self-sponsored)
Then the plan records which brand is funding/sponsoring the campaign
And `BrandID` remains the issuing brand (the one whose vouchers are produced)

**AC2: Cross-tenant Outlet Authorization**
Given a plan with `SponsorBrandID` different from `BrandID`
When the Planner selects outlets in `SalesRange`
Then outlets from BOTH the issuing Brand and the Sponsor Brand are available for selection
And the UI clearly labels which outlets belong to which brand

**AC3: Redeem Brand Attribution on Usage**
Given a cross-tenant voucher is redeemed at an outlet
When the `VoucherUsage` record is created
Then it records: `IssuingBrandID` (from plan's BrandID), `SponsorBrandID` (from plan), `RedeemOutletID`, `RedeemBrandID` (the outlet's brand)

**AC4: Plan List Filter by Sponsor**
Given plans exist with various sponsors
When a Brand Manager filters the plan list
Then they can filter by "Sponsored by me" (SponsorBrandID = my brand) or "Issued by me" (BrandID = my brand)

## Tasks / Subtasks

- [ ] Task 1: Schema extension (AC1, AC3)
  - [ ] Subtask 1.1: Add `sponsor_brand_id` (uuid FK nullable) to `voucher_plan_headers`
  - [ ] Subtask 1.2: Add `sponsor_brand_id` (uuid nullable) and `redeem_brand_id` (uuid nullable) to `voucher_usages`
  - [ ] Subtask 1.3: EF migration
- [ ] Task 2: Plan service update (AC1, AC2)
  - [ ] Subtask 2.1: Accept `SponsorBrandID` in CreatePlanRequest DTO
  - [ ] Subtask 2.2: SalesRange outlet picker includes Sponsor Brand outlets
  - [ ] Subtask 2.3: Validation: SponsorBrandID must reference a valid Brand (if provided)
- [ ] Task 3: Redemption attribution (AC3)
  - [ ] Subtask 3.1: On commit (Story 4.3), populate `sponsor_brand_id` and `redeem_brand_id` on `VoucherUsage`
- [ ] Task 4: UI updates (AC2, AC4)
  - [ ] Subtask 4.1: Optional Sponsor Brand dropdown on plan create/edit (filtered to platform brands, Admin sees all, BrandManager sees linked brands)
  - [ ] Subtask 4.2: Filter toggle on plan list: "Issued by me" / "Sponsored by me"
- [ ] Task 5: Tests
  - [ ] Subtask 5.1: Integration test for cross-tenant usage attribution
  - [ ] Subtask 5.2: Unit test for SalesRange including sponsor outlets

## Dev Notes

### Schema Changes
```sql
ALTER TABLE voucher_plan_headers ADD COLUMN sponsor_brand_id UUID NULL REFERENCES brands(id);
ALTER TABLE voucher_usages ADD COLUMN sponsor_brand_id UUID NULL;
ALTER TABLE voucher_usages ADD COLUMN redeem_brand_id UUID NULL;
```

### Backward Compatibility
- `sponsor_brand_id` is nullable. Existing plans (null) are treated as self-sponsored.
- No change to existing plan or redemption logic when `sponsor_brand_id` is null.

### References
- [Source: docs/proposals/giga-mall-discussion-summary.md#Q4]
- [Source: docs/architecture.md#Cross-tenant settlement]
