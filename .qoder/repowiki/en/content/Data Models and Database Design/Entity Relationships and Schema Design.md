</think>

Based on my comprehensive analysis of the codebase, I can now update the documentation to reflect the password reset token support and improved email-based authentication workflows. Here's the updated documentation:

# Entity Relationships and Schema Design

<cite>
**Referenced Files in This Document**
- [data-models.md](file://docs/data-models.md)
- [architecture.md](file://docs/architecture.md)
- [source-tree-analysis.md](file://docs/source-tree-analysis.md)
- [Key Functionalities.txt](file://Key Functionalities.txt)
- [implementation-readiness-report-2026-04-17.md](file://_bmad-output/planning-artifacts/implementation-readiness-report-2026-04-17.md)
- [WelcomeGrantPolicy.cs](file://src/NonCash.Core/Entities/WelcomeGrantPolicy.cs)
- [CreditBatch.cs](file://src/NonCash.Core/Entities/CreditBatch.cs)
- [UserAccount.cs](file://src/NonCash.Core/Entities/UserAccount.cs)
- [Business.cs](file://src/NonCash.Core/Entities/Business.cs)
- [UserAccountConfiguration.cs](file://src/NonCash.Infrastructure/Data/Configurations/UserAccountConfiguration.cs)
- [EmailNotificationService.cs](file://src/NonCash.Infrastructure/Services/EmailNotificationService.cs)
- [AuthService.cs](file://src/NonCash.Core/Services/AuthService.cs)
- [AuthController.cs](file://src/NonCash.API/Controllers/AuthController.cs)
- [PasswordReset.html](file://src/NonCash.Infrastructure/EmailTemplates/PasswordReset.html)
- [INotificationService.cs](file://src/NonCash.Core/Interfaces/INotificationService.cs)
- [AuthDtos.cs](file://src/NonCash.API/DTOs/AuthDtos.cs)
- [AddPasswordResetToken Migration](file://src/NonCash.Infrastructure/Migrations/20260814114913_AddPasswordResetToken.cs)
- [SplitWelcomePolicy Migration](file://src/NonCash.Infrastructure/Migrations/20260814050918_SplitWelcomePolicy.cs)
- [AddEmailToUserAccount Migration](file://src/NonCash.Infrastructure/Migrations/20260814090258_AddEmailToUserAccount.cs)
- [Migration SQL Script](file://tools/migration-split-welcome-policy.sql)
</cite>

## Update Summary
**Changes Made**
- Added new WelcomeGrantPolicy entity and table with business_id foreign key relationship
- Updated CreditBatch entity to include welcome_policy_id column establishing lineage between credit batches and governing policies
- **Added email column support to UserAccount entity via migration '20260814090258_AddEmailToUserAccount'** - The user_accounts table now includes an email field (varchar(255)) enabling email-based authentication, notifications, and account recovery features
- **Enhanced UserAccount entity with password reset token support** - Added PasswordResetToken and PasswordResetTokenExpiry fields for secure password reset functionality
- **Implemented comprehensive email-based authentication workflows** - Complete forgot password and reset password functionality with secure token generation and validation
- Enhanced entity relationship diagrams to show new policy-batch relationships and enhanced authentication capabilities
- Updated multi-tenancy section to reflect business-level policy isolation and email-based communication capabilities

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
This document provides architectural documentation for the entity relationship model and database schema design in NonCash. It details the relational schema, primary keys, foreign key constraints, and table relationships among VoucherPlanHeader, VoucherPlanDetail, VoucherUsage, VoucherDistribution, Brand, Outlet, UserAccount, Customer, and the newly introduced WelcomeGrantPolicy entities. It also explains the three-tier architecture implications for data modeling, including multi-tenancy with BrandID isolation and business-level policy management, and documents the database design patterns used, particularly the repository pattern implementation with Entity Framework Core. Finally, it covers indexing strategies, performance considerations, data access patterns, schema evolution, migration strategies, and version management approaches.

## Project Structure
The NonCash project is structured around a 3-tier architecture with clear separation of concerns:
- Frontend (Blazor): Management portal for business users.
- Backend (Microservices in .NET Core): Business logic layer implementing microservices for planning, approval, distribution, usage, and identity/tenant management.
- Data Access Layer (Infrastructure): PostgreSQL-backed persistence using Entity Framework Core with the repository pattern.

```mermaid
graph TB
subgraph "NonCash Application"
UI["NonCash.Web<br/>Blazor UI"]
API["NonCash.API<br/>RESTful POS Integration"]
BLL["NonCash.Core<br/>Microservices & Domain"]
DAL["NonCash.Infrastructure<br/>EF Core + PostgreSQL"]
SHARED["NonCash.Shared<br/>Shared Models"]
end
UI --> BLL
API --> BLL
BLL --> DAL
UI -. uses .-> SHARED
API -. uses .-> SHARED
BLL -. uses .-> SHARED
```

**Diagram sources**
- [source-tree-analysis.md:7-34](file://docs/source-tree-analysis.md#L7-L34)

**Section sources**
- [source-tree-analysis.md:1-50](file://docs/source-tree-analysis.md#L1-L50)
- [architecture.md:5-52](file://docs/architecture.md#L5-L52)

## Core Components
This section defines the core entities and their attributes, focusing on primary keys, foreign keys, and relationships. All entities are modeled as relational tables with GUID primary keys and explicit foreign key relationships.

### Core Voucher Entities
- VoucherPlanHeader
  - PK: ID
  - FK: CreatorID → UserAccount.UserID, ApproverID → UserAccount.UserID, BrandID → Brand.BrandID
  - Attributes include plan metadata, approval status, target quantities, budget, validity ranges, and outlet acceptance lists.

- VoucherPlanDetail
  - PK: ID
  - FK: ParentID → VoucherPlanHeader.ID
  - Attributes include serial number, dynamic voucher code, optional owner assignment, usage status, and used date.

- VoucherUsage
  - PK: ID
  - FK: VoucherID → VoucherPlanDetail.ID
  - Attributes include POS identifier, transaction linkage, usage timestamp, and amount applied.

- VoucherDistribution
  - PK: ID
  - FK: VoucherID → VoucherPlanDetail.ID, MemberID → Customer.CustomerID
  - Attributes include distribution method and timestamp.

### Tenant and User Management Entities
- Brand
  - PK: BrandID
  - FK: BusinessId → Business.BusinessID
  - Attributes include branding details and status.

- Outlet
  - PK: OutletID
  - FK: BrandID → Brand.BrandID
  - Attributes include location details and status.

- UserAccount
  - PK: UserID
  - FK: BrandID → Brand.BrandID (nullable for system super-admins)
  - **Updated**: Now includes Email field (varchar(255), nullable) enabling email-based authentication, notifications, and account recovery features
  - **Enhanced**: Added PasswordResetToken (varchar(255), nullable) and PasswordResetTokenExpiry (datetime, nullable) for secure password reset functionality
  - Attributes include credentials, role, status, and email contact information.

- Customer
  - PK: CustomerID
  - Attributes include contact details and status.

### New Welcome Grant Policy System
- WelcomeGrantPolicy
  - PK: ID
  - FK: BusinessId → Business.BusinessID
  - Attributes include name, welcome credits amount, expiry months, effective period, active status, and creator.
  - Represents per-business commercial terms that grant free credits to new brands under that business.

- CreditBatch (Updated)
  - PK: ID
  - FK: BrandId → Brand.BrandID, PolicyId → CreditPricingPolicy.PolicyID, WelcomePolicyId → WelcomeGrantPolicy.ID
  - Now includes welcome_policy_id column establishing lineage between credit batches and their governing welcome policies.

These definitions are derived from the data models documentation and align with the three-tier architecture's multi-tenancy strategy enforced via BrandID and BusinessID isolation.

**Section sources**
- [data-models.md:9-98](file://docs/data-models.md#L9-L98)
- [Key Functionalities.txt:7-166](file://Key Functionalities.txt#L7-L166)
- [WelcomeGrantPolicy.cs:11-36](file://src/NonCash.Core/Entities/WelcomeGrantPolicy.cs#L11-L36)
- [CreditBatch.cs:27-74](file://src/NonCash.Core/Entities/CreditBatch.cs#L27-L74)
- [UserAccount.cs:20-37](file://src/NonCash.Core/Entities/UserAccount.cs#L20-L37)

## Architecture Overview
NonCash employs a 3-layer SaaS architecture:
- Frontend (Blazor): Provides management dashboards and user interactions.
- Business Logic Layer (Microservices): Encapsulates business capabilities and orchestrates workflows across planning, approval, distribution, usage, and identity/tenant management.
- Data Access Layer (Infrastructure): Implements repository pattern with Entity Framework Core over PostgreSQL, ensuring decoupling and transactional consistency, especially for POS usage.

Multi-tenancy is enforced by isolating data per BrandID across entities such as UserAccount, Outlet, and VoucherPlanHeader, while welcome grant policies operate at the Business level to provide uniform commercial terms across all brands under a business.

```mermaid
graph TB
subgraph "Layer: Frontend (Blazor)"
WEB["NonCash.Web"]
end
subgraph "Layer: Business Logic (Microservices)"
CORE["NonCash.Core"]
end
subgraph "Layer: Data Access (EF Core + PostgreSQL)"
INFRA["NonCash.Infrastructure"]
REPO["Repository Pattern"]
DB["PostgreSQL"]
end
WEB --> CORE
CORE --> INFRA
INFRA --> REPO
REPO --> DB
```

**Diagram sources**
- [architecture.md:5-52](file://docs/architecture.md#L5-L52)
- [source-tree-analysis.md:7-34](file://docs/source-tree-analysis.md#L7-L34)

**Section sources**
- [architecture.md:5-52](file://docs/architecture.md#L5-L52)
- [source-tree-analysis.md:1-50](file://docs/source-tree-analysis.md#L1-L50)

## Detailed Component Analysis
This section focuses on the entity relationship model, constraints, and data access patterns.

### Relational Schema and Constraints
The following diagram illustrates the relational schema with primary keys and foreign key relationships among the core entities, including the new welcome grant policy system and enhanced authentication capabilities.

```mermaid
erDiagram
BRAND {
uuid BrandID PK
uuid BusinessId FK
string Name
string TaxCode
string ContactEmail
enum Status
}
OUTLET {
uuid OutletID PK
uuid BrandID FK
string Name
string Address
enum Status
}
USERACCOUNT {
uuid UserID PK
uuid BrandID FK
string Username
string PasswordHash
string FullName
string Email
enum Role
enum Status
string PasswordResetToken
datetime PasswordResetTokenExpiry
}
CUSTOMER {
uuid CustomerID PK
string PhoneNumber
string FullName
string Email
enum Status
}
BUSINESS {
uuid BusinessID PK
string BusinessName
string TaxCode
string Address
string ContactEmail
string PhoneNumber
bool IsActive
}
WELCOMEGRANTPOLICY {
uuid ID PK
uuid BusinessId FK
string Name
int WelcomeCredits
int WelcomeCreditExpiryMonths
datetime EffectiveFrom
datetime EffectiveTo
bool IsActive
uuid CreatedBy
}
CREDITBATCH {
uuid ID PK
uuid BrandId FK
uuid PolicyId FK
uuid WelcomePolicyId FK
enum BatchType
int OriginalAmount
int RemainingAmount
decimal PricePerCreditVnd
decimal TotalPaidVnd
datetime ExpiresAt
}
VOICEPLANHEADER {
uuid ID PK
datetime PlanDate
uuid CreatorID FK
uuid ApproverID FK
uuid BrandID FK
enum VoucherType
string ImageURL
string IconURL
enum ValueType
decimal FaceValue
decimal NetValue
datetime ExpiryDate
datetime PublishDate
json SalesRange
daterange TimeRange
int TargetQuantity
decimal Budget
int TargetDistributed
int TargetUsed
enum ApprovalStatus
}
VOICEPLANDetail {
uuid ID PK
uuid ParentID FK
string SerialNo
string VoucherCode
uuid MemberID FK
enum UsageStatus
datetime UsedDate
}
VOICEUSAGE {
uuid ID PK
uuid VoucherID FK
string POSID
string TransactionID
datetime UsageDate
decimal AmountUsed
}
VOICEDISTRIBUTION {
uuid ID PK
uuid VoucherID FK
uuid MemberID FK
enum Method
datetime DistributionDate
}
BUSINESS ||--o{ BRAND : "owns"
BUSINESS ||--o{ WELCOMEGRANTPOLICY : "has_policies"
BRAND ||--o{ OUTLET : "operates"
BRAND ||--o{ USERACCOUNT : "employs"
BRAND ||--o{ VOICEPLANHEADER : "creates"
BRAND ||--o{ CREDITBATCH : "receives_credits"
WELCOMEGRANTPOLICY ||--o{ CREDITBATCH : "generates"
VOICEPLANHEADER ||--o{ VOICEPLANDetail : "generates"
VOICEPLANDetail ||--o{ VOICEUSAGE : "consumed_by"
VOICEPLANDetail ||--o{ VOICEDISTRIBUTION : "distributed_to"
CUSTOMER ||--o{ VOICEDISTRIBUTION : "receives"
```

**Diagram sources**
- [data-models.md:9-98](file://docs/data-models.md#L9-L98)
- [Key Functionalities.txt:7-166](file://Key Functionalities.txt#L7-L166)
- [WelcomeGrantPolicy.cs:11-36](file://src/NonCash.Core/Entities/WelcomeGrantPolicy.cs#L11-L36)
- [CreditBatch.cs:27-74](file://src/NonCash.Core/Entities/CreditBatch.cs#L27-L74)
- [UserAccount.cs:20-37](file://src/NonCash.Core/Entities/UserAccount.cs#L20-L37)

### Repository Pattern Implementation with Entity Framework Core
The Data Access Layer implements the repository pattern to abstract persistence concerns:
- Interfaces define contracts for data operations.
- Implementations encapsulate EF Core queries, projections, and transactions.
- Dependency injection binds interfaces to implementations, enabling testability and flexibility.

This pattern supports schema evolution and technology changes without affecting the Business Logic Layer.

**Section sources**
- [architecture.md:28-35](file://docs/architecture.md#L28-L35)
- [source-tree-analysis.md:15-18](file://docs/source-tree-analysis.md#L15-L18)

### Multi-Tenancy with BrandID and BusinessID Isolation
Multi-tenancy is achieved through dual-level isolation:
- **Brand Level**: Core operational entities isolated by BrandID (UserAccount, Outlet, VoucherPlanHeader, CreditBatch).
- **Business Level**: Commercial policies isolated by BusinessID (WelcomeGrantPolicy), allowing uniform terms across all brands under a business.

Access control and filtering enforce that users and outlets operate within their tenant boundaries, preventing cross-tenant data leakage while enabling business-level policy management.

**Section sources**
- [architecture.md:36-41](file://docs/architecture.md#L36-L41)
- [data-models.md:63-98](file://docs/data-models.md#L63-L98)
- [WelcomeGrantPolicy.cs:3-9](file://src/NonCash.Core/Entities/WelcomeGrantPolicy.cs#L3-L9)

### Data Access Patterns
- CRUD Abstraction: Repositories encapsulate create, read, update, delete operations.
- Query Composition: LINQ queries leverage navigation properties and projections to minimize round-trips.
- Transactions: Critical workflows (e.g., POS usage) are wrapped in transactions to ensure atomicity.
- Projection and Pagination: Selective field retrieval and paging improve performance for reporting and dashboards.
- Policy Resolution: Welcome policy resolution follows most recent active policy per business with fallback to configuration defaults.
- **Enhanced Email-Based Communication**: UserAccount email field enables email-based authentication, notifications, and account recovery workflows with secure token management.

**Section sources**
- [architecture.md:28-35](file://docs/architecture.md#L28-L35)
- [Key Functionalities.txt:135-156](file://Key Functionalities.txt#L135-L156)
- [WelcomePolicyService.cs:25-39](file://src/NonCash.Infrastructure/Services/WelcomePolicyService.cs#L25-L39)
- [EmailNotificationService.cs:40-63](file://src/NonCash.Infrastructure/Services/EmailNotificationService.cs#L40-L63)

### Welcome Grant Policy Orchestration (Sequence)
The following sequence illustrates welcome grant policy resolution and credit batch creation workflow.

```mermaid
sequenceDiagram
participant Brand as "New Brand Activation"
participant API as "NonCash.API"
participant SVC as "Welcome Policy Service"
participant REPO as "Repository"
participant DB as "PostgreSQL"
Brand->>API : "Activate New Brand"
API->>SVC : "Resolve Welcome Policy"
SVC->>REPO : "Find Active Policy for Business"
REPO->>DB : "SELECT * FROM welcome_grant_policies WHERE business_id = ? AND is_active = true AND effective_from <= NOW() ORDER BY effective_from DESC"
DB-->>REPO : "Most recent active policy"
REPO-->>SVC : "WelcomeGrantPolicy entity"
SVC->>REPO : "Create CreditBatch (WelcomeGrant type)"
REPO->>DB : "INSERT INTO credit_batches (brand_id, welcome_policy_id, batch_type, original_amount)"
DB-->>REPO : "Batch created"
REPO-->>SVC : "CreditBatch entity"
SVC-->>API : "Welcome credits granted"
API-->>Brand : "Activation complete with credits"
```

**Diagram sources**
- [WelcomePolicyService.cs:25-39](file://src/NonCash.Infrastructure/Services/WelcomePolicyService.cs#L25-L39)
- [SplitWelcomePolicy Migration:14-72](file://src/NonCash.Infrastructure/Migrations/20260814050918_SplitWelcomePolicy.cs#L14-L72)

### Enhanced Email-Based Authentication and Account Recovery System
The email column addition and password reset token support enable comprehensive email-based functionality:

- **Authentication**: Email validation and verification for user registration and login processes
- **Notifications**: Automated email notifications for account activities, voucher distributions, and business events
- **Account Recovery**: Secure password reset workflow with time-limited tokens and email delivery
- **Admin Notifications**: Email alerts for administrative actions and system events

```mermaid
sequenceDiagram
participant User as "User Account"
participant Auth as "Authentication Service"
participant Email as "Email Notification Service"
participant DB as "PostgreSQL"
User->>Auth : "Forgot Password Request"
Auth->>DB : "Query UserAccount by Username/Email"
DB-->>Auth : "UserAccount with Email"
Auth->>Auth : "Generate Secure Token (32 bytes)"
Auth->>DB : "Update PasswordResetToken + Expiry"
Auth->>Email : "Send Password Reset Email"
Email->>DB : "Retrieve Admin Emails"
DB-->>Email : "Active Admin Accounts"
Email-->>User : "Password Reset Email with Token"
User->>Auth : "Reset Password with Token"
Auth->>DB : "Validate Token + Check Expiry"
Auth->>DB : "Update PasswordHash + Clear Token"
DB-->>Auth : "Password Reset Complete"
Auth-->>User : "Success Response"
```

**Diagram sources**
- [AuthService.cs:93-170](file://src/NonCash.Core/Services/AuthService.cs#L93-L170)
- [EmailNotificationService.cs:367-384](file://src/NonCash.Infrastructure/Services/EmailNotificationService.cs#L367-L384)
- [PasswordReset.html:1-25](file://src/NonCash.Infrastructure/EmailTemplates/PasswordReset.html#L1-L25)

### Security Features
- **Secure Token Generation**: Uses cryptographic random number generator for password reset tokens
- **Token Expiration**: 30-minute expiration window for security
- **Email Validation**: Server-side email format validation prevents invalid data entry
- **Account Enumeration Prevention**: Forgot password endpoint returns success regardless of account existence
- **Token Cleanup**: Automatic cleanup of expired or used tokens

**Section sources**
- [AuthService.cs:93-170](file://src/NonCash.Core/Services/AuthService.cs#L93-L170)
- [EmailNotificationService.cs:367-384](file://src/NonCash.Infrastructure/Services/EmailNotificationService.cs#L367-L384)
- [PasswordReset.html:1-25](file://src/NonCash.Infrastructure/EmailTemplates/PasswordReset.html#L1-L25)
- [AuthController.cs:67-86](file://src/NonCash.API/Controllers/AuthController.cs#L67-L86)

## Dependency Analysis
The dependency relationships across layers and modules are as follows:
- NonCash.Web depends on NonCash.Core for business logic and NonCash.Shared for shared models.
- NonCash.API depends on NonCash.Core and NonCash.Shared.
- NonCash.Core depends on NonCash.Shared and uses interfaces defined in its own layer to interact with NonCash.Infrastructure.
- NonCash.Infrastructure depends on PostgreSQL and EF Core, exposing repositories to NonCash.Core.

```mermaid
graph LR
WEB["NonCash.Web"] --> CORE["NonCash.Core"]
API["NonCash.API"] --> CORE
CORE --> SHARED["NonCash.Shared"]
CORE --> INFRA["NonCash.Infrastructure"]
INFRA --> DB["PostgreSQL"]
```

**Diagram sources**
- [source-tree-analysis.md:7-34](file://docs/source-tree-analysis.md#L7-L34)

**Section sources**
- [source-tree-analysis.md:1-50](file://docs/source-tree-analysis.md#L1-L50)

## Performance Considerations
Indexing and performance strategies have been enhanced with the new welcome grant policy system and email functionality:

### New Indexes and Optimizations
- **Composite Index on WelcomeGrantPolicy**: `IX_welcome_grant_policies_business_active_from` optimizes business-scoped policy resolution queries with filters on business_id, is_active, and effective_from.
- **Foreign Key Index on CreditBatch**: `IX_credit_batches_welcome_policy_id` accelerates joins between credit batches and their governing welcome policies.
- **Password Reset Token Index**: `IX_user_accounts_password_reset_token` with filter optimization for non-null tokens improves password reset lookup performance.
- **Existing CreditBatch Indexes**: Maintained existing indexes on brand_id+created_at and brand_id+expires_at for efficient brand-scoped queries.
- **Email Field Optimization**: Email field configured with varchar(255) limit for optimal storage and query performance.

### General Performance Strategies
- Primary Keys: Ensure clustered indexes on GUID PKs for efficient row access.
- Foreign Keys: Add non-clustered indexes on FK columns (e.g., BrandID, UserID, OutletID, CustomerID, BusinessID) to accelerate joins.
- High-Cardinality Filters: Index columns frequently used in WHERE clauses (e.g., SerialNo, PhoneNumber, ApprovalStatus, Email, PasswordResetToken).
- Range Queries: Index DateRange and DateTime fields used in validity checks (e.g., ExpiryDate, PublishDate, UsageDate, EffectiveFrom, EffectiveTo, PasswordResetTokenExpiry).
- Composite Indexes: Consider composite indexes for frequent filter combinations (e.g., BrandID + Status, OutletID + Status, BusinessID + IsActive + EffectiveFrom).
- Query Patterns: Use projection to fetch only required columns; apply pagination for large result sets.
- Concurrency: Use optimistic concurrency tokens for entities updated by multiple services.
- Transactions: Keep transactions short; avoid long-held locks during POS usage workflows.
- **Email Validation**: Implement server-side email validation to prevent invalid data entry and reduce database load.
- **Token Management**: Efficient token lookup using filtered indexes and automatic cleanup of expired tokens.

**Section sources**
- [SplitWelcomePolicy Migration:52-62](file://src/NonCash.Infrastructure/Migrations/20260814050918_SplitWelcomePolicy.cs#L52-L62)
- [WelcomeGrantPolicyConfiguration:17-19](file://src/NonCash.Infrastructure/Data/Configurations/WelcomeGrantPolicyConfiguration.cs#L17-L19)
- [UserAccountConfiguration:35-55](file://src/NonCash.Infrastructure/Data/Configurations/UserAccountConfiguration.cs#L35-L55)
- [AddPasswordResetToken Migration:29-34](file://src/NonCash.Infrastructure/Migrations/20260814114913_AddPasswordResetToken.cs#L29-L34)

## Troubleshooting Guide
Common issues and resolutions:
- Cross-Tenant Access Violations: Verify BrandID filters on all queries for UserAccount, Outlet, and VoucherPlanHeader.
- Duplicate SerialNo or PhoneNumber: Enforce unique constraints at the database level and handle uniqueness violations gracefully.
- POS Usage Conflicts: Ensure transactional boundaries around voucher locking and committing; implement retry logic for transient failures.
- Migration Failures: Validate migration scripts against PostgreSQL compatibility; test in staging before applying to production.
- Audit and Tracing: Log repository operations and key business events for diagnostics and compliance.
- Welcome Policy Resolution Issues: Verify business_id associations and effective date ranges when troubleshooting welcome credit grants.
- Policy-Batch Lineage Problems: Check welcome_policy_id foreign key relationships when auditing credit batch origins.
- **Email Functionality Issues**: Verify email field population and SMTP configuration when troubleshooting email-based authentication and notification failures.
- **Email Validation Errors**: Check email format validation and ensure proper error handling for invalid email addresses during user registration and updates.
- **Password Reset Token Issues**: Verify token generation, storage, and cleanup processes; check token expiration handling and database index performance.
- **Authentication Flow Problems**: Debug forgot password and reset password endpoints; verify email delivery and template rendering.

**Section sources**
- [architecture.md:36-41](file://docs/architecture.md#L36-L41)
- [Key Functionalities.txt:135-156](file://Key Functionalities.txt#L135-L156)
- [EmailNotificationService.cs:40-63](file://src/NonCash.Infrastructure/Services/EmailNotificationService.cs#L40-L63)
- [AuthService.cs:93-170](file://src/NonCash.Core/Services/AuthService.cs#L93-L170)

## Conclusion
NonCash's relational schema and repository-driven persistence align with a robust 3-tier SaaS architecture. The introduction of the WelcomeGrantPolicy system enhances multi-tenancy by providing business-level policy management alongside brand-level data isolation. The addition of email support to UserAccount entities and comprehensive password reset token functionality significantly enhances the platform's communication and security capabilities. Multi-tenancy is enforced via BrandID across core entities and BusinessID for commercial policies, while the microservices-based Business Logic Layer orchestrates complex workflows such as voucher planning, distribution, POS usage, welcome credit grants, and enhanced email-based authentication with secure account recovery. The documented entity relationships, enhanced indexing strategies, migration practices, and comprehensive email functionality provide a solid foundation for scalable, secure, and maintainable operations.

## Appendices
- Implementation Readiness: The project demonstrates strong adherence to database entity mapping and story dependencies, with foundational tables established early and transaction tables introduced progressively. The new welcome grant policy system and enhanced email-enabled user accounts with password reset capabilities represent significant enhancements to the credit management and communication architectures.

**Section sources**
- [_bmad-output/implementation-readiness-report-2026-04-17.md:91-123](file://_bmad-output/planning-artifacts/implementation-readiness-report-2026-04-17.md#L91-L123)