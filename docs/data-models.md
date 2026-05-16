# Data Models - NonCash Project

This document outlines the core data models and relationships based on the business requirements.

## Entity Relationship Overview

The system uses a relational model (PostgreSQL) managed via Entity Framework Core.

### 1. Voucher Production Planning

#### `VoucherPlanHeader` (Plan Header)
Represents the overall strategy for a voucher campaign.
- `ID`: GUID (Primary Key)
- `PlanDate`: DateTime (Creation date)
- `CreatorID`: GUID (FK to UserAccount)
- `ApproverID`: GUID? (Nullable, FK to UserAccount)
- `BrandID`: GUID (FK to Brand)
- `VoucherType`: Enum (Complimentary, Gift)
- `ImageURL`: String (Url for detailed display)
- `IconURL`: String (Url for grid/logo display)
- `ValueType`: Enum (Value, Percentage)
- `FaceValue`: Decimal (Usage value)
- `NetValue`: Decimal (Reference cost)
- `ExpiryDate`: DateTime (Hard expiry)
- `PublishDate`: DateTime (Availability date)
- `SalesRange`: List<OutletID> (Accepted outlet locations)
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

### 3. Identity and Operations Management

#### `Brand` (Organization / Tenant)
Represents businesses that create and distribute vouchers (e.g., The Coffee House).
- `BrandID`: GUID (Primary Key)
- `Name`: String
- `TaxCode`: String
- `ContactEmail`: String
- `Status`: Enum (Active, Suspended)

#### `Outlet` (Point of Sale / Store)
Represents physical or digital stores belonging to a Brand.
- `OutletID`: GUID (Primary Key)
- `BrandID`: GUID (FK to Brand)
- `Name`: String
- `Address`: String
- `Status`: Enum (Active, Closed)

#### `UserAccount` (Back-office Users)
Platform access for creating, reviewing, and approving plans.
- `UserID`: GUID (Primary Key)
- `BrandID`: GUID (FK to Brand, nullable for system super-admins)
- `Username`: String
- `PasswordHash`: String
- `FullName`: String
- `Role`: Enum (Admin, Planner, Approver)
- `Status`: Enum (Active, Locked)

#### `Customer` (End-User / App Member)
The consumers who hold and use the distributed vouchers.
- `CustomerID`: GUID (Primary Key)
- `PhoneNumber`: String (Primary identifier for transfer/login)
- `FullName`: String
- `Email`: String
- `Status`: Enum (Active, Blacklisted)
