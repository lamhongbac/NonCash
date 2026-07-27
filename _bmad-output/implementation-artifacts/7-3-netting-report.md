# Story 7.3: Netting Report

Status: backlog

## Story

As a Mall Operator or Admin,
I want to generate a monthly netting report showing the net financial obligations between all participating brands,
So that the mall can act as a clearinghouse and offset settlement amounts against rent or service charges.

## Acceptance Criteria

**AC1: Netting Computation**
Given a date range (e.g., last month)
When the Admin generates the netting report
Then for each brand pair (A, B), the system computes:
- Gross owed: Brand A owes Brand B = sum of face values where A is Sponsor and B is Redeemer
- Net obligation: if A owes B 2,500K and B owes A 800K → Net: A pays B 1,700K

**AC2: Report Format**
Given the netting computation
When displayed or exported
Then the report shows a matrix/table:
- Rows: Sponsor Brand
- Columns: Redeem Brand
- Cells: Net amount (positive = row brand pays column brand)
- Summary row/column: Total net per brand

**AC3: Export**
Given the report is generated
When Admin clicks Export
Then a CSV/Excel file is downloaded with the full matrix

**AC4: Integration with Loyalty App API**
Given a Loyalty App partner with Mall Operator role
When they call `GET /integration/settlements/netting?from=&to=`
Then they receive the same netting data in JSON format

## Tasks / Subtasks

- [ ] Task 1: Netting computation service (AC1)
  - [ ] Subtask 1.1: `ISettlementService.ComputeNetting(from, to)` returning a brand-pair matrix
  - [ ] Subtask 1.2: Group settlement entries by sponsor-redeemer pair, sum face values, compute net
- [ ] Task 2: Report UI (AC2, AC3)
  - [ ] Subtask 2.1: `NettingReport.razor` page with date range picker
  - [ ] Subtask 2.2: Matrix table display
  - [ ] Subtask 2.3: CSV/Excel export
- [ ] Task 3: API endpoint (AC4)
  - [ ] Subtask 3.1: `GET /integration/settlements/netting`
- [ ] Task 4: Tests
  - [ ] Subtask 4.1: Unit test for netting computation with multiple brand pairs

## Dev Notes

### Business Logic
- Netting is computed at the Brand level, not outlet level (all outlets of a brand are aggregated).
- The report is informational — actual payment between brands happens off-platform.

### References
- [Source: docs/proposals/giga-mall-discussion-summary.md#Q4]
- [Source: Story 7.2 Settlement Ledger]

---

# Epic 8: Voucher Display & Presentation

Ensure vouchers have rich, standardized display data (images, T&C, value formatting) for consistent rendering across the member store, member wallet, Loyalty App integration, and POS preview. Adopts best practices from leading voucher/gift card platforms.

## Stories

| # | Story | Summary |
|---|---|---|
| 8.1 | Voucher Display Data Model | Add structured display fields to plan header: CoverImageURL, TermsAndConditions, DisplayConfig. |
| 8.2 | Member Store & Wallet Display | Build the member-facing catalog and wallet views using the standardized display data. |
| 8.3 | Loyalty App Display Payload | Ensure the Integration API wallet response includes all fields needed for rich in-app rendering. |
