# NonCash Member / Customer User Guide

This guide is for **Members** (individual customers or organizations) who use the NonCash member portal to browse, purchase, manage, and transfer vouchers.

---

## 1. Getting Started

### 1.1 Register an Account

1. Open the NonCash member portal or mobile app.
2. Click **Register**.
3. Enter your phone number.
4. Complete the verification process (SMS or OTP based on platform configuration).
5. Set your password and full name.

The system creates a Customer record and a linked UserAccount. Your phone number is your primary identifier.

### 1.2 Log In

1. Open the member portal.
2. Enter your phone number or username and password.
3. Tap **Log In**.

After login, the system issues a JWT token scoped to your Member identity.

### 1.3 Home Screen

The member home screen shows:

- **Voucher Store** — browse available gift vouchers.
- **My Vouchers** — vouchers you currently own.
- **Transfers** — pending incoming transfers and transfer history.
- **Profile** — update personal information and password.

---

## 2. Voucher Store

The Voucher Store lists Gift-type vouchers that are Approved, Published, and currently valid.

### 2.1 Browse Available Vouchers

1. Tap **Voucher Store**.
2. Browse the catalog. Each card shows:
   - Brand name
   - Voucher image
   - Face value
   - Net value / price
   - Validity period
   - Expiry date
3. Tap a voucher to see details.

### 2.2 Voucher Details

The detail screen shows:

- Full terms and conditions
- Applicable outlet list (Sales Range)
- Remaining stock
- Your purchase history for this voucher (if any)

---

## 3. Self-Purchase

Members can purchase Gift vouchers directly from the store.

### 3.1 Select Quantity

1. On the voucher detail screen, choose the **Quantity**.
2. The total amount updates automatically based on the net value.

### 3.2 Add Invoice Information (Optional)

For B2B or company purchases:

1. Toggle **Need Invoice**.
2. Enter:
   - Company Name
   - Tax Code
3. The invoice metadata is attached to your order.

### 3.3 Place Order

1. Tap **Buy Now** or **Add to Cart**.
2. Review the order summary.
3. Tap **Confirm Purchase**.

The system creates a `PurchaseOrder` with status `PendingPayment` and checks that enough unassigned vouchers are available.

### 3.4 Complete Payment

1. Follow the platform's payment flow (internal wallet, bank transfer, or external payment gateway).
2. Once payment is confirmed by the system, your order status changes to `Paid`.
3. Vouchers are automatically allocated to your account.

You will receive a notification when allocation is complete.

### 3.5 Insufficient Stock

If the quantity you selected exceeds available stock, the system shows an **Insufficient Stock** message with the current available count. Reduce the quantity or choose another voucher.

---

## 4. My Vouchers

My Vouchers shows all vouchers currently assigned to you.

### 4.1 View Owned Vouchers

1. Tap **My Vouchers**.
2. Each card shows:
   - Serial number (partially masked)
   - Brand name
   - Face value
   - Expiry date
   - Usage status:
     - `Pending` — not yet used
     - `Used` — already redeemed
     - `Expired` — past expiry date
     - `Locked` — pending transfer

### 4.2 View Voucher Details

1. Tap a voucher card.
2. The detail screen shows:
   - Full serial number
   - Redemption QR code or dynamic voucher code
   - Applicable outlets
   - Terms and conditions
   - Transfer history (if any)

### 4.3 Redeem at POS

To use a voucher at a participating outlet:

1. Open **My Vouchers**.
2. Select the voucher you want to use.
3. Present the QR code or dynamic voucher code to the cashier.
4. The cashier scans or enters the code into the POS system.
5. The POS system validates the code and applies the discount to your bill.

> **Note:** A voucher that is pending transfer (locked) cannot be redeemed until the transfer is resolved.

---

## 5. Peer-to-Peer Voucher Transfers

Members can gift vouchers to other people via phone number or MemberID. Transfers are free — no payment is involved.

### 5.1 Initiate a Transfer

1. Go to **My Vouchers**.
2. Select the voucher you want to transfer.
3. Tap **Transfer / Gift**.
4. Choose how to identify the recipient:
   - **Phone Number** — enter the recipient's registered phone number.
   - **MemberID** — enter the recipient's MemberID if known.
5. Optionally add a note (for example, "Happy birthday!").
6. Tap **Send**.

