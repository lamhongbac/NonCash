# NonCash Brand User Guide

This guide is for **Brand Managers**, **Planners**, and **Approvers** who operate within a single Brand tenant on the NonCash platform.

---

## 1. Getting Started

### 1.1 Log In

1. Open the NonCash web application.
2. Enter your username and password.
3. After authentication, the system issues a JWT token scoped to your Brand and Role.

### 1.2 Home Dashboard

After login, the Brand dashboard provides navigation based on your role:

- **Outlets** — manage physical store locations (Brand Manager).
- **Customers** — manage customer records and blacklist (Brand Manager).
- **Plans** — create and manage voucher campaigns (Planner).
- **Approvals** — review and approve/reject pending plans (Approver).
- **Distribution** — execute batch promotions and view distribution reports (Brand Manager).
- **Transfers** — view member voucher transfer activity (Brand Manager, read-only).

---

## 2. Outlet Configuration

Outlets are the physical locations where customers can redeem vouchers.

### 2.1 Create an Outlet

1. Go to **Outlets**.
2. Click **Create Outlet**.
3. Enter the details:
   - **Name** — required display name of the store.
   - **Address** — optional physical address.
   - **Status** — `Active` or `Closed`.
4. Click **Save**.

The system automatically assigns the Outlet to your Brand and generates an `ApiKeyPrefix` as a placeholder for future POS API key provisioning.

### 2.2 Update or Close an Outlet

1. From the Outlet list, click **Edit** on the row you want to change.
2. Update the name or address, or set **Status** to `Closed`.
3. Click **Save**.

> **Note:** Closing an Outlet does not delete it. Historical plan references remain intact.

### 2.3 Search Outlets

- Filter by **Name** or **Status**.
- Results are scoped to your Brand automatically.

---

## 3. Customer Record Management

Customer records are global in NonCash because a customer may hold vouchers from multiple Brands. Brand Managers can create, update, import, and blacklist customers.

### 3.1 Create a Customer

1. Go to **Customers**.
2. Click **Create Customer**.
3. Enter the details:
   - **Phone Number** — required, unique across the platform.
   - **Full Name** — optional.
   - **Email** — optional.
   - **Status** — `Active` or `Blacklisted`.
4. Click **Save**.

The system normalizes the phone number before storage (non-digit characters are stripped).

### 3.2 Blacklist a Customer

1. From the Customer list, click **Blacklist** on the row you want to block.
2. Confirm the action.

Blacklisted customers are excluded from future batch promotions and self-purchases.

### 3.3 Import Customers in Bulk

1. Go to **Customers**.
2. Click **Import**.
3. Upload a CSV or Excel file with columns for `PhoneNumber`, `FullName`, and `Email`.
4. Review the parsed preview.
5. Click **Confirm Import**.

The system uses upsert logic: existing customers are matched by phone number and updated if the name or email changed.

### 3.4 Search Customers

- Search by **Phone Number**, **Full Name**, or **Email**.
- Blacklisted customers are visually flagged in the UI.

---

## 4. Voucher Plan Management

### 4.1 Create a Plan Header

1. Go to **Plans**.
2. Click **Create Plan**.
3. Fill in the plan header form:
   - **Plan Date** — date of the plan.
   - **Voucher Type** — `Complimentary` or `Gift`.
   - **Value Type** — `Value` (fixed amount) or `Percentage`.
   - **Face Value** — required, must be greater than 0.
   - **Net Value** — required, must be less than or equal to Face Value.
   - **Expiry Date** — must be greater than or equal to Publish Date.
   - **Publish Date** — date the voucher becomes available.
   - **Valid From / Valid To** — optional validity window.
   - **Target Quantity** — required, must be greater than 0.
   - **Budget** — total campaign budget.
   - **Sales Range** — select the Outlets where the voucher can be redeemed (only Outlets in your Brand are shown).
4. Click **Save Draft**.

The system sets `CreatorID` and `BrandID` from your JWT token and sets `ApprovalStatus = Pending`.

### 4.2 Edit a Draft Plan

1. From the Plan list, click **Edit** on a plan with `ApprovalStatus = Pending`.
2. Update the allowed fields.
3. Click **Save**.

> **Note:** Only plans in `Pending` status can be edited. Approved or Rejected plans must be versioned instead.

### 4.3 Submit for Approval

1. Open a draft plan.
2. Click **Submit for Approval**.
3. The plan status changes to `Pending` and appears in the Approver's queue.

### 4.4 Approve or Reject a Plan (Approver Role)

1. Go to **Approvals**.
2. Click a pending plan to review details.
3. Click **Approve** or **Reject**.
   - If rejecting, enter a reason.
