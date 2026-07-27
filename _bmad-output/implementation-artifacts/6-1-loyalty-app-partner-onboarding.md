# Story 6.1: Loyalty App Partner Onboarding & API Key Management

Status: backlog

## Story

As a System Admin,
I want to register a Loyalty App partner (e.g., Giga Mall App, Coffee House App) and issue scoped API credentials,
So that the partner can authenticate against the NonCash Integration API securely.

## Acceptance Criteria

**AC1: Partner Registration**
Given an Admin is on the Integration Partners screen
When they create a new partner with: Partner Name, Contact Email, Callback URL (for webhooks)
Then the system creates an `IntegrationPartner` record with a unique `PartnerID`
And generates an API key pair: `ApiKeyPrefix` (visible) + `ApiKeyHash` (stored, never shown again)

**AC2: API Key Scoping**
Given a partner has an API key
When they call any `/integration/*` endpoint
Then the system authenticates via `X-API-Key` header
And scopes all queries to data the partner is authorized to access (based on `BrandID` partnerships)

**AC3: Partner-Brand Association**
Given a registered partner
When Admin links the partner to one or more Brands
Then the partner can only query/distribute vouchers for those Brands
And cross-brand data access is blocked

**AC4: Key Rotation**
Given an Admin selects a partner
When they click "Regenerate API Key"
Then the old key is invalidated immediately
And a new key pair is generated and shown once

**AC5: Callback URL Management**
Given a partner record
When Admin updates the Callback URL
Then webhook events are sent to the new URL from that point forward
And the system validates the URL is reachable (HTTP 200 on test ping)

## Tasks / Subtasks

- [ ] Task 1: Define `IntegrationPartner` entity (AC1)
  - [ ] Subtask 1.1: `IntegrationPartner.cs` in `NonCash.Core/Entities/`
  - [ ] Subtask 1.2: `PartnerBrand` join entity for many-to-many Brand association (AC3)
  - [ ] Subtask 1.3: EF FluentAPI configuration and migration
- [ ] Task 2: API key generation and validation (AC1, AC2, AC4)
  - [ ] Subtask 2.1: `IIntegrationPartnerService` with Create, RegenerateKey, ValidateKey
  - [ ] Subtask 2.2: API key middleware for `/integration/*` route prefix
  - [ ] Subtask 2.3: BCrypt hash for key storage, prefix for lookup
- [ ] Task 3: Admin UI (AC1, AC3, AC4, AC5)
  - [ ] Subtask 3.1: `IntegrationPartners.razor` list + create/edit page
  - [ ] Subtask 3.2: Brand multi-select for partner association
  - [ ] Subtask 3.3: Callback URL test ping button
- [ ] Task 4: API endpoints (AC1, AC2)
  - [ ] Subtask 4.1: `IntegrationPartnersController` (Admin-only CRUD)
  - [ ] Subtask 4.2: `POST /api/v1/integration/partners`, `GET`, `PUT`, `DELETE`
- [ ] Task 5: Database migration
- [ ] Task 6: Tests
  - [ ] Subtask 6.1: Unit tests for API key generation and validation
  - [ ] Subtask 6.2: Integration tests for Partner-Brand scoping

## Dev Notes

### Architecture Compliance
- `IntegrationPartner` is a platform-level entity (not Brand-scoped). The `PartnerBrand` join table creates the many-to-many authorization scope.
- API key follows the same pattern as POS `Outlet.ApiKeyPrefix` but with separate storage and middleware.
- Callback URL is used by Story 6.4 (Webhooks). Store it on the partner record for reuse.

### Entity Schema
- Table: `integration_partners`
- Columns: `id` (uuid PK), `name` (varchar 200), `contact_email` (varchar 200), `callback_url` (text nullable), `api_key_prefix` (varchar 16 unique), `api_key_hash` (text), `is_active` (bool default true), `created_at` (timestamptz), `updated_at` (timestamptz)
- Table: `partner_brands` (`partner_id` uuid FK, `brand_id` uuid FK, PK composite)

### Security
- API key shown only once on creation. Admin must copy it before closing the dialog.
- Key rotation invalidates the old key immediately — no grace period.

### References
- [Source: docs/api-contracts.md#Loyalty App Integration API]
- [Source: docs/architecture.md#External Integration Boundary]
