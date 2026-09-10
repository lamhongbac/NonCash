# NonCash Member / Customer User Guide

This guide is for **Members** (individual customers or organizations) who use the NonCash member portal to browse, purchase, manage, and gift vouchers.

---

## 1. Getting Started

### 1.1 Register an Account

1. Open the NonCash member portal or mobile app.
2. Click **Register**.
3. Enter your full name, phone number and email address.
4. Choose a password (at least 8 characters).
5. Click **Create Member Account** — you are signed in immediately.

The system creates a Customer record and a linked MemberAccount. Your phone number and your
email address are the two identifiers you sign in with; **NonCash has no username**.

### 1.2 Log In

1. Open the member portal.
2. Enter the **phone number or email address** on your account, and your password.
3. Tap **Sign In**.

After login, the system issues a JWT token scoped to your Member identity.

**Forgot your password?** Tap **Forgot password?** under the password field and enter your phone
number or email. We send a sign-in link that works for **30 minutes**; open it and you are signed
in, then choose **Set a Password**. The confirmation is the same whether or not an account was
found, so check your spam folder — if nothing arrives, we hold no email address for you and you
should contact the brand that sent your voucher.

### 1.3 Home Screen

The member home screen shows:

- **Voucher Store** — browse available gift vouchers.
- **My Vouchers** — vouchers you currently own.
- **Gifts from friends** — vouchers a friend sent you, waiting for you to accept or decline.
- **Profile** — update personal information and password.

---

## 2. Voucher Store

The Voucher Store lists Gift-type vouchers that are Approved, Published, and currently valid.

### 2.1 Browse Available Vouchers

1. Tap **Voucher Store**.
2. Browse the catalog. Each voucher card shows:
   - Cover image (when the Brand has provided one)
   - Brand color accent bar
   - Display name (marketing name) and short description
   - Face value formatted in local currency (for example, "100,000 ₫")
   - Net value / price
   - Validity period and expiry date
   - Valid days of week (for example, "Mon–Fri") when the voucher is restricted to specific days
3. Tap a voucher to see details.

### 2.2 Voucher Details

The detail screen shows:

- Full terms and conditions (collapsible section)
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

### 3.6 Voucher Temporarily Unavailable

Occasionally a voucher cannot be purchased and the store shows a **"temporarily unavailable"** message. This happens when the issuing Brand's account is temporarily suspended for new sales on the platform. No action is needed on your side — try again later or choose a voucher from another Brand.

> **Note:** This only affects **new purchases**. Vouchers you already own remain fully valid and can still be redeemed at POS or sent as a gift as usual.

---

## 4. My Vouchers

My Vouchers shows all vouchers currently assigned to you.

### 4.1 View Owned Vouchers

1. Tap **My Vouchers**. Your wallet loads automatically — there is nothing to type and no search button.
2. Each voucher card shows:
   - Cover image, or the brand icon on a colored banner (when the Brand provided one)
   - Brand name
   - Face value formatted in local currency
   - Expiry countdown (for example "27 days left"), turning orange in the last 3 days
   - Status badge:
     - `Available` — ready to use
     - `Expiring Soon` — close to the expiry date; use it before it lapses
     - `On its way` — you sent this voucher as a gift and it is still waiting for the other person to decide. It stays visible in your wallet with this badge, but it cannot be used at the counter until the gift is answered.
     - `Used` — already redeemed
     - `Expired` — past expiry date
3. The refresh icon (top right) reloads the wallet, for example after a gift is accepted.
4. Below the wallet, the **Gifts you sent** panel tracks every gift you have sent — see §5.5.

### 4.2 View Voucher Details

1. Tap a voucher card. A detail window opens with:
   - Cover image
   - Face value and status badge
   - Expiry countdown and the exact expiry date
   - Applicable stores
   - A **Terms & Conditions** link
   - For vouchers you can still use: the barcode and code to present at the checkout counter
   - For a voucher that is on its way as a gift: an information panel instead of the barcode, naming the recipient and the date the reservation ends, with a pointer to **Gifts you sent** if you want to cancel