The system:

- Resolves the recipient by phone or MemberID.
- Creates a placeholder Customer record if the phone number is not yet registered.
- Creates a `VoucherTransfer` record with status `PendingAcceptance`.
- Soft-locks the voucher so it cannot be used while the transfer is pending.
- Sets an expiry of 7 days for the recipient to respond.

> **Restrictions:**
> - You can only transfer vouchers you own.
> - The voucher must be in `Pending` status.
> - You cannot transfer to yourself.
> - Only one pending transfer can exist per voucher at a time.

### 5.2 View Incoming Transfers (Inbox)

1. Go to **Transfers**.
2. Tap the **Inbox** tab.
3. Each row shows:
   - Sender name
   - Voucher brand and face value
   - Expiry date
   - Sender's note

### 5.3 Accept a Transfer

1. In the **Inbox**, tap the transfer you want to accept.
2. Review the voucher details.
3. Tap **Accept**.

The system:

- Transitions the transfer status to `Accepted`.
- Changes the voucher owner to you (`MemberID` updated).
- Releases the soft-lock.
- The voucher now appears in your **My Vouchers** list.

### 5.4 Reject a Transfer

1. In the **Inbox**, tap the transfer you want to decline.
2. Tap **Reject**.
3. Optionally enter a reason.
4. Confirm.

The system:

- Transitions the transfer status to `Rejected`.
- Returns the voucher to the sender.
- Releases the soft-lock.

### 5.5 Cancel an Outgoing Transfer

1. Go to **Transfers**.
2. Tap the **History** or **Outbox** tab.
3. Find the pending transfer you sent.
4. Tap **Cancel**.
5. Confirm.

The system:

- Transitions the transfer status to `Cancelled`.
- Returns the voucher to your account.
- Releases the soft-lock.

> **Note:** You can only cancel transfers that are still `PendingAcceptance`.

### 5.6 Transfer Expiry

If the recipient does not accept or reject the transfer within 7 days, the transfer automatically expires. The voucher returns to the sender and the soft-lock is released.

### 5.7 View Transfer History

1. Go to **Transfers**.
2. Tap the **History** tab.
3. The list shows all your incoming and outgoing transfers with statuses:
   - `PendingAcceptance` — waiting for action
   - `Accepted` — completed
   - `Rejected` — declined
   - `Cancelled` — sender cancelled
   - `Expired` — timed out

---

## 6. Profile Management

### 6.1 Update Personal Information

1. Tap **Profile**.
2. Update your full name or email.
3. Tap **Save**.

### 6.2 Change Password

1. Tap **Profile > Change Password**.
2. Enter your current password.
3. Enter and confirm your new password.
4. Tap **Save**.

### 6.3 Phone Number

Your phone number is your primary login identifier. Contact platform support if you need to change it.

---

## 7. Common Tasks Quick Reference

| Task | Path |
| --- | --- |
| Browse vouchers | Voucher Store |
| Purchase a voucher | Voucher Store > Select voucher > Buy Now |
| View owned vouchers | My Vouchers |
| Redeem at POS | My Vouchers > Select voucher > Show QR/Code |
| Transfer a voucher | My Vouchers > Select voucher > Transfer |
| Accept incoming transfer | Transfers > Inbox > Accept |
| Reject incoming transfer | Transfers > Inbox > Reject |
| Cancel outgoing transfer | Transfers > History/Outbox > Cancel |
| Update profile | Profile |
| Change password | Profile > Change Password |

---

## 8. Troubleshooting

| Issue | Cause | Resolution |
| --- | --- | --- |
| Cannot log in | Wrong password or locked account | Reset password or contact support. |
| Voucher not visible in My Vouchers | Order not yet paid | Wait for payment confirmation. |
| Cannot transfer voucher | Voucher is locked, used, or expired | Check voucher status in My Vouchers. |
| Transfer button missing | Voucher already pending transfer | Wait for existing transfer to resolve. |
| Cannot redeem at POS | Voucher is locked by pending transfer | Ask sender to cancel or recipient to reject. |
| Transfer expired | Recipient did not act within 7 days | Sender can initiate a new transfer. |
| Purchase failed with Insufficient Stock | Not enough vouchers available | Reduce quantity or choose another voucher. |

