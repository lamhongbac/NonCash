# NonCash Admin User Guide

This guide is for **System Administrators** who manage the NonCash platform, tenants (Brands), staff accounts, and business registration approvals.

---

## 1. Getting Started

### 1.1 Log In

1. Open the NonCash web application.
2. Enter your username and password.
   - Default seed account: `admin` / `Admin@123` (change this immediately in production).
3. The system issues a JWT token scoped to the Admin role. Admin users have cross-brand access for platform-level operations.

### 1.2 Home Dashboard

After login, the Admin dashboard provides navigation to:

- **Businesses** — create and manage Businesses (the parent company of Brands).
- **Brands** — create and manage Brands (tenants) under a Business.
- **Users** — create and manage staff accounts and roles.
- **Registration Review** — approve or reject business self-registration requests.
- **Credits** — view credit batches/balances, record top-ups, review the ledger.
- **Credit Policies** — define pricing/expiry/low-balance policies (global, group, or brand scope).
- **Credit Adjustments** — approve or reject credit adjustment requests.
- **Welcome Policy Templates** — create reusable welcome-credit grant terms and mark one as the platform default.
- **Welcome Policies** — view the welcome-credit policy assigned to each Business after approval.
- **Integration Partners** — onboard external Loyalty Apps and manage their API keys.

Platform features that remain API-only (Swagger UI at `/swagger`):

- **Settlements** — review and settle cross-tenant redemption balances.
- **POS / Redemption, Distribution, Plans** — operated by Brand roles; Admin has cross-brand read access.

---

## 2. Business & Brand Management

A **Business** is the company record; a **Brand** is the tenant entity under it. All plans, outlets, vouchers, and brand-scoped users belong to exactly one Brand.

### 2.1 Create a Business

1. Go to **Businesses**.
2. Click **Create Business**.
3. Enter **Business Name** (required), **Tax Code** (required, unique), **Address** (required), and optional **Contact Email** / **Phone Number**.
4. Click **Save**.

If a **Contact Email** is provided, the contact receives a **Brand Created** email confirming the business was created and activated. This template is separate from the one used when a self-registered business is approved.

### 2.2 Create a Brand

1. Go to **Business Management > Brands**.
2. Click **Create Brand**.
3. Enter the required fields:
   - **Name** — required, max 200 characters.
   - **Tax Code** — required, unique across the platform, max 50 characters.
   - **Contact Email** — optional, max 255 characters.
   - **Status** — choose `Active` or `Suspended`.
4. Click **Save**.

The system initializes a unique `BrandID` (GUID). The new Brand appears in the active list.

### 2.3 Update a Brand

1. From the Brand list, click **Edit** on the row you want to update.
2. Modify **Name** or **Contact Email** as needed.
3. Click **Save**.

> **Note:** You cannot change the **Tax Code** if the Brand already has linked Outlets or Plans. This rule is enforced by the business layer.

### 2.4 Search and Filter Brands

- Use the search box to filter by **Name**.
- Use the status filter to show only `Active` or `Suspended` Brands.
- Results are paginated. Use the page controls at the bottom of the grid.

---

## 3. Staff Account Management

Staff accounts are `UserAccount` records tied to a Brand (or platform-wide for Admins). Each account has a role that controls permissions.

### 3.1 Create a Staff Account

1. Go to **User Management > Users**.
2. Click **Create User**.
3. Fill in the form:
   - **Username** — required, unique across the platform.
   - **Password** — required, minimum 8 characters.
   - **Full Name** — optional display name.
   - **Role** — select one of:
     - `Admin` — full platform access, cross-brand.
     - `BrandManager` — manages Outlets, Customers, and views plans within their Brand.
     - `Planner` — creates and edits Voucher Plan Headers within their Brand.
     - `Approver` — approves or rejects plans within their Brand.
   - **Brand** — required for `BrandManager`, `Planner`, and `Approver`. Leave empty for `Admin`.
   - **Status** — `Active` or `Locked`.
4. Click **Save**.

The password is stored as a salted hash using BCrypt.

### 3.2 Lock or Unlock an Account

