# Customer Action Matrix

**Status: IMPLEMENTED (2026-09-03) — enforcement status in §7**
**Created: 2026-09-03 · Owner: Platform / Business · Related: customer data model redesign (Option A)**

This document defines **which actions are allowed or denied** for a customer in each
state, across every touchpoint of the platform. It is the authoritative reference for
the customer-model redesign (`brand_customers` mapping) and for any future feature
that touches customer behavior.

---

## 1. Identity model — unique customer, separate brand ownership

> **A customer exists exactly once (in `customers`). Which brands "own" a
> relationship with that customer is tracked separately (in `brand_customers`).**

| Table | Role | Key facts |
|---|---|---|
| `customers` | **Global platform identity** | One row per person — `PhoneNumber` is unique platform-wide (1 person = 1 phone = 1 wallet). Pure identity data; no brand reference. |
| `brand_customers` | **Brand ↔ customer relationship** | One row per (BrandId, CustomerId) pair. Carries `Source` (how the link started — never upgraded on re-link), `IsBlocked` (S1), `MarketingOptOut`, `CreatedBy`. |

### 1.1 Customer creation flows — with brand vs without brand

**Without brand** (platform asset, no mapping):

| Path | Entry point | Result |
|---|---|---|
| Admin creates a customer | `POST /api/v1/customers` as Admin (API-only — no Admin UI) | `customers` row only |
| Member self-registration | `POST /api/v1/members/register` (public) | `customers` + `member_accounts` rows, **no mapping** — the first brand touch creates the first mapping (D2) |

**With brand** (mapping written at the touchpoint):

| Path | `Source` | Notes |
|---|---|---|
| BrandManager Add (Customers page) | `Manual` | Link-or-create: an already-known phone is linked to the brand, not duplicated |
| BrandManager CSV import | `Import` | Same link-or-create upsert rule |
| Batch promotion distribution | `PromotionAuto` | Existing customers only — recipients must pre-exist |
| Store self-purchase (Gift plan) | `SelfPurchase` | |
| B2B gifting / P2P transfer | `GiftingAuto` / `Transfer` | Unknown recipient phone → placeholder `customers` row first (enriched with real profile data at self-registration) |
| POS redemption | `Redemption` | |

Rule of thumb: **staff-side paths always map; member-side onboarding never maps** — the
mapping appears when the customer first touches a brand (D2).

---

## 2. Core principle — prospective-only enforcement ("grandfathering")

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

## 3. Customer states

| State | Field (target) | Set by | Meaning |
|---|---|---|---|
| **S0 — Active** | — | — | Normal customer. Mapped to ≥ 1 brand, or platform-only (no brand relation yet). |
| **S1 — Brand-blocked** | `BrandCustomer.IsBlocked = true` (per brand) | BrandManager, own brand only | The brand no longer wants to distribute to this customer. Soft, commercial-level. |
| **S2 — Platform-blacklisted** | `CustomerStatus.Blacklisted` | Admin only | Fraud-level. Global, affects all channels. |

A customer can be S1 for Brand B and perfectly S0 for Brand C at the same time.
S2 is exclusive and overrides everything.

---

## 4. The matrix — customer-initiated actions (member side)

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

## 5. The matrix — staff-initiated actions (management side)

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

## 6. Mapping creation events (what writes `brand_customers`)

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

## 7. Enforcement status (implemented 2026-09-03)

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

Auto-link hooks per §6 are active on all events (promotion, self-purchase, P2P
transfer, gifting accept, POS redemption). Staff-side scoping (§5) is enforced in
`CustomersController` + `CustomerService` (BrandManager sees/edits mapped customers
only; blacklist/unblacklist are Admin-only; block/unblock are per-brand).

`GET /api/v1/customers` and `GET /api/v1/customers/{id}` are no longer
`[AllowAnonymous]` — the public PII exposure noted earlier is closed.

---

## 8. Decisions (resolved 2026-09-03)

| # | Question | Decision | Rationale |
|---|---|---|---|
| **O1** | Does platform blacklist (S2) block POS redemption of already-held vouchers? | **No — redemption stays allowed.** A voucher already owned by the customer is honored: rights on owned assets are preserved, used normally. POS redemption is a *continuing* action in the purchase-and-use chain started before the blacklist, and it is voucher-code based (no member login involved). Fraud with stolen instruments is handled via the credit adjustment / compensation workflow, not by voiding vouchers. |
| **O2** | Can an S2 customer log in? | **No — login rejected.** Blacklist means "not welcome for ALL subsequent actions": login, promotions, purchases, transfers, registration. It is a *stop-until-clarified* state: after investigation the customer is either un-blacklisted (returns to normal — P4) or banned permanently. The only thing that continues is redemption of already-held vouchers (O1), which does not require login. Accepted consequence: a pending P2P transfer awaiting a blacklisted recipient's confirmation will expire unconfirmed — the soft-lock releases automatically. |

