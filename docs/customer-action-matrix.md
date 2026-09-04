# Customer Action Matrix

**Status: IMPLEMENTED (2026-09-03) — enforcement status in §6**
**Created: 2026-09-03 · Owner: Platform / Business · Related: customer data model redesign (Option A)**

This document defines **which actions are allowed or denied** for a customer in each
state, across every touchpoint of the platform. It is the authoritative reference for
the customer-model redesign (`brand_customers` mapping) and for any future feature
that touches customer behavior.

---

## 1. Core principle — prospective-only enforcement ("grandfathering")

> **A paid asset stays valid as it is. A block only prevents actions from the moment
> the block occurs.**

Concretely, four rules:

| # | Rule | Meaning |
|---|---|---|
| P1 | **Grandfathering** | Vouchers already owned at the moment of blocking remain fully valid: redeemable, transferable. A block never confiscates, freezes, or claws back existing assets. |
| P2 | **Prospective** | Blocks apply only from the block moment onward, and only to *new* actions — acquisitions (receive, buy, be gifted), and for S2 also account access (login). They never touch *continuing* actions inside an already-started purchase-and-use chain: redemption of a held voucher is honored. |
| P3 | **Scope** | A brand block affects only that brand's distribution channels; the customer remains fully normal everywhere else. The platform blacklist affects all channels. |
| P4 | **Reversibility** | Unblock / un-blacklist restores normal status immediately. Nothing is retroactive — including things that happened while blocked (a voucher received in error is handled by adjustment, not by the block). |

This principle is the direct consequence of design decision D3 (2026-09-03).

---

## 2. Customer states

| State | Field (target) | Set by | Meaning |
|---|---|---|---|
| **S0 — Active** | — | — | Normal customer. Mapped to ≥ 1 brand, or platform-only (no brand relation yet). |
| **S1 — Brand-blocked** | `BrandCustomer.IsBlocked = true` (per brand) | BrandManager, own brand only | The brand no longer wants to distribute to this customer. Soft, commercial-level. |
| **S2 — Platform-blacklisted** | `CustomerStatus.Blacklisted` | Admin only | Fraud-level. Global, affects all channels. |

A customer can be S1 for Brand B and perfectly S0 for Brand C at the same time.
S2 is exclusive and overrides everything.

---

## 3. The matrix — customer-initiated actions (member side)

Target state. "Brand B" = the brand that applied the block in S1.

| # | Action | S0 Active | S1 Brand-blocked (by B) | S2 Platform-blacklisted |
|---|---|---|---|---|
| 1 | Receive batch promotion **from B** | ✅ | ❌ skipped | ❌ skipped |
| 2 | Receive batch promotion from another brand | ✅ | ✅ | ❌ skipped |
| 3 | Buy from Store — **B's plans** | ✅ | ❌ blocked | ❌ blocked |
| 4 | Buy from Store — other brands' plans | ✅ | ✅ | ❌ blocked |
| 5 | Be B2B gifting recipient — **B's plans** | ✅ | ❌ skipped | ❌ skipped |
| 6 | Be B2B gifting recipient — other brands | ✅ | ✅ | ❌ skipped |
| 7 | Receive P2P transfer (member → member) | ✅ | ✅ * | ❌ skipped |
| 8 | Send P2P transfer | ✅ | ✅ | ❌ blocked |
| 9 | Redeem voucher at POS (held vouchers, code-based) | ✅ | ✅ | ✅ |
| 10 | Log in / use wallet (view My Vouchers, history) | ✅ | ✅ | ❌ blocked (O2) |
| 11 | Self-register / claim wallet | ✅ | ✅ | ❌ rejected |

\* P2P transfer is member-to-member, not brand-initiated. Blocking a member from
receiving a friend's gift because one brand blocked them exceeds the block's scope (P3).

**Redemption note:** POS redemption never checks customer status — not for S1
(P3: a brand block never touches redemption) and not for S2 (O1: redemption of an
already-held voucher is a *continuing* action in the purchase-and-use chain started
before the blacklist, and the customer's rights on owned assets are preserved).
POS redemption is voucher-code based and requires no member login, so the O2 login
block does not reach it.

**Rationale for rows 3–4:** the Store purchase check runs against the plan's owning
brand, so a block by B blocks B's plans only — matching P3. (Today's single global
check blocks everything; this refines with the mapping.)

---

## 4. The matrix — staff-initiated actions (management side)

| # | Action | BrandManager | Admin | Notes |
|---|---|---|---|---|
| 1 | View / search customers | Own brand's mapped customers only | All customers | D4 — the single-box search runs over `customers ⋈ brand_customers` |
| 2 | Create customer manually | ✅ — auto-maps to own brand | ✅ — platform asset, no mapping | |
| 3 | Import customers (file) | ✅ — every imported row maps to own brand | ✅ — no mapping | Upsert unchanged; mapping is new |
| 4 | Edit customer profile | Mapped customers only | All | |
| 5 | Block / unblock per-brand (S1) | ✅ own brand only | ✅ any | New capability |
| 6 | Blacklist / un-blacklist platform (S2) | ❌ | ✅ | Unchanged |
| 7 | See *who* mapped a customer (Source, CreatedBy) | Own brand's links | All | Audit trail of the relationship |

**Visibility policy (platform lever):** default **Closed** — brands see only mapped
customers; unmapped platform members are invisible to all brands. A future lever may
allow platform-run sponsored outreach to platform members (platform as controller,
respecting per-brand `MarketingOptOut`); brands never browse each other's subsets.

---

## 5. Mapping creation events (what writes `brand_customers`)

