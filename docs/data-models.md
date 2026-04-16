# Data Models - NonCash Project

This document outlines the core data models and relationships based on the business requirements.

## Entity Relationship Overview

The system uses a relational model (PostgreSQL) managed via Entity Framework Core.

### 1. Voucher Production Planning

#### `VoucherPlanHeader` (Plan Header)
Represents the overall strategy for a voucher campaign.
- `ID`: GUID (Primary Key)
- `PlanDate`: DateTime (Creation date)
- `CreatorID`: GUID (Link to Member/User)
- `BrandID`: GUID (Issuing brand)
- `VoucherType`: Enum (Complimentary, Gift)
- `DisplayMode`: Object (Image, Icon)
- `ValueType`: Enum (Value, Percentage)
- `FaceValue`: Decimal (Usage value)
- `NetValue`: Decimal (Reference cost)
- `ExpiryDate`: DateTime (Hard expiry)
- `PublishDate`: DateTime (Availability date)
- `SalesRange`: List<BrandID> (Accepted locations)
- `TimeRange`: DateRange (Valid from-to)
- `TargetQuantity`: Integer (Expected volume)
- `Budget`: Decimal (Total cost)
- `TargetDistributed`: Integer (Goal for distribution)
- `TargetUsed`: Integer (Goal for POS usage)
- `ApprovalStatus`: Enum (Pending, Approved, Rejected)

#### `VoucherPlanDetail` (Voucher Detail)
Represents individual vouchers generated after a plan is approved.
- `ID`: GUID (Primary Key)
- `ParentID`: GUID (FK to `VoucherPlanHeader`)
- `SerialNo`: String (Unique external ID)
- `VoucherCode`: String (Dynamic/JWT-like code for usage)
- `MemberID`: GUID (Nullable - Assigned owner)
- `UsageStatus`: Enum (Pending, In-Use, Complete)
- `UsedDate`: DateTime? (Nullable)

### 2. Tracking and Distribution

#### `VoucherUsage`
Stores the history of voucher redemptions at POS.
- `ID`: GUID
- `VoucherID`: GUID (FK to `VoucherPlanDetail`)
- `POSID`: String (Redemption location)
- `TransactionID`: String (Link to POS transaction)
- `UsageDate`: DateTime
- `AmountUsed`: Decimal

#### `VoucherDistribution`
Tracks how vouchers were sent to customers.
- `ID`: GUID
- `VoucherID`: GUID
- `MemberID`: GUID
- `Method`: Enum (Sale, Promotion, Transfer)
- `DistributionDate`: DateTime

### 3. Identity and Members

#### `Member` (B2B & B2C)
Unified entity for individuals and organizations.
- `MemID`: GUID (Primary Key)
- `Name`: String
- `Type`: Enum (Customer, Organization)
- `PhoneNumber`: String (Primary identifier for transfer)
- `Email`: String
- `Status`: Enum (Active, Blacklisted)
