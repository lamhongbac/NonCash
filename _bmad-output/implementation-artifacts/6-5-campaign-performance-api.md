# Story 6.5: Campaign Performance Query API

Status: done

## Story

As a Loyalty App partner,
I want to query aggregated performance data for campaigns I sponsored,
So that I can measure ROI, redemption rates, and per-outlet uplift without building my own analytics.

## Acceptance Criteria

**AC1: Performance Query**
Given a partner calls `GET /integration/campaigns/{planID}/performance`
When the plan exists and the partner is authorized for the plan's Brand
Then the response includes: `totalIssued`, `totalDistributed`, `totalRedeemed`, `redemptionRate`, `totalRedeemedValue`, `perOutlet[]` (outlet name, redeemed count, value)

**AC2: Authorization Enforcement**
Given a partner queries a plan for a Brand not in their association
When the request arrives
Then the API returns `403 Forbidden`

**AC3: Incremental Uplift (Optional)**
Given the partner provides a `baselineValue` query parameter
When the performance is computed
Then the response additionally returns `incrementalValue` (totalRedeemedValue - baselineValue) and `upliftPercentage`

## Tasks / Subtasks

- [x] Task 1: Performance endpoint (AC1, AC2)
  - [x] Subtask 1.1: `IntegrationController.GetCampaignPerformance`
  - [x] Subtask 1.2: Aggregate from `VoucherPlanDetail` (issued, distributed counts) + usage status (redeemed count)
  - [x] Subtask 1.3: Per-outlet breakdown from `VoucherPlanDetail.LockedOutletId` joined with `outlets`
- [ ] Task 2: Incremental uplift (AC3)
  - [ ] Subtask 2.1: Accept optional `baselineValue` query param
  - [ ] Subtask 2.2: Compute and return uplift metrics
- [ ] Task 3: Tests
  - [ ] Subtask 3.1: Integration test for authorization enforcement
  - [ ] Subtask 3.2: Unit test for redemption rate and uplift calculations

## Dev Notes

### API Contract
- `GET /integration/campaigns/{planID}/performance` — see `docs/api-contracts.md` #5
- Auth: `X-API-Key` header

### References
- [Source: docs/api-contracts.md#5 Query Campaign Performance]

---

# Epic 7: Cross-Tenant Settlement & Campaign Sponsorship

Enable brands to sponsor cross-tenant campaigns and track financial settlement between the sponsoring brand and the redeeming brand. Giga Mall (or any mall operator) can act as a clearinghouse.

## Stories

| # | Story | Summary |
|---|---|---|
| 7.1 | Plan Sponsor & Redeem Brand Tracking | Add `SponsorBrandID` to plan header so campaigns can be attributed to a sponsoring brand separate from the issuing brand. |
| 7.2 | Settlement Ledger | Record every cross-tenant redemption as a settlement entry (who owes whom, how much). |
| 7.3 | Netting Report | Monthly netting report showing net obligations between all participating brands. |
