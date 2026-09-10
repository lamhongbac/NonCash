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

**Login identifier (CR-2026-09-09-27):** a customer signs in with the **phone number** or the
**email address** on their `customers` row, plus their password. `member_accounts.username` no
longer exists — the column and its unique index were dropped, because no persona ever saw it and
keeping it made the real credential undiscoverable. Both surviving identifiers are unique on the
global customer row (phone always, email whenever present), so neither can match two accounts.
An identifier that is neither — no `@` and not 9–15 digits — is reported as malformed input, not
as a wrong password, because the customer's next action is different.

**Lost password (CR-2026-09-09-27 C1):** `POST /api/v1/auth/member/forgot-password` emails a
sign-in link valid for **30 minutes** (`purpose: sign_in_link`), landing on the same
`/member/welcome` route as the 7-day voucher magic link, where the customer can sign in and set a
password. The response is identical whether or not a link was actually sent, so the endpoint
cannot be used to enumerate accounts, and a blacklisted customer (O2) is never mailed a way in.

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
| B2B gifting / P2P gift | `GiftingAuto` | Unknown recipient phone → placeholder `customers` row first (enriched with real profile data at self-registration). Since CR-2026-09-10-30 the mapping is written when the recipient **accepts** the gift, not when it is sent — a declined, cancelled or expired gift maps nobody. The `Transfer` source value is no longer written by any path. |
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
| 7 | Receive a P2P gift (member → member) | ✅ | ❌ if blocked by the voucher's own brand * | ❌ skipped |
| 8 | Send a P2P gift | ✅ | ✅ | ❌ blocked |
| 9 | Redeem voucher at POS (held vouchers, code-based) | ✅ | ✅ | ✅ |
| 10 | Log in / use wallet (view My Vouchers, history) | ✅ | ✅ | ❌ blocked (O2) |
| 11 | Self-register / claim wallet | ✅ | ✅ | ❌ rejected |
| 12 | Write in a gift's message box (CR-2026-09-10-31) | ✅ | ✅ | ❌ blocked (O2) |

\* **Refined by CR-2026-09-10-30.** A P2P gift is member-to-member, so one brand's block
does not stop a customer from receiving gifts in general (P3) — but accepting a gift of
**brand B's** voucher writes a `brand_customers` row for B, i.e. it creates exactly the
relationship B refused. `VoucherTransferService.InitiateAsync` therefore rejects the gift
with `RecipientBrandBlocked` when the voucher's owning brand has blocked the recipient
(§7 row 5). A block by any *other* brand leaves the gift untouched.

**Two-stage gifting (CR-2026-09-10-30):** sending a gift no longer moves ownership. It
reserves the voucher for 7 days and records a `voucher_transfers` row in
`PendingAcceptance`; ownership, the `voucher_distributions` record, the
`voucher.transferred` webhook and the `GiftingAuto` mapping all happen only when the
recipient accepts. Both recipient checks (rows 5–6 of §7) run at *send* time, so a blocked
recipient never receives an accept link to begin with — a gift is never created and then
confiscated, which keeps P1/P2 intact.

**Gift message box (CR-2026-09-10-31, row 12):** a conversation belongs to the two
customers in a gift, not to a brand, so an S1 brand block has nothing to act on and
`PostMessageAsync` adds no status check of its own. Authorization is participant-only —
anyone who is neither the sender nor the recipient gets `Forbidden` whether they are
reading or writing — and the endpoint is reachable only through member login, so S2 is
already stopped by the O2 gate. Abuse is limited by the caps instead: 20 messages per
gift, 10 per member per hour, 500 characters, and writing closes with the gift. Reading a
closed conversation stays allowed, which is a *continuing* action under P2.

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
| Gift accepted (B2B batch or P2P) | `GiftingAuto` | `VoucherTransferService.AcceptAsync` — CR-2026-09-10-30: **sending** a gift maps nobody; only acceptance does |
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
| 2 | `TransferService` — gift recipient (bulk send path) | ✅ delegated to row 5 per voucher | ✅ skipped (`Blacklisted`) before any gift is created | closed |
| 3 | `TransferService` — gift **sender** (bulk send path) | — none (P3) | ✅ rejected (`SenderBlacklisted`) | closed — was gap; **see the hole below** |
| 4 | `PurchaseService` — store buyer | ✅ rejected for own-brand plans (`BrandBlocked`) | ✅ rejected (`MemberBlacklisted`) | closed |
| 5 | `VoucherTransferService` — gifting recipient | ✅ rejected (`RecipientBrandBlocked`) | ✅ rejected (`RecipientBlacklisted`) | closed — was gap |
| 6 | `PosService` — POS redemption | — no check (P3) | — no check (O1) | by design |
| 7 | `MemberRegistrationController` — wallet claim | — none (no mapping yet — D2) | ✅ rejected 403 | closed — was gap |
| 8 | `AuthService` — member login | — none (P3) | ✅ rejected ("Account is locked.") | closed — was gap |

