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

- **Business Management** — create and manage Brands.
- **User Management** — create and manage staff accounts.
- **Registration Review** — approve or reject business self-registration requests.

---

## 2. Brand Management

A Brand is the root tenant entity in NonCash. All plans, outlets, vouchers, and brand-scoped users belong to exactly one Brand.

### 2.1 Create a Brand

1. Go to **Business Management > Brands**.
2. Click **Create Brand**.
3. Enter the required fields:
   - **Name** — required, max 200 characters.
   - **Tax Code** — required, unique across the platform, max 50 characters.
   - **Contact Email** — optional, max 255 characters.
   - **Status** — choose `Active` or `Suspended`.
4. Click **Save**.

The system initializes a unique `BrandID` (GUID). The new Brand appears in the active list.

### 2.2 Update a Brand

1. From the Brand list, click **Edit** on the row you want to update.
2. Modify **Name** or **Contact Email** as needed.
3. Click **Save**.

> **Note:** You cannot change the **Tax Code** if the Brand already has linked Outlets or Plans. This rule is enforced by the business layer.

### 2.3 Search and Filter Brands

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

Businesses can register themselves through the public self-registration flow. Admins review these requests before activating the account.

### 4.1 View Pending Registrations

1. Go to **Registration Review**.
2. The list shows all `BrandRegistrationRequest` records with status `Submitted`, ordered oldest first.
3. Each row displays:
   - Company Name
   - Tax Code
   - Contact Email
   - Submitted Date

### 4.2 Review Registration Details

1. Click a row to open the detail drawer.
2. Review submitted information and any auto-verification flags (for example, Tax Code format validity).

### 4.3 Approve a Registration

1. Click **Approve**.
2. Optionally add review notes.
3. Confirm.

The system atomically:

- Sets the request status to `Approved`.
- Activates the linked Brand (`Status = Active`).
- Activates the linked UserAccount (`Status = Active`).
- Records `ReviewedAt`, `ReviewedByUserId`, and `ReviewNotes`.
- Sends a notification to the business representative with login instructions.

### 4.4 Reject a Registration

1. Click **Reject**.
2. Enter **Review Notes** (minimum 10 characters).
3. Confirm.

The system atomically:

- Sets the request status to `Rejected`.
- Sets the linked Brand status to `Rejected`.
- Sets the linked UserAccount status to `Rejected` (or deletes it based on configuration).
- Records `ReviewedAt` and `ReviewedByUserId`.
- Sends a rejection notification with the reason.

### 4.5 Approval Rules

- Only users with the `Admin` role can approve or reject registrations.
- A request can only be approved or rejected once. Repeated attempts return a 409 Conflict.
- If any part of the transaction fails, the request remains `Submitted`.

---

## 5. Security and Multi-Tenancy

- **JWT tokens** carry `sub` (UserID), `brandId`, and `role` claims.
- **Brand scoping** is enforced automatically. Non-Admin users can only access data belonging to their Brand.
- **Role-based access control** is enforced on every controller action. Do not share Admin credentials.

---

## 6. Common Tasks Quick Reference

| Task | Path | Role |
| --- | --- | --- |
| Create a Brand | Business Management > Brands | Admin |
| Create a staff user | User Management > Users | Admin |
| Lock a user | User Management > Users > Edit | Admin |
| Approve registration | Registration Review | Admin |
| Reject registration | Registration Review | Admin |
| View all Brands | Business Management > Brands | Admin |

---

## 7. Troubleshooting

| Issue | Cause | Resolution |
| --- | --- | --- |
| Cannot create Brand with Tax Code | Tax Code already exists | Use a unique Tax Code. |
| Cannot update Brand Tax Code | Brand has linked Outlets or Plans | Tax Code is immutable after linked records exist. |
| User cannot log in | Account is `Locked` or Brand is not `Active` | Unlock the account or activate the Brand. |
| Registration approval fails | Request already Approved/Rejected | Check the request status and open a new request if needed. |

