# NonCash Functional Test Plan — Epics 6, 7, 8

> **Created:** 2026-07-27  
> **Scope:** Cross-Tenant Settlement (Epic 7), Voucher Display (Epic 8), Loyalty App Integration (Epic 6), MSA Image Storage  
> **Target Environment:** Development (https://localhost:7107)

---

## 1. Pre-Test Setup

### 1.1 Apply Migrations

```powershell
cd src\NonCash.API
dotnet ef database update --project ..\NonCash.Infrastructure
```

### 1.2 Seed Test Data

Run `seed-test-data.sql` in pgAdmin against the `noncash` database.

### 1.3 Start the API

```powershell
cd src\NonCash.API
dotnet run --launch-profile https
```

- Swagger UI: `https://localhost:7107/swagger`
- Health check: `https://localhost:7107/health`

### 1.4 Known Test Data IDs

| Entity | ID | Purpose |
|--------|----|---------|
| Brand "Test Coffee Shop" | `a0000000-0000-0000-0000-000000000001` | Issuing brand |
| Outlet "Main Street Store" | `b0000000-0000-0000-0000-000000000001` | Redemption outlet |
| Customer "Alice Sender" | `c0000000-0000-0000-0000-000000000001` | Member with 2 vouchers |
| Customer "Bob Receiver" | `c0000000-0000-0000-0000-000000000002` | Member with 1 voucher |
| Customer "Carol Third" | `c0000000-0000-0000-0000-000000000003` | Another member |
| Voucher Plan Header | `e0000000-0000-0000-0000-000000000001` | Approved plan (100,000 VND) |
| Voucher Detail (Alice #1) | `f0000000-0000-0000-0000-000000000001` | Pending |
| Voucher Detail (Alice #2) | `f0000000-0000-0000-0000-000000000002` | Pending |
| Voucher Detail (Bob #1) | `f0000000-0000-0000-0000-000000000003` | Pending |

### 1.5 Obtain Auth Tokens

**Admin token:**
```
POST https://localhost:7107/api/v1/auth/login
Content-Type: application/json

{ "username": "admin", "password": "Admin@123" }
```
Save returned `token` as `{{ADMIN_TOKEN}}`.

**Brand Manager token (alice):**
```
POST https://localhost:7107/api/v1/auth/login
Content-Type: application/json

{ "username": "alice", "password": "Test@123" }
```
Save returned `token` as `{{BRAND_TOKEN}}`.

---

## 2. Epic 8 — Voucher Display & Image Storage

### TC-8.1 Upload Image via MSA

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/upload/image` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Body | `multipart/form-data` |

**Form fields:**

| Field | Value |
|-------|-------|
| `file` | Select a small JPG/PNG (< 5 MB) |
| `entity` | `voucher_plan_headers` |
| `uniqueCode` | `e0000000-0000-0000-0000-000000000001_cover_image` |

**Expected:** HTTP 200
```json
{
  "success": true,
  "url": "/noncash/images/voucher_plan_headers/e0000000-0000-0000-0000-000000000001_cover_image.jpg"
}
```
The returned value is a **RelativeUrl** (no domain). This is what gets stored in DB.

### TC-8.2 Upload Image — Missing Entity Field

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/upload/image` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Body | `multipart/form-data` with `file` only (no `entity`, no `uniqueCode`) |
| Expected | HTTP 400, `{ "success": false, "error": "The 'entity' field is required..." }` |

### TC-8.3 Upload Image — No File

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/upload/image` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Body | `multipart/form-data` with `entity` + `uniqueCode` but no `file` |
| Expected | HTTP 400, `{ "success": false, "error": "No file uploaded." }` |

### TC-8.4 Upload Image — Oversized File

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/upload/image` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Body | Upload a file > 5 MB |
| Expected | HTTP 400, error message mentions "File too large" |

### TC-8.5 Upload Image — Invalid Format

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/upload/image` |
| Auth | Bearer `{{BRAND_TOKEN}}` |
| Body | Upload a `.txt` or `.pdf` file |
| Expected | HTTP 400, error message mentions "Invalid image format" |

### TC-8.6 Upload Image — Delete-Before-Upload Verification

| Step | Action |
|------|--------|
| 1 | Upload an image with `entity=voucher_plan_headers`, `uniqueCode=test_plan_cover` |
| 2 | Note the returned RelativeUrl |
| 3 | Upload a **different** image with the same `entity` + `uniqueCode` |
| 4 | Verify: old file on MSA is deleted (check MSA CDN — old URL returns 404) |
| 5 | Verify: new RelativeUrl is returned |

### TC-8.7 Store Catalog Shows Display Fields

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/store/vouchers` |
| Auth | None (`[AllowAnonymous]`) |
| Expected | HTTP 200, each item includes these fields (may be null for older plans): |

```json
{
  "planId": "...",
  "faceValue": 100000,
  "coverImageUrl": "/noncash/images/...",
  "brandColor": "#FF5733",
  "displayName": "Coffee Lover Voucher",
  "shortDescription": "Enjoy a free coffee",
  "termsAndConditions": "Valid on weekdays only",
  "validDaysOfWeek": "Mon-Fri"
}
```

---

## 3. Epic 7 — Cross-Tenant Settlement

### TC-7.1 Settlement Ledger (Empty Initially)

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/settlements` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200 |

```json
{ "entries": [], "totalCount": 0, "page": 1, "pageSize": 50 }
```

### TC-7.2 Settlement Ledger with Filters

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/settlements?sponsorBrandId=a0000000-0000-0000-0000-000000000001&status=Pending&page=1&pageSize=10` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200, returns only matching entries |

### TC-7.3 Settlement Ledger — Date Range Filter

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/settlements?from=2026-01-01&to=2026-12-31` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200, entries filtered by CreatedAt within date range |

### TC-7.4 Netting Report (Empty Range)

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/settlements/netting?from=2026-01-01&to=2026-12-31` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200 |

```json
{ "from": "2026-01-01T00:00:00", "to": "2026-12-31T00:00:00", "rows": [] }
```

### TC-7.5 Mark Settlement as Settled

> Prerequisite: A settlement entry must exist. See **E2E-1** for creating one via cross-tenant POS redemption.

| Field | Value |
|-------|-------|
| Endpoint | `PUT /api/v1/settlements/{id}/settle` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200, `{ "message": "Settlement marked as settled." }` |

### TC-7.6 Mark Settlement — Not Found

| Field | Value |
|-------|-------|
| Endpoint | `PUT /api/v1/settlements/00000000-0000-0000-0000-000000000099/settle` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 404, `{ "error": "Entry not found or already settled." }` |

### TC-7.7 Mark Already-Settled Entry

| Field | Value |
|-------|-------|
| Endpoint | `PUT /api/v1/settlements/{already-settled-id}/settle` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 404, `{ "error": "Entry not found or already settled." }` |

---

## 4. Epic 6 — Loyalty App Integration

### 4.1 Partner Management (Admin Endpoints)

#### TC-6.1 Create Integration Partner

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/integration-partners` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |

```json
{
  "name": "LoyaltyApp Test",
  "contactEmail": "dev@loyaltyapp.test",
  "callbackUrl": "https://webhook.site/your-unique-id",
  "brandIds": ["a0000000-0000-0000-0000-000000000001"]
}
```

| Expected | HTTP 200, response includes `id` and `apiKeyPrefix` |
|----------|------|

**Save the returned `id` as `{{PARTNER_ID}}`.**

#### TC-6.2 Generate API Key

| Field | Value |
|-------|-------|
| Endpoint | `POST /api/v1/integration-partners/{{PARTNER_ID}}/generate-key` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200 |

```json
{
  "apiKey": "64-character-hex-string...",
  "prefix": "first8ch",
  "warning": "Store this key securely — it will not be shown again."
}
```

**IMPORTANT:** Copy the `apiKey` value — it cannot be retrieved again. Save as `{{INTEGRATION_API_KEY}}`.

#### TC-6.3 List Partners

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/integration-partners` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200, array containing the partner just created |

#### TC-6.4 Get Partner by ID

| Field | Value |
|-------|-------|
| Endpoint | `GET /api/v1/integration-partners/{{PARTNER_ID}}` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200, partner details with brand associations |

#### TC-6.5 Update Partner

| Field | Value |
|-------|-------|
| Endpoint | `PUT /api/v1/integration-partners/{{PARTNER_ID}}` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |

```json
{
  "name": "LoyaltyApp Updated",
  "contactEmail": "dev@loyaltyapp.test",
  "callbackUrl": "https://webhook.site/your-unique-id",
  "isActive": true
}
```

| Expected | HTTP 200, updated name reflected |

#### TC-6.6 Set Partner Brand Associations

| Field | Value |
|-------|-------|
| Endpoint | `PUT /api/v1/integration-partners/{{PARTNER_ID}}/brands` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |

```json
{ "brandIds": ["a0000000-0000-0000-0000-000000000001"] }
```

| Expected | HTTP 200, `{ "message": "Brand associations updated." }` |

#### TC-6.7 Delete Integration Partner

| Field | Value |
|-------|-------|
| Endpoint | `DELETE /api/v1/integration-partners/{{PARTNER_ID}}` |
| Auth | Bearer `{{ADMIN_TOKEN}}` |
| Expected | HTTP 200, `{ "message": "Partner deleted." }` |

> Note: Run this test LAST — other tests depend on the partner.

---

### 4.2 Integration API (API Key Authenticated)

#### TC-6.8 Get Member Wallet — No API Key → 401

| Field | Value |
|-------|-------|
| Endpoint | `GET /integration/member/0909111111/vouchers` |
| Auth | None |
| Expected | HTTP 401 |

#### TC-6.9 Get Member Wallet — Invalid API Key → 401

| Field | Value |
|-------|-------|
| Endpoint | `GET /integration/member/0909111111/vouchers` |
| Header | `X-API-Key: invalid-key-here` |
| Expected | HTTP 401 |

#### TC-6.10 Get Member Wallet — Valid API Key

| Field | Value |
|-------|-------|
| Endpoint | `GET /integration/member/0909111111/vouchers` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |
| Expected | HTTP 200, array of voucher wallet items with display fields |

```json
[
  {
    "voucherId": "...",
    "serialNo": "VC-TEST-2026-00000001",
    "faceValue": 100000,
    "valueType": "Value",
    "expiryDate": "2027-07-27T...",
    "usageStatus": "Pending",
    "coverImageUrl": "/noncash/images/...",
    "brandColor": "#FF5733",
    "displayName": "...",
    "shortDescription": "...",
    "termsAndConditions": "...",
    "brandName": "Test Coffee Shop"
  }
]
```

#### TC-6.11 Get Member Wallet — Unknown Phone

| Field | Value |
|-------|-------|
| Endpoint | `GET /integration/member/0999999999/vouchers` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |
| Expected | HTTP 200, empty array `[]` |

#### TC-6.12 Get Member Event History

| Field | Value |
|-------|-------|
| Endpoint | `GET /integration/member/0909111111/events?limit=20` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |
| Expected | HTTP 200, array of event items (may be empty) |

#### TC-6.13 Distribute Vouchers via Integration

| Field | Value |
|-------|-------|
| Endpoint | `POST /integration/distribute` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |

```json
{
  "planId": "e0000000-0000-0000-0000-000000000001",
  "brandId": "a0000000-0000-0000-0000-000000000001",
  "phoneNumbers": ["0909222222", "0909333333"],
  "externalMemberIds": {
    "0909222222": "EXT-BOB-001",
    "0909333333": "EXT-CAROL-001"
  }
}
```

| Expected | HTTP 200 |

```json
{
  "distributedCount": 2,
  "skippedCount": 0,
  "errors": []
}
```

#### TC-6.14 Distribute — Unauthorized Brand → 403

| Field | Value |
|-------|-------|
| Endpoint | `POST /integration/distribute` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |
| Body | Same as TC-6.13 but with `brandId` = random GUID not in partner's brand list |
| Expected | HTTP 403 Forbidden |

#### TC-6.15 Distribute — Idempotency Check

| Field | Value |
|-------|-------|
| Endpoint | `POST /integration/distribute` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |
| Body | Same request as TC-6.13 (repeat) |
| Expected | HTTP 200, `distributedCount: 0`, `skippedCount: 2` (already distributed) |

#### TC-6.16 Campaign Performance

| Field | Value |
|-------|-------|
| Endpoint | `GET /integration/campaigns/e0000000-0000-0000-0000-000000000001/performance` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |
| Expected | HTTP 200, performance metrics object |

#### TC-6.17 Campaign Performance — Non-Existent Plan

| Field | Value |
|-------|-------|
| Endpoint | `GET /integration/campaigns/00000000-0000-0000-0000-000000000099/performance` |
| Header | `X-API-Key: {{INTEGRATION_API_KEY}}` |
| Expected | HTTP 404, `{ "error": "Plan not found or not accessible." }` |

---

## 5. End-to-End Scenarios

### E2E-1 — Full Settlement Lifecycle (Cross-Tenant Redemption)

**Prerequisite:** Create a second brand and outlet:

```sql
-- Second brand (redeemer)
INSERT INTO public.brands (id, created_at, name, tax_code, contact_email, status)
VALUES ('a0000000-0000-0000-0000-000000000002', NOW(), 'Test Bakery', 'TAX-TEST-002', 'bakery@test.com', 'Active')
ON CONFLICT (id) DO NOTHING;

-- Outlet for the second brand
INSERT INTO public.outlets (id, created_at, brand_id, name, address, status, api_key_prefix, api_key_hash)
VALUES ('b0000000-0000-0000-0000-000000000002', NOW(), 'a0000000-0000-0000-0000-000000000002', 'Bakery Outlet', '456 Bakery St', 'Active', 'BAKERY01', '<bcrypt-hash-of-bakery-api-key>')
ON CONFLICT (id) DO NOTHING;

-- Set sponsor_brand_id on the existing plan
UPDATE public.voucher_plan_headers
SET sponsor_brand_id = 'a0000000-0000-0000-0000-000000000001'
WHERE id = 'e0000000-0000-0000-0000-000000000001';
```

**Steps:**

| Step | Action | Verify |
|------|--------|--------|
| 1 | POS Lock Alice's voucher at Bakery outlet (cross-tenant) | Lock response with `Status: "Locked"` |
| 2 | POS Commit the lock | Commit response with `Status: "Success"` |
| 3 | `GET /api/v1/settlements` | Entry appears with `sponsorBrandId: a000...001`, `redeemBrandId: a000...002`, `status: "Pending"` |
| 4 | `GET /api/v1/settlements/netting?from=2026-01-01&to=2027-12-31` | Netting row shows net amount between Coffee Shop and Bakery |
| 5 | `PUT /api/v1/settlements/{id}/settle` | `status: "Settled"`, `settledAt` populated |
| 6 | `GET /api/v1/settlements?status=Settled` | Entry appears in settled filter |

---

### E2E-2 — Webhook Event Delivery

**Setup:**
1. Go to https://webhook.site and copy your unique URL
2. Create a partner (TC-6.1) with `callbackUrl` = your webhook.site URL
3. Generate API key (TC-6.2)

**Steps:**

| Step | Action | Verify |
|------|--------|--------|
| 1 | `POST /integration/distribute` to distribute vouchers | HTTP 200, distribution succeeds |
| 2 | Wait 30-60 seconds | WebhookDeliveryService polls every 30s |
| 3 | Check webhook.site for incoming POST | Request received |
| 4 | Verify header `X-NonCash-Signature` | Format: `sha256=<64-char-hex>` |
| 5 | Verify payload contains `eventType: "voucher.distributed"` | Correct event type |
| 6 | Verify HMAC signature using partner's `webhookSecret` | Signature matches |

---

### E2E-3 — Voucher Display on Blazor UI

**Setup:** Start the Blazor web app (`src/NonCash.Web`).

| Step | Action | Verify |
|------|--------|--------|
| 1 | Navigate to **Store** page | Vouchers show `CoverImage`, brand color bar, display name |
| 2 | Login as a member | |
| 3 | Navigate to **My Vouchers** | Status badges render correctly: |
| | | - Active voucher → green "Active" badge |
| | | - Expiring within 3 days → orange "Expiring Soon" badge |
| | | - Expired voucher → red "Expired" badge |
| | | - Used voucher → "Used" badge |

---

### E2E-4 — Image Upload → Plan Update → Store Display

| Step | Action | Verify |
|------|--------|--------|
| 1 | Create a voucher plan via `POST /api/v1/plans` | Note the returned `planId` |
| 2 | Upload cover image: `POST /api/v1/upload/image` with `entity=voucher_plan_headers`, `uniqueCode={planId}_cover_image` | Get back RelativeUrl |
| 3 | Update plan: `PUT /api/v1/plans/{planId}` setting `coverImageUrl` = returned RelativeUrl | Plan updated |
| 4 | Approve and publish the plan | |
| 5 | `GET /api/v1/store/vouchers` | Plan appears with `coverImageUrl` matching the RelativeUrl |
| 6 | Upload a new image with same `entity` + `uniqueCode` | Old image deleted on MSA, new RelativeUrl returned |

---

## 6. Summary Checklist

| # | Test Area | Cases | Priority | Status |
|---|-----------|-------|----------|--------|
| 8 | Image Upload (MSA) | TC-8.1 to TC-8.7 | High | ☐ |
| 7 | Settlement Ledger + Netting | TC-7.1 to TC-7.7 | High | ☐ |
| 6 | Partner CRUD | TC-6.1 to TC-6.7 | Medium | ☐ |
| 6 | Integration API (API Key) | TC-6.8 to TC-6.17 | High | ☐ |
| — | E2E: Settlement Lifecycle | E2E-1 | High | ☐ |
| — | E2E: Webhook Delivery | E2E-2 | Medium | ☐ |
| — | E2E: Blazor Display | E2E-3 | Medium | ☐ |
| — | E2E: Image Upload Flow | E2E-4 | Medium | ☐ |

**Total: 28 test cases + 4 end-to-end scenarios**

---

## 7. Tester Notes

### Image Storage Config

The API can be toggled between MSA (remote) and Local storage via `appsettings.json`:

```json
"MediaServiceConfig": {
  "ImageStorage": "MSA",    // or "Local" for dev fallback
  "BaseURL": "http://45.119.87.247:8001/",
  "CDNEndpointURL": "http://45.119.87.247:8001/cdn",
  ...
}
```

- `appsettings.json` (production): `ImageStorage = "MSA"`
- `appsettings.Development.json` (dev): `ImageStorage = "Local"`

When using **Local** mode, images are stored at `wwwroot/uploads/{entity}/{uniqueCode}.ext`.
When using **MSA** mode, images are uploaded to the remote media service and RelativeUrls are returned.

### Webhook Testing Tips

- Use https://webhook.site (free, no signup required)
- Each visit generates a unique URL — copy it for the partner's `callbackUrl`
- The `WebhookDeliveryService` background worker polls every 30 seconds
- Failed deliveries retry with exponential backoff: 1m, 5m, 25m, 2h, 10h (max 5 retries)

### Common Issues

| Issue | Resolution |
|-------|-----------|
| 401 on `/api/v1/*` endpoints | Check Bearer token is valid and not expired |
| 401 on `/integration/*` endpoints | Check `X-API-Key` header; regenerate key if needed |
| Image upload returns 400 | Verify `entity` and `uniqueCode` form fields are present |
| Webhooks not arriving | Check `WebhookDeliveryService` logs; verify `callbackUrl` is reachable |
| Settlement entry not created | Ensure `sponsorBrandId != redeemBrandId` on the redeemed voucher's plan |
