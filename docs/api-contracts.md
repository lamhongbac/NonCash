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
    "posID": "STORE_001"
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
    "posID": "STORE_001"
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