O2 implementation note: login reads `Customer.Status` directly and does **not**
mirror the blacklist into `MemberAccount.Status` — un-blacklisting restores login
instantly (P4). Service-level checks (rows 1–5) remain as defense-in-depth because
integration partners call APIs without member login.

**Open hole (found 2026-09-10 while implementing CR-2026-09-10-30):** the sender-side S2
check of row 3 lives only in `TransferService.TransferAsync` — the bulk endpoint
`POST /api/v1/member/vouchers/transfer`, which is what the member wallet's **Gửi Quà**
dialog calls. The single-voucher endpoint `POST /api/v1/member/vouchers/{id}/initiate-transfer`
calls `VoucherTransferService.InitiateAsync` directly and never re-checks the sender, so an
S2 customer still holding a JWT issued before the blacklist could send a gift that way.
Nothing in the web UI uses that endpoint and the O2 login gate blocks the normal path, so
this is latent rather than exploitable today. Proposed fix, **pending owner approval**: move
the sender check into `InitiateAsync` so both entry points enforce §4 row 8.

Auto-link hooks per §6 are active on all events (promotion, self-purchase, gift accept,
POS redemption). Staff-side scoping (§5) is enforced in
`CustomersController` + `CustomerService` (BrandManager sees/edits mapped customers
only; blacklist/unblacklist are Admin-only; block/unblock are per-brand).

`GET /api/v1/customers` and `GET /api/v1/customers/{id}` are no longer
`[AllowAnonymous]` — the public PII exposure noted earlier is closed.

---

## 8. Decisions (resolved 2026-09-03)

| # | Question | Decision | Rationale |
|---|---|---|---|
| **O1** | Does platform blacklist (S2) block POS redemption of already-held vouchers? | **No — redemption stays allowed.** A voucher already owned by the customer is honored: rights on owned assets are preserved, used normally. POS redemption is a *continuing* action in the purchase-and-use chain started before the blacklist, and it is voucher-code based (no member login involved). Fraud with stolen instruments is handled via the credit adjustment / compensation workflow, not by voiding vouchers. |
| **O2** | Can an S2 customer log in? | **No — login rejected.** Blacklist means "not welcome for ALL subsequent actions": login, promotions, purchases, gifts, registration. It is a *stop-until-clarified* state: after investigation the customer is either un-blacklisted (returns to normal — P4) or banned permanently. The only thing that continues is redemption of already-held vouchers (O1), which does not require login. Accepted consequence: a gift already pending when the blacklist lands cannot be accepted — `AcceptAsync` is only reachable through login, so it expires unconfirmed, the reservation releases automatically and the sender is emailed that nobody answered in time (CR-2026-09-10-30). A *new* gift to a blacklisted recipient is refused at send time and never created (§7 row 5). |

**Implementation note (O2):** `MemberAccount.Status` already has a `Locked` value — the login check may consult `Customer.Status` directly or set the member account `Locked` on blacklisting; decide in the implementation plan. Service-level checks (Promotion / Purchase / Transfer) remain as defense-in-depth even though the login gate already blocks the web paths, because integration partners call APIs without member login.

---

## 9. Registered Changes (CR Registry)

