# Data Models - NonCash Project

This document outlines the core data models and relationships based on the business requirements.

## Entity Relationship Overview

The system uses a relational model (PostgreSQL) managed via Entity Framework Core.

### 1. Voucher Production Planning

#### `VoucherPlanHeader` (Plan Header)
Represents the overall strategy for a voucher campaign.
- `ID`: GUID (Primary Key)
- `PlanDate`: DateTime (Creation date)
- `CreatorID`: GUID (FK to UserAccount)
- `ApproverID`: GUID? (Nullable, FK to UserAccount)
- `BrandID`: GUID (FK to Brand)
- `VoucherType`: Enum (Complimentary, Gift)
- `ImageURL`: String (Url for detailed display)
- `IconURL`: String (Url for grid/logo display)
- `ValueType`: Enum (Value, Percentage)
- `FaceValue`: Decimal (Usage value)
- `NetValue`: Decimal (Reference cost)
- `ExpiryDate`: DateTime (Hard expiry)
- `PublishDate`: DateTime (Availability date)
- `SalesRange`: List<OutletID> (Accepted outlet locations)
- `TimeRange`: DateRange (Valid from-to)
- `TargetQuantity`: Integer (Expected volume)
- `Budget`: Decimal (Total cost)
- `TargetDistributed`: Integer (Goal for distribution)
- `TargetUsed`: Integer (Goal for POS usage)
- `ApprovalStatus`: Enum (Pending, Approved, Rejected)

#### `VoucherPlanDetail` (Voucher Detail)
Represents individual vouchers generated after a plan is approved.
- `ID`: GUID (Primary Key)
- `ParentID`: GUID (FK to `VoucherPlanHeader`)
- `SerialNo`: String (Unique external ID)
- `VoucherCode`: String (Dynamic/JWT-like code for usage)
- `MemberID`: GUID (Nullable - Assigned owner)
- `UsageStatus`: Enum (Pending, In-Use, Complete)
- `UsedDate`: DateTime? (Nullable)

### 2. Tracking and Distribution

#### `VoucherUsage`
Stores the history of voucher redemptions at POS.
- `ID`: GUID
- `VoucherID`: GUID (FK to `VoucherPlanDetail`)
- `POSID`: String (Redemption location)
- `TransactionID`: String (Link to POS transaction)
- `UsageDate`: DateTime
- `AmountUsed`: Decimal — **the value the bill absorbed**, not the value deducted from the voucher. A voucher is one-shot: committing consumes the whole voucher whatever this figure says, and it drives neither settlement (uses plan `FaceValue`) nor credit (charged at plan approval). Its purpose is measurement — `FaceValue − AmountUsed` is **breakage**, the issued value that never reached a real bill, and the campaign-efficiency signal brands read. Computable today by join: `voucher_usages.voucher_id` → `voucher_plan_details.id` → `.parent_id` → `voucher_plan_headers.face_value`; no report exposes it yet.

#### `VoucherDistribution`
Tracks how vouchers were sent to customers.
- `ID`: GUID
- `VoucherID`: GUID
- `MemberID`: GUID
- `Method`: Enum (Sale, Promotion, Transfer)
- `DistributionDate`: DateTime

### 3. Identity and Operations Management

#### `Brand` (Organization / Tenant)
Represents businesses that create and distribute vouchers (e.g., The Coffee House).
- `BrandID`: GUID (Primary Key)
- `Name`: String
- `TaxCode`: String
- `ContactEmail`: String
- `Status`: Enum (Active, Suspended)

#### `Outlet` (Point of Sale / Store)
Represents physical or digital stores belonging to a Brand.
- `OutletID`: GUID (Primary Key)
- `BrandID`: GUID (FK to Brand)
- `Name`: String
- `Address`: String
- `Status`: Enum (Active, Closed)

#### `UserAccount` (Back-office Users)
Platform access for creating, reviewing, and approving plans.
- `UserID`: GUID (Primary Key)
- `BrandID`: GUID (FK to Brand, nullable for system super-admins)
- `Username`: String
- `PasswordHash`: String
- `FullName`: String
- `Role`: Enum (Admin, Planner, Approver)
- `Status`: Enum (Active, Locked)