2. **Gửi Quà** (bottom of the window) starts a gift for just this voucher. It is only shown for vouchers that are still usable and not already on their way.
3. Tap **Close** to return to the wallet.

### 4.3 Redeem at POS

To use a voucher at a participating outlet:

1. Open **My Vouchers** and tap the voucher you want to use.
2. Present the barcode in the detail window to the cashier (or read out the code printed under it).
3. The cashier scans or enters the code into the POS system.
4. The POS system validates the code and applies the discount to your bill.

> **Note:** A voucher marked `On its way` has been reserved for a gift and cannot be redeemed until the gift is answered. Open **Gifts you sent** below your wallet and tap **Cancel gift** to bring it straight back.

**Code validity and safety:**

- The displayed code is generated fresh each time you open the voucher and stays valid for **2 minutes**. If the cashier says the code has expired, close and re-open the voucher to show a new one.
- Treat the code like cash — anyone holding a currently valid code can redeem it. Never share screenshots of your code.

### 4.4 Vouchers from a Loyalty App

If a Brand you follow runs its own Loyalty App connected to NonCash, vouchers may be delivered to you through that app:

- Vouchers distributed by the Loyalty App appear in your NonCash wallet just like any other voucher.
- You can also view and redeem them from within the partner's Loyalty App, which reads your wallet through a secure integration.
- Redemption at POS works the same way regardless of which app you use to present the voucher.

---

## 5. Sending and Receiving Gifts

You can give a voucher you own to someone else, using their phone number. Gifts are free — no payment is involved.

A gift is never taken out of your wallet the moment you press send. The voucher stays visible with the badge
**On its way** and is reserved for your friend for **7 days**. It only leaves your wallet when they accept it.
If they decline it, ignore it, or you cancel it, the voucher comes straight back and is usable again.

### 5.1 Send a gift

1. Go to **My Vouchers**.
2. Pick the vouchers to give: tick **Select to send as a gift** on each card, or open a single voucher and tap **Gửi Quà**.
3. Tap **Gửi Quà** (the button shows how many vouchers you picked, for example `Gửi Quà (2)`).
4. Enter the recipients' phone numbers — one per line, exactly as many lines as selected vouchers. The window shows a live count of both sides, so a mismatch is visible before you send.
5. Optionally write a message under **Message with your gift (optional)** — up to 280 characters. It travels with every voucher in this send and becomes the first line of that gift's message box (see [5.9](#59-the-gift-message-box-hộp-thư-món-quà)).
6. Tap **Gửi Quà**.

> **Restrictions:**
> - You can only gift vouchers you own.
> - The voucher must still be unused and unexpired.
> - You cannot send a gift to your own phone number.
> - A voucher already on its way cannot be sent again until that gift is answered or cancelled.

### 5.2 What you are told after sending

Every send ends with a summary window that says exactly where each gift went.

| Your friend's situation | Status line in the window | The message you get |
| --- | --- | --- |
| Has an email address on file — whether or not they ever set a password | **Accept link emailed** | "We emailed {name} a secure link to accept your gift. We'll let you know the moment they decide — the gift stays reserved for 7 days." |
| On NonCash, but we hold no email for them | **Held — no email on file** | "{phone} has no email on file, so we can't send the accept link yet. Your gift is reserved for them for 7 days — ask them to sign in with that phone number to accept it." |
| Not on NonCash yet | **Held — not on NonCash yet** | "{phone} isn't on NonCash yet, so we're holding your gift for 7 days. Please ask them to register with that phone number — we'll email the accept link the moment they do." |

The window title tells you the overall outcome: **Your gift is on its way**, **Part of your gift went through**,
or **No gifts were sent**.

If a number could not be used, the same window lists it with the reason and what to do next, for example
`0913660575: that is your own number. Enter your friend's phone number to send them a gift.`