| CR ID | Item | Status |
|---|---|---|
| CR-2026-09-06-10 | Fix import lost-update: `UpsertAsync` updates an existing customer's name/email without calling `Update()`, so those changes are silently never persisted against the production DB (masked in tests by InMemory change tracking). Direction set by CR-2026-09-06-11: persist **fills into empty fields only**, never overwrite existing values; the import result must report skipped-overwrite rows | **Verified 2026-09-07** |
| CR-2026-09-06-11 | **3-tier write model for the global customer record** (decided 2026-09-07): (1) Customer (member app) owns their own data; (2) Brand writes (Add-link, Import, Edit) are **fill-empty-only** — may populate empty/placeholder fields (placeholder rows from transfer/gifting: FullName = phone, no email) but must never overwrite existing values; (3) Platform Admin has full edit — see CR-2026-09-07-14. Phone number is the natural key and is not editable under this model | **Verified 2026-09-07** |
| CR-2026-09-07-14 | **Admin customer edit**: split `PUT /api/v1/customers/{id}` (currently BrandManager+Admin) so BrandManager edits follow fill-empty-only while Admin gets full edit; build the missing Admin → Customers UI (list/detail/edit); add an audit trail for admin edits (who/when/old→new) since email is the voucher delivery channel; phone number not editable. Optional phase 2: notify the customer when an admin changes their email | **Verified 2026-09-07** |
| CR-2026-09-07-15 | **Contract clause: 3-layer customer-data ownership** — add to the business contract template: (1) identity record = customer-owned, platform custodian, brand contributor (fill-empty-only, no overwrite rights); (2) relationship data (`brand_customers` mapping, original import lists, per-brand transaction history) = brand-exclusive asset, platform guarantees no cross-brand sharing; (3) platform data (fraud signals, global blacklist, billing) = platform-owned. Plus import warranty (brand warrants lawful basis/consent for uploaded contacts), curation clause (admin edit with audit), exit clause (brand may export relationship data on offboarding; identity record stays). Also: one-line notice on the Import screen ("imported customers join the shared platform identity; your brand keeps exclusive rights to its list and activity data") and a §1 identity-model update in this doc | Approved — pending implementation |
| CR-2026-09-09-26 | **Customer login by phone number + honest "no password yet" error.** Trigger: the owner tried `0913660575 / hellobac123` and got "Invalid username or password." even though the password was correct — member login only ever matched `member_accounts.username`, and **no persona (Admin / BrandManager / customer) has any screen where that username is visible**, so the credential was undiscoverable by design. **(A)** `AuthService.LoginMemberAsync(identifier, …)` now resolves the member by username **first**, then falls back to `Customer.NormalizePhoneNumber` → `customers.phone_number` → `member_accounts.customer_id` (formatted input such as `0913 660 575` normalizes; an identifier that is not 9–15 digits skips the customer lookup). The JSON field stays `username` for contract compatibility. `MemberLogin.razor` label → "Phone number", helper text states the username also works. **(B)** Error messages now separate the two human actions: an auto-provisioned member account has an **empty** `PasswordHash` (distribution/gifting creates it with `Username = PhoneNumber`, and the only entry is the 7-day magic link), so it returns `AuthService.NoPasswordYetMessage` — "your account was created automatically when a brand sent you a voucher, so it has no password yet… open that email and sign in with its link, then choose Set Password… if it has expired, contact the brand" — instead of "Invalid username or password." Wrong password / unknown identifier keep the generic message (no account enumeration). Remaining gaps registered, not implemented: username discoverability surfaces, and member forgot-password (`ForgotPasswordAsync` covers `UserAccount` only, so an expired magic link + no password = permanent lock-out) | **Implemented** 2026-09-09 — 154 unit + 142 integration = 296 tests pass. **Superseded by CR-2026-09-09-27**: the username lookup, the username column and the `username` JSON field were all removed, and the forgot-password gap it registered was closed |
| CR-2026-09-09-27 | **Abolish the customer username; sign in with phone **or** email; member forgot-password (C1 + C2).** Owner directive: "không còn khái niệm username cho customer và sử dụng số dt hoặc email dùng để login vào trang khách hàng", then "xoá hẳn username". CR-26 had only added phone as a *fallback* — the username stayed as a column, a unique index, a JSON field and a concept, so the credential a customer was never shown still existed. **(A) Username removed outright.** Migration `20260909085516_DropMemberAccountUsername` drops `IX_member_accounts_username` + `public.member_accounts.username`; `MemberAccount.Username`, its EF mapping, `IMemberAccountRepository.GetByUsernameAsync`/`UsernameExistsAsync` and every `Username = phone` provisioning site (Promotion, Transfer, VoucherTransfer, SeedTool) are deleted. Verified against the dev DB before applying: all 16 member accounts have **both** a phone and an email, so nobody lost access (the 16 usernames were exported to a scratch CSV first — the drop itself is one-way). The member JWT no longer emits `unique_name` (one producer, zero consumers). **(B) Identifier resolution.** `LoginMemberAsync(identifier, password)`: `@` present → `customers.email` (lowercased), else 9–15 digits after normalization → `customers.phone_number`, then → `member_accounts.customer_id`. Both keys are unique on the global customer row, so neither can match two accounts. Neither shape → `MalformedIdentifierMessage` ("Enter the phone number or email on your account — for example 0913660575 or you@example.com."), *not* a password error, because the next action differs; unknown identifier and wrong password still share `InvalidIdentifierMessage` (no enumeration). Contract: `MemberLoginRequest(Identifier, Password)` for members, `LoginRequest(Username, Password)` retained for staff; registration becomes `MemberRegisterRequest(Password, FullName, PhoneNumber, Email)` and its response drops `Username`. **(C) C1 — forgot password.** New `POST /api/v1/auth/member/forgot-password` (`AllowAnonymous`, `member-auth` fixed-window limit 5/min/IP, shared with member login): `GenerateSignInLinkToken` mints a **30-minute** token (`purpose: sign_in_link`) that the existing validator accepts alongside the 7-day voucher `magic_link`, so it lands on the same `/member/welcome` route where the customer signs in and sets a password. New `MemberSignInLink.html` + `SignInLinkNotification` + `NotifySignInLinkAsync`. The reply is byte-identical whether a link was sent, no member matched, the customer has no email, or the customer is blacklisted (O2 — never mailed a way in); only malformed input returns 400. **(D) C2 — surfaces.** New `/member/forgot-password` page, "Forgot password?" link on `MemberLogin.razor`, "Email me a new link" on the `Welcome.razor` failure screen, and `NoPasswordYetMessage` now points at Forgot password instead of at an email that may have expired. Login/register/set-password copy states phone-or-email and that NonCash never asks for a username; the member `PendingActivation` and locked messages now name the brand as the party who can act | **Implemented** 2026-09-09 — 165 unit + 149 integration = 314 tests pass (11 new unit + 7 new integration tests cover identifier resolution, the neutral recovery reply and the sign-in-link token); migration applied to the dev DB; API restart required. **Verified 2026-09-09** — owner manual test on the running dev stack: member login succeeds with the email and with the phone; the former username on `/member-login` is refused with the malformed-identifier guidance ("Enter the phone number or email address on your account — for example 0913660575 or you@example.com."); a staff username such as `lamhongbac` still signs in on `/login`, which is the staff path and was never part of this CR |

