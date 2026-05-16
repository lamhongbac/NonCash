# Story 1.3: Quan ly Danh muc Khach hang (Customer Record Management)

Status: review

## Story

As a Brand Manager or via Import,
I want to create and manage Customer records and maintain a Blacklist,
So that I can control valid participants and block fraudulent users.

## Acceptance Criteria

**AC1: Customer Creation**
Given customer identifying information
When it is entered into the system
Then a Customer record is created successfully with a unique `CustomerID`
And `PhoneNumber` is enforced as unique and required

**AC2: Blacklist Management**
Given an existing Customer
When a Brand Manager marks the Customer as Blacklisted
Then the `Status` field updates to `Blacklisted`
And the customer is excluded from future batch promotions and self-purchases

**AC3: Customer Import**
Given a CSV or Excel file with customer data
When the Brand Manager uploads it
Then the system parses the file and creates/updates Customer records in bulk
And duplicate `PhoneNumber` entries are handled via update (upsert) logic

**AC4: Customer Search & List**
Given a list of customers exists
When the Brand Manager searches by `PhoneNumber`, `FullName`, or `Email`
Then paginated results are returned
And Blacklisted customers are visually flagged in the UI

**AC5: Data Model Integrity**
Given the Customer entity
When persisted
Then it stores: `CustomerID` (PK, GUID), `PhoneNumber` (unique, required), `FullName`, `Email`, `Status` (Active | Blacklisted), `CreatedAt`, `UpdatedAt`

## Tasks / Subtasks

- [x] Task 1: Define Customer domain model (AC5)
  - [x] Subtask 1.1: `Customer.cs` entity in `NonCash.Core/Entities/`
  - [x] Subtask 1.2: Unique index on `PhoneNumber`
  - [x] Subtask 1.3: EF FluentAPI configuration
- [x] Task 2: Implement Customer service (AC1, AC2, AC4)
  - [x] Subtask 2.1: `CustomerService` with `CreateAsync`, `UpdateAsync`, `BlacklistAsync`, `SearchAsync`
  - [x] Subtask 2.2: `BlacklistAsync` must validate no active vouchers are in-use (optional business rule: warn or block)
  - [x] Subtask 2.3: Search with `LIKE` on `FullName` and `Email`, exact on `PhoneNumber`
- [x] Task 3: Import functionality (AC3)
  - [x] Subtask 3.1: `ICustomerImportService` interface
  - [x] Subtask 3.2: CSV parser using `CsvHelper` NuGet package
  - [x] Subtask 3.3: Upsert logic: match by `PhoneNumber`, update `FullName`/`Email` if changed
  - [ ] Subtask 3.4: Background processing or chunked upload for large files (>1000 rows)
- [x] Task 4: API endpoints (AC1, AC2, AC4)
  - [x] Subtask 4.1: `CustomersController`
  - [x] Subtask 4.2: `POST /api/v1/customers`, `PUT /api/v1/customers/{id}/blacklist`, `GET /api/v1/customers`, `POST /api/v1/customers/import`
  - [x] Subtask 4.3: DTOs with validation rules
- [x] Task 5: Blazor UI (AC1, AC2, AC3, AC4)
  - [x] Subtask 5.1: `Customers.razor` page with search, grid, blacklist toggle
  - [x] Subtask 5.2: File upload component for CSV import with progress indication
- [x] Task 6: Database migration
  - [x] Subtask 6.1: `customers` table migration
- [x] Task 7: Tests
  - [x] Subtask 7.1: Unit tests for upsert logic and blacklist business rule
  - [x] Subtask 7.2: Integration tests for import endpoint and duplicate handling

## Dev Notes

### Architecture Compliance
- Customer is a **global entity** (not Brand-scoped) because a customer may hold vouchers from multiple Brands. However, Brand-specific views may filter via VoucherDistribution joins.
- The Blacklist status affects Distribution stories (Epic 3). The Service layer should expose `IsBlacklisted(Guid customerId)` for use by downstream services.
- Import must be transactional: if parsing fails mid-file, do not partially commit. Use a transaction around the batch.

### File Structure Requirements
```
src/NonCash.Core/Entities/Customer.cs
src/NonCash.Core/Interfaces/ICustomerRepository.cs
src/NonCash.Core/Interfaces/ICustomerImportService.cs
src/NonCash.Core/Services/CustomerService.cs
src/NonCash.Infrastructure/Data/Configurations/CustomerConfiguration.cs
src/NonCash.Infrastructure/Services/CsvCustomerImportService.cs
src/NonCash.API/Controllers/CustomersController.cs
src/NonCash.Web/Pages/BrandManager/Customers.razor
```

### Database Schema
- Table: `customers`
- Columns: `customer_id` (uuid PK), `phone_number` (varchar 20 unique not null), `full_name` (varchar 200), `email` (varchar 255), `status` (varchar 20), `created_at` (timestamptz), `updated_at` (timestamptz)
- Index: `IX_customers_phone_number` (unique), `IX_customers_status`

### API Contracts
- Base path: `/api/v1/customers`
- Auth: JWT Bearer (BrandManager, Admin)
- Import endpoint accepts `multipart/form-data` with CSV. Return a job ID if processed asynchronously.

### Security & NFR
- NFR3 (RBAC): Only BrandManager and Admin can blacklist. Planners/Approvers have read-only access.
- PhoneNumber should be normalized (strip non-digits) before storage to ensure uniqueness integrity.

### Testing Standards
- Test upsert logic: same PhoneNumber, different Name -> expect update.
- Test CSV import with malformed rows -> expect graceful skip or rollback.

### References
- [Source: docs/data-models.md#Customer] — Entity definition.
- [Source: Key Functionalities.txt#V] — Customer Management, Blacklist, Import/Export.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