1. From the User list, click **Edit** on the account.
2. Change **Status** to `Locked` to block login, or `Active` to restore access.
3. Click **Save**.

Existing sessions are invalidated on the next token validation.

### 3.3 Search Users

- Filter by **Username**, **Full Name**, or **Role**.
- Use the Brand filter to narrow results to a specific tenant.

---

## 4. Business Registration Approval

Businesses can register themselves through the public self-registration flow. A registration is only a **request** until it is approved — no Business, Brand, or UserAccount exists before then. Before activation, the platform and the business must agree to a contract. Admins manage this workflow from the **Registration Review** page.

### 4.1 Optional First Brand Declaration

During self-registration, the applicant may optionally declare a **First Brand**:

- **Brand Name** — the name of the first brand to create under the business.
- **Brand Manager Username / Password** — credentials for the first user, who will have the `BrandManager` role.

If the applicant skips this section, only the **Business** record is created on approval. The admin must later create the first Brand and its user manually from the **Brands** or **Users** page.

### 4.2 Registration Statuses

A self-registered business moves through these stages:

| Stage | Meaning | Admin Action |
|---|---|---|
| `Submitted` | Business registered; contract not yet sent. | Send contract |
| `Contract Sent` | Contract emailed to the business; awaiting signature. | Print contract, upload signed copy |
| `Contract Signed` | Signed hardcopy received. | Approve or reject |
| `Approved` | Business activated. | — |
| `Rejected` | Registration declined. | — |

### 4.3 View Pending Registrations

1. Go to **Registration Review**.
2. Use the filter buttons:
   - **Pending Contract** — requests waiting for a contract to be sent.
   - **Pending Review** — requests with signed contracts ready for approval/rejection.
   - **All Requests** — every request.
3. Each row displays:
   - Company Name / Tax Code / Contact Email / Phone / Address
   - Representative Name
   - First Brand and Manager (if declared), or a warning if none
   - Submitted Date
   - Selected Welcome Policy (if any)
   - Contract Status

### 4.4 Send the Contract

1. From **Pending Contract**, click **Send Contract** on the request.
2. Select a **Welcome Policy Template** that reflects the agreed commercial terms.
   - The platform default template is pre-selected.
   - You can choose another active template if the business negotiated custom terms.
3. Click **Send Contract**.

The system:

- Maps the selected policy template to the request.
- Generates a platform agreement (HTML) from the policy terms and business details.
- Emails the contract to the business contact using the **Contract Sent** template.
- Sets the request contract status to `Sent`.

> **Tip:** Click **Print** on a request with a sent contract to open the agreement in a new browser tab for printing or saving as PDF.

### 4.5 Upload Signed Contract

1. From **Pending Contract**, click **Upload Signed Contract** on a request whose contract status is `Sent`.
2. Enter the file URL or path where the signed hardcopy is stored.
3. Click **Upload**.

The system sets the contract status to `Signed`. The request now appears under **Pending Review** and can be approved or rejected.

### 4.6 Approve a Registration

1. From **Pending Review**, click **Approve**.
2. Optionally add review notes.
3. Confirm.

The system atomically:

- Creates the **Business** record and sets it to active.
- If a first brand was declared, creates the **Brand** and the **UserAccount** with role `BrandManager`, both active.
- Assigns the selected **Welcome Policy Template** to the new Business.
- Grants the welcome credits defined by the template to the first brand (if one was declared).
- Records `ReviewedAt`, `ReviewedByUserId`, and `ReviewNotes`.
- Sends a **Business Activated** email to the business contact with login instructions and the welcome-credit details (delivered via SMTP; recorded in the email audit log).

> **Note:** The **Business Activated** template is used only for approved self-registrations. Businesses created directly by an Admin receive the **Brand Created** email instead.

### 4.7 Reject a Registration

1. From **Pending Review**, click **Reject**.
2. Enter **Review Notes** (minimum 10 characters).
3. Confirm.

The system atomically:

- Sets the request status to `Rejected`.
- Records `ReviewedAt` and `ReviewedByUserId`.
- Sends a **Registration Rejected** email to the business contact with the reason and next steps (delivered via SMTP; recorded in the email audit log).

### 4.8 Approval Rules