| Event | Source value | Trigger point |
|---|---|---|
| File import | `Import` | `CustomerService.UpsertAsync` (brand context) |
| Manual create | `Manual` | `CustomerService.CreateAsync` (brand context) |
| Batch promotion distribution | `PromotionAuto` | `PromotionService.DistributeAsync` |
| Loyalty-app segment distribution | `PromotionAuto` | integration path of distribution |
| Store self-purchase | `SelfPurchase` | `PurchaseService.ConfirmPaymentAsync` |
| B2B gifting batch transfer | `GiftingAuto` | `VoucherTransferService` |
| P2P transfer received | `Transfer` | `TransferService.TransferAsync` |
| POS redemption | `Redemption` | `PosService` commit path |
| Member self-registration | — | **No mapping** — self-registration creates a platform asset only; the first brand touch creates the first mapping (D2) |

---

## 6. Enforcement status (implemented 2026-09-03)

All touchpoints below now enforce the matrix. The per-brand block and the
`brand_customers` mapping are implemented (`BrandCustomer` entity, repository,
`AddBrandCustomerMapping` migration with the D1 backfill to the earliest-created
Active brand).

| # | Touchpoint | S1 (per-brand block) | S2 (platform blacklist) | Status |
|---|---|---|---|---|
| 1 | `PromotionService` — promotion recipient | ✅ skipped (`BrandBlocked`) | ✅ skipped (`Blacklisted`) | closed |
| 2 | `TransferService` — transfer recipient | — none (P3: member-to-member) | ✅ skipped (`Blacklisted`) | closed |
| 3 | `TransferService` — transfer **sender** | — none (P3) | ✅ rejected (`SenderBlacklisted`) | closed — was gap |
| 4 | `PurchaseService` — store buyer | ✅ rejected for own-brand plans (`BrandBlocked`) | ✅ rejected (`MemberBlacklisted`) | closed |
| 5 | `VoucherTransferService` — gifting recipient | ✅ rejected (`RecipientBrandBlocked`) | ✅ rejected (`RecipientBlacklisted`) | closed — was gap |
| 6 | `PosService` — POS redemption | — no check (P3) | — no check (O1) | by design |
| 7 | `MemberRegistrationController` — wallet claim | — none (no mapping yet — D2) | ✅ rejected 403 | closed — was gap |
| 8 | `AuthService` — member login | — none (P3) | ✅ rejected ("Account is locked.") | closed — was gap |

O2 implementation note: login reads `Customer.Status` directly and does **not**
mirror the blacklist into `MemberAccount.Status` — un-blacklisting restores login
instantly (P4). Service-level checks (rows 1–5) remain as defense-in-depth because
integration partners call APIs without member login.

Auto-link hooks per §5 are active on all events (promotion, self-purchase, P2P
transfer, gifting accept, POS redemption). Staff-side scoping (§4) is enforced in
`CustomersController` + `CustomerService` (BrandManager sees/edits mapped customers
only; blacklist/unblacklist are Admin-only; block/unblock are per-brand).

`GET /api/v1/customers` and `GET /api/v1/customers/{id}` are no longer
`[AllowAnonymous]` — the public PII exposure noted earlier is closed.

---

## 7. Decisions (resolved 2026-09-03)

| # | Question | Decision | Rationale |
|---|---|---|---|
| **O1** | Does platform blacklist (S2) block POS redemption of already-held vouchers? | **No — redemption stays allowed.** A voucher already owned by the customer is honored: rights on owned assets are preserved, used normally. POS redemption is a *continuing* action in the purchase-and-use chain started before the blacklist, and it is voucher-code based (no member login involved). Fraud with stolen instruments is handled via the credit adjustment / compensation workflow, not by voiding vouchers. |
| **O2** | Can an S2 customer log in? | **No — login rejected.** Blacklist means "not welcome for ALL subsequent actions": login, promotions, purchases, transfers, registration. It is a *stop-until-clarified* state: after investigation the customer is either un-blacklisted (returns to normal — P4) or banned permanently. The only thing that continues is redemption of already-held vouchers (O1), which does not require login. Accepted consequence: a pending P2P transfer awaiting a blacklisted recipient's confirmation will expire unconfirmed — the soft-lock releases automatically. |

**Implementation note (O2):** `MemberAccount.Status` already has a `Locked` value — the login check may consult `Customer.Status` directly or set the member account `Locked` on blacklisting; decide in the implementation plan. Service-level checks (Promotion / Purchase / Transfer) remain as defense-in-depth even though the login gate already blocks the web paths, because integration partners call APIs without member login.

---

## 8. Changelog

| Date | Change |
|---|---|
| 2026-09-03 | Implemented (Option A): `BrandCustomer` mapping (entity, repository, migration + D1 backfill); S1 per-brand block enforced at promotion, store purchase (own-brand plans), and gifting recipient; S2 enforced at member login (O2), self-registration, and transfer sender; auto-link hooks on all §5 events; brand-scoped staff visibility (§4); `AllowAnonymous` removed from customer GET endpoints; §6 gaps closed. |
| 2026-09-03 | O1 decided: S2 does **not** block POS redemption of held vouchers (continuing action, code-based, no login). O2 decided: S2 **blocks login** — stop-until-clarified state; pending-transfer expiry accepted. P2 rule refined (new vs continuing actions); §3/§6 updated. |
| 2026-09-03 | Created. Principles P1–P4 (from D3 decision); matrices for member-side and staff-side actions; mapping events; 8 verified enforcement gaps; O1/O2 open. |

---

*Related: [Credit Pricing Strategy](./credit-pricing-strategy.md) (per-brand policies),
[Notification Matrix](./notification-matrix.md) (customer-facing notifications),
`src/NonCash.Core/Entities/Customer.cs`, `src/NonCash.Core/Entities/BrandCustomer.cs`.*
