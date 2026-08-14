# Member App API

<cite>
**Referenced Files in This Document**
- [MemberVouchersController.cs](file://src/NonCash.API/Controllers/MemberVouchersController.cs)
- [MemberTransfersController.cs](file://src/NonCash.API/Controllers/MemberTransfersController.cs)
- [PaymentsController.cs](file://src/NonCash.API/Controllers/PaymentsController.cs)
- [IntegrationController.cs](file://src/NonCash.API/Controllers/IntegrationController.cs)
- [VoucherTransferService.cs](file://src/NonCash.Core/Services/VoucherTransferService.cs)
- [TransferService.cs](file://src/NonCash.Core/Services/TransferService.cs)
- [IPaymentService.cs](file://src/NonCash.Core/Interfaces/IPaymentService.cs)
- [ZaloPayPaymentService.cs](file://src/NonCash.Infrastructure\Services\ZaloPayPaymentService.cs)
- [api-contracts.md](file://docs/api-contracts.md)
- [data-models.md](file://docs/data-models.md)
- [architecture.md](file://docs/architecture.md)
- [source-tree-analysis.md](file://docs/source-tree-analysis.md)
- [Key Functionalities.txt](file://Key Functionalities.txt)
</cite>

## Update Summary
**Changes Made**
- Enhanced GET /member/vouchers endpoint with improved display data and pagination support
- Added comprehensive voucher transfer workflow with recipient confirmation system
- Integrated payment processing with ZaloPay gateway for voucher purchases
- Implemented enhanced wallet functionality with event history aggregation
- Added new transfer management endpoints (inbox/outbox, accept/reject/cancel)
- Updated authentication and authorization requirements for all endpoints

## Table of Contents
1. [Introduction](#introduction)
2. [Project Structure](#project-structure)
3. [Core Components](#core-components)
4. [Architecture Overview](#architecture-overview)
5. [Detailed Component Analysis](#detailed-component-analysis)
6. [Enhanced Transfer Workflow](#enhanced-transfer-workflow)
7. [Payment Processing Integration](#payment-processing-integration)
8. [Wallet & Event History](#wallet--event-history)
9. [Dependency Analysis](#dependency-analysis)
10. [Performance Considerations](#performance-considerations)
11. [Troubleshooting Guide](#troubleshooting-guide)
12. [Conclusion](#conclusion)
13. [Appendices](#appendices)

## Introduction
This document provides comprehensive API documentation for the enhanced Member App API focused on advanced voucher management for end users. The platform now includes:
- Enhanced voucher listing with rich display data and pagination
- Complete peer-to-peer voucher transfer workflow with recipient confirmation
- Integrated payment processing for voucher purchases via ZaloPay
- Comprehensive wallet functionality with event history tracking
- Advanced transfer management with inbox/outbox views and status tracking

The API supports JWT authentication for member operations and API key authentication for integration partners.

## Project Structure
The Member App API is part of the NonCash platform's backend services with enhanced capabilities for voucher transfers, payments, and wallet management.

```mermaid
graph TB
subgraph "Member App API"
A["GET /member/vouchers<br/>Enhanced Display Data"]
B["POST /member/transfers<br/>Transfer Management"]
C["POST /member/vouchers/{id}/initiate-transfer<br/>Single Transfer"]
D["POST /member/vouchers/transfer<br/>Batch Transfer"]
E["GET /member/transfers/inbox<br/>Pending Transfers"]
F["GET /member/transfers/outbox<br/>Sent Transfers"]
end
subgraph "Payment Processing"
G["POST /payments/{orderId}/create<br/>Create Payment"]
H["POST /payments/webhook<br/>Webhook Handler"]
I["GET /payments/transactions/{id}<br/>Transaction Status"]
end
subgraph "Backend Services"
Svc["Voucher Services<br/>Transfer Workflow"]
PaySvc["Payment Service<br/>ZaloPay Integration"]
IdSvc["Identity & Tenant Service<br/>JWT + RBAC"]
end
subgraph "Data Layer"
DAL["PostgreSQL via EF Core"]
end
A --> Svc
B --> Svc
C --> Svc
D --> Svc
E --> Svc
F --> Svc
G --> PaySvc
H --> PaySvc
I --> PaySvc
Svc --> IdSvc
PaySvc --> DAL
Svc --> DAL
```

**Diagram sources**
- [MemberVouchersController.cs:8-143](file://src/NonCash.API/Controllers/MemberVouchersController.cs#L8-L143)
- [MemberTransfersController.cs:9-184](file://src/NonCash.API/Controllers/MemberTransfersController.cs#L9-L184)
- [PaymentsController.cs:13-244](file://src/NonCash.API/Controllers/PaymentsController.cs#L13-L244)
- [IntegrationController.cs:11-201](file://src/NonCash.API/Controllers/IntegrationController.cs#L11-L201)

## Core Components
- **Enhanced Member Vouchers**: Improved listing with display data, pagination, and filtering
- **Advanced Transfer System**: Complete peer-to-peer transfer workflow with recipient confirmation
- **Payment Integration**: ZaloPay payment processing for voucher purchases
- **Wallet Management**: Event history tracking and comprehensive wallet queries
- **Authentication & Authorization**: JWT-based member authentication with role-based access control

**Section sources**
- [MemberVouchersController.cs:30-143](file://src/NonCash.API/Controllers/MemberVouchersController.cs#L30-L143)
- [MemberTransfersController.cs:27-184](file://src/NonCash.API/Controllers/MemberTransfersController.cs#L27-L184)
- [PaymentsController.cs:47-244](file://src/NonCash.API/Controllers/PaymentsController.cs#L47-L244)
- [IntegrationController.cs:85-201](file://src/NonCash.API/Controllers/IntegrationController.cs#L85-L201)

## Architecture Overview
The enhanced Member App API integrates with multiple services to provide comprehensive voucher management capabilities.

```mermaid
graph TB
Client["Mobile App"]
API["Member App API"]
Auth["Identity & Tenant Service"]
VoucherSvc["Voucher Services"]
PaySvc["Payment Service"]
DB["PostgreSQL"]
Webhook["Webhook Handler"]
Client --> API
API --> Auth
API --> VoucherSvc
API --> PaySvc
VoucherSvc --> DB
PaySvc --> Webhook
Webhook --> PaySvc
PaySvc --> DB
```

**Diagram sources**
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

## Detailed Component Analysis

### Enhanced GET /member/vouchers
**Updated** - Now includes rich display data and pagination support

Purpose:
- Retrieve a list of vouchers owned by the authenticated member with enhanced display information
- Support for pagination, filtering, and sorting for large voucher collections

Authentication:
- Authorization: Bearer <JWT>

Request:
- Method: GET
- Endpoint: /member/vouchers
- Headers:
  - Authorization: Bearer <JWT>
  - Content-Type: application/json
- Query Parameters:
  - page: int (default: 1)
  - pageSize: int (default: 20)
  - status: string (filter by usage status)
  - brandId: string (filter by brand)

Response:
- Status: 200 OK
- Body: Paginated array of VoucherPlanDetail items with enhanced display fields

Enhanced VoucherPlanDetail schema:
- id: string (GUID)
- parentID: string (GUID)
- serialNo: string
- voucherCode: string
- memberID: string (nullable)
- usageStatus: string (enum: Pending, In-Use, Complete)
- usedDate: string (nullable)
- **New Fields:**
  - displayName: string (human-readable name)
  - shortDescription: string (brief description)
  - faceValue: decimal (monetary value)
  - valueType: string (Value or Percentage)
  - expiryDate: datetime (expiration date)
  - coverImageUrl: string (main image URL)
  - iconUrl: string (brand icon URL)
  - brandColor: string (hex color code)
  - termsAndConditions: string (usage terms)

Example request:
- GET https://api.noncash.service/v1/member/vouchers?page=1&pageSize=20&status=Pending
- Headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

Example response:
- 200 OK
- Body:
  {
    "items": [
      {
        "id": "f5212345-...-9876543210ab",
        "parentID": "a1b2c3d4-...-fedcba987654",
        "serialNo": "SERIAL123",
        "voucherCode": "DYNAMIC_CODE",
        "displayName": "Weekend Treat 200K",
        "shortDescription": "Get 200K off any weekend combo",
        "faceValue": 200000,
        "valueType": "Value",
        "usageStatus": "Pending",
        "expiryDate": "2026-08-30T00:00:00Z",
        "coverImageUrl": "https://cdn.example.com/voucher-cover.jpg",
        "iconUrl": "https://cdn.example.com/brand-icon.png",
        "brandColor": "#E53935",
        "termsAndConditions": "Valid on weekends only..."
      }
    ],
    "totalCount": 150,
    "page": 1,
    "pageSize": 20
  }

Security and authorization:
- Requires a valid JWT issued to the member
- The service enforces ownership and tenant isolation
- Brand-specific filtering based on member permissions

Rate limiting:
- Not defined in the contract; implement client-side throttling and exponential backoff

**Section sources**
- [MemberVouchersController.cs:30-69](file://src/NonCash.API/Controllers/MemberVouchersController.cs#L30-L69)
- [api-contracts.md:93-96](file://docs/api-contracts.md#L93-L96)

### POST /member/transfers - Enhanced Transfer Management
**Updated** - Comprehensive transfer management with inbox/outbox views

Purpose:
- Manage incoming and outgoing voucher transfers with full lifecycle tracking
- Support for accepting, rejecting, and canceling pending transfers

Authentication:
- Authorization: Bearer <JWT>

#### GET /member/transfers/inbox
Lists pending inbound transfers for the authenticated member.

Request:
- Method: GET
- Endpoint: /member/transfers/inbox
- Query Parameters:
  - status: string (filter by transfer status)
  - page: int (default: 1)
  - pageSize: int (default: 20)

Response:
- Status: 200 OK
- Body: Array of TransferInboxDto items

#### GET /member/transfers/outbox
Lists outgoing transfers initiated by the authenticated member.

Request:
- Method: GET
- Endpoint: /member/transfers/outbox
- Query Parameters:
  - status: string (filter by transfer status)
  - page: int (default: 1)
  - pageSize: int (default: 20)

Response:
- Status: 200 OK
- Body: Array of TransferOutboxDto items

#### POST /member/transfers/{transferId}/accept
Accepts a pending transfer, transferring ownership to the recipient.

Request:
- Method: POST
- Endpoint: /member/transfers/{transferId}/accept
- Headers: Authorization: Bearer <JWT>

Response:
- Status: 200 OK
- Body: TransferActionDto { status: "Accepted", voucherId: "GUID" }

#### POST /member/transfers/{transferId}/reject
Rejects a pending transfer with optional reason.

Request:
- Method: POST
- Endpoint: /member/transfers/{transferId}/reject
- Headers: Authorization: Bearer <JWT>
- Body: { reason: "string" }

Response:
- Status: 200 OK
- Body: TransferActionDto { status: "Rejected", voucherId: null }

#### POST /member/transfers/{transferId}/cancel
Cancels a pending transfer initiated by the sender.

Request:
- Method: POST
- Endpoint: /member/transfers/{transferId}/cancel
- Headers: Authorization: Bearer <JWT>

Response:
- Status: 200 OK
- Body: TransferActionDto { status: "Cancelled", voucherId: null }

**Section sources**
- [MemberTransfersController.cs:27-184](file://src/NonCash.API/Controllers/MemberTransfersController.cs#L27-L184)

### POST /member/vouchers/{voucherId}/initiate-transfer
**Updated** - Single voucher transfer initiation with enhanced validation

Purpose:
- Initiate a transfer of a specific voucher to another member
- Supports both phone number and member ID targeting
- Includes automatic placeholder account creation for new recipients

Authentication:
- Authorization: Bearer <JWT>

Request:
- Method: POST
- Endpoint: /member/vouchers/{voucherId}/initiate-transfer
- Headers:
  - Authorization: Bearer <JWT>
  - Content-Type: application/json
- Body:
  - recipientPhone: string (optional)
  - recipientMemberId: string (optional)
  - note: string (optional)

Response:
- Status: 200 OK
- Body: InitiateTransferResponse { transferId: "GUID", status: "PendingAcceptance" }

Error Responses:
- 404 Not Found: Voucher not found
- 403 Forbidden: Voucher not owned by sender
- 409 Conflict: Transfer already pending for this voucher
- 400 Bad Request: Invalid recipient or validation error

**Section sources**
- [MemberVouchersController.cs:30-69](file://src/NonCash.API/Controllers/MemberVouchersController.cs#L30-L69)
- [VoucherTransferService.cs:27-77](file://src/NonCash.Core/Services/VoucherTransferService.cs#L27-L77)

### POST /member/vouchers/transfer
**Updated** - Batch transfer capability with detailed reporting

Purpose:
- Transfer multiple vouchers to multiple recipients in a single operation
- Provides detailed reporting of successful and skipped transfers

Authentication:
- Authorization: Bearer <JWT>

Request:
- Method: POST
- Endpoint: /member/vouchers/transfer
- Headers:
  - Authorization: Bearer <JWT>
  - Content-Type: application/json
- Body:
  - fromMemberId: string (GUID)
  - voucherIds: array of strings (GUIDs)
  - recipientPhones: array of strings (phone numbers)

Response:
- Status: 200 OK
- Body: TransferResponse { transferredCount: int, skippedCount: int, skippedPhones: array }

Error Responses:
- 400 Bad Request: Validation errors or insufficient data
- 403 Forbidden: FromMemberId doesn't match authenticated user
- 404 Not Found: One or more vouchers not found

**Section sources**
- [MemberVouchersController.cs:71-113](file://src/NonCash.API/Controllers/MemberVouchersController.cs#L71-L113)

## Enhanced Transfer Workflow
The transfer system implements a complete two-way confirmation process with comprehensive state management.

```mermaid
sequenceDiagram
participant Client as "Mobile App"
participant API as "Member App API"
participant TransferSvc as "Transfer Service"
participant Recipient as "Recipient Device"
participant DB as "Database"
Note over Client,DB : Transfer Initiation
Client->>API : POST /member/vouchers/{id}/initiate-transfer
API->>TransferSvc : Validate ownership & eligibility
TransferSvc->>DB : Check voucher status & lock
DB-->>TransferSvc : Voucher available
TransferSvc->>DB : Create transfer record
DB-->>TransferSvc : Transfer created
API-->>Client : 200 OK {transferId, status}
Note over Client,Recipient : Notification & Confirmation
Client->>Recipient : Push notification
Recipient->>API : GET /member/transfers/inbox
API->>TransferSvc : Get pending transfers
TransferSvc->>DB : Query transfers
DB-->>TransferSvc : Transfer details
API-->>Recipient : Transfer info
Note over Client,Recipient : Accept/Reject Decision
Recipient->>API : POST /member/transfers/{id}/accept
API->>TransferSvc : Process acceptance
TransferSvc->>DB : Atomic update (transfer + ownership)
DB-->>TransferSvc : Success
API-->>Recipient : 200 OK {status : "Accepted"}
Note over Client,DB : Finalization
TransferSvc->>DB : Release transfer lock
DB-->>TransferSvc : Lock released
```

**Diagram sources**
- [VoucherTransferService.cs:27-232](file://src/NonCash.Core/Services/VoucherTransferService.cs#L27-L232)
- [MemberTransfersController.cs:27-184](file://src/NonCash.API/Controllers/MemberTransfersController.cs#L27-L184)

### Transfer States and Lifecycle
- **PendingAcceptance**: Transfer created, awaiting recipient action
- **Accepted**: Recipient accepted, ownership transferred
- **Rejected**: Recipient rejected the transfer
- **Expired**: Transfer expired without action (7-day default)
- **Cancelled**: Sender cancelled the transfer

### Automatic Placeholder Account Creation
When transferring to a phone number that doesn't exist in the system:
1. Creates a placeholder Customer record
2. Creates a placeholder MemberAccount linked to the customer
3. Allows immediate transfer completion
4. Recipient can later complete registration

**Section sources**
- [VoucherTransferService.cs:172-230](file://src/NonCash.Core/Services/VoucherTransferService.cs#L172-L230)

## Payment Processing Integration
**New** - Integrated payment processing for voucher purchases via ZaloPay

### POST /payments/{orderId}/create
Creates a payment session for an existing pending order.

Authentication:
- Authorization: Bearer <JWT>

Request:
- Method: POST
- Endpoint: /payments/{orderId}/create
- Headers:
  - Authorization: Bearer <JWT>
  - Content-Type: application/json
- Body:
  - returnUrl: string (optional, defaults to configured redirect URL)

Response:
- Status: 200 OK
- Body: PaymentCreateResponse { paymentUrl: string, transactionId: GUID, gatewayTransactionId: string }

Error Responses:
- 401 Unauthorized: Missing or invalid JWT
- 404 Not Found: Order not found
- 403 Forbidden: Order doesn't belong to authenticated user
- 400 Bad Request: Order not in pending payment state
- 502 Bad Gateway: Payment gateway configuration error

### POST /payments/webhook
Handles ZaloPay webhook notifications for payment status updates.

Authentication:
- No authentication required (verified via MAC signature)

Request:
- Method: POST
- Endpoint: /payments/webhook
- Headers:
  - Content-Type: application/json
- Body: ZaloPayWebhookPayload { data: string, mac: string }

Response:
- Status: 200 OK
- Body: { return_code: 1, return_message: "success" }

### GET /payments/transactions/{transactionId}
Retrieves transaction status for polling after payment redirection.

Authentication:
- Authorization: Bearer <JWT>

Request:
- Method: GET
- Endpoint: /payments/transactions/{transactionId}
- Headers: Authorization: Bearer <JWT>

Response:
- Status: 200 OK
- Body: PaymentTransactionResponse { id: GUID, purchaseOrderId: GUID, status: string, amount: decimal, gatewayTransactionId: string }

**Section sources**
- [PaymentsController.cs:47-244](file://src/NonCash.API/Controllers/PaymentsController.cs#L47-L244)
- [IPaymentService.cs:5-50](file://src/NonCash.Core/Interfaces/IPaymentService.cs#L5-L50)
- [ZaloPayPaymentService.cs:38-71](file://src/NonCash.Infrastructure\Services\ZaloPayPaymentService.cs#L38-L71)

## Wallet & Event History
**New** - Enhanced wallet functionality with comprehensive event tracking

### GET /integration/member/{phone}/vouchers
Returns the member's voucher wallet with display fields, scoped to partner's authorized brands.

Authentication:
- X-API-Key header (for integration partners)

Request:
- Method: GET
- Endpoint: /integration/member/{phone}/vouchers
- Headers: X-API-Key: <partner_api_key>

Response:
- Status: 200 OK
- Body: Array of IntegrationWalletItem objects with enhanced display fields

Each wallet item includes:
- voucherID: string (GUID)
- serialNo: string
- faceValue: decimal
- valueType: string (Value or Percentage)
- expiryDate: datetime
- usageStatus: string (Pending, In-Use, Complete)
- imageUrl: string (fallback image)
- iconUrl: string (brand icon)
- coverImageUrl: string (main cover image)
- brandColor: string (hex color)
- displayName: string (human-readable name)
- shortDescription: string (brief description)
- termsAndConditions: string (usage terms)
- brandName: string (brand name)

### GET /integration/member/{phone}/events
Returns unified event history aggregated from distributions, usages, and transfers.

Authentication:
- X-API-Key header (for integration partners)

Request:
- Method: GET
- Endpoint: /integration/member/{phone}/events
- Headers: X-API-Key: <partner_api_key>
- Query Parameters:
  - limit: int (default: 50)
  - from: date (optional, start date filter)
  - to: date (optional, end date filter)
  - eventType: string (optional, comma-separated event types)

Response:
- Status: 200 OK
- Body: Array of IntegrationEventItem objects

Each event includes:
- eventType: string (Distributed, Redeemed, Transferred, Expired, Cancelled)
- occurredAt: datetime
- voucherId: string (GUID, nullable)
- serialNo: string (nullable)
- brandName: string (nullable)
- details: string (JSON object with event-specific details)

**Section sources**
- [IntegrationController.cs:85-142](file://src/NonCash.API/Controllers/IntegrationController.cs#L85-L142)
- [IPromotionService.cs:39-62](file://src/NonCash.Core/Interfaces/IPromotionService.cs#L39-L62)

## Dependency Analysis
The enhanced API depends on multiple services and components:

```mermaid
graph LR
API["Member App API"] --> IdSvc["Identity & Tenant Service"]
API --> VoucherSvc["Voucher Services"]
API --> PaySvc["Payment Service"]
API --> PartnerSvc["Partner Service"]
VoucherSvc --> DB["PostgreSQL"]
PaySvc --> DB
PaySvc --> Webhook["Webhook Handler"]
PartnerSvc --> DB
```

**Diagram sources**
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

**Section sources**
- [architecture.md:17-26](file://docs/architecture.md#L17-L26)
- [source-tree-analysis.md:23-26](file://docs/source-tree-analysis.md#L23-L26)

## Performance Considerations
- **Pagination**: All list endpoints support pagination with configurable page sizes
- **Rate Limiting**: Implement client-side throttling and exponential backoff
- **Caching**: Consider caching non-sensitive metadata for short periods
- **Network Efficiency**: Compress responses where supported and minimize unnecessary fields
- **Database Optimization**: Use efficient queries with proper indexing for large datasets
- **Transfer Expiry**: Background sweep service handles expired transfers automatically

## Troubleshooting Guide
Common issues and resolutions:

### Authentication Issues
- **401 Unauthorized**: Missing or invalid JWT token
  - Resolution: Re-authenticate and obtain a new JWT
- **403 Forbidden**: Insufficient permissions or tenant mismatch
  - Resolution: Verify membership and brand association

### Transfer Issues
- **404 Not Found**: Voucher or transfer ID does not exist
  - Resolution: Confirm voucher ownership and ID validity
- **409 Conflict**: Transfer already in progress or resolved
  - Resolution: Check current transfer status before attempting actions
- **Validation Errors**: Invalid phone number format or missing fields
  - Resolution: Validate phone numbers and ensure required fields are present

### Payment Issues
- **502 Bad Gateway**: Payment gateway configuration error
  - Resolution: Check ZaloPay configuration and network connectivity
- **Order State Errors**: Order not in expected state for payment
  - Resolution: Verify order status before initiating payment

### Debugging Tips
- Capture request/response logs with masked sensitive data
- Verify JWT claims (sub, tenant) on the client
- Test with small subsets of data to isolate issues
- Monitor network latency and retry behavior
- Use staging environment for testing payment flows

**Section sources**
- [api-contracts.md:93-109](file://docs/api-contracts.md#L93-L109)

## Conclusion
The enhanced Member App API provides comprehensive voucher management capabilities including advanced transfer workflows, integrated payment processing, and enriched wallet functionality. The system supports both individual and batch operations, with robust error handling and security measures. Mobile app developers should implement proper authentication, handle asynchronous confirmations, validate inputs thoroughly, and adopt best practices for rate limiting and error handling.

## Appendices

### API Definition Summary
- **Base URL**: https://api.noncash.service/v1
- **Authentication**:
  - Member App: Authorization: Bearer <JWT>
  - Integration Partners: X-API-Key header
- **Enhanced Endpoints**:
  - GET /member/vouchers (with pagination and filtering)
  - POST /member/vouchers/{id}/initiate-transfer (single transfer)
  - POST /member/vouchers/transfer (batch transfer)
  - GET /member/transfers/inbox (pending transfers)
  - GET /member/transfers/outbox (sent transfers)
  - POST /member/transfers/{id}/accept (accept transfer)
  - POST /member/transfers/{id}/reject (reject transfer)
  - POST /member/transfers/{id}/cancel (cancel transfer)
  - POST /payments/{orderId}/create (create payment)
  - POST /payments/webhook (webhook handler)
  - GET /payments/transactions/{id} (transaction status)
  - GET /integration/member/{phone}/vouchers (wallet query)
  - GET /integration/member/{phone}/events (event history)

**Section sources**
- [api-contracts.md:6-8](file://docs/api-contracts.md#L6-L8)
- [api-contracts.md:89-109](file://docs/api-contracts.md#L89-L109)
- [api-contracts.md:223-276](file://docs/api-contracts.md#L223-L276)

### Data Model Reference
- **Enhanced VoucherPlanDetail**: Includes display fields, pricing, and metadata
- **VoucherTransfer**: Complete transfer lifecycle with status tracking
- **PaymentTransaction**: Payment processing records with gateway integration
- **Customer**: End-user entity with phone number and profile information

**Section sources**
- [data-models.md:34-42](file://docs/data-models.md#L34-L42)
- [data-models.md:91-98](file://docs/data-models.md#L91-L98)
- [VoucherPlanHeader.cs:22-76](file://src/NonCash.Core\Entities\VoucherPlanHeader.cs#L22-L76)