# Story 6.4: Webhook Lifecycle Events (Push to Loyalty App)

Status: backlog

## Story

As a Loyalty App partner,
I want NonCash to push real-time voucher lifecycle events to my registered callback URL,
So that I can update wallet state and trigger push notifications instantly without polling.

## Acceptance Criteria

**AC1: Event Emission**
Given a voucher lifecycle event occurs (Distributed, Redeemed, Transferred, Expired, Cancelled)
When the event is committed to the database
Then the system enqueues a webhook delivery to all partners authorized for that Brand

**AC2: Webhook Delivery**
Given a queued webhook
When the delivery worker processes it
Then it sends a `POST` to the partner's `callbackURL` with the event payload
And includes a `X-NonCash-Signature` header (HMAC-SHA256) for payload verification
And retries up to 3 times with exponential backoff on non-2xx responses

**AC3: Payload Structure**
Given a webhook delivery
When the payload is constructed
Then it includes: `event` (type), `timestamp`, `data{}` (voucherID, memberPhone, brand, outlet, faceValue, transactionID where applicable)

**AC4: Delivery Logging**
Given a webhook delivery attempt
When it succeeds or fails
Then the system logs: partnerID, event type, HTTP status, response time, retry count
And failed deliveries after 3 retries are marked as `Failed` and visible in the Admin dashboard

**AC5: Webhook Secret Management**
Given a partner record
When Admin generates or rotates the webhook signing secret
Then the new secret is shown once
And all subsequent webhooks use the new secret for HMAC signing

## Tasks / Subtasks

- [ ] Task 1: Event emitter (AC1)
  - [ ] Subtask 1.1: `IVoucherEventPublisher` interface in Core
  - [ ] Subtask 1.2: Hook into existing service methods (distribution, redemption, transfer) to publish events
  - [ ] Subtask 1.3: `VoucherEvent` domain event record
- [ ] Task 2: Webhook delivery worker (AC2, AC4)
  - [ ] Subtask 2.1: `WebhookDeliveryService` as a `BackgroundService` or `IHostedService`
  - [ ] Subtask 2.2: `webhook_deliveries` table: `id`, `partner_id`, `event_type`, `payload_json`, `http_status`, `retry_count`, `delivered_at`, `created_at`
  - [ ] Subtask 2.3: Retry logic with exponential backoff (1s, 5s, 30s)
- [ ] Task 3: HMAC signing (AC2, AC5)
  - [ ] Subtask 3.1: Add `webhook_secret` (text nullable) to `integration_partners`
  - [ ] Subtask 3.2: HMAC-SHA256 signing utility
  - [ ] Subtask 3.3: Admin UI for secret generation
- [ ] Task 4: Admin delivery log dashboard (AC4)
  - [ ] Subtask 4.1: `WebhookDeliveries.razor` page showing recent deliveries, filterable by partner and status
- [ ] Task 5: Tests
  - [ ] Subtask 5.1: Unit test for HMAC signature generation
  - [ ] Subtask 5.2: Integration test for retry on simulated failure

## Dev Notes

### Architecture Compliance
- Event publishing follows the outbox pattern: write the event to a `voucher_events` table in the same transaction as the business operation, then a background worker delivers webhooks. This prevents missed events.
- The `IVoucherEventPublisher` is in Core; the delivery implementation is in Infrastructure.

### Tables
- `voucher_events`: `id` (uuid), `event_type` (varchar 30), `voucher_id` (uuid), `member_phone` (varchar 20), `brand_id` (uuid), `payload_json` (jsonb), `created_at` (timestamptz)
- `webhook_deliveries`: `id` (uuid), `partner_id` (uuid FK), `event_id` (uuid FK), `http_status` (int nullable), `retry_count` (int default 0), `delivered_at` (timestamptz nullable), `error_message` (text nullable)

### API Contract
- Webhook payload format — see `docs/api-contracts.md` #4
- Partner verifies signature: `HMAC-SHA256(webhook_secret, raw_body) == X-NonCash-Signature`

### References
- [Source: docs/api-contracts.md#4 Webhook: Voucher Lifecycle Events]
