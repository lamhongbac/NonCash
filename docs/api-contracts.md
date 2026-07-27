# API Contracts - NonCash Project

This document defines the RESTful API endpoints for POS integration and external system components.

## Overview
- **Base URL**: `https://api.noncash.service/v1`
- **Authentication**: API Key (Header: `X-API-Key`) and JWT (Bearer Token).
- **Format**: JSON

## POS Integration API

Used by Point-of-Sale systems to verify and redeem vouchers.

### 1. Verify Voucher
Checks if a voucher is valid and available for use.
- **Endpoint**: `POST /pos/verify`
- **Request**:
  ```json
  {
    "voucherCode": "DYNAMIC_CODE_HERE",
    "outletID": "STORE_001"
  }
  ```
- **Response**:
  ```json
  {
    "status": "Valid",
    "voucherInfo": {
      "faceValue": 100000,
      "expiryDate": "2026-12-31",
      "brand": "The Coffee House"
    }
  }
  ```

### 2. Lock Voucher
Sets voucher to `In-Use` status to prevent double-spending during a transaction.
- **Endpoint**: `POST /pos/lock`
- **Request**:
  ```json
  {
    "voucherCode": "DYNAMIC_CODE_HERE",
    "outletID": "STORE_001"
  }
  ```
- **Response**:
  ```json
  {
    "status": "Locked",
    "lockID": "GUID_LOCK_ID"
  }
  ```

### 3. Redeem Voucher (Commit)
Finalizes the usage of the voucher after the POS transaction is successful.
- **Endpoint**: `POST /pos/redeem`
- **Request**:
  ```json
  {
    "lockID": "GUID_LOCK_ID",
    "transactionID": "POS_TRANS_12345"
  }
  ```
- **Response**:
  ```json
  {
    "status": "Success",
    "message": "Voucher completed"
  }
  ```

### 4. Rollback Lock
Unlocks the voucher if the POS transaction fails or is cancelled.
- **Endpoint**: `POST /pos/rollback`
- **Request**:
  ```json
  {
    "lockID": "GUID_LOCK_ID"
  }
  ```
- **Response**:
  ```json
  {
    "status": "Success",
    "message": "Voucher released"
  }
  ```

## Member App API

Interactions for the user mobile application.

### 1. List My Vouchers
- **Endpoint**: `GET /member/vouchers`
- **Header**: `Authorization: Bearer <JWT>`
- **Response**: List of `VoucherPlanDetail` items owned by the member.

### 2. Transfer Voucher
Initiates a transfer to another member via Phone Number.
- **Endpoint**: `POST /member/transfer`
- **Request**:
  ```json
  {
    "voucherID": "GUID",
    "recipientPhone": "0987654321"
  }
  ```
- **Response**: `202 Accepted` (Requires recipient confirmation).

---

## Loyalty App Integration API

A generic integration layer for **any brand Loyalty App** (e.g., Giga Mall App, Coffee House App, Golden Gate App, etc.) to connect with NonCash. NonCash does not own customer data or marketing logic — it provides the voucher engine and exposes event history via API.

### Responsibility Boundary

| Concern | Owned By |
|---|---|
| Customer data (profiles, segments, visit history) | Loyalty App |
| Analytics, segmentation, marketing plan creation | Loyalty App |
| Push notifications to members | Loyalty App |
| Voucher wallet display in-app | Loyalty App (consuming NonCash data) |
| Voucher production, distribution, redemption, fraud protection | NonCash |
| Voucher event history (issued, distributed, redeemed, transferred, expired) | NonCash (exposed via API) |
| Cross-tenant settlement tracking | NonCash |

### Integration Principles

1. **NonCash is a voucher engine, not a CRM.** Customer master data always belongs to the Loyalty App. NonCash stores only the minimum member reference needed for voucher ownership (phone number / member ID).
2. **Loyalty App is the marketing brain.** Segmentation, targeting, campaign timing, and push notifications are all executed by the Loyalty App. NonCash provides the data it needs to make those decisions.
3. **Event-driven sync.** NonCash emits lifecycle events. The Loyalty App consumes them to update wallet views, trigger notifications, and feed analytics.

