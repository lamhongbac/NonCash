# Notification Matrix

This document describes all notification scenarios in the NonCash platform, including the trigger, recipient, delivery channel, and email template used.

## Email Notifications

The email notification system uses SMTP delivery with HTML templates stored in `src/NonCash.Infrastructure/EmailTemplates/`. Every send attempt (success or failure) is recorded in the `email_logs` table for audit traceability.

### Registration & Onboarding

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 1 | New business registration submitted | `RegistrationService.SubmitAsync` | All Admin users with email | `AdminNewRegistration` | `NewRegistration` |
| 2 | Registration submitted (confirmation) | `RegistrationService.SubmitAsync` | Applicant (contact email) | `ApplicantRegistrationSubmitted` | `RegistrationConfirmation` |
| 3 | Business activated (registration approved) | `RegistrationService.ReviewAsync` | Business `ContactEmail` (fallback: brand) | `ActiveBusiness` | `BusinessActivated` |
| 4 | Registration rejected | `RegistrationService.ReviewAsync` | Applicant (user account email) | `RegistrationRejected` | `RegistrationRejected` |
| 5 | Staff account created | `UserService.CreateAsync` | New staff user | `StaffAccountCreated` | `StaffAccountCreated` |

### Voucher Plan & Approval

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 6 | Plan approved | `ApprovalService.ApproveAsync` | Plan creator | `PlanReviewed` | `PlanReviewed` |
| 7 | Plan rejected | `ApprovalService.RejectAsync` | Plan creator | `PlanReviewed` | `PlanReviewed` |

### Voucher Distribution & Gifting

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 8 | Voucher received (promotion/sale) | `PromotionService` distribution | Member (email if available) | `VoucherReceived` | `VoucherDistribution` |
| 9 | Gift sent — accept link | `VoucherTransferService.InitiateAsync` | Gift recipient, **only when they have an email** | `VoucherTransferInitiated` | `GiftSent` |
| 9a | Gift accepted (with thank-you note) | `VoucherTransferService.AcceptAsync` | Sender (`customers.email`) | `GiftAccepted` | `GiftAccepted` |
| 9b | Gift declined (with reason) | `VoucherTransferService.RejectAsync` | Sender (`customers.email`) | `GiftDeclined` | `GiftDeclined` |
| 9c | Gift expired after 7 days | `TransferExpirySweepService` → `VoucherTransferService.SweepExpiredAsync` | Sender (`customers.email`) | `GiftExpired` | `GiftExpired` |
| 9d | Gift message written | `VoucherTransferService.PostMessageAsync` | The other participant (`customers.email`) | `GiftMessage` | `GiftMessage` |

Email subjects as delivered: `{sender} sent you a gift`, `{recipient} accepted your gift`,
`{recipient} declined your gift`, `Your gift to {recipient} expired — the voucher is back in your wallet`,
`{author} sent you a message about your gift`.

> **Magic Link (CR-2026-09-07-19):** Rows 8–9 include a signed JWT magic link (7-day TTL) in the email body. The link auto-logs the customer into the member portal at `/member/welcome?token=...` without requiring a password. This ensures customers auto-onboarded via promotion/gift (who have `PasswordHash = ""`) can still access their vouchers. The “Set Password” page at `/member/set-password` is offered as an optional convenience after magic-link login.

> **Gifting is confirm-first (CR-2026-09-10-30):** row 9 is sent when the gift is *created*, not when it
> completes — the voucher stays in the sender's wallet, reserved for 7 days, until the recipient accepts.
> Rows 9a–9c are what closes the loop for the sender, so they always learn the outcome. A missing email never
> fails the gift: row 9 needs the **recipient's** email (without it the gift is held and the sender is told
> so), rows 9a–9c need the **sender's** (without them the outcome is still visible under **Gifts you sent**
> and in the BrandManager gift log).
>
> A recipient who is not on NonCash yet (or who has no email) gets no email at that moment. The sender is
> told which case applies, and `NotifyPendingGiftsAsync` re-sends row 9 automatically as soon as that phone
> number registers with an email address.

