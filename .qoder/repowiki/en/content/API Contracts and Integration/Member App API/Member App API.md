# Member App API

<cite>
**Referenced Files in This Document**
- [api-contracts.md](file://docs/api-contracts.md)
- [data-models.md](file://docs/data-models.md)
- [architecture.md](file://docs/architecture.md)
- [source-tree-analysis.md](file://docs/source-tree-analysis.md)
- [Key Functionalities.txt](file://Key Functionalities.txt)
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
This document provides comprehensive API documentation for the Member App API focused on voucher management for end users. It covers:
- Retrieving user-owned vouchers via GET /member/vouchers with JWT authentication
- Initiating voucher transfers via POST /member/transfer with phone number validation and recipient confirmation
It also includes request/response schemas, authentication headers, authorization requirements, workflow explanations, implementation guidelines for mobile app developers, error handling strategies, security considerations, rate limiting, pagination guidance, and debugging approaches.

## Project Structure
The Member App API is part of the NonCash platform’s backend services. The API surface for POS and Member App is defined in the documentation. The backend follows a 3-layer architecture with microservices and uses JWT for authentication.

```mermaid
graph TB
subgraph "Member App API"
A["GET /member/vouchers"]
B["POST /member/transfer"]
end
subgraph "Backend Services"
Svc["Voucher Services<br/>Transfer Workflow"]
IdSvc["Identity & Tenant Service<br/>JWT + RBAC"]
end
subgraph "Data Layer"
DAL["PostgreSQL via EF Core"]
end
A --> Svc
B --> Svc
Svc --> IdSvc
Svc --> DAL
```

**Diagram sources**
- [api-contracts.md:93-109](file://docs/api-contracts.md#L93-L109)
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

**Section sources**
- [api-contracts.md:93-109](file://docs/api-contracts.md#L93-L109)
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

## Core Components
- Member App API endpoints for end-user interactions:
  - GET /member/vouchers: Lists vouchers owned by the authenticated member
  - POST /member/transfer: Initiates a transfer to another member via phone number
- Authentication and authorization:
  - JWT Bearer tokens for user identity
  - API Key for system-level integrations (POS)
- Data models:
  - VoucherPlanDetail: represents individual vouchers with ownership and usage status
  - Customer: end-user entity with phone number used for transfers

**Section sources**
- [api-contracts.md:93-109](file://docs/api-contracts.md#L93-L109)
- [data-models.md:34-42](file://docs/data-models.md#L34-L42)
- [data-models.md:91-98](file://docs/data-models.md#L91-L98)
- [Key Functionalities.txt:127-133](file://Key Functionalities.txt#L127-L133)

## Architecture Overview
The Member App API resides in the backend and integrates with:
- Identity & Tenant Service for JWT-based authentication and multi-tenancy
- Voucher services orchestrating transfer workflows
- Data Access Layer backed by PostgreSQL

```mermaid
graph TB
Client["Mobile App"]
API["Member App API"]
Auth["Identity & Tenant Service"]
VoucherSvc["Voucher Services"]
DB["PostgreSQL"]
Client --> API
API --> Auth
API --> VoucherSvc
VoucherSvc --> DB
```

**Diagram sources**
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

## Detailed Component Analysis

### GET /member/vouchers
Purpose:
- Retrieve a list of vouchers owned by the authenticated member.

Authentication:
- Authorization: Bearer <JWT>

Request:
- Method: GET
- Endpoint: /member/vouchers
- Headers:
  - Authorization: Bearer <JWT>
  - Content-Type: application/json

Response:
- Status: 200 OK
- Body: Array of VoucherPlanDetail items owned by the member

VoucherPlanDetail schema (selected fields):
- id: string (GUID)
- parentID: string (GUID)
- serialNo: string
- voucherCode: string
- memberID: string (nullable)
- usageStatus: string (enum: Pending, In-Use, Complete)
- usedDate: string (nullable)

Notes:
- Pagination is not defined in the current contract; mobile apps should implement client-side pagination if the list grows large.
- Sorting and filtering are not defined; clients should request server-side controls if needed.

Example request:
- GET https://api.noncash.service/v1/member/vouchers
- Headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

Example response:
- 200 OK
- Body:
  [
    {
      "id": "f5212345-...-9876543210ab",
      "parentID": "a1b2c3d4-...-fedcba987654",
      "serialNo": "SERIAL123",
      "voucherCode": "DYNAMIC_CODE",
      "memberID": "member-guid",
      "usageStatus": "Pending",
      "usedDate": null
    },
    ...
  ]

Security and authorization:
- Requires a valid JWT issued to the member
- The service enforces ownership and tenant isolation

Rate limiting:
- Not defined in the contract; implement client-side throttling and exponential backoff

**Section sources**
- [api-contracts.md:93-96](file://docs/api-contracts.md#L93-L96)
- [data-models.md:34-42](file://docs/data-models.md#L34-L42)

### POST /member/transfer
Purpose:
- Initiate a transfer of a voucher to another member via phone number.

Authentication:
- Authorization: Bearer <JWT>

Request:
- Method: POST
- Endpoint: /member/transfer
- Headers:
  - Authorization: Bearer <JWT>
  - Content-Type: application/json
- Body:
  - voucherID: string (GUID)
  - recipientPhone: string (phone number)

Response:
- Status: 202 Accepted
- Body: Empty or minimal success payload indicating transfer initiation

Transfer workflow:
- Sender initiates transfer with voucherID and recipientPhone
- System validates ownership and voucher eligibility
- System locates the recipient by phone number (if not registered, registration may be required)
- Recipient receives a notification prompting confirmation
- Upon confirmation, the transfer is finalized and ownership updates

Recipient confirmation process:
- The business rules indicate a two-way confirmation: sender initiates, recipient confirms
- The confirmation step is required before ownership changes

Phone number validation:
- The phone number is used as the primary identifier for locating the recipient
- Validation should ensure a valid phone number format and existence in the system

```mermaid
sequenceDiagram
participant Client as "Mobile App"
participant API as "Member App API"
participant VoucherSvc as "Voucher Services"
participant Recipient as "Recipient Device"
Client->>API : POST /member/transfer {voucherID, recipientPhone}
API->>VoucherSvc : Validate ownership and eligibility
VoucherSvc-->>API : OK
API-->>Client : 202 Accepted
Note over Client,Recipient : Notification sent to recipient
Client->>API : Confirmation request (if applicable)
API->>VoucherSvc : Finalize transfer
VoucherSvc-->>API : Success
API-->>Client : Transfer confirmed
```

**Diagram sources**
- [api-contracts.md:98-108](file://docs/api-contracts.md#L98-L108)
- [Key Functionalities.txt:127-133](file://Key Functionalities.txt#L127-L133)

**Section sources**
- [api-contracts.md:98-108](file://docs/api-contracts.md#L98-L108)
- [Key Functionalities.txt:127-133](file://Key Functionalities.txt#L127-L133)

## Dependency Analysis
- API depends on:
  - Identity & Tenant Service for JWT validation and RBAC
  - Voucher Services for transfer orchestration and data access
  - PostgreSQL via EF Core for persistence

```mermaid
graph LR
API["Member App API"] --> IdSvc["Identity & Tenant Service"]
API --> VoucherSvc["Voucher Services"]
VoucherSvc --> DB["PostgreSQL"]
```

**Diagram sources**
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

**Section sources**
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

## Performance Considerations
- Pagination:
  - Not defined for GET /member/vouchers; implement client-side pagination or request server-side pagination/filtering if needed
- Rate limiting:
  - Not defined; implement client-side throttling and exponential backoff
- Caching:
  - Consider caching non-sensitive metadata for short periods to reduce load
- Network efficiency:
  - Compress responses where supported and minimize unnecessary fields

[No sources needed since this section provides general guidance]

## Troubleshooting Guide
Common issues and resolutions:
- 401 Unauthorized:
  - Cause: Missing or invalid JWT
  - Resolution: Re-authenticate and obtain a new JWT
- 403 Forbidden:
  - Cause: Insufficient permissions or tenant mismatch
  - Resolution: Verify membership and brand association
- 404 Not Found:
  - Cause: voucherID does not exist or belongs to another member
  - Resolution: Confirm voucher ownership and ID
- 422 Unprocessable Entity:
  - Cause: Invalid phone number format or missing fields
  - Resolution: Validate phone number and ensure required fields are present
- 429 Too Many Requests:
  - Cause: Client exceeded rate limits
  - Resolution: Implement backoff and retry logic
- 5xx Server Errors:
  - Cause: Internal service failure
  - Resolution: Retry with backoff; contact support if persistent

Debugging tips:
- Capture request/response logs with masked JWTs
- Verify JWT claims (sub, tenant) on the client
- Test with a small subset of data to isolate issues
- Monitor network latency and retry behavior

**Section sources**
- [api-contracts.md:93-109](file://docs/api-contracts.md#L93-L109)

## Conclusion
The Member App API provides essential endpoints for end users to manage their vouchers. GET /member/vouchers retrieves owned vouchers secured by JWT, while POST /member/transfer initiates transfers requiring recipient confirmation. Mobile app developers should implement robust authentication, handle asynchronous confirmations, validate phone numbers, and adopt client-side pagination and rate limiting. Security and tenant isolation are enforced by the Identity & Tenant Service and Voucher Services.

[No sources needed since this section summarizes without analyzing specific files]

## Appendices

### API Definition Summary
- Base URL: https://api.noncash.service/v1
- Authentication:
  - Member App: Authorization: Bearer <JWT>
  - POS Integrations: X-API-Key header (not covered in this doc)
- GET /member/vouchers
  - Headers: Authorization: Bearer <JWT>
  - Response: Array of VoucherPlanDetail
- POST /member/transfer
  - Headers: Authorization: Bearer <JWT>, Content-Type: application/json
  - Request body: { voucherID: string(GUID), recipientPhone: string }
  - Response: 202 Accepted

**Section sources**
- [api-contracts.md:6-8](file://docs/api-contracts.md#L6-L8)
- [api-contracts.md:93-109](file://docs/api-contracts.md#L93-L109)

### Data Model Reference
- VoucherPlanDetail
  - Fields: id, parentID, serialNo, voucherCode, memberID, usageStatus, usedDate
- Customer
  - Fields: CustomerID, PhoneNumber, FullName, Email, Status

**Section sources**
- [data-models.md:34-42](file://docs/data-models.md#L34-L42)
- [data-models.md:91-98](file://docs/data-models.md#L91-L98)