# Story 6.2: Segment Distribution API (Loyalty App Push)

Status: done

## Story

As a Loyalty App partner,
I want to push a target member segment (list of phone numbers + emails + external member IDs) to NonCash for batch voucher distribution,
So that I can trigger targeted campaigns from my own segmentation engine without manual CSV uploads.

## Prerequisites

- A **VoucherPlanHeader** must exist and be in `Approved` status with generated `VoucherPlanDetail` stock.
- The plan is created and approved via the NonCash admin UI or API (not via the Integration API).
- The Loyalty App partner is registered (Story 6.1) and associated with the target Brand.

## Customer Notification Channels

After successful distribution, customers are notified of their new voucher through **all applicable channels**:

| # | Channel | Who sends | When | Mechanism |
|---|---------|-----------|------|-----------|
| 1 | **Email** | NonCash | Immediately after distribution | `NotifyVoucherReceivedAsync` — uses existing `VoucherReceived.html` template. Requires member email in request payload or on file. |
| 2 | **Webhook** | NonCash → Loyalty App | Immediately after distribution | `voucher.received` event (Story 6.4) — Loyalty App sends push notification to customer. |
| 3 | **In-app wallet** | Loyalty App | When customer opens Loyalty App | Loyalty App queries 6.3 Wallet API to display vouchers. |
| 4 | **Member portal** | NonCash | When customer logs in to NonCash platform | Customer sees vouchers in their member dashboard (`/member/vouchers`). |

> **Important:** Email is the primary transactional notification. Not all customers will have a Loyalty App, so email ensures universal delivery. The webhook enables the Loyalty App to send a branded push notification for customers who do have the app.

## Acceptance Criteria

**AC1: Segment Distribution Request**
Given a Loyalty App partner calls `POST /integration/distribute`
When the request includes: `planId`, `members[]` (phone + email + optional `externalMemberID`), optional `callbackURL`
Then NonCash matches phones to existing Customers (or creates placeholders)
And stores email on the Customer/MemberAccount if not already present
And distributes one voucher per member from the plan's available stock
And sends `VoucherReceived` email to each member with a valid email
And fires `voucher.received` webhook event for each successful distribution (Story 6.4)
And returns a `distributionId` with counts (requested, distributed, skipped)

**AC2: Idempotency**
Given the same `planId` and `members[]` payload is sent twice
When the second call arrives
Then already-distributed members are reported in `skipped[]` with reason `"already_distributed"`
And no duplicate vouchers are assigned
And no duplicate emails are sent

**AC3: Stock Validation**
Given a plan with fewer remaining vouchers than requested members
When the distribution is triggered
Then the request fails entirely (all-or-nothing) with error `"insufficient_stock"`
And no vouchers are assigned
And no emails are sent

**AC4: Blacklist Enforcement**
Given a member phone that is blacklisted
When included in a distribution request
Then that member is skipped and reported in `skipped[]` with reason `"blacklisted"`
And no email is sent to that member

**AC5: External Member ID Mapping**
Given a member payload includes `externalMemberID`
When the distribution succeeds
Then the `externalMemberID` is stored on the `VoucherDistribution` record
So the Loyalty App can correlate vouchers back to its own member system

**AC6: Email Notification**
Given a member has a valid email (from request payload or existing Customer record)
When distribution succeeds
Then NonCash calls `NotifyVoucherReceivedAsync` with the member's email, voucher details, and plan name
If the member has no email on file and none was provided in the request
Then the email is skipped (logged as `"skipped: no email"`) and distribution still succeeds

**AC7: Member Portal Visibility**
Given a customer has received a voucher via segment distribution
When the customer logs in to the NonCash member portal
Then the voucher appears in their `/member/vouchers` dashboard
With status, face value, expiry date, and applicable outlets

## API Contract

### Request

```
POST /api/v1/integration/distribute
Headers:
  X-API-Key: <partner-api-key>
  X-API-Secret: <partner-api-secret>
Body:
{
  "planId": "guid-of-approved-plan",
  "members": [
    {
      "phone": "0901234567",
      "email": "alice@example.com",       // required for email notification
      "externalMemberId": "EXT-001",      // optional — partner's member ID
      "fullName": "Alice Nguyen"           // optional
    },
    {
      "phone": "0907654321",
      "email": "bob@example.com",
      "externalMemberId": "EXT-002",
      "fullName": "Bob Tran"
    }
  ]
}
```

