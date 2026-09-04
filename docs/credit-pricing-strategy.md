# Credit Pricing Strategy — Decision Document

**Status: DRAFT — open for discussion**
**Created: 2026-09-03 · Owner: Platform / Business · Review: before first paid contract signing**

This document captures the market-based analysis behind credit pricing, the recommended
phased approach, and — most importantly — the **open questions we must decide** before
moving to each phase. It is the reference for all future pricing discussions.

---

## 1. What a credit actually is (economic model)

Per `CreditConsumption` (Epic 10):

- **1 credit = 1 voucher**, charged exactly once in the voucher's lifetime.
- Charged at the voucher's *value moment*: **Gift when sold, Complimentary when redeemed**.
- Consumption is FIFO from the oldest non-expired batch; never blocks the business operation.

So "credit price" is really a **per-voucher platform fee**, and the number that matters
commercially is the **effective take rate**:

```
take rate = price per credit ÷ voucher face value
```

### Current platform defaults (as implemented)

| Parameter | Value | Source |
|---|---|---|
| List price (fallback) | **5,000 VND/credit** | `CreditConfig.PricePerCreditVnd` |
| Welcome credits (free trial) | 500 credits/brand | `CreditConfig.WelcomeCredits` |
| Credit expiry | 12 months | `CreditConfig.CreditExpiryMonths` |
| Subscription fee | 0 VND (MVP) | `CreditConfig.SubscriptionFeeVnd` |
| Minimum commitment | 12 months | `CreditConfig.MinimumCommitmentMonths` |

Pricing is **versioned, time-bound, and scoped** (`CreditPricingPolicy`):
resolution order **Brand → BrandGroup → Global → config fallback**.
This means price discrimination (intro discounts, volume tiers, per-brand deals)
needs **no code changes** — only new policy rows.

**Current model: "Model B" — flat unit price, no volume tiers.**

---

## 2. The three lenses (how the price was evaluated)

### Lens 1 — Cost floor: ≈ 500–1,000 VND/credit

| Cost component | VND/voucher (est.) |
|---|---|
| Email delivery | ~0–50 |
| SMS (if enabled later) | ~250–350 |
| Infrastructure (amortized, at scale) | ~100–300 |
| Support/ops allocation | ~200–500 |

→ Anything above ~1,000 VND is margin. **Hard floor for any price: 1,500 VND.**
Below that we subsidize volume with no compensating data or strategic value.

### Lens 2 — Alternatives (what the brand would otherwise pay)

What a VN F&B brand pays today to put one offer in a customer's hands:

| Alternative | Cost per voucher/reach | Weakness vs NonCash |
|---|---|---|
| Print coupons + handout | 500–2,000 (print + labor) | No tracking, no redemption data |
| Facebook/Google ads | 3,000–10,000 **per click**; at 1–5% click→visit, ≈ **30,000–80,000 per actual visit** | No guaranteed value in hand |
| Deal sites (e-coupon) | 5–10% of voucher value | Margin loss on every redemption |
| Gift card issuance (global norm) | 1–3% of face value, B2B bulk | Not localized, no POS integration |

**Sales framing to use with brands:** at 5,000/credit and a typical 20% redemption rate,
the brand pays 5,000 ÷ 0.2 = **25,000 VND per customer who actually walks in** —
cheaper than paid social *and* the voucher value is guaranteed in the customer's hand.

### Lens 3 — Willingness to pay (ceiling)

- F&B brands budget **2–5% of revenue** for promotion.
- Per-voucher fees above **10% of face value** feel expensive; **3–7%** is the accepted
  "gift card / deal infrastructure" norm.

---

## 3. The core tension: flat price vs face value

The flat Model B price is **regressive across voucher face values**:

| Voucher face value | Cost @ 5,000/credit | Take rate | Brand reaction |
|---|---|---|---|
| 20,000 (a drink) | 5,000 | **25%** | "Too expensive" → churn risk |
| 50,000 | 5,000 | 10% | Borderline acceptable |
| 100,000 (typical) | 5,000 | 5% | Sweet spot |
| 500,000 (spa/hotel) | 5,000 | 1% | Bargain — revenue left on the table |

A flat price structurally favors mid/high-face-value brands and repels small-ticket ones.
The machinery to fix this (scoped, versioned policies) already exists — the question is
which model to adopt and when (see §6, Q2).

---

## 4. Recommendation — phased

### Phase 1 — LAND (now, MVP)

- **List price: 5,000 VND/credit** (keep current default — round, memorable, defensible:
  5× cost floor, 5% take on the typical 100k voucher, cheaper per *redeemed* voucher
  than any ad channel).
- **Intro price 2,000–3,000/credit** for signed brands, as a **time-boxed Brand-scoped
  policy** (`EffectiveTo` set). Time-boxing avoids the "raise it later" negotiation fight
  because the expiry is in the contract from day one.
- **500 welcome credits stay** — 500 free vouchers of product trial = land-and-expand.

### Phase 2 — EXPAND (6–12 months in, with usage data)

- **Volume tiers via BrandGroup scope** (no code change), e.g.:
  - < 10,000 credits/year: 5,000/credit
  - ≥ 10,000 credits/year prepaid: 4,000/credit
  - National chains: negotiated Brand-scope 3,000–4,000 + monthly minimum burn
- Consider a **small subscription (299k–699k VND/month)** once brands are dependent —
  hybrid lets small brands start light while heavy users self-select into volume pricing.

### Phase 3 — Model C (only if small-ticket brands matter commercially)

- **Face-value-indexed price: e.g. 4% of face value, floor 2,000, cap 10,000.**
- Fixes the 20k-voucher problem; captures value from 500k vouchers.
- Cost: harder sales story; the "1 voucher = 1 credit" ledger stays but the *purchase*
  price becomes value-dependent — a real pricing-story change. **Defer until we have data.**

