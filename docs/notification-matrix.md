# Notification Matrix

This document describes all notification scenarios in the NonCash platform, including the trigger, recipient, delivery channel, and email template used.

## Email Notifications

The email notification system uses SMTP delivery with HTML templates stored in `src/NonCash.Infrastructure/EmailTemplates/`. Every send attempt (success or failure) is recorded in the `email_logs` table for audit traceability.

### Registration & Onboarding

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 1 | New business registration submitted | `RegistrationService.SubmitAsync` | All Admin users with email | `AdminNewRegistration` | `NewRegistration` |
| 2 | Registration submitted (confirmation) | `RegistrationService.SubmitAsync` | Applicant (contact email) | `ApplicantRegistrationSubmitted` | `RegistrationConfirmation` |
| 3 | Registration approved | `RegistrationReviewService.ApproveAsync` | Brand representative | `ApplicantReviewResult` | `RegistrationReview` |
| 4 | Registration rejected | `RegistrationReviewService.RejectAsync` | Brand representative | `ApplicantReviewResult` | `RegistrationReview` |
| 5 | Staff account created | `UserService.CreateAsync` | New staff user | `StaffAccountCreated` | `StaffAccountCreated` |

### Voucher Plan & Approval

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 6 | Plan approved | `ApprovalService.ApproveAsync` | Plan creator | `PlanReviewed` | `PlanReviewed` |
| 7 | Plan rejected | `ApprovalService.RejectAsync` | Plan creator | `PlanReviewed` | `PlanReviewed` |

### Voucher Distribution & Transfer

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 8 | Voucher received (promotion/sale) | `PromotionService` distribution | Member (email if available) | `VoucherReceived` | `VoucherDistribution` |
| 9 | Voucher transfer received | `TransferService.TransferAsync` | Transfer recipient | `VoucherTransferInitiated` | `VoucherTransfer` |

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

### Security

| # | Scenario | Trigger | Recipient | Template | NotificationType |
|---|----------|---------|-----------|----------|-----------------|
| 17 | Password reset requested | `AuthService.ForgotPasswordAsync` | User (by username or email lookup) | `PasswordReset` | `PasswordReset` |

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
  "EmailEnabled": false
}
```

When `EmailEnabled` is `false` OR `Smtp:Host` is empty, the system falls back to `ConsoleNotificationService` (logs to stdout).

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
| **Zalo ZNS** | Planned | Activates once OA and templates are approved |
| **Push** | N/A | Loyalty App responsibility (not NonCash) |
| **SMS** | Excluded | Cost; no plans for v1 |

## Recipient Data Requirements

For notifications to be delivered, the relevant records must have email addresses populated:

- **`UserAccounts.Email`** — used for staff notifications, plan review, password reset, adjustment notifications
- **`Brands.ContactEmail`** — used for credit-related alerts (welcome credits, purchase receipt, low balance, expiry, forfeiture)
- **`Customers.Email`** — used for member-facing notifications (voucher received, transfer received)

If the email field is empty, the service logs `skipped: no email on file` and continues without error.
