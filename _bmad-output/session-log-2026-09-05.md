# Session Log — 2026-09-05

## Context
- Continuation of task-d8b. Prior session (09-04) committed everything as `ea3f4d2` and left "Direction A" (hierarchy-aware POS redeem) PENDING approval.
- This session: the owner made a product decision to move credit deduction to **plan APPROVAL** time (was: sale for Gift, redemption for Complimentary), because approval is when the brand takes ownership of the vouchers — the fair, unambiguous billing moment. This SUPERSEDES the old per-voucher charging and closes the over-issuance hole (approve N vouchers with 0 credits).
- Owner decisions (D1–D4):
  - **D1 (unified):** 1 credit = 1 voucher for ALL types (Gift, Complimentary, and future coupon/giftcard/credit). Charge = plan `TargetQuantity` at approval.
  - **D2 (two-point reusable check):** (1) opening the approve form → informational balance check ("not enough credit, please top up to continue"), does NOT block opening; (2) pressing Approve → hard re-check, if short → same message + do nothing + exit (no state change). ONE shared service method backs both points.
  - **D3/D4:** existing data is test-only → no auto-refund, no retro-charge/grandfathering.

## Work done (charge-at-approval)

### 1. Ledger schema — `CreditConsumption` supports two row shapes
- `src/NonCash.Core/Entities/CreditConsumption.cs`: added `PlanId` (Guid?), `Quantity` (int = 1); made `BatchId` + `VoucherDetailId` nullable. Per-voucher row = VoucherDetailId set, Quantity 1; plan-level approval row = PlanId set, Quantity N, BatchId null (aggregate).
- `src/NonCash.Infrastructure/Data/Configurations/CreditBatchConfiguration.cs` (`CreditConsumptionConfiguration`): dropped IsRequired on BatchId/VoucherDetailId; `Quantity` HasDefaultValue(1); filtered UNIQUE indexes `IX_credit_consumptions_voucher_detail_id` (WHERE voucher_detail_id IS NOT NULL) and `IX_credit_consumptions_plan_id` (WHERE plan_id IS NOT NULL). Double-quoted identifiers work on both PostgreSQL (prod) and SQLite (integration tests).

### 2. Reusable funding check — `CreditService`
- `ICreditService` (+ impl): `EvaluatePlanFundingAsync(brandId, requiredQuantity) → CreditFundingResult(Sufficient, Balance, Required)` (the ONE shared check) and `TryConsumeForPlanAsync(brandId, planId, quantity, reference)` — idempotent per planId via `AnyAsync(c => c.PlanId == planId)`; hard-refuses when balance < quantity; drains FIFO by `ExpiresAt ?? MaxValue, CreatedAt`; records ONE aggregate plan-level row; `DbUpdateException` on the unique plan index → treated as already-charged (true); general exception → false (fail-closed). Per-voucher `TryConsumeAsync` kept intact (legacy FIFO unit tests) but no longer called by any business flow.

### 3. Charge + gate at approval — `ApprovalService` (point 2)
- Injected `ICreditService`. In `ApproveAsync`, AFTER the single-level Pending check and BEFORE setting `ApprovalStatus = Approved`: `TryConsumeForPlanAsync(plan.BrandId, plan.Id, plan.TargetQuantity, ...)`. If false → `ApprovalResult(false, "InsufficientCredits", "Not enough credit, please top up to continue.")` — no state change (charge-first + idempotent so a retried approval after a transient failure self-heals without double-charging).
- Added `GetPlanFundingAsync(planId, brandId)` → loads plan, tenant-checks, delegates to `EvaluatePlanFundingAsync` (point 1 backing). `IApprovalService` updated.

### 4. Funding endpoint — `ApprovalsController` (point 1)
- `GET api/v1/plans/{planId}/funding` → `CheckFunding` returns `{ Sufficient, Balance, Required }` (404 if plan not found / not own brand). `ToActionResult` maps `"InsufficientCredits"` → HTTP 402 with the message.

