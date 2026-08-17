# Core Services Implementation

<cite>
**Referenced Files in This Document**
- [architecture.md](file://docs/architecture.md)
- [data-models.md](file://docs/data-models.md)
- [api-contracts.md](file://docs/api-contracts.md)
- [source-tree-analysis.md](file://docs/source-tree-analysis.md)
- [Key Functionalities.txt](file://Key Functionalities.txt)
- [description.txt](file://description.txt)
- [epics.md](file://_bmad-output/planning-artifacts/epics.md)
- [config.yaml](file://_bmad/core/config.yaml)
- [bmm-config.yaml](file://_bmad/bmm/config.yaml)
- [manifest.yaml](file://_bmad/_config/manifest.yaml)
- [PromotionService.cs](file://src/NonCash.Core/Services/PromotionService.cs)
- [IPromotionService.cs](file://src/NonCash.Core/Interfaces/IPromotionService.cs)
- [IntegrationController.cs](file://src/NonCash.API/Controllers/IntegrationController.cs)
- [Customer.cs](file://src/NonCash.Core/Entities/Customer.cs)
- [CustomerRepository.cs](file://src/NonCash.Infrastructure/Repositories/CustomerRepository.cs)
- [6-3-member-wallet-event-history-api.md](file://_bmad-output/implementation-artifacts/6-3-member-wallet-event-history-api.md)
- [6-5-campaign-performance-api.md](file://_bmad-output/implementation-artifacts/6-5-campaign-performance-api.md)
</cite>

## Update Summary
**Changes Made**
- Enhanced PromotionService with comprehensive member wallet functionality for Epic 6.3
- Added event history tracking across distributions, usages, and transfers
- Implemented upsert mechanism for customer email updates from integration payloads
- Added campaign performance tracking with outlet-level analytics for Epic 6.5
- Updated Integration API endpoints for wallet queries, event history, and campaign performance
- Enhanced distribution service with improved member management and notification capabilities

## Table of Contents
1. [Introduction](#introduction)
2. [Project Structure](#project-structure)
3. [Core Components](#core-components)
4. [Architecture Overview](#architecture-overview)
5. [Detailed Component Analysis](#detailed-component-analysis)
6. [Enhanced Promotion Service](#enhanced-promotion-service)
7. [Dependency Analysis](#dependency-analysis)
8. [Performance Considerations](#performance-considerations)
9. [Troubleshooting Guide](#troubleshooting-guide)
10. [Conclusion](#conclusion)
11. [Appendices](#appendices)

## Introduction
This document details the core services implementation for the NonCash SaaS platform, focusing on the five microservices: Planning Service, Approval Service, Distribution Service, Usage Service, and Identity/Tenant Service. It explains responsibilities, implementation patterns, invocation relationships, and integration points across the 3-layer architecture. The content has been updated to reflect recent enhancements including comprehensive member wallet functionality, event history tracking, customer email upsert mechanisms, and campaign performance analytics with outlet-level insights.

## Project Structure
The NonCash project follows a 3-layer SaaS architecture with a clear separation of concerns:
- Business Logic Layer (BLL): Implemented as microservices under NonCash.Core.Services.
- Data Access Layer (DAL): Implemented via NonCash.Infrastructure with Entity Framework Core and PostgreSQL.
- Presentation Layer: NonCash.Web (Blazor) for management staff and NonCash.API (RESTful) for POS integrations.
- Shared Contracts: NonCash.Shared for cross-cutting models and constants.

```mermaid
graph TB
subgraph "Presentation Layer"
WEB["NonCash.Web<br/>Blazor UI"]
API["NonCash.API<br/>RESTful POS API"]
end
subgraph "Business Logic Layer"
CORE["NonCash.Core<br/>Entities, Interfaces, Services, Specifications"]
end
subgraph "Data Access Layer"
INFRA["NonCash.Infrastructure<br/>DbContext, Repositories, Migrations"]
end
DB["PostgreSQL"]
WEB --> CORE
API --> CORE
CORE --> INFRA
INFRA --> DB
```

**Diagram sources**
- [source-tree-analysis.md:10-28](file://docs/source-tree-analysis.md#L10-L28)
- [architecture.md:17-34](file://docs/architecture.md#L17-L34)

**Section sources**
- [source-tree-analysis.md:1-50](file://docs/source-tree-analysis.md#L1-L50)
- [architecture.md:1-52](file://docs/architecture.md#L1-L52)

## Core Components
This section maps each microservice to its responsibilities, boundary definitions, and integration patterns with other layers and services.

- Planning Service
  - Responsibilities: Create and manage voucher plan headers and details, define budgets, targets, validity ranges, and outlet acceptance lists. Coordinate with Approval Service for routing and state transitions.
  - Boundaries: Operates on entities such as VoucherPlanHeader and VoucherPlanDetail; integrates with Identity/Tenant Service for brand and user context.
  - Integration: Invoked by Web UI for plan creation and by Approval Service for review workflows.

- Approval Service
  - Responsibilities: Route plans for review, manage approval state transitions, and record reviewer actions with notes and timestamps.
  - Boundaries: Maintains plan lifecycle state machine and collaborates with Identity/Tenant Service for role-based access checks.
  - Integration: Receives requests from Planning Service and emits notifications/state updates to Planning Service.

- Distribution Service
  - Responsibilities: Handle sales, promotions, and inbox deliveries; track distribution events and member ownership; support transfer workflows.
  - Boundaries: Works with VoucherDistribution and VoucherPlanDetail; enforces multi-tenancy via BrandID and Outlet constraints.
  - Integration: Consumes Planning/Approval outcomes and supports Member App interactions.

- Usage Service
  - Responsibilities: Orchestrates POS redemption via Verify/Lock/Redeem/Rollback workflows; maintains transactional integrity for usage events.
  - Boundaries: Manages VoucherPlanDetail usage status and VoucherUsage records; validates outlet permissions and plan publication dates.
  - Integration: Exposes REST endpoints consumed by POS systems; integrates with Identity/Tenant Service for API Key and JWT validation.

- Identity/Tenant Service
  - Responsibilities: Enforce RBAC for UserAccount roles, manage multi-tenancy via BrandID and Outlet scoping, and maintain Customer profiles.
  - Boundaries: Provides identity and tenant context to all services; validates JWT tokens and API keys for external integrations.
  - Integration: Supplies tenant-aware context to Planning/Approval/Distribution/Usage services; secures API endpoints.

**Section sources**
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [data-models.md:9-98](file://docs/data-models.md#L9-L98)
- [api-contracts.md:1-109](file://docs/api-contracts.md#L1-L109)
- [Key Functionalities.txt:7-167](file://Key Functionalities.txt#L7-L167)

## Architecture Overview
The microservices collaborate across layers with explicit data exchange and security controls:

```mermaid
graph TB
subgraph "External Integrations"
POS["POS Systems"]
MEMBER["Member App"]
LOYALTY["Loyalty Apps"]
end
subgraph "Presentation"
WEBUI["NonCash.Web<br/>Blazor"]
REST["NonCash.API<br/>Controllers"]
end
subgraph "Business Logic"
PLAN["Planning Service"]
APPROVAL["Approval Service"]
DIST["Distribution Service"]
USAGE["Usage Service"]
PROMO["Enhanced Promotion Service"]
IDT["Identity/Tenant Service"]
end
subgraph "Data Access"
REPO["Repositories"]
DB["PostgreSQL"]
end
POS --> REST
MEMBER --> REST
LOYALTY --> REST
WEBUI --> PLAN
WEBUI --> APPROVAL
WEBUI --> DIST
REST --> USAGE
REST --> PROMO
PLAN --> APPROVAL
APPROVAL --> DIST
DIST --> USAGE
PLAN --> IDT
APPROVAL --> IDT
DIST --> IDT
USAGE --> IDT
PROMO --> IDT
PLAN --> REPO
APPROVAL --> REPO
DIST --> REPO
USAGE --> REPO
PROMO --> REPO
IDT --> REPO
REPO --> DB
```

**Diagram sources**
- [architecture.md:9-34](file://docs/architecture.md#L9-L34)
- [source-tree-analysis.md:10-28](file://docs/source-tree-analysis.md#L10-L28)
- [data-models.md:9-98](file://docs/data-models.md#L9-L98)

## Detailed Component Analysis

### Planning Service
- Responsibilities
  - Create and update VoucherPlanHeader with budget, targets, validity ranges, and outlet lists.
  - Generate VoucherPlanDetail entries upon approval.
  - Track plan progress and maintain audit trails.
- Implementation patterns
  - Domain-driven design with Entities and Specifications.
  - Repository pattern for persistence via NonCash.Infrastructure.
  - Tenant scoping via BrandID from Identity/Tenant Service context.
- Invocation relationships
  - Called by Web UI for plan creation and updates.
  - Triggers Approval Service for review routing.
- Data exchange
  - Uses DTOs for plan creation/update; persists via repositories.
- Error handling
  - Validates input constraints and plan eligibility; returns structured errors to caller.
- Scalability and monitoring
  - Stateless service; scale out horizontally; monitor plan throughput and approval latency.

```mermaid
flowchart TD
Start(["Plan Creation Request"]) --> Validate["Validate Plan Inputs"]
Validate --> Valid{"Valid?"}
Valid --> |No| Err["Return Validation Error"]
Valid --> |Yes| Persist["Persist VoucherPlanHeader"]
Persist --> TriggerApproval["Trigger Approval Workflow"]
TriggerApproval --> End(["Plan Created"])
Err --> End
```

**Diagram sources**
- [Key Functionalities.txt:7-86](file://Key Functionalities.txt#L7-L86)
- [data-models.md:9-43](file://docs/data-models.md#L9-L43)

**Section sources**
- [Key Functionalities.txt:7-86](file://Key Functionalities.txt#L7-L86)
- [data-models.md:9-43](file://docs/data-models.md#L9-L43)
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)

### Approval Service
- Responsibilities
  - Route plans for review, enforce single-level approval process, and record reviewer actions.
  - Update plan state to Approved/Rejected and adjust publish date as needed.
- Implementation patterns
  - State machine for plan lifecycle; repository-backed persistence.
  - Role-based access checks via Identity/Tenant Service.
- Invocation relationships
  - Receives requests from Planning Service; notifies downstream services on state change.
- Data exchange
  - Accepts review decisions and returns updated plan state.
- Error handling
  - Rejects invalid reviewers or out-of-scope brands; logs review actions.
- Scalability and monitoring
  - Stateless; scale out; monitor approval rate and reviewer response time.

```mermaid
sequenceDiagram
participant Planner as "Planning Service"
participant Approver as "Approval Service"
participant IDT as "Identity/Tenant Service"
Planner->>Approver : "Submit Plan for Review"
Approver->>IDT : "Validate Reviewer Role and Brand Scope"
IDT-->>Approver : "Authorization Result"
Approver->>Approver : "Update Plan State"
Approver-->>Planner : "Approval Outcome"
```

**Diagram sources**
- [architecture.md:20-26](file://docs/architecture.md#L20-L26)
- [Key Functionalities.txt:70-86](file://Key Functionalities.txt#L70-L86)

**Section sources**
- [Key Functionalities.txt:70-86](file://Key Functionalities.txt#L70-L86)
- [architecture.md:20-26](file://docs/architecture.md#L20-L26)

### Distribution Service
- Responsibilities
  - Process sales, promotions, and inbox deliveries.
  - Track distribution events and member ownership.
  - Support transfer workflows between members.
- Implementation patterns
  - Event-driven updates to VoucherPlanDetail and VoucherDistribution.
  - Multi-tenancy enforcement via BrandID and Outlet constraints.
- Invocation relationships
  - Consumes approved plan outcomes; supports Member App queries.
- Data exchange
  - Creates distribution records and updates ownership metadata.
- Error handling
  - Validates member existence and transfer eligibility; handles batch promotion imports.
- Scalability and monitoring
  - Stateless; scale out; monitor distribution throughput and transfer confirmations.

```mermaid
flowchart TD
Start(["Distribution Request"]) --> Type{"Type: Sale/Promotion/Transfer"}
Type --> |Sale| CreateOrder["Create Order and Payment Records"]
Type --> |Promotion| BatchImport["Import Recipient List"]
Type --> |Transfer| Confirm["Require Recipient Confirmation"]
CreateOrder --> LogDist["Log VoucherDistribution"]
BatchImport --> LogDist
Confirm --> LogDist
LogDist --> End(["Distribution Recorded"])
```

**Diagram sources**
- [Key Functionalities.txt:87-134](file://Key Functionalities.txt#L87-L134)
- [data-models.md:44-62](file://docs/data-models.md#L44-L62)

**Section sources**
- [Key Functionalities.txt:87-134](file://Key Functionalities.txt#L87-L134)
- [data-models.md:44-62](file://docs/data-models.md#L44-L62)

### Usage Service
- Responsibilities
  - POS redemption orchestration: Verify, Lock, Redeem, and Rollback.
  - Maintain transactional integrity for usage events.
- Implementation patterns
  - RESTful controllers for POS endpoints; repository-backed usage tracking.
  - Dynamic voucher code validation aligned with security requirements.
- Invocation relationships
  - POS systems call Usage Service endpoints; returns lock identifiers and status.
- Data exchange
  - Uses API DTOs for verify/lock/redeem/rollback; writes VoucherUsage records.
- Error handling
  - Validates outlet permissions, plan publish date, and lock ownership; supports rollback on failure.
- Scalability and monitoring
  - Stateless; scale out; monitor redemption latency and lock timeouts.

```mermaid
sequenceDiagram
participant POS as "POS System"
participant Usage as "Usage Service"
participant IDT as "Identity/Tenant Service"
POS->>Usage : "POST /pos/verify"
Usage->>IDT : "Validate API Key and Outlet Scope"
IDT-->>Usage : "Validation Result"
Usage-->>POS : "Verification Response"
POS->>Usage : "POST /pos/lock"
Usage-->>POS : "LockID"
POS->>Usage : "POST /pos/redeem"
Usage-->>POS : "Success"
POS->>Usage : "POST /pos/rollback"
Usage-->>POS : "Released"
```

**Diagram sources**
- [api-contracts.md:10-88](file://docs/api-contracts.md#L10-L88)
- [data-models.md:46-54](file://docs/data-models.md#L46-L54)
- [architecture.md:36-40](file://docs/architecture.md#L36-L40)

**Section sources**
- [api-contracts.md:10-88](file://docs/api-contracts.md#L10-L88)
- [data-models.md:46-54](file://docs/data-models.md#L46-L54)
- [architecture.md:36-40](file://docs/architecture.md#L36-L40)

### Identity/Tenant Service
- Responsibilities
  - RBAC for UserAccount roles (Admin, Planner, Approver).
  - Multi-tenancy via BrandID and Outlet scoping.
  - Profile management for Customer and JWT token issuance.
- Implementation patterns
  - Centralized identity provider; integrates with Planning/Approval/Distribution/Usage services.
- Invocation relationships
  - All services call into Identity/Tenant Service for authorization and tenant context.
- Data exchange
  - Provides user roles, brand/outlet scopes, and customer profiles.
- Error handling
  - Rejects unauthorized access attempts; logs security events.
- Scalability and monitoring
  - Stateless; scale out; monitor auth failures and token validation rates.

```mermaid
classDiagram
class IdentityTenantService {
+ValidateJWT(token)
+ValidateAPIKey(key)
+GetUserRoles(userID)
+GetBrandScope(userID)
+GetOutletScope(userID)
+GetCustomerProfile(memberID)
}
class PlanningService {
+CreatePlan(planDTO)
}
class ApprovalService {
+ReviewPlan(reviewDTO)
}
class DistributionService {
+DistributeVouchers(distributionDTO)
}
class UsageService {
+Verify(voucherCode, outletID)
+Lock(voucherCode, outletID)
+Redeem(lockID, transactionID)
+Rollback(lockID)
}
PlanningService --> IdentityTenantService : "authorizes and scopes"
ApprovalService --> IdentityTenantService : "authorizes and scopes"
DistributionService --> IdentityTenantService : "authorizes and scopes"
UsageService --> IdentityTenantService : "validates API Key/JWT"
```

**Diagram sources**
- [architecture.md:20-26](file://docs/architecture.md#L20-L26)
- [data-models.md:63-98](file://docs/data-models.md#L63-L98)

**Section sources**
- [architecture.md:20-26](file://docs/architecture.md#L20-L26)
- [data-models.md:63-98](file://docs/data-models.md#L63-L98)

## Enhanced Promotion Service

**Updated** The Promotion Service has been significantly enhanced with comprehensive member wallet functionality, event history tracking, and campaign performance analytics.

### Member Wallet Functionality (Epic 6.3)
The enhanced PromotionService now provides comprehensive wallet management capabilities for loyalty app partners:

- **Wallet Query API**: `GET /integration/member/{phone}/vouchers` returns all vouchers for a member across authorized brands with display fields including images, icons, and branding information.
- **Event History Tracking**: `GET /integration/member/{phone}/events` provides unified event history aggregating distributions, redemptions, and transfers chronologically.
- **Brand Scoping**: All wallet queries are scoped to partner-authorized brands, ensuring data isolation and security.

### Customer Email Upsert Mechanism
The distribution service now includes intelligent email management:

- **Upsert Logic**: When processing integration payloads, existing customer emails are updated if not already present in the system.
- **Phone Normalization**: Phone numbers are normalized before email mapping to ensure accurate matching.
- **Fallback Handling**: If no email is provided in the payload, the system continues without email notifications.

### Campaign Performance Analytics (Epic 6.5)
New campaign performance tracking provides outlet-level analytics:

- **Performance Metrics**: Redemption rates, total distributed/redeemed counts, and redemption value calculations.
- **Outlet Breakdown**: Per-outlet analytics showing redemption counts and total redeemed values.
- **Authorization Enforcement**: Partners can only query campaigns for brands they're authorized to access.

```mermaid
sequenceDiagram
participant LoyaltyApp as "Loyalty App"
participant IntegrationAPI as "Integration Controller"
participant PromoService as "Promotion Service"
participant CustomerRepo as "Customer Repository"
participant PlanRepo as "Plan Repository"
LoyaltyApp->>IntegrationAPI : GET /integration/member/{phone}/vouchers
IntegrationAPI->>PromoService : GetMemberVouchersByPhoneAsync(phone, brandIds)
PromoService->>CustomerRepo : GetByPhoneNumberAsync(normalized phone)
CustomerRepo-->>PromoService : Customer entity
PromoService->>PlanRepo : Load member's vouchers with plan details
PlanRepo-->>PromoService : Voucher details with display fields
PromoService-->>IntegrationAPI : Wallet items with branding
IntegrationAPI-->>LoyaltyApp : JSON wallet response
Note over LoyaltyApp,LoyaltyApp : Similar flow for events and campaign performance APIs
```

**Diagram sources**
- [IntegrationController.cs:112-164](file://src/NonCash.API/Controllers/IntegrationController.cs#L112-L164)
- [PromotionService.cs:236-273](file://src/NonCash.Core/Services/PromotionService.cs#L236-L273)
- [IPromotionService.cs:15-24](file://src/NonCash.Core/Interfaces/IPromotionService.cs#L15-L24)

### Enhanced Distribution Processing
The distribution workflow has been improved with better member management:

- **Email Resolution**: Intelligent email resolution from integration payloads using phone-to-email mapping.
- **Member Account Creation**: Automatic member account creation for new customers with proper initialization.
- **Notification Integration**: Email notifications sent to recipients when email addresses are available.

```mermaid
flowchart TD
Start(["Integration Distribution Request"]) --> ParseMembers["Parse Members Array"]
ParseMembers --> BuildMapping["Build Phone→Email Mapping"]
BuildMapping --> Distribute["Call PromotionService.DistributeAsync"]
Distribute --> CheckCustomers["Check/Create Customers"]
CheckCustomers --> UpsertEmail{"Email Available & Not Set?"}
UpsertEmail --> |Yes| UpdateEmail["Update Customer Email"]
UpsertEmail --> |No| SkipEmail["Skip Email Update"]
UpdateEmail --> EnsureMember["Ensure Member Account"]
SkipEmail --> EnsureMember
EnsureMember --> AllocateVouchers["Allocate Vouchers"]
AllocateVouchers --> SendNotifications["Send Email Notifications"]
SendNotifications --> ReturnResult["Return Distribution Result"]
```

**Diagram sources**
- [PromotionService.cs:43-216](file://src/NonCash.Core/Services/PromotionService.cs#L43-L216)
- [IntegrationController.cs:43-105](file://src/NonCash.API/Controllers/IntegrationController.cs#L43-L105)

**Section sources**
- [PromotionService.cs:6-413](file://src/NonCash.Core/Services/PromotionService.cs#L6-L413)
- [IPromotionService.cs:1-79](file://src/NonCash.Core/Interfaces/IPromotionService.cs#L1-L79)
- [IntegrationController.cs:1-234](file://src/NonCash.API/Controllers/IntegrationController.cs#L1-L234)
- [6-3-member-wallet-event-history-api.md:1-33](file://_bmad-output/implementation-artifacts/6-3-member-wallet-event-history-api.md#L1-L33)
- [6-5-campaign-performance-api.md:1-63](file://_bmad-output/implementation-artifacts/6-5-campaign-performance-api.md#L1-L63)

## Dependency Analysis
The services exhibit low coupling and high cohesion, with clear dependency directions:

```mermaid
graph LR
IDT["Identity/Tenant Service"] --> PLAN["Planning Service"]
IDT --> APPROVAL["Approval Service"]
IDT --> DIST["Distribution Service"]
IDT --> USAGE["Usage Service"]
IDT --> PROMO["Enhanced Promotion Service"]
PLAN --> APPROVAL
APPROVAL --> DIST
DIST --> USAGE
PLAN --> REPO["Repositories"]
APPROVAL --> REPO
DIST --> REPO
USAGE --> REPO
PROMO --> REPO
IDT --> REPO
REPO --> DB["PostgreSQL"]
```

**Diagram sources**
- [architecture.md:17-34](file://docs/architecture.md#L17-L34)
- [source-tree-analysis.md:10-28](file://docs/source-tree-analysis.md#L10-L28)

**Section sources**
- [architecture.md:17-34](file://docs/architecture.md#L17-L34)
- [source-tree-analysis.md:10-28](file://docs/source-tree-analysis.md#L10-L28)

## Performance Considerations
- Horizontal scaling
  - All services are stateless and can be scaled independently based on workload.
- Data consistency
  - Use database transactions for POS usage operations to ensure atomicity.
- Caching
  - Cache frequently accessed plan and outlet metadata; invalidate on plan changes.
- Monitoring
  - Track service latency, error rates, and throughput; instrument cross-service calls.
- Resilience
  - Implement circuit breakers and retries for inter-service calls; use idempotent operations where possible.
- **Enhanced Performance Features**
  - Member wallet queries optimized with efficient database joins and filtering.
  - Event history aggregation uses chronological sorting with configurable limits.
  - Campaign performance metrics calculated with grouped outlet analytics.

## Troubleshooting Guide
- Authorization failures
  - Verify JWT/API Key validity and tenant scope; check user roles and brand/outlet associations.
- POS redemption issues
  - Confirm plan publish date, outlet permissions, and lock ownership; handle rollback on failures.
- Distribution errors
  - Validate member existence and transfer eligibility; review batch import logs.
- **Enhanced Troubleshooting**
  - **Wallet Query Issues**: Check partner brand authorization and phone number normalization.
  - **Event History Gaps**: Verify distribution, usage, and transfer records exist for the member.
  - **Campaign Performance Discrepancies**: Ensure outlet IDs are properly set during redemption processes.
  - **Email Upsert Failures**: Validate phone number format and email address syntax in integration payloads.
- Audit and tracing
  - Enable structured logging and correlation IDs for end-to-end tracing across services.

**Section sources**
- [architecture.md:36-40](file://docs/architecture.md#L36-L40)
- [Key Functionalities.txt:135-156](file://Key Functionalities.txt#L135-L156)

## Conclusion
The NonCash platform's microservices are designed for scalability, security, and maintainability within a 3-layer SaaS architecture. The enhanced Promotion Service now provides comprehensive member wallet functionality, event history tracking, and campaign performance analytics with outlet-level insights. Planning, Approval, Distribution, Usage, and Identity/Tenant Services each encapsulate distinct responsibilities, communicate via well-defined contracts, and integrate with PostgreSQL through a robust repository pattern. By adhering to the documented boundaries, data models, and API contracts, teams can implement resilient, observable, and extensible solutions.

## Appendices
- Security and compliance
  - Multi-tenancy enforced via BrandID; dynamic voucher codes mitigate reuse; API Key and JWT used for external integrations.
- Operational guidelines
  - Follow 3-layer architecture; keep services stateless; leverage shared models in NonCash.Shared; monitor and alert on SLIs/SLOs.
- **Enhanced Features Documentation**
  - Member wallet queries support brand-scoped voucher retrieval with display field optimization.
  - Event history provides unified timeline of all member interactions across distributions, usages, and transfers.
  - Campaign performance analytics enable ROI measurement with outlet-level redemption tracking.
  - Customer email upsert mechanism ensures data consistency across integration touchpoints.

**Section sources**
- [description.txt:22-31](file://description.txt#L22-L31)
- [epics.md:26-37](file://_bmad-output/planning-artifacts/epics.md#L26-L37)
- [manifest.yaml:1-25](file://_bmad/_config/manifest.yaml#L1-L25)
- [bmm-config.yaml:1-17](file://_bmad/bmm/config.yaml#L1-L17)
- [config.yaml:1-10](file://_bmad/core/config.yaml#L1-L10)