### Response (200 OK)

```json
{
  "distributionId": "guid",
  "planId": "guid",
  "summary": {
    "requested": 2,
    "distributed": 2,
    "skipped": 0
  },
  "distributed": [
    {
      "phone": "0901234567",
      "voucherId": "guid",
      "voucherCode": "DYN-CODE-1",
      "emailSent": true
    }
  ],
  "skipped": []
}
```

### Response (400 — insufficient stock)

```json
{
  "error": "insufficient_stock",
  "message": "Plan has 1 remaining voucher(s) but 2 members requested.",
  "availableStock": 1,
  "requestedCount": 2
}
```

## Tasks / Subtasks

- [ ] Task 1: Integration distribution endpoint (AC1, AC3)
  - [ ] Subtask 1.1: `IntegrationController.DistributeAsync`
  - [ ] Subtask 1.2: `IntegrationDistributionService` reusing batch promotion logic from Story 3.1
  - [ ] Subtask 1.3: All-or-nothing stock check
- [ ] Task 2: Idempotency and blacklist logic (AC2, AC4)
  - [ ] Subtask 2.1: Check existing `VoucherDistribution` for duplicate member+plan combos
  - [ ] Subtask 2.2: Blacklist check per member phone
- [ ] Task 3: External member ID storage (AC5)
  - [ ] Subtask 3.1: Add `external_member_id` (varchar nullable) column to `voucher_distributions`
  - [ ] Subtask 3.2: Migration
- [x] Task 4: Email notification on distribution (AC6)
  - [x] Subtask 4.1: After each successful distribution, call `NotifyVoucherReceivedAsync`
  - [x] Subtask 4.2: Upsert email on Customer/MemberAccount from request payload if not on file
  - [x] Subtask 4.3: Skip email gracefully when no email available (log + continue)
- [x] Task 5: Webhook event firing (AC1 — webhook part)
  - [x] Subtask 5.1: Fire `voucher.received` webhook after each successful distribution (Story 6.4)
- [x] Task 6: Member portal visibility (AC7)
  - [x] Subtask 6.1: Ensure `/member/vouchers` page shows vouchers received via integration distribution
  - [x] Subtask 6.2: Verify voucher data includes plan name, face value, expiry, outlets
- [ ] Task 7: Tests
  - [ ] Subtask 7.1: Integration test for idempotent re-submit
  - [ ] Subtask 7.2: Integration test for insufficient stock rollback
  - [ ] Subtask 7.3: Unit test for email notification trigger
  - [ ] Subtask 7.4: Integration test for email skip when no email on file

## Dev Notes

### Architecture Compliance
- This endpoint reuses the core distribution logic from `VoucherDistributionService` (Story 3.1). The integration layer is a thin API adapter.
- Partner-Brand scope is enforced by middleware (from Story 6.1): the partner can only distribute for Brands in their `partner_brands` association.

### API Contract
- `POST /api/v1/integration/distribute` — see `docs/api-contracts.md` Section "Loyalty App Integration API #1"
- Auth: `X-API-Key` + `X-API-Secret` headers

### Schema Change
- Add `external_member_id` (varchar 100 nullable) to `voucher_distributions` table.

### Email Notification
- Reuses existing `VoucherReceived.html` template and `NotifyVoucherReceivedAsync` method.
- Email is sent asynchronously — distribution response returns immediately, email delivery happens in background.
- If email send fails, the distribution is still successful (email failure does not rollback distribution).
- **Future:** Email notification will be replaced/supplemented by Zalo OA message (using phone number). The `NotificationChannel.Zalo` enum value is already defined. When Zalo integration is ready, change `notifyChannel` in `IntegrationController.Distribute` to `NotificationChannel.Zalo` or `NotificationChannel.Both`.

### Member Portal
- The `/member/vouchers` page already shows vouchers for the logged-in customer.
- Vouchers distributed via 6.2 are stored in the same `voucher_distributions` + `voucher_plan_details` tables, so they appear automatically.
- No separate UI work needed unless we want to add a "source" badge (e.g., "via Giga Mall App").

### References
- [Source: docs/api-contracts.md#1 Distribute to Segment]
- [Source: Story 3.1 batch promotion logic]
- [Source: docs/notification-matrix.md — VoucherReceived scenario]