> **Gift conversations are private (CR-2026-09-10-31):** row 9d goes to whichever of the two participants did
> *not* write the message, and to nobody else — the brand that issued the voucher is never notified and never
> sees a follow-up message. The message body is member-typed free text, so it is HTML-encoded before it enters
> the template, and links in it are stored and shown as plain text, never rendered. Announcing a message is
> best-effort: if the send throws, the exception is swallowed because the message is already stored and losing
> it would be worse than not announcing it.

### Credit Management

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 10 | Welcome credits granted | `CreditService.GrantWelcomeAsync` | Brand `ContactEmail` | `WelcomeCreditGranted` | `WelcomeCreditGranted` |
| 11 | Credit purchase receipt | `CreditService.CreatePurchaseAsync` | Brand `ContactEmail` | `CreditPurchased` | `CreditPurchased` |
| 12 | Low credit balance warning | `CreditService.TryConsumeAsync` (below threshold) | Brand `ContactEmail` | `LowCreditBalance` | `LowCreditBalance` |
| 13 | Credits expiring soon | `CreditExpirySweepService` | Brand `ContactEmail` | `CreditsExpiring` | `CreditsExpiring` |
| 14 | Credits forfeited (expired) | `CreditExpirySweepService` | Brand `ContactEmail` | `CreditsForfeited` | `CreditsForfeited` |
| 15 | Adjustment pending approval | `CreditAdjustmentService` | FinancialControllers | `AdjustmentPending` | `AdjustmentPending` |
| 16 | Adjustment approved/rejected | `CreditAdjustmentService` | Adjustment requester | `AdjustmentReviewed` | `AdjustmentReviewed` |

> Note: on registration approval, the welcome-credit email is suppressed — the credit info is carried inside the `ActiveBusiness` email instead. The amount/expiry come from the selected welcome-policy template (or the platform default). `WelcomeCreditGranted` is still sent when an admin creates a new brand directly (`BrandService.CreateAsync`).

### Security

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 17 | Password reset requested (staff) | `AuthService.ForgotPasswordAsync` | Staff `UserAccount` (by username or email lookup) | `PasswordReset` | `PasswordReset` |
| 18 | Sign-in link requested (customer) | `AuthService.MemberForgotPasswordAsync` | Member's `customers.email` | `MemberSignInLink` | `MemberSignInLink` |

> Row 18 (CR-2026-09-09-27): the link carries a `sign_in_link` token that expires in **30 minutes**
> and lands on `/member/welcome`, the same route as the 7-day voucher magic link. It is **not** sent
> when the customer has no email address or is platform-blacklisted (matrix rule O2) — but the API
> returns the identical confirmation either way, so this endpoint cannot be used to discover who has
> an account. Sends are rate-limited to 5 per minute per IP (`member-auth` policy).

## Configuration

### SMTP Settings