#### `Customer` (End-User / App Member)
The consumers who hold and use the distributed vouchers. **Global identity: one row per person** — `PhoneNumber` is unique platform-wide (1 person = 1 phone = 1 wallet); there is deliberately no `BrandID` on this table.
- `CustomerID`: GUID (Primary Key)
- `PhoneNumber`: String (Primary identifier for transfer/login)
- `FullName`: String
- `Email`: String
- `Status`: Enum (Active, Blacklisted)

#### `BrandCustomer` (Brand ↔ Customer Relationship)
Tracks which brands hold a relationship with a customer — brand ownership is separate from the unique global identity above. One row per brand–customer pair, created at the first brand touchpoint (import, manual add, promotion, purchase, transfer, gifting, redemption).
- `ID`: GUID (Primary Key)
- `BrandID`: GUID (FK to Brand)
- `CustomerID`: GUID (FK to Customer)
- `Source`: Enum (Import, Manual, PromotionAuto, SelfPurchase, GiftingAuto, Transfer, Redemption) — how the link started; never upgraded on re-link
- `IsBlocked`: Boolean (per-brand block) · `BlockedAt`: DateTime?
- `MarketingOptOut`: Boolean
- `CreatedBy`: GUID? (UserAccount; null for system auto-links)

See [Customer Action Matrix](./customer-action-matrix.md) §1 for the full identity model and the customer creation flows (with brand / without brand).

### 4. Billing (Prepaid Credits)

#### `CreditLedgerEntry`
Append-only ledger for the prepaid credit billing model (Epic 9). A Brand's balance is the SUM of `Amount` across its entries. Each voucher consumes exactly 1 credit once in its lifetime — Gift at sale (payment confirmed), Complimentary at POS redemption.
- `ID`: GUID (Primary Key)
- `BrandID`: GUID (FK to Brand)
- `EntryType`: Enum (Grant, Purchase, Consumption, Adjustment)
- `Amount`: Integer (signed: + for Grant/Purchase, − for Consumption; Adjustment may be either)
- `Reference`: String? (e.g. bank transfer reference or note)
- `VoucherDetailID`: GUID? (set on Consumption; **unique index when not null** — guarantees 1 voucher = max 1 credit)
- `CreatedBy`: GUID? (admin/reviewer who recorded the entry)
- `CreatedAt`: DateTime

Indexes: unique filtered index on `VoucherDetailID` (idempotent consumption), composite index on `(BrandID, CreatedAt)` for ledger queries.

---

## Registered Changes (CR Registry)