### Expiry

Keep **12 months**. Breakage is revenue, but the low-balance/expiry warning emails
(already implemented — see `docs/notification-matrix.md`) make it read as customer care,
not a trap.

---

## 5. Guardrails — metrics to watch before ANY price change

| # | Metric | Why / trigger | Needs |
|---|---|---| |
| 1 | **Effective take rate per brand** (credit spend ÷ face value distributed) | Flag > 12% (churn risk) or < 2% (underpriced) | R1, R2 |
| 2 | **Credit repurchase cycle** (balance-zero → next purchase) | Brands who never repurchase = price exceeded perceived value | R3 |
| 3 | **Brand's cost per redeemed voucher** | Must stay visibly below their paid-social CAC | R2 |
| 4 | **Elasticity test** (two cohorts at 3,000 vs 5,000, compare activation + repurchase) | One clean experiment beats guessing; versioned policies run it without code changes | R3 |

---

## 6. OPEN QUESTIONS — the discussion list

> **These are the points to resolve in future pricing discussions.**
> Each question lists options with trade-offs. Record the outcome in the Decision Log (§7).

### Q1. Intro discount mechanics (Phase 1)
Should the 2,000–3,000 intro price be:
- **(a)** Time-boxed per brand (auto-reverts to list on `EffectiveTo`) — *recommended;
  no renegotiation trap, expiry is contractual* — or
- **(b)** Volume-anchored (intro price until first N credits consumed), or
- **(c)** Case-by-case manual (max flexibility, max admin overhead)?

### Q2. Flat vs face-value (Model B vs Model C)
Stay flat forever (simplicity, predictable brand cost) or move to face-value-indexed
(fair across ticket sizes, captures high-value segments)?
- Current recommendation: **stay flat through Phase 2**, revisit with elasticity data.
- What face-value distribution do we actually see across brands? (Needs report R1 —
  see §9 Analytics Backlog.)

### Q3. Subscription layer (Phase 2)
Pure usage-based, or hybrid subscription + usage?
- If hybrid: what floor price, and what does the subscription *include*
  (e.g. POS integrations, number of outlets, support SLA)?
- Interaction with `SubscriptionFeePolicy` (currently free/0 for MVP) and the
  12-month minimum commitment.

### Q4. Welcome credits amount
500 credits = 500 free vouchers per brand. As product value grows, is that:
- generous enough to land brands (keep), or
- too generous (reduce → frees margin for lower list price), or
- should it scale with brand size (per-outlet multiplication)?

### Q5. Small-ticket segment (20–50k vouchers)
Do we want café/small-ticket brands at all?
- If **yes**: Model C or a low-face-value BrandGroup tier is eventually required
  (25% take rate is unsellable).
- If **no**: position explicitly for mid/high-ticket brands and stop worrying
  about the regressive flat price.

### Q6. Competitive response
If a competitor (deal site, bank loyalty platform, POS vendor adding vouchers)
undercuts on per-voucher price, do we compete on price or on the
redemption-data / POS-integration story? (Recommendation: the story, not the price.)

### Q7. Currency & rounding
All prices in VND with no decimals — confirm we never need USD pricing
(international brands/tourism segment) or promotional fractional credits.

---

## 7. Decision Log

| Date | Question | Decision | Rationale / Data |
|---|---|---|---|
| 2026-09-03 | — | Document created; Phase 1 recommendation: list 5,000, intro 2,000–3,000 time-boxed, stay flat | Market analysis (this doc) |
| | | | |

---

## 8. How to implement a pricing change (mechanics, for reference)

1. **Global fallback change**: edit `CreditConfig` in `src/NonCash.API/appsettings.json`.
2. **Scoped policy (Brand / BrandGroup / Global, versioned, time-bound)**: create in
   Admin → Pricing Policies (`/admin/credit-policies`) — resolution is automatic
   (Brand → BrandGroup → Global → fallback).
3. **Purchase price snapshot**: `CreditService.CreatePurchaseAsync` snapshots the
   resolved policy into the batch — existing batches are never repriced retroactively.
4. **Welcome grants** are governed separately by `WelcomeGrantPolicy`
   (see `docs/user-guides/admin-user-guide.md`).

---

## 9. Analytics Backlog — data needed for the pricing decisions

> Agreed 2026-09-03: build these reports before Phase 2 pricing decisions.
> Tracked as `pricing-analytics` in `_bmad-output/implementation-artifacts/sprint-status.yaml`.

| # | Report | Unblocks | Sketch |
|---|---|---|---|
| **R1** | **Voucher face-value distribution per brand** | **Q2 (flat vs Model C)** — the main open question | Per brand: voucher count and share by face-value band (e.g. <50k / 50–200k / >200k), from `voucher_plan_headers.FaceValue` weighted by distributed quantity; platform-wide roll-up on top |
| R2 | Effective take rate per brand | Guardrail #1, #3 | Credit consumptions ÷ face value distributed, per brand, over time (join `credit_consumptions` ↔ distributed vouchers ↔ plan face value) |
| R3 | Credit purchase & burn timeline per brand | Guardrail #2, #4 (elasticity cohorts) | Purchase batches vs consumption rate; days balance-zero → next purchase |

**R1 is the priority** — Q2 cannot be decided without it. R2/R3 can follow in the same
analytics page once the data is assembled. Audience: Admin only
(brand-level take rate is internal pricing intelligence, not for brand eyes).

---

*Related: [Notification Matrix](./notification-matrix.md) (low-balance & expiry warnings),
[Admin User Guide](./user-guides/admin-user-guide.md) (pricing policy management),
`src/NonCash.Core/Entities/CreditPricingPolicy.cs` (policy model).*