> **Your friend is not on NonCash yet:** we keep a seat for them. The instant they register with that exact phone
> number, the accept link is emailed to them automatically — you do not have to send the gift again.

### 5.3 Gifts from friends

1. Tap **Gifts from friends** in the side menu (or the button at the top of the **Gifts you sent** panel).
2. The **Show** filter defaults to **Waiting for me**; switch it to **Everything** to also see gifts you already answered.
3. Each card shows the brand, the value, who sent it, their note, and how long the gift is still reserved for you — for example `Reserved for you for 5 more days (until 17/09/2026 09:12).` In the last two days it reads `Accept by tomorrow, then it goes back to your friend.` and `Last day to accept — it goes back to your friend tonight.`
4. Every card also carries a **Messages** button — labelled with how many lines the conversation holds, for example `Messages (2)`, and badged with the number you have not read yet. It is there whether the gift is still waiting or long answered, so the conversation stays readable as a memory.
5. If nothing is waiting you see **No gifts waiting** — "When a friend sends you a voucher it appears here, and stays reserved for you for 7 days."

### 5.4 Accept a gift

1. On the gift card, tap **Accept gift**.
2. In the **Accept this gift** window, keep or edit the thank-you note. It is pre-filled with `Cảm ơn món quà của bạn!` — clear it if you would rather not send one.
3. Tap **Accept gift**. (Tap **Not now** to leave the gift waiting.)

The window then confirms: "The voucher from {name} is now in your wallet, and your thank-you note was sent to them. Open My Vouchers to see its code."

Behind that confirmation the system:

- Moves the voucher into your wallet — it appears in **My Vouchers** with its own barcode.
- Emails the sender that you accepted, together with your thank-you note.
- Records the gift in its history, so both of you can see it later.
- Adds you as a customer of the brand that issued the voucher, so that brand can send you its own offers.

### 5.5 Gifts you sent

Below your wallet, the **Gifts you sent** panel is the one place that tracks everything you have given away.
Its header reminds you: "Your friend has 7 days to accept. If they do not, the voucher comes back to you."

Each row shows when you sent it, the voucher (brand and serial number), who it went to, a status chip, and a
plain-language **What happened** column.

| Status | What happened says |
| --- | --- |
| **Waiting** | `Waiting for {name} — they have until {date}.` The row also carries a **Cancel gift** button. |
| **Accepted** | `{name} accepted your gift and wrote: "Cảm ơn món quà của bạn!"` |
| **Declined** | `{name} declined: "{reason}". The voucher is back in your wallet.` |
| **Expired** | `No answer within 7 days. The voucher is back in your wallet.` |
| **Cancelled by you** | `You cancelled this gift. The voucher stayed in your wallet.` |

Under that summary each row carries a **Messages** button — for example `Messages (3)` — badged with the number of
lines you have not read yet. Tap it to open the gift's message box ([5.9](#59-the-gift-message-box-hộp-thư-món-quà)).

The refresh icon in the panel header reloads it.

### 5.6 Cancel a gift you sent

1. In **Gifts you sent**, find the row with status **Waiting**.
2. Tap **Cancel gift**, then confirm in the **Cancel gift** window.

The reservation is released at once: the voucher loses its **On its way** badge and can be used or gifted again.
You can only cancel while the gift is still waiting.

### 5.7 Decline a gift

1. On the gift card under **Gifts from friends**, tap **Decline**.
2. The **Decline this gift** window explains that the voucher goes straight back to the sender's wallet and that they are told you declined.
3. Optionally type a **Reason** — it is sent with the notice — then tap **Decline gift**. Tap **Keep it for now** to leave the gift waiting instead.

The window then confirms: "The voucher went back to {name}'s wallet and they were told you declined it."

### 5.8 Gift expiry

If your friend neither accepts nor declines within 7 days, the gift expires automatically: the reservation is
released, the voucher is usable in your wallet again, you are emailed that nobody answered in time, and the
**Gifts you sent** panel shows status **Expired**. You are free to send the voucher to someone else.