- Only users with the `Admin` role can send contracts, upload signed contracts, approve, or reject registrations.
- A request can only be approved or rejected once. Repeated attempts return a 409 Conflict.
- Approval is blocked until the contract status is `Signed`.
- If any part of the transaction fails, the request remains in its current status.

---

## 5. Integration Partner Management (Loyalty Apps)

External Loyalty Apps (partners) integrate with NonCash through API-key authenticated endpoints under `/integration/*`. Admins manage these partners from the **Integration Partners** page in the Admin console, or via the Admin API.

### 5.1 Create an Integration Partner

1. Call `POST /api/v1/integration-partners` with your Admin token.
2. Provide:
   - **Name** — partner display name.
   - **Contact Email** — technical contact.
   - **Callback URL** — HTTPS endpoint that will receive webhook notifications.
   - **Brand IDs** — the Brands this partner is allowed to access.
3. The response returns the new partner `id`.

### 5.2 Generate an API Key

1. Call `POST /api/v1/integration-partners/{id}/generate-key`.
2. The response contains the full **API key** (64-character hex string) and its 8-character **prefix**.

> **IMPORTANT:** The full API key is shown **only once**. It is stored as a BCrypt hash — only the prefix is kept for identification. Deliver the key to the partner through a secure channel. If lost, generate a new key (the old one is invalidated).

### 5.3 Manage Brand Associations

1. Call `PUT /api/v1/integration-partners/{id}/brands` with the list of Brand IDs.
2. The partner can only distribute vouchers and query data for its associated Brands.

### 5.4 Update or Deactivate a Partner

- `PUT /api/v1/integration-partners/{id}` — update name, contact email, callback URL, or set `isActive: false` to block all API access without deleting the record.
- `DELETE /api/v1/integration-partners/{id}` — permanently remove the partner.

### 5.5 Webhook Delivery

When voucher events occur (for example, `voucher.distributed`), the platform:

- Records the event in an outbox and delivers it asynchronously to the partner's Callback URL.
- Signs each payload with **HMAC-SHA256** so the partner can verify authenticity.
- Retries failed deliveries with exponential backoff (1m → 5m → 25m → 2h → 10h, maximum 5 attempts).

No Admin action is required for delivery; check the `webhook_deliveries` table if a partner reports missing notifications.

---

## 6. Cross-Tenant Settlement

When a voucher sponsored by one Brand is redeemed at an outlet belonging to a different Brand, the system automatically creates a **settlement entry** (who owes whom, and how much). Admins reconcile these balances.

### 6.1 View the Settlement Ledger

1. Call `GET /api/v1/settlements` with your Admin token.
2. Optional filters:
   - `sponsorBrandId` / `redeemBrandId` — filter by either side of the transaction.
   - `status` — `Pending` or `Settled`.
   - `from` / `to` — date range on the entry creation date.
   - `page` / `pageSize` — pagination.

### 6.2 Netting Report

1. Call `GET /api/v1/settlements/netting?from={date}&to={date}`.
2. The report aggregates all entries per sponsor/redeem Brand pair and computes the **net amount** owed in each direction, so Brands can settle with a single payment instead of per-transaction transfers.

### 6.3 Mark an Entry as Settled

1. After the payment between Brands is confirmed offline, call `PUT /api/v1/settlements/{id}/settle`.
2. The entry status changes to `Settled` and is excluded from future pending balances.

> **Note:** An entry can only be settled once. Settling an unknown or already-settled entry returns 404.

---

## 7. Credit & Billing Management

Brands prepay **credits** to use the platform (usage-based fee). The billing rule is simple: **each voucher consumes exactly 1 credit, once in its lifetime, at its value moment** — a Gift voucher is charged when it is sold (payment confirmed), a Complimentary voucher is charged when it is redeemed at POS. Transfers and Gift redemptions consume nothing (the Gift voucher was already charged at sale).

### 7.1 Welcome Credits (Free Period)

Every newly activated Brand — whether created directly by an Admin or activated through registration approval — automatically receives a **welcome grant** (default: 500 credits, configurable via `CreditConfig:WelcomeCredits` in `appsettings.json`). The grant appears in the ledger as an entry of type `Grant` with reference "Welcome credits".

