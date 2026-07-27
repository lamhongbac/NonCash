# Story 8.1: Voucher Display Data Model (Best Practices)

Status: backlog

## Story

As a Planner or Brand Manager,
I want to define rich, standardized display data for each voucher plan (cover image, icon, T&C, value formatting, brand colors),
So that vouchers render consistently and attractively across the member store, member wallet, Loyalty App, and POS preview.

## Acceptance Criteria

**AC1: Extended Display Fields on Plan Header**
Given a Planner creates or edits a plan
When they fill in the display section
Then the following fields are persisted:
- `CoverImageURL` (text, nullable) — hero/banner image for voucher card (recommended 1200×630px, 16:9)
- `IconURL` (text, nullable) — small brand logo/icon for grid views and thumbnails (recommended 128×128px, 1:1)
- `TermsAndConditions` (text, nullable) — plain text or markdown describing usage rules
- `BrandColor` (varchar 7, nullable) — hex color code (e.g., `#E53935`) for card accent/background
- `DisplayName` (varchar 200, nullable) — short marketing name (e.g., "Weekend Treat 200K")
- `ShortDescription` (varchar 500, nullable) — one-line summary for catalog listing
- `ValidDaysOfWeek` (varchar 50, nullable) — e.g., `"Mon,Tue,Wed,Thu"` for day-restricted vouchers

**AC2: Image Upload and Validation**
Given a Planner uploads a cover image or icon
When the file is submitted
Then the system validates: format (JPEG, PNG, WebP), max size (cover: 2MB, icon: 500KB), and recommended aspect ratio
And stores the file in object storage (e.g., cloud blob) and returns the URL
And generates a thumbnail variant for icon (if cover is uploaded)

**AC3: Terms & Conditions Preview**
Given a plan has T&C content
When a Planner previews it
Then the system renders the T&C in a collapsible panel matching how it will appear in the member wallet

**AC4: Display Defaults**
Given a plan where no cover image is uploaded
When the voucher is rendered in any view
Then the system generates a fallback card using: BrandColor (or brand default), IconURL, DisplayName, FaceValue
And the card remains visually consistent

**AC5: Value Formatting**
Given a plan with `ValueType = Value` and `FaceValue = 200000`
When rendered in any display context
Then it shows as `"200,000 ₫"` (locale-aware currency formatting for VND)
And for `ValueType = Percentage` with `FaceValue = 20` it shows as `"20% OFF"`

## Tasks / Subtasks

- [ ] Task 1: Schema extension (AC1)
  - [ ] Subtask 1.1: Add display fields to `VoucherPlanHeader` entity: `cover_image_url`, `terms_and_conditions`, `brand_color`, `display_name`, `short_description`, `valid_days_of_week`
  - [ ] Subtask 1.2: EF migration
- [ ] Task 2: Image upload service (AC2)
  - [ ] Subtask 2.1: `IImageStorageService` interface in Core (Upload, Delete, GetUrl)
  - [ ] Subtask 2.2: Implementation in Infrastructure using cloud blob or local file storage
  - [ ] Subtask 2.3: Upload API endpoint: `POST /api/v1/upload/image` with validation
  - [ ] Subtask 2.4: File validation: format, size, aspect ratio warning (not block)
- [ ] Task 3: Plan form UI update (AC1, AC3)
  - [ ] Subtask 3.1: Add "Display" section to plan create/edit form
  - [ ] Subtask 3.2: Image upload component with preview
  - [ ] Subtask 3.3: T&C rich text or textarea with collapsible preview
  - [ ] Subtask 3.4: Color picker for BrandColor
- [ ] Task 4: Value formatting utility (AC5)
  - [ ] Subtask 4.1: `VoucherDisplayHelper` static class with `FormatValue(faceValue, valueType, culture)`
  - [ ] Subtask 4.2: Use in all display contexts (Blazor, API responses, webhook payloads)
- [ ] Task 5: Fallback card generation (AC4)
  - [ ] Subtask 5.1: Default card rendering logic when CoverImageURL is null
- [ ] Task 6: Tests
  - [ ] Subtask 6.1: Unit tests for value formatting (VND, percentage)
  - [ ] Subtask 6.2: Integration test for image upload round-trip

## Dev Notes

### Best Practices Adopted
These display fields follow conventions from leading platforms (Grab Vouchers, Shopee Food, Starbucks App, Urbox):

| Element | Best Practice | NonCash Implementation |
|---|---|---|
| **Cover Image** | 16:9 ratio, high-quality hero. Used in detail views and share previews. | `cover_image_url`, 1200×630px recommended |
| **Icon** | 1:1 square, brand logo. Used in list/grid views and notifications. | `icon_url` (existing), 128×128px recommended |
| **Display Name** | Short, marketing-friendly title (max 40 chars ideal). | `display_name` varchar 200 |
| **Short Description** | One-liner for catalog cards (max 80 chars ideal). | `short_description` varchar 500 |
| **Brand Color** | Hex accent for card background/border. Differentiates brands visually. | `brand_color` varchar 7 |
| **Value Display** | Locale-aware currency or percentage. Prominent, large font. | `VoucherDisplayHelper.FormatValue()` |
| **T&C** | Collapsible, scrollable. Never hidden — transparency builds trust. | `terms_and_conditions` text |
| **Validity Indicator** | Expiry date + day-of-week restrictions shown clearly. | `expiry_date` + `valid_days_of_week` |
| **Status Badge** | Visual indicator: Active, Used, Expired. | Derived from `usage_status` + `expiry_date` |
| **Outlet Scope** | Show where it can be used (outlet names or "All outlets"). | Resolved from `plan_outlets` join |

### Schema Changes
```sql
ALTER TABLE voucher_plan_headers ADD COLUMN cover_image_url TEXT NULL;
ALTER TABLE voucher_plan_headers ADD COLUMN terms_and_conditions TEXT NULL;
ALTER TABLE voucher_plan_headers ADD COLUMN brand_color VARCHAR(7) NULL;
ALTER TABLE voucher_plan_headers ADD COLUMN display_name VARCHAR(200) NULL;
ALTER TABLE voucher_plan_headers ADD COLUMN short_description VARCHAR(500) NULL;
ALTER TABLE voucher_plan_headers ADD COLUMN valid_days_of_week VARCHAR(50) NULL;
```

### Backward Compatibility
- All new fields are nullable. Existing plans render with fallback defaults.
- `image_url` (existing) is aliased as the cover image if `cover_image_url` is null.

### References
- [Source: Key Functionalities.txt#ImageURL, IconURL]
- [Source: docs/data-models.md#VoucherPlanHeader]
