# Story 8.3: Loyalty App Display Payload

Status: backlog

## Story

As a Loyalty App partner,
I want the Integration API wallet response to include all display fields needed for rich in-app voucher rendering,
So that my app can show branded, visually consistent voucher cards without making additional API calls or guessing display logic.

## Acceptance Criteria

**AC1: Enhanced Wallet Response**
Given a partner calls `GET /integration/member/{phone}/vouchers`
When vouchers exist
Then each voucher object includes the full display payload:
```json
{
  "voucherID": "GUID",
  "brand": "Coffee House",
  "brandColor": "#E53935",
  "iconURL": "https://...",
  "coverImageURL": "https://...",
  "displayName": "Weekend Treat 200K",
  "shortDescription": "Get 200K off any weekend combo",
  "faceValue": 200000,
  "faceValueFormatted": "200,000 ₫",
  "valueType": "Value",
  "usageStatus": "Pending",
  "statusBadge": "Active",
  "expiryDate": "2026-08-30",
  "expiryDisplay": "5 days left",
  "validDaysOfWeek": "Sat,Sun",
  "outlets": [
    { "name": "Coffee House Giga Mall", "address": "..." }
  ],
  "termsAndConditions": "Valid on weekends only...",
  "dynamicCode": null
}
```

**AC2: Pre-computed Display Fields**
Given the wallet response
When the partner renders it
Then the following are pre-computed by NonCash (not the partner):
- `faceValueFormatted` — locale-aware currency string
- `statusBadge` — "Active", "Expiring Soon", "Expired", "Used", "In Use"
- `expiryDisplay` — human-readable countdown (e.g., "5 days left", "Expires today", "Expired")

**AC3: Dynamic Code on Demand**
Given a partner needs the current dynamic code for POS scanning
When they call `GET /integration/member/{phone}/vouchers/{voucherID}/code`
Then the response returns the current rotating code and its TTL (seconds until refresh)
And this endpoint is rate-limited to prevent abuse

**AC4: Image Variants**
Given the wallet response includes image URLs
When the partner fetches images
Then they can append query params for size variants: `?w=128&h=128` (icon), `?w=600` (card), `?w=1200` (full)
And the image service returns appropriately sized variants

## Tasks / Subtasks

- [ ] Task 1: Enhanced wallet DTO (AC1, AC2)
  - [ ] Subtask 1.1: `IntegrationVoucherResponse` DTO with all display fields
  - [ ] Subtask 1.2: Map from `VoucherPlanHeader` display fields (Story 8.1)
  - [ ] Subtask 1.3: Compute `faceValueFormatted`, `statusBadge`, `expiryDisplay` server-side
- [ ] Task 2: Dynamic code endpoint (AC3)
  - [ ] Subtask 2.1: `GET /integration/member/{phone}/vouchers/{voucherID}/code`
  - [ ] Subtask 2.2: Rate limiting (max 10 calls/minute per voucher)
- [ ] Task 3: Image variant support (AC4)
  - [ ] Subtask 3.1: Image resize/variant service (if using object storage with CDN, this may be CDN-level)
  - [ ] Subtask 3.2: Document supported query params
- [ ] Task 4: Update existing wallet endpoint (AC1)
  - [ ] Subtask 4.1: Extend `IntegrationController.GetMemberWallet` to use new DTO
- [ ] Task 5: Tests
  - [ ] Subtask 5.1: Unit test for display field computation
  - [ ] Subtask 5.2: Integration test for dynamic code rate limiting

## Dev Notes

### Why Pre-compute Display Fields?
Loyalty Apps should not need to implement NonCash business logic (expiry countdown, status determination, currency formatting). Pre-computing these server-side ensures consistency across all partners and reduces integration effort.

### Rate Limiting
The dynamic code endpoint is sensitive — each call generates a fresh code. Apply per-voucher rate limiting:
- 10 calls/minute per voucher ID
- Return `429 Too Many Requests` if exceeded

### Image Variants
If using a CDN (Cloudflare, Azure CDN), image resizing can be handled at the edge with URL params. If self-hosted, implement a lightweight resize endpoint or pre-generate common sizes on upload.

### References
- [Source: docs/api-contracts.md#2 Get Member Voucher Wallet]
- [Source: Story 8.1 Voucher Display Data Model]
- [Source: Story 8.2 Member Store & Wallet Display]