### 5.9 The gift message box (Hộp thư món quà)

Every gift keeps its own conversation, stored with the gift. It is there as a memory of the exchange — what was
written on the way in, and what was answered — long after the voucher has been used.

**What a conversation holds**

| Line | Written by | Label you see |
| --- | --- | --- |
| The note that travelled with the gift | the sender, while sending | **Sent with the gift** |
| The thank-you left when accepting | the recipient, on accepting | **Thank-you** |
| The reason left when declining | the recipient, on declining | **Decline reason** |
| Anything either of them adds later | sender or recipient | **Message** |

**Where to open it**

- On a gift you received: **Gifts from friends** → the card's **Messages** button.
- On a gift you sent: **My Vouchers** → **Gifts you sent** → the row's **Messages** button.

Opening the box marks the lines addressed to you as read, so the unread badge clears.

**Writing a message**

Type up to **280 characters** under **Write a message** and tap **Send**. Only the two people in the gift can read
or write it — nobody else, not even the brand. Under the box it says so: "Only the two of you can read this. The
brand never sees it."

The other person is told by email ("{name} sent you a message about your gift"), and the message is stored whether
or not that email goes out.

**Limits**

| Limit | What happens |
| --- | --- |
| A conversation holds at most **20 messages** | Sending is refused: "This gift already holds 20 messages, the most one gift can keep. You can still read the conversation, but nothing more can be added to it." |
| One member may send at most **10 messages an hour** | "You have sent 10 messages in the last hour, which is the hourly limit. Wait a few minutes, then send this one again." |
| An empty or whitespace-only message | "Your message is empty. Write something, then send it again." |
| More than **500 characters** (only reachable through the API — the screen stops you at 280) | "Your message is {n} characters and the limit is 500. Shorten it, then send it again." |

**When a conversation closes**

Writing stays open while the gift is **Waiting** and after it is **Accepted** — an accepted gift keeps its thread,
because that is when most thank-yous are written. It closes for every other outcome, and the box says why instead
of showing an empty field:

| Outcome | What the box tells you |
| --- | --- |
| Declined | "This gift was declined, so its conversation closed on {date}. The voucher went back to the sender — send a new gift to start a fresh one." |
| Expired | "Nobody accepted this gift before {date}, so its conversation closed. The voucher is back in the sender's wallet and can be sent again." |
| Cancelled by the sender | "The sender cancelled this gift on {date}, so its conversation closed. The voucher is back in the sender's wallet and can be sent again." |

A closed conversation stays readable — only writing stops.

**What the brand sees**

The brand that issued the voucher can read the note that travelled with it and the reply it produced, in its own
read-only **Gifts** log. Follow-up messages between the two of you are never shown to the brand, and the log says
so: "Only the note sent with the gift and the reply it produced are shown here. Private follow-up messages between
the two customers stay between them."

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

Your phone number and email address are both login identifiers, and both must stay unique
platform-wide. Contact platform support if you need to change your phone number.

---

## 7. Common Tasks Quick Reference

| Task | Path |
| --- | --- |
| Browse vouchers | Voucher Store |
| Purchase a voucher | Voucher Store > Select voucher > Buy Now |
| View owned vouchers | My Vouchers |
| Redeem at POS | My Vouchers > tap voucher > show barcode |
| Send a voucher as a gift | My Vouchers > tick vouchers > **Gửi Quà** (or tap a voucher > **Gửi Quà**) |
| Add a message to a gift you are sending | My Vouchers > **Gửi Quà** > **Message with your gift (optional)** |
| Read a gift's messages | My Vouchers > **Gifts you sent** > **Messages**, or Gifts from friends > **Messages** |
| Reply in a gift's message box | Open **Messages** > type in **Write a message** > **Send** |
| Track gifts you sent | My Vouchers > **Gifts you sent** panel |
| Accept a gift from a friend | Gifts from friends > **Accept gift** |
| Decline a gift from a friend | Gifts from friends > **Decline** |
| Cancel a gift you sent | My Vouchers > **Gifts you sent** > **Cancel gift** |
| Update profile | Profile |
| Change password | Profile > Change Password |
| Recover access when the password is lost | Sign-in screen > **Forgot password?** |