### 1. Distribute to Segment

Loyalty App pushes a target member segment (list of phone numbers or member IDs) to NonCash for batch distribution.

- **Endpoint**: `POST /integration/distribute`
- **Header**: `X-API-Key` (issued to the Loyalty App partner)
- **Request**:
  ```json
  {
    "planID": "GUID",
    "members": [
      { "phone": "0912345678", "externalMemberID": "GM-00123" },
      { "phone": "0987654321", "externalMemberID": "GM-00456" }
    ],
    "callbackURL": "https://loyalty-app.example.com/webhooks/noncash"
  }
  ```
- **Response**:
  ```json
  {
    "distributionID": "GUID",
    "totalRequested": 2,
    "totalDistributed": 2,
    "skipped": []
  }
  ```

### 2. Get Member Voucher Wallet

Loyalty App retrieves the current voucher state for a specific member to display in-app.

- **Endpoint**: `GET /integration/member/{phone}/vouchers`
- **Header**: `X-API-Key`
- **Response**:
  ```json
  {
    "memberPhone": "0912345678",
    "vouchers": [
      {
        "voucherID": "GUID",
        "brand": "Your F&B",
        "faceValue": 50000,
        "usageStatus": "Pending",
        "expiryDate": "2026-08-30",
        "issuedDate": "2026-07-27",
        "outlets": ["Outlet A - Giga Mall"]
      }
    ]
  }
  ```

### 3. Get Voucher Event History

Loyalty App retrieves full lifecycle events for a voucher or member (for analytics, wallet timeline, push notification triggers).

- **Endpoint**: `GET /integration/member/{phone}/events`
- **Query params**: `?from=2026-07-01&to=2026-07-31&eventType=distributed,redeemed,transferred,expired`
- **Header**: `X-API-Key`
- **Response**:
  ```json
  {
    "events": [
      {
        "eventID": "GUID",
        "eventType": "Distributed",
        "voucherID": "GUID",
        "brand": "Your F&B",
        "timestamp": "2026-07-27T10:00:00Z",
        "details": { "method": "Promotion", "planID": "GUID" }
      },
      {
        "eventID": "GUID",
        "eventType": "Redeemed",
        "voucherID": "GUID",
        "brand": "Your F&B",
        "timestamp": "2026-07-29T12:30:00Z",
        "details": { "outlet": "Outlet A", "transactionID": "POS-123" }
      }
    ]
  }
  ```

### 4. Webhook: Voucher Lifecycle Events (Push)

NonCash pushes real-time events to the Loyalty App's registered callback URL so the app can update wallet state and send push notifications instantly.

- **Direction**: NonCash → Loyalty App
- **Events pushed**: `VoucherDistributed`, `VoucherRedeemed`, `VoucherTransferred`, `VoucherExpired`, `VoucherCancelled`
- **Payload**:
  ```json
  {
    "event": "VoucherRedeemed",
    "timestamp": "2026-07-29T12:30:00Z",
    "data": {
      "voucherID": "GUID",
      "memberPhone": "0912345678",
      "brand": "Your F&B",
      "outlet": "Outlet A - Giga Mall",
      "faceValue": 50000,
      "transactionID": "POS-123"
    }
  }
  ```

### 5. Query Campaign Performance

Loyalty App queries aggregated performance data for campaigns it sponsored.

- **Endpoint**: `GET /integration/campaigns/{planID}/performance`
- **Header**: `X-API-Key`
- **Response**:
  ```json
  {
    "planID": "GUID",
    "brand": "Your F&B",
    "totalIssued": 500,
    "totalDistributed": 500,
    "totalRedeemed": 127,
    "redemptionRate": 0.254,
    "totalRedeemedValue": 6350000,
    "perOutlet": [
      { "outlet": "Outlet A", "redeemed": 127, "value": 6350000 }
    ]
  }
  ```