### 7.2 Check a Brand's Balance

1. Call `GET /api/v1/credits/balance?brandId={brandId}` with your Admin token.
2. The response returns the current balance (sum of all ledger entries). Admins can query any Brand; Brand users can only see their own balance.

### 7.3 Top Up Credits (Manual Bank-Transfer Flow)

Payments are manual in v1: the Brand pays by bank transfer, and once the payment is confirmed, an Admin records the top-up.

1. Call `POST /api/v1/credits/topup` with your Admin token:

   ```json
   {
     "brandId": "<brand-guid>",
     "amount": 1000,
     "type": "Purchase",
     "reference": "Bank transfer #TX-2026-0728"
   }
   ```

2. Allowed types:
   - `Purchase` — paid top-up (positive amount only).
   - `Grant` — free/promotional credits (positive amount only).
   - `Adjustment` — manual correction; **the only type that accepts a negative amount** (clawback).
3. The response returns the created ledger entry. `Consumption` entries cannot be created manually — they are recorded automatically by the system.

### 7.4 Review the Credit Ledger

1. Call `GET /api/v1/credits/ledger` with your Admin token.
2. Optional filters: `brandId`, `type` (`Grant`/`Purchase`/`Consumption`/`Adjustment`), `from`/`to` date range, `page`/`pageSize`.
3. Consumption entries carry the `voucherDetailId` that was charged — each voucher can appear at most once (enforced by a unique database index), so a voucher is never double-charged.

### 7.5 Grace Overdraft Policy

Redemption at POS **never fails because of credit balance** — customer-facing operations must not break. If a Complimentary voucher is redeemed while the Brand's balance is 0, the balance simply goes negative. Instead, the platform blocks *upstream* actions when a Brand's balance is ≤ 0:

- Voucher generation is blocked.
- Batch/partner distribution is blocked.
- New self-purchase orders are blocked (customers see "temporarily unavailable").

Once the Brand tops up, these operations resume automatically. Negative balances should be recovered through the next top-up.

### 7.6 Admin Console for Credits

The **Credits** page shows credit batches and balances per Brand, and lets Admins record top-ups/adjustments and review the ledger without using the API.

### 7.7 Credit Policies

The **Credit Policies** page defines pricing and lifecycle rules (price per credit, expiry months, low-balance warning %, adjustment approval threshold) at global, brand-group, or brand scope. More specific scopes override broader ones.

### 7.8 Credit Adjustments

The **Credit Adjustments** page lists adjustment requests awaiting review. Approvers approve or reject them; approved adjustments post an `Adjustment` ledger entry.

### 7.9 Welcome Policies

The **Welcome Policies** page shows the welcome-credit policy assigned to each Business after registration approval. The actual terms come from a **Welcome Policy Template**; this page is read-only and reflects which template was applied and when.

### 7.10 Welcome Policy Templates

**Welcome Policy Templates** are reusable onboarding terms created ahead of time. Admins select one when approving a business registration. One template must be marked as the platform **Default**; it is used automatically when no specific template is chosen.

#### 7.10.1 Create a Template

1. Go to **Welcome Policy Templates**.
2. Click **New Template**.
3. Enter:
   - **Name** — a descriptive name (for example, "Standard 500 Credits", "Enterprise 12-Month").
   - **Welcome Credits** — number of free credits granted on activation.
   - **Expiry (Months)** — optional credit expiry in months; leave empty for no expiry.
   - **Active** — inactive templates do not appear in the approval dropdown.
4. Click **Save**.

#### 7.10.2 Set the Default Template

1. In the template list, click the **Check Circle** icon on the template you want as default.
2. The system removes the default flag from the previous template and applies it to the selected one.
3. New registrations approved without an explicit selection will use this template.

> **Important:** There must always be exactly one active default template. If you deactivate the current default, set a new default before approving registrations.

#### 7.10.3 Edit or Deactivate a Template

- Click **Edit** to change name, credits, expiry, or active status.
- Click **Block** to deactivate a template. Deactivated templates are not available for new approvals.
- Changes to a template do **not** affect Businesses already approved under that template; they only affect future approvals.

