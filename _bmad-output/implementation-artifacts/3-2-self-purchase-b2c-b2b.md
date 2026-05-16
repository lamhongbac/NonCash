# Story 3.2: Mua voucher qua kenh Truc tiep (Self-Purchase B2C/B2B)

Status: ready-for-dev

## Story

As a Member (Customer or Organization),
I want to select and purchase available vouchers and complete payment,
So that I or my organization can manage a collection of vouchers for use or redistribution.

## Acceptance Criteria

**AC1: Voucher Catalog Browsing**
Given a logged-in Member
When they view the voucher store
Then they see a list of Approved/Published plans with `VoucherType = Gift`
And each item displays: Image, FaceValue, NetValue, Price (NetValue or markup), Validity period

**AC2: Purchase Request**
Given a Member selects a voucher and quantity Q
When they confirm purchase
Then the system validates that at least Q unassigned `VoucherPlanDetail` records exist for that plan
And creates a `PurchaseOrder` record with status `PendingPayment`

**AC3: Payment Recording (Placeholder)**
Given a purchase order exists
When payment is confirmed (internal platform or external)
Then the system updates the order to `Paid`
And triggers voucher allocation

**AC4: Voucher Allocation on Payment**
Given an order is Paid
When allocation triggers
Then Q `VoucherPlanDetail` records are assigned to the purchaser's `MemberID`
And `VoucherDistribution` records are created with `Method = Sale`
And the Member can see the vouchers in "My Voucher"

**AC5: Invoice Information**
Given a purchase request
When the Member provides invoice details (company name, tax code)
Then the system stores invoice metadata linked to the `PurchaseOrder`

**AC6: Insufficient Stock**
Given a purchase quantity exceeds available vouchers
When the Member attempts checkout
Then the system returns 400 / InsufficientStock with current available count

## Tasks / Subtasks

- [ ] Task 1: Define PurchaseOrder entity (AC2, AC3, AC5)
  - [ ] Subtask 1.1: `PurchaseOrder.cs` in `NonCash.Core/Entities/`
  - [ ] Subtask 1.2: `OrderStatus` enum: PendingPayment, Paid, Cancelled
  - [ ] Subtask 1.3: `OrderDetail` child entity linking to `VoucherPlanHeader` with quantity
- [ ] Task 2: Implement Purchase service (AC2, AC4, AC6)
  - [ ] Subtask 2.1: `IPurchaseService` with `CreateOrderAsync`, `ConfirmPaymentAsync`, `CancelOrderAsync`
  - [ ] Subtask 2.2: Stock reservation during order creation (pessimistic or optimistic — recommend optimistic with retry)
  - [ ] Subtask 2.3: On payment confirmation, allocate vouchers and create distribution records in a transaction
- [ ] Task 3: Catalog API (AC1)
  - [ ] Subtask 3.1: `GET /api/v1/store/vouchers` — public/catalog endpoint
  - [ ] Subtask 3.2: Filter by `VoucherType = Gift`, `ApprovalStatus = Approved`, `PublishDate <= Now`, `ExpiryDate > Now`
- [ ] Task 4: Purchase API (AC2, AC3, AC5, AC6)
  - [ ] Subtask 4.1: `POST /api/v1/orders` — create order
  - [ ] Subtask 4.2: `POST /api/v1/orders/{orderId}/pay` — simulate payment confirmation (Admin/service endpoint for MVP)
  - [ ] Subtask 4.3: `GET /api/v1/orders/{orderId}` — order detail with invoice info
- [ ] Task 5: Member App / Blazor UI (AC1, AC4)
  - [ ] Subtask 5.1: Store page showing available voucher catalog
  - [ ] Subtask 5.2: Checkout flow with quantity selector and invoice info form
  - [ ] Subtask 5.3: "My Voucher" page listing owned vouchers (reused in Story 4.1 context)
- [ ] Task 6: Database migration
  - [ ] Subtask 6.1: `purchase_orders` and `order_details` tables
- [ ] Task 7: Tests
  - [ ] Subtask 7.1: Unit tests for purchase allocation logic
  - [ ] Subtask 7.2: Integration tests for concurrent purchases depleting stock
  - [ ] Subtask 7.3: Integration tests for order cancellation releasing reservations

## Dev Notes

### Architecture Compliance
- Payment processing is **out of scope** for full implementation in MVP. Story 3.2 implements the order lifecycle and a simulated/manual payment confirmation endpoint. A real payment gateway integration would be a future epic.
- Stock management: either reserve vouchers at order creation (set a `ReservedUntil` timestamp) or check availability at payment time. For MVP, checking at payment with optimistic concurrency is acceptable.
- `PurchaseOrder` is a new business object not explicitly in the original data model but required to track B2C/B2B sales.

### File Structure Requirements
```
src/NonCash.Core/Entities/PurchaseOrder.cs
src/NonCash.Core/Entities/OrderDetail.cs
src/NonCash.Core/Enums/OrderStatus.cs
src/NonCash.Core/Interfaces/IPurchaseService.cs
src/NonCash.Core/Services/PurchaseService.cs
src/NonCash.API/Controllers/StoreController.cs
src/NonCash.API/Controllers/OrdersController.cs
src/NonCash.Web/Pages/Member/Store.razor
src/NonCash.Web/Pages/Member/MyVouchers.razor
```

### Database Schema
- Table: `purchase_orders`
- Columns: `order_id` (uuid PK), `member_id` (uuid FK not null), `status` (varchar 20), `invoice_company_name` (varchar 200), `invoice_tax_code` (varchar 50), `total_amount` (numeric(18,2)), `created_at` (timestamptz), `updated_at` (timestamptz)
- Table: `order_details`
- Columns: `detail_id` (uuid PK), `order_id` (uuid FK), `plan_id` (uuid FK), `quantity` (int), `unit_price` (numeric(18,2))

### API Contracts
- `GET /api/v1/store/vouchers` => list of available gift vouchers
- `POST /api/v1/orders` => `{ planId, quantity, invoice? { companyName, taxCode } }` => order creation
- `POST /api/v1/orders/{id}/pay` => Admin/service endpoint to confirm payment and allocate

### Security & NFR
- NFR4: Members only see their own orders and allocated vouchers.
- Catalog endpoint (`GET /api/v1/store/vouchers`) can be public or require authentication based on business need. For MVP, require JWT.
- Payment confirmation endpoint MUST be restricted to service/internal roles to prevent self-confirmation.

### Testing Standards
- Simulate two Members buying the last voucher concurrently. One should succeed, one should get InsufficientStock.
- Verify that order cancellation does not leave orphaned voucher assignments.

### References
- [Source: Key Functionalities.txt#III] — Sale/Tu Mua flow, B2C and B2B use cases.
- [Source: docs/data-models.md] — VoucherPlanDetail, Customer entities.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