| CR ID | Item | Status |
|---|---|---|
| CR-2026-09-07-16 | **Bearer/anonymous distribution channel for data-minimizing brands** — brands that rent the platform but refuse to share customer data. Three candidate options: **A** — export minted codes, brand self-sends (platform never sees recipient data); **B** — platform sends to (email, name) then purges per a contract-defined retention window; **C** — claim-link without account (bearer voucher; optional self-registration converts it into a wallet voucher, shifting the consent basis to the customer). Requires: `voucher_plan_details` allowing no member binding (bearer state), a login-free claim page (C), an email purge job (B/C), a contract addendum (retention commitment) + Import-screen notice. Documented trade-offs: no unified wallet, no P2P transfer/gifting, no identity-based blacklist, no cross-brand dedupe for bearer vouchers. | Registered — pending decision (owner evaluating trade-offs) |
| CR-2026-09-07-17 | **Multi-instrument generalization** (voucher / gift card / prepaid / credit / coupon — NonCash as a digital-instrument engine). Classification via three independent axes: funding model (Promotional / Prepaid-cash / Postpaid-credit), redemption semantics (One-shot / Balance-decrement / Discount-rule), value backing (None / Cash / Credit). **Phase 0** (schema only, no behavior change): add `InstrumentType` (default `Voucher`) + `Subtype` + policy columns to the plan header. Later phases: gift card (builds on the SelfPurchase cash flow), prepaid (needs balance + an `instrument_transactions` ledger + topup API), credit instrument (after Epic 7 settlement; rename to avoid clashing with the internal billing "credits"), coupon (POS discount semantics). **3-layer code rule**: the redemption code stays an opaque credential forever (no embedded metadata); classification travels server-side via the verify/commit API response; a structured **reference number** (type prefix + serial + check digit, per-brand template) is designed at Phase 1 for balance instruments. Regulatory note: prepaid/credit may require licensing (e-money/lending territory) — contract terms must differ per instrument type. **Amended 2026-09-11 (CR-2026-09-11-35)** — two constraints Phase 0 must honour: **(a) `amount_used` carries two meanings by instrument.** For a one-shot voucher it is the value the **bill absorbed** and the remainder is **forfeited**, so breakage = `FaceValue − AmountUsed`. For a balance instrument it is the value **deducted from the balance** and the remainder is **still the customer's**, so breakage = 0 and the meaningful companion figure is the **remaining balance**. Same column, two meanings; breakage is only computable if the instrument type is discoverable from a usage row (today by join `voucher_usages` → `voucher_plan_details` → `voucher_plan_headers`). Phase 0 must keep that path intact and decide whether the companion remaining-balance figure is stored on the usage row or only returned in the commit response — storing nothing would silently destroy the voucher-side breakage metric brands rely on. **(b) Branching is Factory + Strategy, keyed on `InstrumentType` only — not `Subtype`.** One entry point stays (`PosService.CommitAsync`); a factory resolves a strategy from the plan. Evidence for dropping `Subtype` from the key: redemption is already completely type-agnostic — `PosService`, `VoucherLockRepository.CommitAsync` and `SettlementService` contain **zero** `VoucherType` references, and the only two behavioural branches in the whole of `src/` are `PurchaseService.cs:42,75`, which are the **sales** gate (only Gift is sellable). Gift and Complimentary therefore redeem identically, so a subtype-keyed factory would return the same strategy for both — a dead axis. Subtype stays a sales/display concern; add it to the key only when some subtype actually changes redemption behaviour. A strategy owns exactly four things: how much may be consumed, what the terminal state is, what companion figure the response carries, and what `amount_used` means for that instrument. Everything else stays **shared and must not be re-implemented per type**: the `ValidateCoreAsync` gates (code signature, expiry, transfer lock, outlet/brand scope, date window), lock acquisition, rate limiting, rollback outlet ownership, `transactionId` idempotency, settlement, webhook publish and the brand-customer link. Because the strategy sits **above** the SQL, `IVoucherLockRepository` must expose two atomic operations — a one-shot flip and a balance decrement — each with its guard **inside** the `ExecuteUpdateAsync` WHERE clause (`RemainingValue >= amountUsed`), since a strategy that reads the balance then compares would be a TOCTOU race between two tills scanning the same card. | Registered — pending decision; amended 2026-09-11 |
| CR-2026-09-24-36 | **Brand redemption report (bill amount vs face value).** New `GET api/v1/brand/redemptions` (+ `/outlets`) behind `BrandManager,Admin`: BrandManager is auto-scoped to their brand (unlinked account → 401 with an actionable message), Admin sees all brands or passes `brandId`. **No schema change** — the report reads existing data: outlet from `voucher_usages.pos_id`, face value by joining `voucher_plan_details → voucher_plan_headers`, and the bill number parsed out of the POS-shaped `transaction_id` (`{StoreCode}-{Bill}-{ticks}`, so the bill is the middle dash-segment(s)). Filters: `outletId`, whole-day `from`/`to` (Web sends `yyyy-MM-dd`; "to" includes the whole picked day). Totals (`TotalFaceValue`, `TotalAmountUsed`, `TotalBreakage`) are computed over the whole filtered set, not just the page; face-value aggregates cover **Value-type rows only** (a Percentage plan's FaceValue is a percent number, not money). **Breakage = FaceValue − AmountUsed** for fixed-value plans, `null` for percentage plans — this **closes the registered gap of CR-2026-09-11-35** (breakage was captured but no report exposed it). Web page `/brandmanager/redemptions` (+ BrandManager nav link) with outlet/date filters, summary cards, and per-row POS/operator attribution. 13 new `BrandRedemptionsControllerTests` (SQLite in-memory, real repository) cover joins, brand scoping, bill parsing, breakage semantics, whole-day filters, and outlet options. | **Implemented** 2026-09-24 — pending user verification (restart API + Web) |
