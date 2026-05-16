# Story 3.1: Phan phoi Tu dong lo lon (Batch Promotion Distribution)

Status: ready-for-dev

## Story

As a Brand Manager,
I want to upload a target customer list (PhoneNumber / MemberID)
So that the system automatically distributes vouchers directly into their inboxes.

## Acceptance Criteria

**AC1: Promotion Eligibility Check**
Given a campaign is in Approved or Published status
When the Brand Manager initiates batch promotion
Then the system verifies the plan status and available voucher stock (unassigned VoucherPlanDetails with `UsageStatus = Pending` and `MemberID = null`)

**AC2: Customer List Upload**
Given a CSV or Excel file with PhoneNumbers
When uploaded
Then the system parses the list, normalizes phone numbers, and matches against existing Customers
And auto-creates new Customer records for unknown phone numbers (with `Status = Active`)

**AC3: Voucher Allocation**
Given a valid customer list and sufficient voucher stock
When allocation runs
Then the system assigns one voucher per customer by setting `VoucherPlanDetail.MemberID`
And creates a `VoucherDistribution` record for each assignment with `Method = Promotion`

**AC4: Insufficient Stock Handling**
Given voucher stock is less than the customer list count
When allocation is attempted
Then the system returns 400 / InsufficientStock
And no partial allocations occur (all-or-nothing transaction)

**AC5: Blacklist Exclusion**
Given a customer on the uploaded list is Blacklisted
When allocation runs
Then that customer is skipped
And a warning report is returned listing skipped records

**AC6: Distribution Log**
Given allocation completes
When the Brand Manager views reports
Then the `VoucherDistribution` table contains accurate records with `DistributionDate` and `Method = Promotion`

## Tasks / Subtasks

- [ ] Task 1: Implement promotion service (AC1, AC3, AC4, AC5)
  - [ ] Subtask 1.1: `IPromotionService` with `DistributeAsync(Guid planId, List<string> phoneNumbers)`
  - [ ] Subtask 1.2: Stock check: count available `VoucherPlanDetail` where `MemberID == null && UsageStatus == Pending`
  - [ ] Subtask 1.3: All-or-nothing transaction around allocation
  - [ ] Subtask 1.4: Skip blacklisted customers with warning collection
- [ ] Task 2: Customer matching & auto-creation (AC2)
  - [ ] Subtask 2.1: Normalize phone numbers (strip non-digits)
  - [ ] Subtask 2.2: Upsert logic: match by PhoneNumber, create if missing
  - [ ] Subtask 2.3: CSV/Excel parser reuse from Story 1.3 or extend `ICustomerImportService`
- [ ] Task 3: VoucherDistribution recording (AC3, AC6)
  - [ ] Subtask 3.1: `VoucherDistribution` entity if not yet created
  - [ ] Subtask 3.2: `DistributionMethod` enum: Sale, Promotion, Transfer
  - [ ] Subtask 3.3: Bulk insert distribution records
- [ ] Task 4: API endpoint (AC1, AC4)
  - [ ] Subtask 4.1: `POST /api/v1/plans/{planId}/promote`
  - [ ] Subtask 4.2: Accept `multipart/form-data` (CSV) or JSON `{ phoneNumbers: [] }`
  - [ ] Subtask 4.3: Response: `{ distributedCount: N, skippedCount: M, skippedPhones: [] }`
- [ ] Task 5: Blazor UI (AC2, AC6)
  - [ ] Subtask 5.1: Batch promotion modal on plan detail page
  - [ ] Subtask 5.2: File upload + preview of parsed list
  - [ ] Subtask 5.3: Result summary with skipped/warning display
- [ ] Task 6: Database migration
  - [ ] Subtask 6.1: `voucher_distributions` table if not exists
- [ ] Task 7: Tests
  - [ ] Subtask 7.1: Unit tests for stock check and all-or-nothing logic
  - [ ] Subtask 7.2: Integration tests for blacklist skip behavior
  - [ ] Subtask 7.3: Integration tests for auto-customer creation

## Dev Notes

### Architecture Compliance
- The `VoucherDistribution` table is the **source of truth** for all voucher movements. Every change of `MemberID` on a Detail must be accompanied by a Distribution record.
- Use a **database transaction** to ensure atomicity: if any step fails (e.g., distribution insert error), rollback all `MemberID` updates.
- Consider background processing for large lists (>1000). Use `IHostedService` or queue-based approach if needed, but for MVP, synchronous with a reasonable timeout is acceptable.

### File Structure Requirements
```
src/NonCash.Core/Entities/VoucherDistribution.cs
src/NonCash.Core/Enums/DistributionMethod.cs
src/NonCash.Core/Interfaces/IPromotionService.cs
src/NonCash.Core/Services/PromotionService.cs
src/NonCash.API/Controllers/PromotionsController.cs
```

### Database Schema
- Table: `voucher_distributions`
- Columns: `id` (uuid PK), `voucher_id` (uuid FK not null), `member_id` (uuid FK not null), `method` (varchar 20 not null), `distribution_date` (timestamptz not null)
- Index: `IX_voucher_distributions_voucher_id`, `IX_voucher_distributions_member_id`

### API Contracts
- `POST /api/v1/plans/{planId}/promote` (multipart or JSON)
- 400 if plan not Approved/Published
- 400 if insufficient stock
- 200 with summary of distributed/skipped

### Security & NFR
- NFR4: Brand isolation — can only promote plans belonging to user's Brand.
- NFR3: Only BrandManager and Admin can execute batch promotion.

### Testing Standards
- Test all-or-nothing: simulate DB failure mid-allocation and assert no vouchers were assigned.
- Test phone normalization: "+84 912 345 678" and "0912345678" should match the same customer.

### References
- [Source: docs/data-models.md#VoucherDistribution] — Distribution entity.
- [Source: Key Functionalities.txt#III] — Batch Promotion flow and Inbox delivery.
- [Source: Key Functionalities.txt#V] — Customer import logic reuse.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