| CR-2026-09-09-28 | **My Vouchers wallet UX redesign (consumer-grade).** Owner verdict on the old page: "hơi mechanic (cơ khí)"; approved a 4-row proposal plus the direction "grid gồm icon + thông tin, click ra detail gồm cover, T&C, mệnh giá, thời hạn — đó là normal practice". **(A)** Removed the Member Account ID field and the Load button; the wallet auto-loads on open and the header keeps only a refresh icon and a count-aware Transfer button. **(B)** Tapping a card opens a detail dialog: cover image (resolved through the shared `VoucherCard.ResolveMediaUrl`), face value + status chip, expiry countdown plus exact datetime, applicable stores, a Terms & Conditions link, and — while redeemable — the barcode and code via the new `Shared/VoucherBarcode.razor` (JsBarcode), with a treat-it-like-cash warning and a "Transfer this voucher" shortcut. **(C)** Badge vocabulary: an unused, unexpired voucher now reads `Available` (was `Active`) in `VoucherDisplayHelper` and the `VoucherCard` color map. **(D)** Transfer dialog: helper "One phone per line — enter exactly N line(s).", live counter "N voucher(s) selected · M phone(s) entered", and a mismatch snackbar that states both numbers; bulk selection via the card checkbox "Select to transfer". Friendly empty state links to the Store. `member-user-guide` §4, §5.1, quick reference and troubleshooting rewritten to match | **Dev-complete 2026-09-09** — solution builds 0 errors, 165 unit + 149 integration pass; pending owner manual verification (6-step script handed over; barcode inside the dialog and MSA cover rendering not browser-verified by the agent) |
| CR-2026-09-09-29 | **Kill-switch log wording.** `EmailNotificationService.SendAsync`'s dev-suppression log line advises "Set Environment:Name to 'production' to enable real delivery", but the project policy is `pilot` — never `production`. One-line copy fix | **Registered — pending decision** 2026-09-09 |
| CR-2026-09-10-30 | **Gifting with confirmation, an honest status for the sender, and a gift log.** Trigger: owner live test — A (`0913660575`) sent one voucher to B (`0363464997`); the voucher vanished from A's wallet with no status anywhere, A never learned whether B received it, and the BrandManager saw nothing and could do nothing. Owner directive: the sender must acknowledge the gift's status; if B is not in the DB, tell A to ask B to register; always say an email was sent and always send one with a magic link; B must confirm receipt with a thank-you message to A; log the gift for tracking; and "transfer is too technical a word — describe it as sending a gift to someone". Owner approved all 7 rows plus row 8 ("thay vì output send email to console … chuyển thành log file"). **(A) Confirm-first gifting.** Ownership no longer moves at send time. `TransferService.TransferAsync` stopped being an instant move: it validates ownership, status and the sender blacklist, then delegates each voucher/phone pair to `VoucherTransferService.InitiateAsync`, which reserves the voucher (`TransferLockId` / `TransferLockedAt`) and writes a `voucher_transfers` row in `PendingAcceptance` with a 7-day expiry. Ownership, the `voucher_distributions` record, the `voucher.transferred` webhook and the `GiftingAuto` brand mapping all moved to `AcceptAsync` (§6). **(B) Honest delivery report.** `InitiateTransferResult` now carries `DeliveryStatus`, `RecipientPhone`, `RecipientName` and a full-sentence `Message`; the three cases are `Emailed`, `HoldingNoEmail` and `HoldingNotRegistered`. **Having an email address is the only requirement for `Emailed`** — the first cut gated on `member_accounts.password_hash` first, which would have reported the owner's own test recipient (`0363464997`: email on file, empty password hash because a brand auto-provisioned her) as "isn't on NonCash yet" and emailed her nothing, contradicting row 3 and CR-2026-09-07-19's always-magic-link decision. Caught against the dev DB before hand-over: the password state now only shapes the wording when there is no email to write to, and the same gate was lifted in `NotifyPendingGiftsAsync`. The bulk result reports the outcome per gift (`Deliveries`) alongside per-phone skips with their reason codes. **(C) Magic link whenever an email exists**, plus a flush hook: `NotifyPendingGiftsAsync` re-sends the accept link for every held gift the moment a placeholder phone registers with an email (called from `MemberRegistrationController`), so the sender never has to send twice. **(D) Recipient confirmation + thanks.** Accept takes an optional note persisted to `voucher_transfers.recipient_note` (with `resolved_at`) and emails the sender `GiftAccepted`; decline emails `GiftDeclined` with the reason; the 7-day sweep emails `GiftExpired`. New `GiftAccepted.html` / `GiftDeclined.html` / `GiftExpired.html` templates. **(E) Sender visibility.** `GET /member/transfers/outbox` returns status, responded-at, note and reason; the wallet keeps the voucher with an **On its way** badge (barcode replaced by an explanation naming the recipient and the reservation deadline) and a **Gifts you sent** panel lists Waiting / Accepted / Declined / Expired / Cancelled with a **Cancel gift** action while waiting. **(F) Gift log + BM screen.** `voucher_transfers` is the single gift log — the old distribution-based member history table was deleted rather than kept as a second overlapping list. New `GET /api/v1/brand/gifts` (BrandManager/Admin, brand-scoped, paged, status-filterable) and a read-only **BrandManager > Gifts** page showing date, serial, sender → recipient phones, status and both messages. **(G) Friendly wording.** New `src/NonCash.Shared/Helpers/GiftMessageHelper.cs` is the only place gifting is worded for customers: badge `On its way`, statuses `Waiting` / `Accepted` / `Declined` / `Expired` / `Cancelled by you`, delivery labels, and a cause + next-action sentence for every skip code. API reason codes stay machine-readable for tests and POS. Member `Transfers.razor` deleted → `Gifts.razor` ("Gifts from friends", `/member/gifts`, accept with a pre-filled thank-you note, decline with a reason); nav, the `Welcome.razor` magic-link landing and Terms §2/§3/§5 updated; `member-user-guide` §4/§5, quick reference and troubleshooting rewritten. **(H) Row 8 — file sink.** `FileNotificationService` replaces the console sink whenever real delivery is off: one structured `UTC [Category] key=value` line per notification appended to `Notifications:LogFile` (default `{BaseDirectory}/logs/notifications.log`), including the magic-link and sign-in URLs that `email_logs` does not store. **Two latent defects fixed on the way:** gift emails named the voucher from `plan.Brand?.Name`, which EF does not populate on that read path, so senders would have seen "your voucher" — `VoucherTransferService` now resolves the brand through `IBrandRepository`; and the member nav pointed at a non-existent `/transfers` route while `Welcome.razor`'s "View My Vouchers" went to `/store`. **One hole registered, not fixed:** the sender-side S2 check of §7 row 3 exists only on the bulk path — see the open hole in §7 | **Dev-complete 2026-09-10** — solution builds 0 errors in Release; 183 unit + 161 integration pass (12 new integration facts for the gift-confirmation flow + a new unit theory for `GiftMessageHelper`). Pending owner manual verification; API restart required |
| CR-2026-09-10-31 | **Gift message box (Hộp thư món quà).** Trigger: owner request after CR-2026-09-10-30 landed — "I also need to save the messages back and ward between sender and receiver somewhere in DB, i think that is a good memory, good experienced for customer and for this platform, please plan it"; the owner approved all 12 proposed rows and added "bạn cần thay đổi button transfer nên label gọn … chỉ là **'Gửi Quà'**". **(A) Storage.** Migration `20260910053357_AddGiftMessages` creates `gift_messages` (one row per line of a conversation) with indexes `(transfer_id, sent_at)` and `(recipient_member_id, is_read)`, and backfills the notes already living in the three scalar columns of `voucher_transfers`. **(B) Two sources, one truth.** `gift_messages` is authoritative; `voucher_transfers.note` / `recipient_note` / `reject_reason` remain as denormalized list summaries and are written in the same `SaveChanges`, so no existing reader changes shape. **(C) Four kinds, four write points.** `GiftNote` at `InitiateAsync`, `ThankYou` at `AcceptAsync`, `DeclineReason` at `RejectAsync` — the latter two inside the transactions those methods already open — and `FollowUp` at the new `PostMessageAsync`. **(D) Follow-up policy.** Only the gift's two participants may read or write; writing stays open while the gift is `PendingAcceptance` and after `Accepted`, and closes with the gift (`Rejected` / `Expired` / `Cancelled`), always saying both the cause and the next action; caps are 20 messages per gift and 10 per member per hour, body limit 500 characters (the screen stops at 280). **(E) Endpoints.** `GET`/`POST /api/v1/member/transfers/{transferId}/messages` (opening a thread marks that member's lines read) with `TransferNotFound` 404 / `Forbidden` 403 / `ConversationClosed` 409 / `MessageLimitReached` 429 / `RateLimited` 429 / `Validation` 400, plus `GET /api/v1/brand/gifts/{transferId}/messages`. **(F) Privacy split.** A brand sees only `GiftNote`, `ThankYou` and `DeclineReason` — never `FollowUp` — and the filter lives in the service/repository, not in the UI, so no client can widen it; the brand screen says so out loud via `GiftMessageHelper.BrandPrivacyNote`. **(G) Counts.** `MessageCount`, `LastMessageAt` and `UnreadCount` are added to the inbox, outbox and brand-gift DTOs through correlated subqueries (no N+1). **(H) Notification.** New `NotificationType.GiftMessage` + `GiftMessage.html` + `NotifyGiftMessageAsync` in every sink; the body is HTML-encoded because members type it freely, and a failing send never loses the stored message (row 9d of the notification matrix). **(I) UI + wording.** One shared `Components/Shared/GiftConversation.razor` is wired into `Member/Gifts.razor`, `Member/MyVouchers.razor` and `BrandManager/Gifts.razor`; the gift-send button everywhere now reads **Gửi Quà**. **(J) One small extension beyond the approved rows**, needed to make row (C)'s `GiftNote` reachable at all: an optional sender note now travels end-to-end (`TransferRequest.Note` → `ITransferService.TransferAsync(note)` → `InitiateAsync`), with a length guard on both the bulk and the single-gift path so an over-long note is a cause-and-action message rather than a 500. **Two gaps registered, not fixed:** the migration's backfill ran against a dev DB whose `voucher_transfers` was empty, so it inserted 0 rows — the SQL is syntax-verified only and has no automated test (it is raw PostgreSQL using `gen_random_uuid()`, which SQLite lacks); and the §7 sender-side S2 hole from CR-2026-09-10-30 is still open | **Dev-complete 2026-09-10** — Web and API build 0 errors in Release; 204 unit + 181 integration pass (20 new integration facts and 6 new unit facts, all tagged `CR-2026-09-10-31`); migration applied to the dev DB. Pending owner manual verification; API restart required |