Configured in `appsettings.json` (or user secrets for development):

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "EnableSsl": true,
  "FromAddress": "noreply@noncash.app",
  "FromDisplayName": "NonCash"
}
```

### Feature Flag

Email delivery can be disabled without changing SMTP config:

```json
"Notifications": {
  "EmailEnabled": false,
  "LogFile": "logs/notifications.log"
}
```

When `EmailEnabled` is `false` OR `Smtp:Host` is empty, the system falls back to `FileNotificationService`
(CR-2026-09-10-30 row 8), which appends one structured line per notification to a rolling text file instead
of writing to the console:

- Default path: `{AppContext.BaseDirectory}/logs/notifications.log`. Set `Notifications:LogFile` to override it
  (a relative path is resolved against the process working directory). The sink is registered **scoped**, so the
  resolved absolute path is written to the application log each time a request first produces a notification —
  read that line to find the file rather than guessing the working directory.
- Each line is `UTC timestamp [Category] key=value …`, e.g. `[GiftSent]`, `[GiftAccepted]`, `[GiftDeclined]`,
  `[GiftExpired]`, `[GiftMessage]`, `[VoucherReceived]`, `[MemberSignInLink]`.
- Unlike `email_logs`, the file sink **does record the magic-link and sign-in URLs**, so a developer can copy a
  link straight out of it while real delivery is off.
- `[GiftMessage]` lines carry the message body verbatim (`body="…"`). That is deliberate — the sink is a
  diagnostic for the period when real delivery is off — but it means the file holds private member
  correspondence, so it must never be committed, shipped, or shared.
- Writes are serialised behind a semaphore; a write failure is logged and the notification is dropped rather
  than failing the user's action.

### Dev Mode Kill-Switch

`Environment:Name` (in `appsettings.json`) is the master switch:

```json
"Environment": {
  "Name": "dev"
}
```

When the name is `dev` (or missing — fails safe), **no real email is ever sent**, regardless of SMTP config or `EmailEnabled`:

- DI registers `FileNotificationService` instead of `EmailNotificationService`.
- `EmailNotificationService.SendAsync` double-checks the mode and suppresses delivery even if registered directly; the suppressed attempt is written to `email_logs` with `Success=false` and `ErrorMessage="Suppressed: dev mode (Environment:Name=dev)"` for audit.

To enable real delivery (pilot/production), set `Environment:Name` to `pilot` or `production` (e.g. via the `Environment__Name` environment variable) and provide SMTP settings.

### Retry Policy

- **Max retries**: 3
- **Backoff**: Exponential (2s, 4s, 8s)
- **Transient errors retried**: `ServiceNotAvailable`, `ServiceClosingTransmissionChannel`, `GeneralFailure`

### Audit Trail

Every send attempt is recorded in `email_logs`:

| Column | Description |
|--------|-------------|
| `to_address` | Recipient email |
| `subject` | Email subject line |
| `template_name` | HTML template used |
| `notification_type` | Scenario category |
| `success` | Whether the send succeeded |
| `error_message` | Error details on failure |
| `retry_count` | Number of retries attempted |
| `sent_at` | UTC timestamp |

## Notification Channels

| Channel | Status | Notes |
|---------|--------|-------|
| **Email** | Active | SMTP delivery with HTML templates |
| **File sink** | Active (fallback) | `FileNotificationService` → `logs/notifications.log`; used whenever real email delivery is off (dev kill-switch, `EmailEnabled=false`, or no SMTP host). Records magic-link URLs, which `email_logs` does not. |
| **Zalo ZNS** | Planned | Activates once OA and templates are approved |
| **Push** | N/A | Loyalty App responsibility (not NonCash) |
| **SMS** | Excluded | Cost; no plans for v1 |

## Recipient Data Requirements

For notifications to be delivered, the relevant records must have email addresses populated:

- **`UserAccounts.Email`** — used for staff notifications, plan review, password reset, adjustment notifications
- **`Brands.ContactEmail`** — used for credit-related alerts (welcome credits, purchase receipt, low balance, expiry, forfeiture)
- **`Customers.Email`** — used for member-facing notifications (voucher received, gift accept link, gift accepted / declined / expired notices, gift-message announcements, sign-in links)

If the email field is empty, the service logs `skipped: no email on file` and continues without error.

## Registered Changes (CR Registry)

| CR ID | Item | Status |
|---|---|---|
| CR-2026-09-06-12 | Reword the `VoucherReceived` template with calmer transactional copy — the current promotional phrasing ("You've received a voucher") is a spam-filter fingerprint risk (observed during the 2026-09-06 deliverability incident) | Registered — pending decision |
| CR-2026-09-06-13 | Email sender-account recovery after the 2026-09-06 compromise: revoke the leaked Gmail app password → change the account password → check filters/forwarding → issue a new app password injected via env var (never committed) → rotate the other secrets sharing that file (DB, ZaloPay, VNPAY, MediaService) → set up SPF/DKIM/DMARC for the sender domain → 24–72h cool-down → retest to the two authorized addresses only. Owner: user (Google account); agent assists on config/env vars | Approved — pending implementation |
| CR-2026-09-07-19 | **Member magic-link access (Strategy A — Always Magic Link):** all voucher notification emails (`VoucherReceived`, `VoucherTransferInitiated`) include a signed JWT magic link that auto-logs the customer into the member app regardless of password state. Password becomes optional convenience only. Requires: (1) magic link token generation in `EmailNotificationService` (JWT, 7-day TTL, scoped to MemberAccountId); (2) `POST /api/v1/auth/magic-link` endpoint to validate token → issue MemberAccount JWT; (3) update email templates with "View My Voucher" button linking to magic URL; (4) member app `/member/welcome?token=...` page; (5) optional "Set password" page for customers who want direct login. Rationale: customers auto-created via promotion/transfer have no password, yet the email says "log in to view" — magic link closes this gap for all distribution paths (promotion, transfer, future channels). Decision: 2026-09-07, owner-approved. | **Implemented** 2026-09-07 |
| CR-2026-09-10-30 | **Gifting closes the loop with the sender, and the dev sink writes to a file.** Trigger: owner live test — a member sent a voucher to a friend, the voucher disappeared from the sender's wallet, and nobody was told anything: the sender never learned whether the friend got it, and no email recorded where the accept link had gone. **(A)** Three new sender-facing emails — `GiftAccepted` (carries the recipient's thank-you note), `GiftDeclined` (carries the reason) and `GiftExpired` (7-day sweep) — so every gift ends with the sender being told the outcome; rows 9a–9c above. **(B)** The accept-link email (row 9) is sent when the gift is *created*, and the sender is told at that moment which case applies: accept link emailed, held because no email is on file, or held because the phone is not on NonCash yet. **(C)** `NotifyPendingGiftsAsync` flushes held gifts: the instant a placeholder phone registers with an email, row 9 goes out automatically without the sender re-sending. **(D)** Row 8, owner-requested ("thay vì output send email to console … chuyển thành log file"): `ConsoleNotificationService` is gone. When real delivery is off, `FileNotificationService` appends one `UTC [Category] key=value` line per notification to `Notifications:LogFile` (default `{BaseDirectory}/logs/notifications.log`), and — unlike `email_logs` — it records the magic-link and sign-in URLs so a link can be copied straight out of the file. **(E)** Gift emails used to name the voucher from `plan.Brand?.Name`, which EF does not populate on that read path, so senders would have read "your voucher"; `VoucherTransferService` now resolves the brand name through `IBrandRepository` | **Implemented** 2026-09-10 — 183 unit + 161 integration pass; templates `GiftAccepted.html`, `GiftDeclined.html`, `GiftExpired.html` in place; pending owner manual verification (API restart required) |
| CR-2026-09-10-31 | **Gift message box (Hộp thư món quà).** Trigger: owner request after CR-2026-09-10-30 — "I also need to save the messages back and ward between sender and receiver somewhere in DB, i think that is a good memory, good experienced for customer and for this platform". Every gift now keeps its own conversation in `gift_messages`; the notification side of it is row 9d. `NotifyGiftMessageAsync` goes to whichever of the two participants did not write the message, with template `GiftMessage.html` and subject `{author} sent you a message about your gift`. The body is member-typed free text, so it is HTML-encoded before rendering and any link in it stays plain text. Delivery is best-effort: a throwing send is swallowed because the message is already stored, and losing it would be worse than not announcing it. The brand is never notified and never sees a follow-up message — its read-only gift log carries only the note sent with the gift and the reply it produced. The file sink records `[GiftMessage]` lines with the body verbatim, so that log holds private correspondence and must never be committed or shared | **Implemented** 2026-09-10 — 204 unit + 181 integration pass; template `GiftMessage.html` in place; pending owner manual verification (API restart required) |