### 5. Removed the old charge points + weak gates
- `PurchaseService`: dropped `ICreditService`; removed the CreateOrder `HasCreditAsync` gate; `ConfirmPaymentAsync` no longer charges (renamed `chargedVouchers`→`boughtVouchers`, kept only for brand-customer auto-link).
- `PosService`: dropped `ICreditService`; removed the complimentary-at-redeem charge block + the now-unused `voucherType` local. (NOTE: the Direction-A scope bug at ~L319-325 is still present/untouched — separate pending task.)
- `VoucherGenerationService`: dropped `ICreditService` + the generation `HasCreditAsync` gate (ctor now 3 params).
- `PromotionService`: dropped `ICreditService` + the distribution `HasCreditAsync` gate.
- DI: all four are `AddScoped<Interface, Impl>()` auto-resolve → NO Program.cs change. `RegistrationService`/`BrandService` keep `ICreditService` (welcome grant, unrelated).

### 6. Web approve form — funding banner (point 1 UI)
- `src/NonCash.Web/Components/Pages/BrandManager/PlanReview.razor`: `LoadFunding()` GETs the funding endpoint into `_funding` (added to `LoadAll`'s `Task.WhenAll`); when `Pending` and `!Sufficient`, a MudAlert warning shows "Not enough credit, please top up to continue. Approval requires N credit(s); current balance is M." The Approve button stays ENABLED (server is the gate — "just inform" at open). `ApprovePlan` error branch parses the JSON `message` (`TryReadErrorMessageAsync`) so the 402 shows the real text.

### 7. Usage-history surfaces Quantity
- `CreditDtos.CreditConsumptionDto`: nullable BatchId/VoucherDetailId + PlanId + Quantity; `CreditsController.GetConsumptions` maps them.
- `Admin/Credits.razor` + `BrandManager/Credits.razor`: "Voucher" column → "Source" (`SourceLabel`: voucher id / "Plan <id>" / "—") + right-aligned "Qty" column; model classes made nullable.

### 8. Tests
- `tests/NonCash.IntegrationTests/Controllers/CreditsControllerTests.cs`: fixed ctor wiring (dropped `_creditService` from CreatePurchaseService/CreatePosService/inline VoucherGenerationService; added `CreateApprovalService`; `SeedPlan` gained an `approvalStatus` param). Rewrote the 3 "charges at sale/redeem" tests → "does NOT charge" (0 consumptions, balance unchanged). Converted `GenerateBatch_BlockedAtZeroBalance`/`CreateOrder_BlockedAtZeroBalance` → `_NotBlockedByCreditBalance` (succeed at zero balance). ADDED 8: Approve charges TargetQuantity + plan-level row; Approve insufficient → rejected + stays Pending; Approve replay → Conflict, no double-charge; GetPlanFunding reuse; TryConsumeForPlan idempotent; TryConsumeForPlan insufficient → false + nothing charged; TryConsumeForPlan FIFO drains soonest-expiring first; EvaluatePlanFunding sufficiency.
- `tests/NonCash.UnitTests/Services/VoucherGenerationServiceTests.cs`: dropped the `ICreditService` field/ctor-arg/`HasCreditAsync` stub.
- `tests/NonCash.IntegrationTests/CustomerActions/BrandCustomerEnforcementTests.cs`: dropped `creditService` from Promotion + Purchase ctors and removed the now-dead CreditService/CreditConfig construction; updated a stale seed comment.

## Verification
- Full-solution `dotnet build`: **0 CS compile errors**; the only failures were MSB3027/MSB3021 file-lock errors because VS was RUNNING `NonCash.API` (PID 20340) + `NonCash.Web` (PID 1344), locking their `bin` DLLs (environmental, not code).
- Workaround (established pattern): built/ran the suites to alternate output dirs to dodge the lock — `dotnet build tests/NonCash.IntegrationTests -o build_tmp_int` (0 errors), then `dotnet test build_tmp_int/NonCash.IntegrationTests.dll`.
- **UnitTests 88/88**, **IntegrationTests 99/99** (was 91 → +8 new). Filtered unique indexes, FIFO plan drain, idempotency, and the approval gate all pass against real SQLite (EnsureCreated).
- Migration `20260905042430_AddCreditConsumptionPlanCharge` generated with **Infrastructure as BOTH project and startup-project** (design-time `ApplicationDbContextFactory` lives there; avoids the VS lock on API/bin and needs no live DB for `migrations add`). Verified Up(): DropIndex old voucher_detail_id → AlterColumn voucher_detail_id/batch_id nullable → AddColumn plan_id (uuid null) + quantity (int NOT NULL default 1) → CreateIndex plan_id + voucher_detail_id (both UNIQUE, filtered IS NOT NULL). Down() reverses cleanly.
- APPLIED to remote dev DB (`45.119.87.247/noncash`): `dotnet ef database update --project src/NonCash.Infrastructure --startup-project src/NonCash.Infrastructure --connection "<DevConnection>"` (elevated — EF spawns child processes). Output: Build succeeded → Applying migration '20260905042430_AddCreditConsumptionPlanCharge' → Done.

## State at session end
- ALL code + test changes and the new migration are UNCOMMITTED in the working tree (git writes need OUTSIDE-sandbox / required_permissions=all). Migration IS applied to the remote dev DB (schema ahead of git history is fine — matches the 09-04 pattern).
- Dev DB `credit_consumptions` now has plan_id/quantity; existing per-voucher rows unaffected (quantity defaults 1, plan_id NULL → excluded from the new filtered unique index).
- The app running in VS is the OLD code (pre-change); unaffected by the additive schema. When the NEW code runs, approval charges/gates correctly.
- Direction A (hierarchy-aware POS redeem) remains PENDING approval — untouched this session.

## Resume cues
- To commit: `git add -A; git commit -m "..."` OUTSIDE the sandbox (required_permissions=all). NOT pushed unless asked.
- VS file-lock workaround for builds/tests: `dotnet build <proj> -o build_tmp_*` then `dotnet test build_tmp_*/<Test>.dll`.
- EF from Infrastructure (avoids API lock, no live DB for add): `dotnet ef migrations add <Name> --project src/NonCash.Infrastructure --startup-project src/NonCash.Infrastructure`; apply with `... database update ... --connection "<DevConnection from appsettings.json>"`.
- Behavior now: generate/distribute/sell/redeem never touch credits; ONLY `ApprovalService.ApproveAsync` charges (plan.TargetQuantity), and both the approve-form open and the approve press reuse `EvaluatePlanFundingAsync`.

---

# Session 2 (same day) — Distribution batch traceability + strict recipient rules

## Context
- Owner (brand manager view): "when I distribute vouchers per promotion plan I want the result of THAT run and the customer's voucher status history (distributed, redeemed, ...)"; requirement: every transaction recorded and traceable — batch promotions must persist batch data, single-voucher flows (sale) intentionally have NO batch row, and each customer's vouchers must be fully queryable for Q&A.
- Follow-up owner decision: "remove the auto-create-on-the-fly rule — it's stupid. Always check the record exists, email or phone; only send to valid customers; a customer appearing n times is sent once."

## A. Batch distribution traceability (10-item approved proposal)

### 1. Schema
- New entity `VoucherDistributionBatch` (Core): PlanId, BrandId, CreatedById, NotifyChannel, RecipientCount, DistributedCount, SkippedCount, `SkippedRecords` (jsonb via the Scope-style ValueConverter/ValueComparer pattern).
- `VoucherDistribution.BatchId` (Guid?, FK, indexed) — set ONLY for promotion runs; Sale/Transfer rows keep NULL.
- `VoucherDistributionBatchConfiguration` + `VoucherDistributionConfiguration` updated; migration `20260905085731_AddVoucherDistributionBatches` applied to the remote dev DB (45.119.87.247/noncash).

### 2. PromotionService
- Every `DistributeAsync` success persists ONE batch row and links all its distributions to it in the SAME atomic SaveChanges (batch Id pre-assigned — BaseEntity ids are otherwise set at save time).
- Plan `TargetDistributed` reconciled to the real assigned-detail count (self-heals historical inflation; dev DB data was already fixed via approved SQL in the prior session).

### 3. Query service (Core)
- `IDistributionBatchService` / `DistributionBatchService`: brand-scoped batch list + detail, plan voucher ledger, customer voucher history.
- Lifecycle status DERIVED, not stored: InStock (unassigned) / Distributed / Redeeming (InUse = POS-locked) / Redeemed (Complete) / Expired (Pending past plan expiry).
- `VoucherCodeSecret` marked `[property: JsonIgnore]` on all outbound records — never serialized; API projects anonymous DTOs adding only the short-lived dynamic code (`IVoucherCodeService.GenerateCode`).
- Generic `Repository<T>` has NO navigation includes → recipients/customers resolved via explicit second queries.

### 4. API
- `DistributionBatchesController` (`[Authorize(Roles = "BrandManager,Admin")]`): `GET api/v1/plans/{planId}/distribution-batches`, `GET api/v1/distribution-batches/{batchId}`; brand context required (BrandManager = own brand; Admin must pass `?brandId=`), cross-brand → empty/404.
- `VoucherGenerationController.ListVouchers` payload extended: Status, RecipientPhone/Name, DistributionMethod, DistributedAt, BatchId.
- `CustomersController`: `GET api/v1/customers/{id}/vouchers` — brand-scoped lifecycle history.

### 5. Web UI
- `DistributionBatchHistory.razor` (run-history panel on the Distribute page, refreshes via `OnDistributed`) + `BatchDetailDialog.razor` (summary + delivered recipients + skipped with reasons).
- `PlanVouchers.razor`: Status / Recipient / Method / Distributed At columns.
- `Customers.razor`: voucher-history icon → `CustomerVouchersDialog.razor` (plan, face value, status chip, method, dates, dynamic code).
- Shared `VoucherStatusDisplay.cs` chip colors (Redeemed=Success, Redeeming=Warning, Distributed=Info, Expired=Error). All messages via modal `MessageDialog`.

## B. Strict recipient rules (replaces auto-create)

- Recipients must ALREADY exist as customers — the auto-create branch (Customer + MemberAccount + brand-link from a bare phone) is DELETED.
- Unknown phone → skipped `CustomerNotFound`; unknown email → `NoCustomerForEmail` (pre-existing). Skips are returned AND persisted on the batch's `SkippedRecords`.
- Duplicates: exact-token dedupe (case-insensitive) + normalized-customer dedupe (catches email+phone of the same customer, or different phone spellings); later occurrences reported as `Duplicate` skips; one voucher per customer.
- All-unknown/invalid list → `NoEligibleCustomers` error, NOTHING persisted (no batch row, no distributions).
- Kept intentionally: auto-LINK (`BrandCustomerSource.PromotionAuto`) for EXISTING recipients (attribution needed for brand-scoped history), member-account provisioning for existing customers, and integration-payload email upsert.
- `CustomerRepository.GetByEmailAsync`/`EmailExistsAsync`: `EF.Functions.ILike` → `LOWER()` equality — same case-insensitive semantics, provider-agnostic (ILike is Npgsql-only and cannot be translated by the SQLite test provider). **No ILike remains anywhere in the codebase.**
- `DistributeVouchers.razor` wording now states the rules ("must match an existing customer… duplicates receive only one voucher").

## Tests
- `DistributionBatchTests` (new, 10 tests, SQLite): batch persistence + links, brand-scoped list/detail, ledger lifecycle + sale-without-batch, customer history, controller auth matrix, unknown-phone skip (nothing created), duplicate send-once + reported, all-unknown → NoEligibleCustomers persists nothing.
- `CreditsControllerTests`: seeds 3 distribute-target customers (previously relied on auto-create); `BrandCustomerEnforcementTests` unchanged (Dave/Carol pre-exist).
- `CustomersControllerTests`: +3 customer-history tests (asserts VoucherCodeSecret is absent from payload).

## Verification
- Integration **117/117**, Unit **88/88**, API + Web builds 0 errors (build_tmp_* output dirs for the VS lock).

## State at session end / resume cues
- EVERYTHING (charge-at-approval + this session) committed at end of this session — see git log. Both migrations already applied to the remote dev DB.
- Behavior change to remember: distribution NEVER creates customers anymore — recipients must be imported/registered first (Customers page import, or Integration API member flows).
- `session-log-2026-09-04.md` was also modified this session (minor update) and is included in the commit.
- Still PENDING from earlier sessions: Direction A (hierarchy-aware POS redeem scope bug ~PosService L319-325).
- Optional (offered, not requested): embed `DistributionBatchHistory` panel on PlanVouchers page too.