## 10. Changelog

| Date | Change |
|---|---|
| 2026-09-10 | **CR-2026-09-10-31 dev-complete** — every gift now keeps its own message box (`gift_messages`, migration `20260910053357_AddGiftMessages`): the note that travelled with the gift, the thank-you or the decline reason it produced, and any follow-up the two customers add later. Writing stays open while a gift is **Waiting** and after it is **Accepted**, and closes with the gift, saying why; caps are 20 messages per gift, 10 per member per hour, 500 characters. A brand sees only the note and the reply — never a private follow-up — and that filter is enforced in the service, not in the screen. The other participant is emailed (`GiftMessage`, row 9d of the notification matrix). New endpoints `GET`/`POST /api/v1/member/transfers/{id}/messages` and `GET /api/v1/brand/gifts/{id}/messages`; inbox, outbox and brand rows now carry `MessageCount`, `LastMessageAt` and `UnreadCount`. One shared `GiftConversation.razor` serves the member inbox, the sender's **Gifts you sent** panel and the read-only BrandManager gift log, and the gift-send button everywhere now reads **Gửi Quà** (owner's wording). §4 gained row 12; §7 names the new button. **Registered, not fixed:** the migration's backfill inserted 0 rows because the dev `voucher_transfers` table was empty, so that SQL is syntax-verified only; and the §7 sender-side S2 hole from CR-2026-09-10-30 remains open. |
| 2026-09-10 | **CR-2026-09-10-30 dev-complete** — gifting is now confirm-first: sending reserves the voucher for 7 days and leaves it in the sender's wallet marked **On its way**; ownership, the distribution record, the webhook and the `GiftingAuto` mapping all moved to acceptance. The sender is told honestly which of three delivery cases applies (emailed / held — no email / held — not on NonCash yet), gets a **Gifts you sent** panel with Waiting / Accepted / Declined / Expired / Cancelled plus a Cancel action, and the recipient confirms on the new **Gifts from friends** page with a thank-you note that is emailed back. `voucher_transfers` is now the single gift log, surfaced to staff on a read-only **BrandManager > Gifts** page; customer-facing wording moved to `GiftMessageHelper` ("send a gift", never "transfer"); notifications with real delivery off now go to `logs/notifications.log` instead of the console. §1.1, §4 rows 7–8, §6, §7 and decision O2 updated to match. **Registered, not fixed:** the sender-side S2 check of §7 row 3 runs only on the bulk send path — `POST /member/vouchers/{id}/initiate-transfer` skips it (§7 open hole). |
| 2026-09-09 | **CR-2026-09-09-28 dev-complete** — My Vouchers wallet redesigned to consumer grade: auto-load without the GUID field, tap-a-card detail dialog (cover, T&C, value, expiry, barcode), `Available` badge vocabulary, guided transfer dialog with live phone counting; `member-user-guide` §4/§5.1 rewritten. Pending owner manual verification. **CR-2026-09-09-29 registered** — the dev kill-switch log still advises `Environment:Name='production'` while policy is `pilot`. |
| 2026-09-09 | **CR-2026-09-09-27 verified** — owner manual test passed: email and phone both sign in on `/member-login`; the former username is refused there with the guidance message; staff username sign-in on `/login` is unaffected (separate staff credential, by design). |
| 2026-09-09 | **CR-2026-09-09-27 implemented** — the customer username is gone: `member_accounts.username` and its unique index dropped (migration `20260909085516`, applied to the dev DB), and customers now sign in with the **phone number or email** on their `customers` row. Added member forgot-password: `POST /auth/member/forgot-password` emails a 30-minute sign-in link with a deliberately neutral reply, plus a `/member/forgot-password` page and links from the login and welcome screens. §1 now carries the login-identifier and lost-password rules. |
| 2026-09-09 | **CR-2026-09-09-26 implemented** — member login accepts the customer's phone number (username still works); accounts provisioned without a password now get an error that states the cause and the next action instead of "Invalid username or password." §1 gained the login-identifier rule. |
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
