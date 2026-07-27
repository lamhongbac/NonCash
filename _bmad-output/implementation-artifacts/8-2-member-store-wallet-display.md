# Story 8.2: Member Store & Wallet Display

Status: backlog

## Story

As a Member (customer),
I want to browse available vouchers in a visually rich catalog (Member Store) and see my owned vouchers in a wallet view,
So that I can easily discover, understand, and manage my vouchers with full display information.

## Acceptance Criteria

**AC1: Member Store Catalog**
Given approved/published Gift-type plans exist
When a Member browses the Store
Then they see voucher cards displaying: CoverImage (or fallback), Icon, DisplayName, ShortDescription, formatted FaceValue, BrandColor accent, "Buy" button with Price
And cards are grouped or filterable by Brand

**AC2: Voucher Detail View**
Given a Member taps a voucher card in the Store
When the detail page opens
Then it shows: full CoverImage, DisplayName, Brand name, formatted FaceValue, NetValue/Price, ExpiryDate, ValidDaysOfWeek, OutletScope (list of outlet names), full TermsAndConditions (scrollable), and a "Purchase" CTA

**AC3: My Wallet View**
Given a Member has owned vouchers
When they open My Wallet
Then they see cards with: Icon, DisplayName, FaceValue, StatusBadge (Active/Used/Expired), ExpiryDate countdown (e.g., "5 days left")
And cards are sorted: Active first (by expiry ascending), then Used, then Expired

**AC4: Wallet Voucher Detail**
Given a Member taps an Active voucher in My Wallet
When the detail opens
Then it shows: full display data, dynamic voucher code (with "Show Code" button), outlet list, T&C, Transfer button, and POS-ready barcode/QR preview

**AC5: Status Badge Logic**
Given a voucher in the wallet
When rendered
Then the badge is computed as:
- `"Active"` — usageStatus = Pending AND not expired
- `"Expiring Soon"` — usageStatus = Pending AND expiry within 7 days (yellow warning)
- `"Expired"` — past expiry date (grey)
- `"Used"` — usageStatus = Complete (green with checkmark)
- `"In Use"` — usageStatus = In-Use (locked indicator)

**AC6: Empty States**
Given no vouchers in wallet or no plans in store
When the view loads
Then a friendly empty state is shown with illustration and CTA (e.g., "Browse available vouchers" or "No vouchers yet — buy one to get started!")

## Tasks / Subtasks

- [ ] Task 1: Member Store page (AC1, AC2)
  - [ ] Subtask 1.1: `MemberStore.razor` — catalog grid of purchasable voucher cards
  - [ ] Subtask 1.2: `VoucherCard` reusable component (cover image, icon, value, brand color, badge)
  - [ ] Subtask 1.3: `VoucherDetail.razor` — full detail page with T&C, outlets, purchase CTA
  - [ ] Subtask 1.4: Filter/sort by Brand, ValueType
- [ ] Task 2: My Wallet page (AC3, AC4, AC5, AC6)
  - [ ] Subtask 2.1: `MyWallet.razor` — owned voucher list
  - [ ] Subtask 2.2: Wallet card variant with status badge and expiry countdown
  - [ ] Subtask 2.3: Wallet detail view with dynamic code, transfer CTA
  - [ ] Subtask 2.4: Empty state component
- [ ] Task 3: Status badge utility (AC5)
  - [ ] Subtask 3.1: `VoucherStatusHelper` computing display badge from usage status + expiry
- [ ] Task 4: API for member store and wallet (AC1, AC3)
  - [ ] Subtask 4.1: `GET /api/v1/member/store` — published Gift plans with display data
  - [ ] Subtask 4.2: `GET /api/v1/member/wallet` — owned vouchers with display data
- [ ] Task 5: Tests
  - [ ] Subtask 5.1: Component render test for VoucherCard with/without cover image
  - [ ] Subtask 5.2: Unit test for status badge logic

## Dev Notes

### UI Component Hierarchy
```
VoucherCard (reusable)
├── CoverImage (or fallback gradient with BrandColor)
├── Icon (top-left overlay or corner)
├── DisplayName (bold, 1-2 lines max)
├── ShortDescription (1 line, truncated)
├── FaceValue (large, formatted)
├── StatusBadge (bottom-right)
└── Price (Store only, bottom)
```

### Card Sizing (Responsive)
- Mobile: full-width card, vertical layout (cover image top, info below)
- Tablet/Desktop: grid of 2-3 columns, horizontal card variant (cover left, info right)

### Dynamic Code Display
- The "Show Code" button in wallet detail reveals the current rotating dynamic code.
- Code refreshes every 60 seconds (countdown timer visible).
- For POS scanning, show a QR code generated from the current dynamic code.

### References
- [Source: Story 8.1 Display Data Model]
- [Source: docs/user-guides/member-user-guide.md]
