# Integration Partner Management

<cite>
**Referenced Files in This Document**
- [description.txt](file://description.txt)
- [Key Functionalities.txt](file://Key Functionalities.txt)
- [BMAD_STRUCTURE.md](file://BMAD_STRUCTURE.md)
- [docs/architecture.md](file://docs/architecture.md)
- [docs/data-models.md](file://docs/data-models.md)
- [docs/api-contracts.md](file://docs/api-contracts.md)
- [_bmad-output/planning-artifacts/epics.md](file://_bmad-output/planning-artifacts/epics.md)
- [_bmad-output/planning-artifacts/implementation-readiness-report-2026-04-17.md](file://_bmad-output/planning-artifacts/implementation-readiness-report-2026-04-17.md)
- [docs/user-guides/admin-user-guide.md](file://docs/user-guides/admin-user-guide.md)
- [docs/user-guides/brand-user-guide.md](file://docs/user-guides/brand-user-guide.md)
- [docs/user-guides/member-user-guide.md](file://docs/user-guides/member-user-guide.md)
</cite>

## Table of Contents
1. [Introduction](#introduction)
2. [Project Structure](#project-structure)
3. [Core Components](#core-components)
4. [Architecture Overview](#architecture-overview)
5. [Detailed Component Analysis](#detailed-component-analysis)
6. [Dependency Analysis](#dependency-analysis)
7. [Performance Considerations](#performance-considerations)
8. [Troubleshooting Guide](#troubleshooting-guide)
9. [Conclusion](#conclusion)
10. [Appendices](#appendices)

## Introduction
This document explains the Integration Partner Management capability for the NonCash voucher platform. It focuses on how external Loyalty Apps integrate with NonCash as a generic voucher engine, including partner onboarding, API key management, segment distribution, member wallet and event history queries, real-time webhook events, and campaign performance analytics. The system is designed as a SaaS platform with a three-layer architecture (GUI, Business Logic Layer, Data Access Layer), multi-tenancy by BrandID, and strong security via JWT and API Keys.

**Section sources**
- [description.txt:1-31](file://description.txt#L1-L31)
- [BMAD_STRUCTURE.md:37-78](file://BMAD_STRUCTURE.md#L37-L78)

## Project Structure
The repository contains product requirements, architecture documentation, data models, API contracts, user guides, and planning artifacts that together define the integration partner capabilities.

```mermaid
graph TB
A["docs/architecture.md"] --> B["docs/api-contracts.md"]
A --> C["docs/data-models.md"]
D["_bmad-output/planning-artifacts/epics.md"] --> B
E["docs/user-guides/admin-user-guide.md"] --> B
F["docs/user-guides/brand-user-guide.md"] --> B
G["docs/user-guides/member-user-guide.md"] --> B
H["description.txt"] --> A
I["Key Functionalities.txt"] --> D
```

**Diagram sources**
- [docs/architecture.md:1-109](file://docs/architecture.md#L1-L109)
- [docs/api-contracts.md:1-321](file://docs/api-contracts.md#L1-L321)
- [docs/data-models.md:1-113](file://docs/data-models.md#L1-L113)
- [_bmad-output/planning-artifacts/epics.md:1-391](file://_bmad-output/planning-artifacts/epics.md#L1-L391)
- [docs/user-guides/admin-user-guide.md:1-324](file://docs/user-guides/admin-user-guide.md#L1-L324)
- [docs/user-guides/brand-user-guide.md:1-335](file://docs/user-guides/brand-user-guide.md#L1-L335)
- [docs/user-guides/member-user-guide.md:1-316](file://docs/user-guides/member-user-guide.md#L1-L316)
- [description.txt:1-31](file://description.txt#L1-L31)
- [Key Functionalities.txt:1-174](file://Key Functionalities.txt#L1-L174)

**Section sources**
- [docs/architecture.md:1-109](file://docs/architecture.md#L1-L109)
- [_bmad-output/planning-artifacts/epics.md:1-391](file://_bmad-output/planning-artifacts/epics.md#L1-L391)

## Core Components
Integration Partner Management centers around these components:
- Partner Onboarding and API Key Management: Admin registers partners, issues scoped API keys, associates Brands, and manages lifecycle.
- Segment Distribution API: Partners push target segments to NonCash for batch distribution.
- Member Wallet and Event History APIs: Partners query vouchers and lifecycle events for members.
- Webhook Lifecycle Events: NonCash pushes real-time events to partner callback endpoints.
- Campaign Performance Query: Partners retrieve aggregated redemption and ROI metrics per plan.

These are supported by:
- Multi-tenancy isolation by BrandID across all operations.
- Security via API Key authentication for partner endpoints and JWT for admin/brand users.
- Prepaid credit model ensuring consumption accounting per voucher lifetime.

**Section sources**
- [docs/api-contracts.md:174-321](file://docs/api-contracts.md#L174-L321)
- [docs/user-guides/admin-user-guide.md:158-198](file://docs/user-guides/admin-user-guide.md#L158-L198)
- [docs/architecture.md:37-98](file://docs/architecture.md#L37-L98)

## Architecture Overview
NonCash exposes a Loyalty App Integration API that any brand-operated loyalty application can consume. NonCash owns voucher production, distribution execution, POS redemption, fraud protection, and event emission; the Loyalty App owns customer master data, segmentation, marketing decisions, push notifications, and in-app wallet display.

```mermaid
graph TB
subgraph "Loyalty App"
LA_UI["Member Wallet UI"]
LA_SEG["Segmentation Engine"]
LA_WEBHOOK["Webhook Consumer"]
end
subgraph "NonCash Platform"
AUTH["API Key + JWT Auth"]
PARTNER["Partner Registry & Scoping"]
DIST["Distribution Service"]
WALLET["Wallet / Voucher State"]
EVENTS["Event Emitter / Outbox"]
PERM["Campaign Performance"]
end
LA_SEG --> |POST /integration/distribute| DIST
LA_UI --> |GET /integration/member/{phone}/vouchers| WALLET
LA_UI --> |GET /integration/member/{phone}/events| EVENTS
LA_UI --> |GET /integration/campaigns/{planID}/performance| PERM
DIST --> |Webhook| LA_WEBHOOK
EVENTS --> |Webhook| LA_WEBHOOK
AUTH --> PARTNER
PARTNER --> DIST
PARTNER --> WALLET
PARTNER --> EVENTS
PARTNER --> PERM
```

**Diagram sources**
- [docs/architecture.md:45-98](file://docs/architecture.md#L45-L98)
- [docs/api-contracts.md:174-321](file://docs/api-contracts.md#L174-L321)
- [docs/user-guides/admin-user-guide.md:158-198](file://docs/user-guides/admin-user-guide.md#L158-L198)

## Detailed Component Analysis

### Partner Onboarding and API Key Management
Admins register external Loyalty App partners, issue scoped API credentials, associate allowed Brands, and manage lifecycle (update/deactivate/delete). Webhooks are delivered asynchronously with HMAC-SHA256 signatures and retry policies.

```mermaid
sequenceDiagram
participant Admin as "Admin"
participant API as "Admin API"
participant Partner as "Partner Registry"
participant KeyStore as "Key Store"
participant PartnerApp as "Loyalty App"
Admin->>API : POST /api/v1/integration-partners
API->>Partner : Create partner record
Partner-->>API : {id}
Admin->>API : POST /api/v1/integration-partners/{id}/generate-key
API->>KeyStore : Hash and store key
KeyStore-->>API : {apiKeyPrefix, fullKeyOnce}
API-->>Admin : Response with prefix and one-time full key
Admin->>API : PUT /api/v1/integration-partners/{id}/brands
API->>Partner : Update allowed Brands
Partner-->>API : OK
PartnerApp->>API : Calls /integration/* with X-API-Key
API->>Partner : Validate key + brand scoping
Partner-->>API : Allow/Deny
```

**Diagram sources**
- [docs/user-guides/admin-user-guide.md:158-198](file://docs/user-guides/admin-user-guide.md#L158-L198)
- [docs/api-contracts.md:174-321](file://docs/api-contracts.md#L174-L321)

**Section sources**
- [docs/user-guides/admin-user-guide.md:158-198](file://docs/user-guides/admin-user-guide.md#L158-L198)

### Segment Distribution API
Partners push a target member segment to NonCash for batch distribution. The request includes planID, members (phone and optional externalMemberID), and an optional callbackURL. Responses include distributionID and counts.

```mermaid
sequenceDiagram
participant Partner as "Loyalty App"
participant API as "Integration API"
participant Dist as "Distribution Service"
participant Wallet as "Voucher Wallet"
participant Events as "Event Emitter"
Partner->>API : POST /integration/distribute {planID, members, callbackURL}
API->>Dist : Validate plan + scope
Dist->>Wallet : Allocate vouchers to members
Wallet-->>Dist : Allocation results
Dist->>Events : Emit Distributed events
Events-->>Partner : Webhook delivery (HMAC-signed)
API-->>Partner : {distributionID, totalRequested, totalDistributed, skipped}
```

**Diagram sources**
- [docs/api-contracts.md:196-221](file://docs/api-contracts.md#L196-L221)
- [docs/user-guides/admin-user-guide.md:189-198](file://docs/user-guides/admin-user-guide.md#L189-L198)

**Section sources**
- [docs/api-contracts.md:196-221](file://docs/api-contracts.md#L196-L221)
- [docs/user-guides/admin-user-guide.md:189-198](file://docs/user-guides/admin-user-guide.md#L189-L198)

### Member Wallet and Event History APIs
Partners query a member’s current vouchers and lifecycle events. These enable in-app wallet rendering and analytics timelines.

```mermaid
sequenceDiagram
participant Partner as "Loyalty App"
participant API as "Integration API"
participant Wallet as "Voucher Wallet"
participant Events as "Event Store"
Partner->>API : GET /integration/member/{phone}/vouchers
API->>Wallet : Fetch vouchers by phone
Wallet-->>API : List of vouchers
API-->>Partner : Wallet response
Partner->>API : GET /integration/member/{phone}/events?from&to&type
API->>Events : Query events by phone/time/type
Events-->>API : Event list
API-->>Partner : Events response
```

**Diagram sources**
- [docs/api-contracts.md:223-276](file://docs/api-contracts.md#L223-L276)

**Section sources**
- [docs/api-contracts.md:223-276](file://docs/api-contracts.md#L223-L276)

### Webhook Lifecycle Events (Push)
NonCash pushes real-time events to the partner’s registered callback URL. Events include distributed, redeemed, transferred, expired, cancelled. Payloads are HMAC-SHA256 signed and retried with exponential backoff.

```mermaid
flowchart TD
Start(["Voucher Event Occurs"]) --> Record["Record event in outbox"]
Record --> Deliver{"Callback reachable?"}
Deliver --> |Yes| Send["Send webhook with HMAC signature"]
Deliver --> |No| Retry["Retry with exponential backoff (up to 5 attempts)"]
Send --> Ack{"Partner ack?"}
Ack --> |Yes| Done([Done])
Ack --> |No| Retry
Retry --> MaxAttempts{"Max attempts reached?"}
MaxAttempts --> |No| Deliver
MaxAttempts --> |Yes| Fail([Log failure for review])
```

**Diagram sources**
- [docs/user-guides/admin-user-guide.md:189-198](file://docs/user-guides/admin-user-guide.md#L189-L198)
- [docs/api-contracts.md:278-298](file://docs/api-contracts.md#L278-L298)

**Section sources**
- [docs/user-guides/admin-user-guide.md:189-198](file://docs/user-guides/admin-user-guide.md#L189-L198)
- [docs/api-contracts.md:278-298](file://docs/api-contracts.md#L278-L298)

### Campaign Performance Query API
Partners retrieve aggregated performance data for campaigns they sponsored, including issued, distributed, redeemed counts, redemption rate, and per-outlet breakdowns.

```mermaid
sequenceDiagram
participant Partner as "Loyalty App"
participant API as "Integration API"
participant Analytics as "Performance Aggregator"
Partner->>API : GET /integration/campaigns/{planID}/performance
API->>Analytics : Aggregate metrics by planID
Analytics-->>API : Metrics payload
API-->>Partner : {planID, brand, totals, rates, perOutlet}
```

**Diagram sources**
- [docs/api-contracts.md:300-321](file://docs/api-contracts.md#L300-L321)

**Section sources**
- [docs/api-contracts.md:300-321](file://docs/api-contracts.md#L300-L321)

### Conceptual Overview
Conceptually, Integration Partner Management enables NonCash to act as a neutral voucher engine while allowing any brand-operated Loyalty App to drive marketing and engagement through well-defined APIs and webhooks. The design ensures clear responsibility boundaries, minimal data duplication, and robust security.

[No sources needed since this section doesn't analyze specific source files]

## Dependency Analysis
Integration Partner Management depends on several core entities and services:
- Partner registry and API key storage for authentication and authorization.
- Distribution service for executing batch promotions and transfers.
- Voucher wallet for stateful voucher lifecycle management.
- Event emitter/outbox for reliable webhook delivery.
- Performance aggregator for campaign analytics.
- Multi-tenant scoping via BrandID to isolate data access.

```mermaid
graph LR
Partner["Partner Registry"] --> Auth["API Key Auth"]
Auth --> Dist["Distribution Service"]
Auth --> Wallet["Voucher Wallet"]
Auth --> Events["Event Emitter"]
Auth --> Perf["Performance Aggregator"]
Dist --> Wallet
Dist --> Events
Events --> Partner
Perf --> Partner
```

**Diagram sources**
- [docs/api-contracts.md:174-321](file://docs/api-contracts.md#L174-L321)
- [docs/user-guides/admin-user-guide.md:158-198](file://docs/user-guides/admin-user-guide.md#L158-L198)

**Section sources**
- [docs/api-contracts.md:174-321](file://docs/api-contracts.md#L174-L321)
- [docs/user-guides/admin-user-guide.md:158-198](file://docs/user-guides/admin-user-guide.md#L158-L198)

## Performance Considerations
- Use pagination and filtering on event history and ledger endpoints to avoid large payloads.
- Ensure webhook callbacks are idempotent and handle retries gracefully.
- Cache member wallet responses at the Loyalty App layer when appropriate to reduce repeated calls.
- Monitor webhook delivery success rates and adjust retry/backoff strategies if necessary.
- Keep segment distribution requests reasonably sized; consider batching for very large audiences.

[No sources needed since this section provides general guidance]

## Troubleshooting Guide
Common issues and resolutions for Integration Partner Management:

- 401 Unauthorized on /integration/*: Missing or invalid X-API-Key, or partner deactivated. Verify key, isActive flag, and brand associations.
- Missing webhook notifications: Callback URL unreachable or retries exhausted (max 5). Confirm HTTPS endpoint reachability and check webhook_deliveries table.
- Insufficient credits blocking distribution: When a Brand balance ≤ 0, generation, batch/partner distribution, and new self-purchase orders fail with InsufficientCredits. Top up credits to resume operations. POS redemption remains unaffected.
- Duplicate distribution due to non-idempotent calls: Ensure partners use idempotency keys or rely on system guarantees to prevent duplicate allocations.
- Performance degradation on event queries: Apply date range filters and type filters to limit result sets.

**Section sources**
- [docs/user-guides/admin-user-guide.md:310-324](file://docs/user-guides/admin-user-guide.md#L310-L324)
- [docs/api-contracts.md:169-171](file://docs/api-contracts.md#L169-L171)
- [docs/user-guides/brand-user-guide.md:285-295](file://docs/user-guides/brand-user-guide.md#L285-L295)

## Conclusion
Integration Partner Management equips NonCash to serve as a universal voucher engine for any brand-operated Loyalty App. Through secure partner onboarding, robust APIs, reliable webhooks, and actionable analytics, partners can execute targeted campaigns, deliver vouchers seamlessly into member wallets, and measure performance accurately. The design emphasizes multi-tenancy, security, and clear responsibility boundaries, enabling scalable adoption across diverse brands and markets.

[No sources needed since this section summarizes without analyzing specific files]

## Appendices

### API Endpoints Summary for Integration Partners
- Distribute to Segment: POST /integration/distribute
- Get Member Voucher Wallet: GET /integration/member/{phone}/vouchers
- Get Voucher Event History: GET /integration/member/{phone}/events
- Webhook: Voucher Lifecycle Events (push from NonCash to partner)
- Query Campaign Performance: GET /integration/campaigns/{planID}/performance

**Section sources**
- [docs/api-contracts.md:196-321](file://docs/api-contracts.md#L196-L321)

### Data Models Relevant to Integration
- VoucherPlanHeader and VoucherPlanDetail underpin distribution and redemption tracking.
- VoucherUsage records POS redemptions.
- VoucherDistribution tracks distribution methods (Sale, Promotion, Transfer).
- CreditLedgerEntry supports prepaid billing and consumption accounting.

**Section sources**
- [docs/data-models.md:9-113](file://docs/data-models.md#L9-L113)

### Epic Coverage for Integration Partner Management
Epic 6 covers external partner integration, including stories for partner onboarding, segment distribution, member wallet/event history, webhooks, and campaign performance.

**Section sources**
- [_bmad-output/planning-artifacts/epics.md:77-83](file://_bmad-output/planning-artifacts/epics.md#L77-L83)
- [_bmad-output/planning-artifacts/epics.md:332-354](file://_bmad-output/planning-artifacts/epics.md#L332-L354)