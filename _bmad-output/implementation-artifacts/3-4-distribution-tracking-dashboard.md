# Story 3.4: Bao cao Thong ke Phan phoi (Distribution Tracking Dashboard)

Status: ready-for-dev

## Story

As a Brand Manager,
I want to view reports on all voucher distribution and transfer activities,
So that I can monitor push performance against TargetDistributed goals.

## Acceptance Criteria

**AC1: Distribution Log Aggregation**
Given distribution transactions have occurred
When the Brand Manager opens the Distribution Report
Then the system aggregates data from `VoucherDistribution` filtered by their Brand's plans
And displays: total distributed, by method (Sale/Promotion/Transfer), by plan, by date range

**AC2: Target Comparison**
Given a plan has `TargetDistributed = X`
When the report runs
Then it shows actual distributed count Y and percentage `Y/X * 100`
And highlights plans where `Y < X` and deadline is near

**AC3: Drill-Down by Plan**
Given the aggregated report is displayed
When the user clicks a plan row
Then they see a detail list of all distribution records for that plan
With: voucher SerialNo, recipient phone/name, method, date

**AC4: Export Capability**
Given a report view is loaded
When the user clicks Export
Then the system generates a CSV/Excel file with the current filtered dataset

**AC5: Date Range Filtering**
Given the report page
When the user selects a date range
Then all aggregations and lists filter accordingly

## Tasks / Subtasks

- [ ] Task 1: Reporting service (AC1, AC2)
  - [ ] Subtask 1.1: `IDistributionReportService` with `GetSummaryAsync(Guid brandId, DateRange range)`
  - [ ] Subtask 1.2: Raw SQL or EF GroupBy queries for performance
  - [ ] Subtask 1.3: `DistributionSummaryDto` and `DistributionDetailDto`
- [ ] Task 2: API endpoints (AC1, AC3, AC5)
  - [ ] Subtask 2.1: `GET /api/v1/reports/distribution?brandId=&from=&to=`
  - [ ] Subtask 2.2: `GET /api/v1/reports/distribution/{planId}/details`
  - [ ] Subtask 2.3: `GET /api/v1/reports/distribution/export?format=csv` (or use frontend export)
- [ ] Task 3: Blazor Dashboard UI (AC1, AC2, AC3, AC4, AC5)
  - [ ] Subtask 3.1: `DistributionReport.razor` page
  - [ ] Subtask 3.2: Summary cards (total distributed, by method)
  - [ ] Subtask 3.3: Data grid with sorting, pagination, and plan drill-down
  - [ ] Subtask 3.4: Date range picker
  - [ ] Subtask 3.5: Export button generating CSV client-side or via API
- [ ] Task 4: Tests
  - [ ] Subtask 4.1: Unit tests for aggregation logic
  - [ ] Subtask 4.2: Integration tests for date filtering

## Dev Notes

### Architecture Compliance
- Reporting queries may be heavy. Consider using **raw SQL** or **Dapper** for aggregation if EF GroupBy performance is insufficient. Do not over-optimize prematurely — measure first.
- The report is **read-only**. No mutations.
- Brand scoping: join `VoucherDistribution -> VoucherPlanDetail -> VoucherPlanHeader` and filter by `BrandID`.

### File Structure Requirements
```
src/NonCash.Core/Interfaces/IDistributionReportService.cs
src/NonCash.Core/Services/DistributionReportService.cs
src/NonCash.Core/DTOs/DistributionReportDtos.cs
src/NonCash.API/Controllers/ReportsController.cs
src/NonCash.Web/Pages/BrandManager/DistributionReport.razor
```

### API Contracts
- `GET /api/v1/reports/distribution?from=YYYY-MM-DD&to=YYYY-MM-DD`
- Response: `{ plans: [ { planId, planName, targetDistributed, actualDistributed, percentage, byMethod: { sale, promotion, transfer } } ], totalDistributed }`

### Security & NFR
- NFR4: Only distributions for the user's Brand are visible.
- NFR3: BrandManager, Planner, Approver, Admin can view. Read-only access.

### Testing Standards
- Seed known data and assert exact aggregation numbers.
- Verify that changing date range excludes out-of-range records.

### References
- [Source: docs/data-models.md#VoucherDistribution] — Distribution entity fields.
- [Source: Key Functionalities.txt#III] — Distribution tracking requirements.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

