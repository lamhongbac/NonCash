# NonCash Functional Test Plan — Epic 9

> **Created:** 2026-07-28  
> **Scope:** Prepaid Credit Billing (usage-based fee) — credit ledger, balance/ledger/top-up API, consumption at value moments, balance guards, welcome grant, grace overdraft  
> **Target Environment:** Development (https://localhost:7107)

**Billing rule under test:** each voucher consumes exactly **1 credit, once in its lifetime, at its value moment** — Gift vouchers when sold (payment confirmed), Complimentary vouchers when redeemed (POS commit). Transfers and Gift redemptions consume nothing.

---

## 1. Pre-Test Setup

### 1.1 Apply Migrations

```powershell
cd src\NonCash.API
dotnet ef database update --project ..\NonCash.Infrastructure
```

Verify the new table exists: `credit_ledger_entries` with a unique filtered index on `voucher_detail_id`.

### 1.2 Configuration

Check `appsettings.Development.json`:

```json
"CreditConfig": { "WelcomeCredits": 500, "LowBalanceWarningPercent": 20 }
```

### 1.3 Start the API

```powershell
cd src\NonCash.API
dotnet run --launch-profile https
```

### 1.4 Obtain Auth Tokens

**Admin token:** `POST /api/v1/auth/login` with `{ "username": "admin", "password": "Admin@123" }` → save as `{{ADMIN_TOKEN}}`.

**Brand Manager token:** log in with a BrandManager account of the test Brand → save as `{{BRAND_TOKEN}}`. Note the Brand's ID as `{{BRAND_ID}}` and a second Brand's ID as `{{OTHER_BRAND_ID}}`.

---

## 2. Welcome Grant (Free Period)

### TC-9.1 Admin-Created Brand Receives Welcome Credits

| Field | Value |
|-------|-------|
| Step 1 | `POST /api/v1/brands` (Admin) — create a new Brand |
| Step 2 | `GET /api/v1/credits/balance?brandId={newBrandId}` (Admin) |
| Expected | HTTP 200, `balance` = 500 |
| Verify | `GET /api/v1/credits/ledger?brandId={newBrandId}` shows one `Grant` entry (+500, reference "Welcome credits") |

### TC-9.2 Approved Registration Receives Welcome Credits

| Field | Value |
|-------|-------|
| Step 1 | Submit a public registration (`POST /api/v1/public/registrations`) |
| Step 2 | Approve it (`Admin`, registration review endpoint) |
| Step 3 | `GET /api/v1/credits/balance?brandId={approvedBrandId}` (Admin) |
| Expected | Balance = 500; ledger shows one `Grant` entry with `createdBy` = reviewer's user ID |

### TC-9.3 Rejected Registration Receives Nothing

| Field | Value |
|-------|-------|
| Step 1 | Submit a registration, then reject it |
| Expected | No credit ledger entries exist for the linked Brand |

---

## 3. Balance & Ledger API

### TC-9.4 Brand User Sees Own Balance

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/credits/balance` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Expected | HTTP 200, `brandId` = own Brand, `balance` = current sum |

### TC-9.5 Brand User Cannot Query Another Brand

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/credits/balance?brandId={{OTHER_BRAND_ID}}` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Expected | HTTP 403 |

### TC-9.6 Admin Queries Any Brand

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/credits/balance?brandId={{BRAND_ID}}` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200 with that Brand's balance |

### TC-9.7 Ledger Filters and Pagination

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/credits/ledger?type=Consumption&page=1&pageSize=10` |
| Auth | Bearer `{{ADMIN_TOKEN}}` (add `&brandId=`) or `{{BRAND_TOKEN}}` (own Brand) |
| Expected | HTTP 200; only `Consumption` entries; `totalCount`, `page`, `pageSize` correct; entries ordered newest first |

---

## 4. Top-Up (Admin Manual Flow)

### TC-9.8 Admin Top-Up Purchase

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/credits/topup` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Body | `{ "brandId": "{{BRAND_ID}}", "amount": 1000, "type": "Purchase", "reference": "Bank transfer #TX-001" }` |
| Expected | HTTP 200; ledger gains a `Purchase` +1000 entry; balance increases by 1000 |

### TC-9.9 Top-Up Is Admin-Only

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/credits/topup` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Expected | HTTP 403 |

### TC-9.10 Top-Up Validation

| Case | Body | Expected |
|------|------|----------|
| Consumption type | `{ ..., "type": "Consumption", "amount": -1 }` | HTTP 400 |
| Zero amount | `{ ..., "type": "Purchase", "amount": 0 }` | HTTP 400 |
| Negative Purchase | `{ ..., "type": "Purchase", "amount": -5 }` | HTTP 400 |
| Negative Grant | `{ ..., "type": "Grant", "amount": -5 }` | HTTP 400 |
| Negative Adjustment | `{ ..., "type": "Adjustment", "amount": -30 }` | HTTP 200 (clawback allowed) |
| Unknown type | `{ ..., "type": "Bonus" }` | HTTP 400 |

---

## 5. Consumption at Value Moments

### TC-9.11 Gift Voucher Charged at Sale

| Field | Value |
|-------|-------|
| Precondition | Approved Gift plan with generated vouchers; Brand balance recorded as `B0` |
| Step 1 | Member creates an order for 2 vouchers (`POST /api/v1/purchases`) |
| Step 2 | Confirm payment (`POST /api/v1/purchases/{orderId}/confirm-payment`) |
| Expected | Ledger gains **2** `Consumption` entries (−1 each), one per allocated `voucherDetailId`; balance = `B0` − 2 |

### TC-9.12 Replayed Payment Confirmation Does Not Double-Charge

| Field | Value |
|-------|-------|
| Step | Repeat the confirm-payment call from TC-9.11 |
| Expected | No new `Consumption` entries (idempotent — unique index on `voucher_detail_id`); balance unchanged |

### TC-9.13 Complimentary Voucher Charged at Redemption

| Field | Value |
|-------|-------|
| Precondition | Complimentary voucher distributed to a member; issuing Brand balance = `B0` |
| Step 1 | POS lock (`POST /api/v1/pos/lock` with dynamic code) |
| Step 2 | POS commit (`POST /api/v1/pos/commit`) |
| Expected | Commit succeeds; ledger gains **1** `Consumption` entry for the voucher; balance = `B0` − 1 |
| Cross-tenant | If the plan has a sponsor Brand, the **sponsor** Brand is charged (falls back to issuing Brand) |

### TC-9.14 Gift Redemption Is Free

| Field | Value |
|-------|-------|
| Precondition | Gift voucher already sold (charged at sale per TC-9.11) |
| Step | POS lock + commit the Gift voucher |
| Expected | Commit succeeds; **no new** `Consumption` entries; balance unchanged |

### TC-9.15 Transfers Consume Nothing

| Field | Value |
|-------|-------|
| Step | Member transfers a voucher to another member; recipient accepts |
| Expected | No credit ledger changes for any Brand |

---

## 6. Balance Guards & Grace Overdraft

Setup: bring a test Brand to balance 0 (e.g. `Adjustment` top-up with a negative amount equal to the current balance).

### TC-9.16 Voucher Generation Blocked at Zero Balance

| Field | Value |
|-------|-------|
| Step | Generate vouchers for a plan of the zero-balance Brand |
| Expected | Request fails with `InsufficientCredits`; no vouchers created |

### TC-9.17 Batch Distribution Blocked at Zero Balance

| Field | Value |
|-------|-------|
| Step | Run batch promotion distribution for a plan of the zero-balance Brand |
| Expected | Request fails with `InsufficientCredits`; no vouchers assigned |

### TC-9.18 New Orders Blocked at Zero Balance

| Field | Value |
|-------|-------|
| Step | Member attempts to create a purchase order for a Gift plan of the zero-balance Brand |
| Expected | Order rejected with `InsufficientCredits` ("temporarily unavailable" to the customer) |

### TC-9.19 POS Redemption Never Blocked (Grace Overdraft)

| Field | Value |
|-------|-------|
| Precondition | Zero-balance Brand has a Complimentary voucher already in a member's wallet |
| Step | POS lock + commit that voucher |
| Expected | Commit **succeeds**; balance goes to −1 (negative allowed); ledger shows the `Consumption` entry |

### TC-9.20 Operations Resume After Top-Up

| Field | Value |
|-------|-------|
| Step 1 | Admin top-up (+100 `Purchase`) for the zero/negative-balance Brand |
| Step 2 | Retry generation / distribution / order creation |
| Expected | All succeed once balance > 0 |

---

## 7. Automated Test Coverage Reference

| Area | Test file |
|------|-----------|
| Balance math, idempotent consumption, overdraft, top-up validation, ledger filters | `tests/NonCash.UnitTests/Services/CreditServiceTests.cs` (14 tests) |
| API scoping, admin-only top-up, gift-sale charge, complimentary-redemption charge, gift-redemption free, guards at zero balance | `tests/NonCash.IntegrationTests/Controllers/CreditsControllerTests.cs` (12 tests) |

Run with:

```powershell
dotnet test NonCash.sln
```
