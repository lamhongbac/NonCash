# POS Cashier Guide — NonCash Voucher Redemption

A short guide for store staff using the NonCash POS app (or the web POS page).
One rule covers everything: **the screen always tells you which buttons matter. If a button is
hidden or greyed out, there is a reason — do not work around it.**

---

## 1. The two redemption modes

Your outlet is configured for exactly one mode. The payment screen names the active mode
under the Amount Used field.

| Mode | Buttons | When used |
|---|---|---|
| **One Click** (default) | VERIFY (optional), REDEEM, CLEAR | Fast checkout: REDEEM locks and commits in one press |
| **Three Step** | VERIFY, LOCK, COMMIT, ROLLBACK, CLEAR | Stores that want to hold the voucher while the bill is finalized |

The mode is set per outlet by your Brand Manager (Outlets screen → POS Redemption Mode).
It is not a setting on the terminal.

---

## 2. One Click — the normal sale

1. Type or scan the customer's voucher code (the rotating code from the member app — valid
   2 minutes; serial numbers starting `VC-` are **not** scannable).
2. **Amount Used** — type the amount the bill actually absorbed (what the customer is paying
   with this voucher). Leave empty for the full face value. The voucher is consumed in full
   either way; this number only records how much of the bill it covered. Do **not** type more
   than the face value — the system refuses and tells you.
3. Bill Number — auto-generated if empty.
4. Press **REDEEM**.

Done = green "Voucher redeemed" message and a row in Shift Transactions.

**If REDEEM fails**: the error message says what happened and what to do. When the failure
left a hold on the voucher (for example the amount was too high), the screen switches to
COMMIT / ROLLBACK — correct the amount and press COMMIT, or press ROLLBACK to release the
customer's voucher.

---

## 3. Three Step — hold first, finish later

1. VERIFY — checks the voucher is valid at this store. Optional but recommended.
2. LOCK — holds the voucher for this bill. **While the hold is active, the voucher cannot be
   used anywhere else** — including by the customer on their phone.
3. COMMIT — finishes the redemption (writes the usage).
4. ROLLBACK — releases the hold if the sale does not proceed.

**While a hold is active**, VERIFY and LOCK disappear (web page: greyed out) and an orange
"Locked" banner appears. Only COMMIT, ROLLBACK and CLEAR remain. This is deliberate: starting
a second transaction would abandon the live hold and leave the voucher stuck for up to
10 minutes.

---

## 4. What "Clear" does — and does not do

CLEAR/RESET wipes this screen (code, bill number, amount). It does **not** release a hold that
is already open on the server.

- If you pressed CLEAR while a hold was active, the voucher stays unavailable until the hold
  expires on its own (about 10 minutes) — or until someone presses ROLLBACK on the original
  screen.
- **If you then scan the same voucher again with a new auto-generated bill number**, the
  system reports **AlreadyInUse**. This is correct, not a bug: the voucher is still held
  against the first bill. Recovery:
  1. Type the **original bill number** back into Bill Number and press REDEEM/COMMIT —
     the same voucher against the same bill number succeeds (the hold matches), **or**
  2. Wait for the hold to expire (~10 minutes) and scan again.

The safe habit: **ROLLBACK before CLEAR** whenever a hold is active.

---

## 5. Failure messages — what they mean and what to do

Every failure message states the cause and the next action. The common ones:

| Message says | Meaning | Do this |
|---|---|---|
| Code expired | The rotating code is older than 2 minutes | Ask the customer to re-open the voucher in their app and scan again |
| Already in use | The voucher is held by another bill/terminal | See §4 recovery |
| Amount exceeds value | Typed amount is higher than the face value | Correct Amount Used, press COMMIT/REDEEM again — the voucher is still held |
| Lock expired | The hold timed out (10 minutes) | Scan the code again and redo the redemption |
| Already complete | The voucher was already redeemed (possibly by you, in a retry) | Check Shift Transactions; do not redeem again |
| Terminal key mismatch | This terminal's setup belongs to a different store | Tell your manager — the terminal must be re-provisioned with this store's Setup Key |
| Outlet not authorized | This voucher is not valid at this store | Tell the customer which stores accept it (shown on Verify) |

A message that says "wait 60 seconds" (rate limit) means exactly that — retry once after a minute.

---

## 6. End of shift

- The **Shift Transactions** list on the Payment screen shows everything redeemed at this
  terminal this shift (time, bill, voucher, amount, status).
- Your Brand Manager sees the same redemptions in the web **Redemptions** report
  (bill amount vs face value, per store, per day).
- Log out at end of shift; the next cashier logs in with their own account.