**Implementation note (O2):** `MemberAccount.Status` already has a `Locked` value — the login check may consult `Customer.Status` directly or set the member account `Locked` on blacklisting; decide in the implementation plan. Service-level checks (Promotion / Purchase / Transfer) remain as defense-in-depth even though the login gate already blocks the web paths, because integration partners call APIs without member login.

---

## 9. Registered Changes (CR Registry)

| CR ID | Item | Status |
|---|---|---|
| CR-2026-09-06-10 | Fix import lost-update: `UpsertAsync` updates an existing customer's name/email without calling `Update()`, so those changes are silently never persisted against the production DB (masked in tests by InMemory change tracking). Direction set by CR-2026-09-06-11: persist **fills into empty fields only**, never overwrite existing values; the import result must report skipped-overwrite rows | **Verified 2026-09-07** |
| CR-2026-09-06-11 | **3-tier write model for the global customer record** (decided 2026-09-07): (1) Customer (member app) owns their own data; (2) Brand writes (Add-link, Import, Edit) are **fill-empty-only** — may populate empty/placeholder fields (placeholder rows from transfer/gifting: FullName = phone, no email) but must never overwrite existing values; (3) Platform Admin has full edit — see CR-2026-09-07-14. Phone number is the natural key and is not editable under this model | **Verified 2026-09-07** |
| CR-2026-09-07-14 | **Admin customer edit**: split `PUT /api/v1/customers/{id}` (currently BrandManager+Admin) so BrandManager edits follow fill-empty-only while Admin gets full edit; build the missing Admin → Customers UI (list/detail/edit); add an audit trail for admin edits (who/when/old→new) since email is the voucher delivery channel; phone number not editable. Optional phase 2: notify the customer when an admin changes their email | **Verified 2026-09-07** |
| CR-2026-09-07-15 | **Contract clause: 3-layer customer-data ownership** — add to the business contract template: (1) identity record = customer-owned, platform custodian, brand contributor (fill-empty-only, no overwrite rights); (2) relationship data (`brand_customers` mapping, original import lists, per-brand transaction history) = brand-exclusive asset, platform guarantees no cross-brand sharing; (3) platform data (fraud signals, global blacklist, billing) = platform-owned. Plus import warranty (brand warrants lawful basis/consent for uploaded contacts), curation clause (admin edit with audit), exit clause (brand may export relationship data on offboarding; identity record stays). Also: one-line notice on the Import screen ("imported customers join the shared platform identity; your brand keeps exclusive rights to its list and activity data") and a §1 identity-model update in this doc | Approved — pending implementation |

## 10. Changelog

| Date | Change |
|---|---|
| 2026-09-07 | **CR-10 + CR-11 + CR-14 Verified** by user test — Sóng 1 (customer fill-empty-only + admin audit trail) confirmed working. Completion gate cleared. |
| 2026-09-07 | Add-customer link-or-create fix (`CustomerService.CreateAsync`) **Verified 2026-09-06** via user test — BrandManager Add on a globally-existing phone now links the customer into the brand; a repeat Add returns 409 already-linked. Completion gate cleared. |
| 2026-09-07 | §9: CR-2026-09-06-11 decided as the **3-tier write model** (customer self-service / brand fill-empty-only / admin full edit) and approved together with CR-2026-09-07-14 (admin customer edit + UI + audit) and CR-2026-09-07-15 (contract ownership clause); CR-2026-09-06-10 direction set (persist fills only, report skipped overwrites). |
| 2026-09-06 | §9 added (CR registry): CR-2026-09-06-10 (import lost-update) and CR-2026-09-06-11 (fill-empty-only rule) registered — both surfaced during the Add-customer link-or-create fix session. |
| 2026-09-06 | §1 added: identity model — unique global customer (`customers`) with brand ownership tracked separately (`brand_customers`); the two customer-creation flows (with brand / without brand) consolidated; later sections renumbered. |
| 2026-09-03 | Implemented (Option A): `BrandCustomer` mapping (entity, repository, migration + D1 backfill); S1 per-brand block enforced at promotion, store purchase (own-brand plans), and gifting recipient; S2 enforced at member login (O2), self-registration, and transfer sender; auto-link hooks on all §6 events; brand-scoped staff visibility (§5); `AllowAnonymous` removed from customer GET endpoints; §7 gaps closed. |
| 2026-09-03 | O1 decided: S2 does **not** block POS redemption of held vouchers (continuing action, code-based, no login). O2 decided: S2 **blocks login** — stop-until-clarified state; pending-transfer expiry accepted. P2 rule refined (new vs continuing actions); §4/§7 updated. |
| 2026-09-03 | Created. Principles P1–P4 (from D3 decision); matrices for member-side and staff-side actions; mapping events; 8 verified enforcement gaps; O1/O2 open. |

---

*Related: [Credit Pricing Strategy](./credit-pricing-strategy.md) (per-brand policies),
[Notification Matrix](./notification-matrix.md) (customer-facing notifications),
`src/NonCash.Core/Entities/Customer.cs`, `src/NonCash.Core/Entities/BrandCustomer.cs`.*