---

## 8. Troubleshooting

| Issue | Cause | Resolution |
| --- | --- | --- |
| "Invalid phone number/email or password." | The identifier or the password does not match an account | Re-enter the phone number or email on your account, or use **Forgot password?**. We do not say which half was wrong. |
| "Enter the phone number or email on your account…" | What was typed is neither a phone number nor an email — typically a former username | Sign in with your phone number or email. NonCash has no username. |
| "Your account was created automatically when a brand sent you a voucher, so it has no password yet." | You received a voucher before ever registering, so no password exists | Use **Forgot password?** to get a sign-in link and set one, or open that voucher email and use its sign-in link. |
| Sign-in link does not work | The link expired (30 minutes for a recovery link, 7 days for a voucher email) or was already used | Tap **Email me a new link** on that screen. |
| Forgot-password email never arrives | We hold no email address for you, or it went to spam | Check spam first; if nothing is there, contact the brand that sent your voucher. |
| "Your account is locked." | The account was locked, or the platform blacklisted the customer | Contact the brand that sent you the voucher and ask them to unlock it. |
| Voucher not visible in My Vouchers | Order not yet paid | Wait for payment confirmation. |
| Voucher image not displayed | Brand has not uploaded a cover image | The card falls back to brand name and face value. |
| Voucher rejected at POS on certain days | Voucher restricted by valid days of week | Check the "Valid days" shown on the voucher card (e.g., Mon–Fri only). |
| "Gửi Quà" button greyed out | No voucher is ticked yet | Tick **Select to send as a gift** on an `Available` voucher, or open the voucher and tap **Gửi Quà**. |
| "{phone}: this voucher cannot be gifted in its current state." | The voucher is used, expired, or already on its way | Open My Vouchers and check the badge. Only unused, unexpired vouchers that are not already on their way can be sent. |
| "{phone}: this voucher is already on its way to someone." | A gift for that voucher is still waiting | Wait for their answer, or cancel it under **Gifts you sent** and send it again. |
| Voucher shows "On its way" and has no barcode | It is reserved for a gift and cannot be used at the counter | Ask your friend to accept or decline it, or cancel the gift under **Gifts you sent**. |
| Gift shows "Expired" | Your friend did not answer within 7 days | The voucher is back in your wallet — you can use it or send it to someone else. |
| Friend says they never got the email | We hold no email for them, or they are not on NonCash yet | The send window already told you which. Ask them to register or sign in with that phone number; the accept link is emailed automatically once they have an account. |
| Message box shows a "closed" notice instead of a write field | The gift was declined, expired, or cancelled — writing stops then, reading does not | Read the conversation as it stands; to start a fresh one, send a new gift. See §5.9. |
| "This gift already holds 20 messages, the most one gift can keep." | One conversation reached its 20-message cap | Nothing more can be added to that gift. Everything already written stays readable. |
| "You have sent 10 messages in the last hour, which is the hourly limit." | The hourly rate limit for one member | Wait a few minutes and send it again — what you typed is kept in the box. |
| "Only the two people in this gift can read or write its messages." | The gift belongs to two other people | Open the gift from your own gift list; a message box is never shared outside its sender and recipient. |
| "We can't find that gift. Refresh your gift list and open it from there." | The gift was removed, or the screen is showing a stale row | Reload **My Vouchers** or **Gifts from friends** and open the gift again from the list. |
| Purchase failed with Insufficient Stock | Not enough vouchers available | Reduce quantity or choose another voucher. |
| Voucher shows "temporarily unavailable" | The issuing Brand is temporarily suspended for new sales | Try again later or pick another Brand; vouchers you already own still work. |

