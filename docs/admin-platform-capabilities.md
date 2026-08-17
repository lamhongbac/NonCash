# NonCash Admin Platform — Capability Overview

> **As of:** August 17, 2026
> **Audience:** Stakeholders, product owners, and new team members who need a quick, complete picture of what the NonCash **admin platform** can do today.
> **Scope:** Everything below is implemented, built (0 errors), and covered by 135 passing tests.

---

## 1. Executive Summary

The NonCash admin platform is the **control plane** for the whole voucher ecosystem. It covers:

1. **Tenant onboarding & governance** — businesses, brands, self-registration approvals, staff accounts & RBAC.
2. **Credit & financial control** — balances, top-ups, pricing policies, adjustments, welcome grants, cross-tenant settlement.
3. **Partner integration** — loyalty-app onboarding with API keys and signed webhooks.
4. **Oversight of the full voucher lifecycle** — plan → distribute → redeem → settle (operated day-to-day by brand roles, visible cross-brand to admins).

All admin actions that matter to a business contact are backed by **real email notifications** (SMTP + HTML templates + an audit log in `email_logs`).

---

## 2. Capability Map (At a Glance)

| # | Area | Key capabilities | Where |
|---|------|------------------|-------|
| 1 | Business & tenant onboarding | Create/edit businesses; create/edit brands; approve/reject self-registrations | Admin UI |
| 2 | Users & RBAC | Create staff accounts; assign roles; lock/unlock; brand scoping | Admin UI |
| 3 | Credits & billing | View batches/balances; top-up; ledger; grace-overdraft policy | Admin UI + API |
| 4 | Credit policies | Pricing / expiry / low-balance / approval-threshold rules (global, group, brand) | Admin UI |
| 5 | Credit adjustments | Review & approve/reject adjustment requests | Admin UI |
| 6 | Welcome policies | Configure welcome-credit grants for new brands | Admin UI |
| 7 | Integration partners | Onboard loyalty apps; issue/revoke API keys; webhook URLs | Admin UI + API |
| 8 | Settlement | Ledger, mark-settled, netting report | API |
| 9 | Voucher lifecycle oversight | Plans, distribution, redemption (POS), gifting, reports | Brand roles; admin read |

---

## 3. Tenant Onboarding & Governance

### 3.1 Businesses
- Create and edit **Businesses** (company name, unique tax code, address, contact email/phone).
- On creation, the contact email receives a **"Your business is now active on NonCash"** email.

### 3.2 Brands
- Create and edit **Brands** (tenants) under a business; set `Active` / `Suspended`.
- New brands automatically receive a **welcome credit grant** and a **welcome email**.
- Brand creation sends **only** the welcome email (no duplicate activation email).

### 3.3 Registration Approvals
- Review the queue of public **self-registration requests**.
- **Approve** → activates brand + user account and emails login instructions.
- **Reject** → requires a reason (min 10 chars) and emails the applicant.
- Idempotent: a request can be decided only once (repeat → 409 Conflict).

### 3.4 Users & RBAC
- Create staff accounts with roles: `Admin`, `BrandManager`, `Planner`, `Approver` (+ financial roles).
- Lock/unlock accounts; passwords stored as BCrypt hashes.
- Non-admin users are strictly **brand-scoped**; admins have cross-brand access.

---

## 4. Credit & Financial Control

- **Billing rule:** each voucher consumes exactly **1 credit, once, at its value moment** (Gift → at sale; Complimentary → at redemption). Transfers and Gift redemptions consume nothing.
- **Balances & ledger** — view per-brand balances and a full audit ledger; consumption entries are unique per voucher (no double-charging).
- **Top-ups** — record `Purchase` / `Grant` / `Adjustment` entries (only `Adjustment` may be negative).
- **Grace overdraft** — POS redemption never fails on balance; upstream actions (generation, distribution, new orders) are blocked while balance ≤ 0.
- **Credit Policies** — price per credit, expiry months, low-balance warning %, adjustment approval threshold; scoped global → group → brand (specific overrides broad).
- **Credit Adjustments** — approval workflow for correction requests.
- **Welcome Policies** — configure the welcome grant (default 500 credits) per business or by default.

---

## 5. Partner Integration (Loyalty Apps)

- Onboard external **integration partners** with contact email + HTTPS callback URL.
- Issue **API keys** (64-hex, shown once, stored as BCrypt hash; 8-char prefix kept for identification).
- Bind partners to specific brands; revoke by deactivating or re-issuing keys.
- **Webhooks** — voucher lifecycle events (`voucher.distributed`, `voucher.redeemed`, `voucher.transferred`) delivered with **HMAC-SHA256** signatures and exponential-backoff retry (1m → 5m → 25m → 2h → 10h, max 5).
- Partner-facing API: distribute, member wallet, event history, campaign performance (per-outlet breakdown).

---

## 6. Cross-Tenant Settlement

- Automatic **settlement entries** when a voucher sponsored by one brand is redeemed at another brand's outlet.
- **Ledger** with filters (sponsor/redeem brand, status, date range).
- **Netting report** — aggregates to a single net amount per brand pair.
- **Mark-settled** once offline payment is confirmed (idempotent; 404 on unknown/already-settled).

---

## 7. Voucher Lifecycle Oversight

Operated day-to-day by brand roles; admins retain cross-brand visibility:

- **Planning** — plan headers (value, dates, cover image, brand color, display fields), serial generation, approval workflow, versioning.
- **Distribution** — batch promotion, B2C/B2B self-purchase (ZaloPay/VNPAY), gifting batch transfer, tracking dashboard.
- **Redemption (POS)** — verify → prepare/lock → commit/log → rollback.
- **Gifting** — member-to-member transfer (initiate / confirm / cancel / history).

---

## 8. Platform-Wide Services

- **Auth** — JWT login with role claims; password reset via emailed secure token.
- **Email notifications** — SMTP delivery, 16 HTML templates, retry policy, feature flag (`Notifications:EmailEnabled`), and a full audit trail in `email_logs`.
- **Image storage** — MSA (production) or local (development) via a config toggle.

---

## 9. Welcome-Grant Policy Templates

Admins can create reusable welcome-credit templates ahead of time and mark one as the platform default. On registration approval, the admin selects the template to apply; unassigned businesses automatically receive the default template.

- **Admin → Welcome Policy Templates** — create, edit, deactivate, and set the default template.
- **Admin → Welcome Policies** — view per-business assignments created from templates.
- **Registration approval dialog** — choose a template (or use default) before confirming approval; the welcome credit amount and expiry in the `ActiveBusiness` email reflect the selected template.

---

## 10. Admin-Action Email Matrix

| Admin action | Email sent to | Template |
|--------------|---------------|----------|
| Create business | Business contact | `BrandCreated` ("Your business is now active") |
| Create / activate brand | Brand contact | `WelcomeCreditGranted` (welcome email only) |
| Approve registration | Business contact | `ActiveBusiness` (welcome + welcome-credit policy from selected/default template) |
| Reject registration | Applicant | `RegistrationRejected` (reason + next steps) |
| New self-registration | Admin + applicant | `AdminNewRegistration` + `ApplicantRegistrationSubmitted` |

---

## 11. How to Access

- **Admin console (Blazor):** `http://localhost:5200` — sign in as `admin` / `Admin@123` (change in production).
- **API / Swagger:** `https://localhost:7107/swagger`.
- Detailed walkthroughs: [`docs/user-guides/admin-user-guide.md`](user-guides/admin-user-guide.md).
