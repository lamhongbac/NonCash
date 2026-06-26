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

### 7.2 Export Data

Use the export buttons on list pages to download current filtered results as CSV or Excel.

---

## 8. Common Tasks Quick Reference

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
| View Transfer Activity | Transfers | BrandManager |
| View Distribution Reports | Reports > Distribution Tracking | BrandManager / Planner |

---

## 9. Troubleshooting

| Issue | Cause | Resolution |
| --- | --- | --- |
| Cannot create Outlet | Missing Brand association | Ensure you are logged in with a Brand-scoped account. |
| Plan save fails validation | Face Value <= 0, Net Value > Face Value, or date errors | Check the validation messages and correct the form. |
| Cannot edit a Plan | Plan is already Approved or Rejected | Only Pending plans can be edited. |
| Batch promotion shows Insufficient Stock | Not enough unassigned vouchers | Generate more vouchers or reduce the recipient list. |
| Customer skipped in promotion | Customer is Blacklisted | Remove from blacklist or exclude from the list. |
| Transfer appears Expired | Recipient did not act within 7 days | Sender can initiate a new transfer. |