4. Confirm.

Approved plans can be used for distribution and sale. Rejected plans remain editable as drafts.

### 4.5 Generate Voucher Details

After a plan is Approved:

1. Open the plan.
2. Click **Generate Vouchers**.
3. Enter the quantity to generate.
4. Confirm.

The system creates `VoucherPlanDetail` records (serials, secrets) tied to the plan header. These vouchers start with `UsageStatus = Pending` and no owner (`MemberID = null`).

### 4.6 Voucher Display and Branding

Each plan supports optional display fields that control how the voucher appears in the member store and wallet:

| Field | Purpose | Recommendation |
| --- | --- | --- |
| **Display Name** | Marketing name shown on the voucher card | ≤ 40 characters |
| **Short Description** | One-line teaser under the name | ≤ 80 characters |
| **Cover Image** | Banner image on the voucher card | 1200×630 px (16:9), JPEG/PNG/WebP, ≤ 2 MB |
| **Brand Color** | Accent color bar on the card | Hex value, e.g. `#FF5733` |
| **Terms & Conditions** | Full usage terms, shown collapsible on detail screen | Plain text |
| **Valid Days of Week** | Days the voucher can be redeemed | e.g. `Mon-Fri` |

If display fields are empty, the store falls back to the plan's face value and brand name.

### 4.7 Upload a Cover Image

Cover images are stored on the platform's media service (MSA). Upload via the API:

1. Call `POST /api/v1/upload/image` (`multipart/form-data`) with your Brand token:
   - `file` — the image file (JPG/PNG/WebP/GIF, max 5 MB).
   - `entity` — `voucher_plan_headers`.
   - `uniqueCode` — `{planId}_cover_image` (the plan's ID plus the field name).
2. The response returns a **relative URL** (no domain), for example `/noncash/images/voucher_plan_headers/{planId}_cover_image.jpg`. This value is stored on the plan's `CoverImageUrl`.

> **Notes:**
> - Re-uploading with the same `entity` + `uniqueCode` automatically replaces the previous image (the old file is deleted first).
> - The displayed URL is composed at runtime as `{CDN endpoint}/{relative URL}`.

---

## 5. Voucher Distribution

### 5.1 Batch Promotion Distribution

1. Go to **Distribution > Batch Promotion**.
2. Select an **Approved** or **Published** plan.
3. Upload a CSV or Excel file containing customer phone numbers, or enter a list manually.
4. Review the parsed list.
5. Click **Distribute**.

The system:

- Matches phone numbers to existing Customers.
- Creates placeholder Customer records for unknown phone numbers.
- Skips blacklisted customers and reports them in a warning list.
- Assigns one voucher per customer (`MemberID = Customer.UserAccount.Id`).
- Creates a `VoucherDistribution` record with `Method = Promotion` for each assignment.
- Fails entirely if voucher stock is insufficient (all-or-nothing transaction).

### 5.2 Self-Purchase Monitoring

Customers can purchase Gift vouchers through the member store. Brand staff can monitor:

- Active catalog items (Approved/Published Gift plans).
- Purchase orders created by customers.
- Payment confirmation and voucher allocation status.

When an order is marked as paid, the system allocates vouchers to the purchaser and records `VoucherDistribution` with `Method = Sale`.

### 5.3 Loyalty App Distribution

Brands connected to an external Loyalty App (integration partner) can have vouchers distributed by the partner:

- The partner calls the platform's integration API with a member segment (phone numbers plus optional external member IDs).
- Distribution is idempotent — repeated calls with the same request key do not duplicate vouchers.
- Each assignment records the partner's `ExternalMemberId` on the `VoucherDistribution`, so campaign performance can be reported back to the Loyalty App.
- Partner onboarding and API keys are managed by the platform Admin (see the Admin User Guide).

---

## 6. Voucher Transfer Oversight

Members can transfer vouchers to each other through the member portal. Brand staff have read-only visibility.

### 6.1 View Transfer Activity

1. Go to **Transfers**.
2. The list shows transfers involving vouchers from your Brand.
3. Status values include:
   - `PendingAcceptance` — waiting for recipient action.
   - `Accepted` — ownership transferred.
   - `Rejected` — recipient declined; voucher returned to sender.
   - `Cancelled` — sender cancelled before recipient action.
   - `Expired` — recipient did not act within 7 days.

### 6.2 Voucher Lock During Transfer

While a transfer is `PendingAcceptance`, the voucher is soft-locked. It cannot be redeemed at POS until the transfer is Accepted, Rejected, Cancelled, or Expired.

---

## 7. Reports and Tracking

### 7.1 Distribution Tracking Dashboard

Go to **Reports > Distribution Tracking** to view:

- Total vouchers generated per plan.
- Number distributed, used, and remaining.
- Distribution method breakdown (Promotion, Sale, Transfer).
- Per-Outlet redemption totals.

### 7.2 Cross-Tenant Redemptions and Settlement

If your vouchers can be redeemed at outlets belonging to another Brand (sponsored campaigns), each cross-tenant redemption automatically creates a **settlement entry** recording which Brand owes which. The platform Admin reconciles these balances periodically using the settlement ledger and netting report — no action is needed from Brand staff, but redemption reports show sponsor and redeeming Brand attribution per usage.

### 7.3 Export Data

Use the export buttons on list pages to download current filtered results as CSV or Excel.

---

## 8. Credits (Platform Usage Fee)

Your Brand prepays **credits** to use the platform. The charging rule is one sentence: **each voucher consumes exactly 1 credit, once in its lifetime** — a Gift voucher when it is sold (payment confirmed), a Complimentary voucher when it is redeemed at POS. Gift redemptions and member transfers are free (the Gift voucher was already charged at sale).

### 8.1 Welcome Credits

When your Brand is activated, it automatically receives a welcome grant of credits (free period) — you can start issuing vouchers immediately.

### 8.2 Check Your Balance and Ledger

- `GET /api/v1/credits/balance` — returns your Brand's current balance.
- `GET /api/v1/credits/ledger` — returns your Brand's credit history (grants, purchases, consumptions, adjustments), with optional `type`, `from`/`to`, and pagination filters.

Both endpoints are automatically scoped to your own Brand.

### 8.3 What Happens at Zero Balance

Customer-facing redemption is **never blocked** — vouchers already in circulation keep working at POS even if your balance reaches 0 (the balance may go slightly negative). However, while your balance is ≤ 0, the following actions are blocked with an `InsufficientCredits` error:

- Generating new vouchers.
- Batch and partner distribution.
- New self-purchase orders from customers (your catalog shows "temporarily unavailable").

### 8.4 How to Top Up

Pay by bank transfer, then contact the platform Admin with the transfer reference. Once payment is confirmed, the Admin records the top-up and the blocked actions resume automatically. Keep an eye on your balance before large campaigns.

---

## 9. Common Tasks Quick Reference

| Task | Path | Role |
| --- | --- | --- |
| Create an Outlet | Outlets | BrandManager |
| Close an Outlet | Outlets > Edit | BrandManager |
| Create a Customer | Customers | BrandManager |
| Blacklist a Customer | Customers > Blacklist | BrandManager |
| Import Customers | Customers > Import | BrandManager |
| Create a Plan | Plans | Planner |
| Submit Plan for Approval | Plans > Open Plan | Planner |
| Approve/Reject Plan | Approvals | Approver |
| Generate Vouchers | Plans > Open Approved Plan | Planner / BrandManager |
| Run Batch Promotion | Distribution > Batch Promotion | BrandManager |
| Upload voucher cover image | API: `POST /api/v1/upload/image` | BrandManager / Planner |
| View Transfer Activity | Transfers | BrandManager |
| View Distribution Reports | Reports > Distribution Tracking | BrandManager / Planner |
| Check credit balance | API: `GET /api/v1/credits/balance` | BrandManager |
| View credit ledger | API: `GET /api/v1/credits/ledger` | BrandManager |

---

## 10. Troubleshooting

| Issue | Cause | Resolution |
| --- | --- | --- |
| Cannot create Outlet | Missing Brand association | Ensure you are logged in with a Brand-scoped account. |
| Plan save fails validation | Face Value <= 0, Net Value > Face Value, or date errors | Check the validation messages and correct the form. |
| Cannot edit a Plan | Plan is already Approved or Rejected | Only Pending plans can be edited. |
| Batch promotion shows Insufficient Stock | Not enough unassigned vouchers | Generate more vouchers or reduce the recipient list. |
| Customer skipped in promotion | Customer is Blacklisted | Remove from blacklist or exclude from the list. |
| Transfer appears Expired | Recipient did not act within 7 days | Sender can initiate a new transfer. |
| Image upload returns 400 | Missing `entity`/`uniqueCode` field, file > 5 MB, or invalid format | Include both form fields and use JPG/PNG/WebP/GIF under 5 MB. |
| Voucher card shows no image | `CoverImageUrl` not set on the plan | Upload a cover image and set the display fields. |
| Generation/distribution fails with `InsufficientCredits` | Credit balance ≤ 0 | Top up via bank transfer and Admin confirmation; redemption of existing vouchers is unaffected. |