---

## 8. Security and Multi-Tenancy

- **JWT tokens** carry `sub` (UserID), `brandId`, and `role` claims.
- **Brand scoping** is enforced automatically. Non-Admin users can only access data belonging to their Brand.
- **Role-based access control** is enforced on every controller action. Do not share Admin credentials.
- **Integration API keys** authenticate partners on `/integration/*` routes via the `X-API-Key` header. Keys are validated against BCrypt hashes; revoke access by deactivating the partner or generating a new key.

---

## 9. Common Tasks Quick Reference

| Task | Path | Role |
| --- | --- | --- |
| Create a Brand | Business Management > Brands | Admin |
| Create a Business | Businesses | Admin |
| Create a staff user | User Management > Users | Admin |
| Lock a user | User Management > Users > Edit | Admin |
| Approve registration | Registration Review | Admin |
| Reject registration | Registration Review | Admin |
| View all Brands | Business Management > Brands | Admin |
| Create integration partner | API: `POST /api/v1/integration-partners` | Admin |
| Generate partner API key | API: `POST /api/v1/integration-partners/{id}/generate-key` | Admin |
| View settlement ledger | API: `GET /api/v1/settlements` | Admin |
| View netting report | API: `GET /api/v1/settlements/netting` | Admin |
| Mark settlement settled | API: `PUT /api/v1/settlements/{id}/settle` | Admin |
| Check a Brand's credit balance | API: `GET /api/v1/credits/balance?brandId={id}` | Admin |
| Top up Brand credits | API: `POST /api/v1/credits/topup` | Admin |
| Review credit ledger | API: `GET /api/v1/credits/ledger` | Admin |
| Manage credits (UI) | Credits | Admin |
| Manage credit policies | Credit Policies | Admin |
| Approve credit adjustment | Credit Adjustments | Admin |
| Manage welcome policy templates | Welcome Policy Templates | Admin |
| Manage welcome policies | Welcome Policies | Admin |
| Manage integration partners (UI) | Integration Partners | Admin |

---

## 10. Troubleshooting

| Issue | Cause | Resolution |
| --- | --- | --- |
| Cannot create Brand with Tax Code | Tax Code already exists | Use a unique Tax Code. |
| Cannot update Brand Tax Code | Brand has linked Outlets or Plans | Tax Code is immutable after linked records exist. |
| User cannot log in | Account is `Locked` or Brand is not `Active` | Unlock the account or activate the Brand. |
| Registration approval fails | Request already Approved/Rejected | Check the request status and open a new request if needed. |
| Partner gets 401 on /integration/* | Missing/invalid `X-API-Key` or partner deactivated | Verify the key, partner `isActive` flag, and brand associations. |
| Partner not receiving webhooks | Callback URL unreachable or retries exhausted (max 5) | Verify the Callback URL is publicly reachable over HTTPS; re-trigger the event if needed. |
| Settle returns 404 | Entry not found or already settled | Check the entry ID and status in the settlement ledger. |
| Top-up returns 400 | Type is `Consumption`, amount is 0, or negative amount on Grant/Purchase | Use `Purchase`/`Grant`/`Adjustment`; only `Adjustment` may be negative. |
| Brand cannot generate/distribute vouchers | Credit balance ≤ 0 (`InsufficientCredits`) | Confirm the bank transfer and record a top-up for the Brand. |
| Business/brand contact did not receive email | Contact Email empty, or record created before SMTP was configured | Set a Contact Email and recreate, or check the `email_logs` table for the send attempt/error. |
| Approval fails with "default template not found" | No active default Welcome Policy Template exists | Go to **Welcome Policy Templates**, activate a template, and click **Set as default**. |
| Approval fails with "template not found or inactive" | The selected template was deactivated before approval | Select an active template or use the default. |
| Approve button is disabled / approval fails | Contract status is not `Signed` | Send the contract, wait for the business to sign, then upload the signed copy. |
| Send contract fails | Selected Welcome Policy Template is inactive or missing | Choose an active template or set a default template. |
| Upload signed contract fails | Contract was never sent | Send the contract first before uploading the signed copy. |

