# Session Log — 2026-09-06

## 1. [BUG] Brand Customers → Add: link-or-create fix

- Root cause: `CustomerService.CreateAsync` ran global phone/email existence checks and threw **before** considering the `brand_customers` mapping → BrandManager could never Add a globally-existing phone (e.g. a self-registered member).
- Fix: lookup-first flow. Phone exists globally → Admin (no brand scope) still 409; brand path → 409 only if the mapping already exists, otherwise **link** the existing customer (`EnsureAsync` with `Source=Manual`) and refresh name/email.
- Trap found: `CustomerRepository.GetByPhoneNumberAsync` is `AsNoTracking` → the link path calls `_customerRepository.Update(existing)` explicitly.
- **Latent bug discovered (unfixed, pending decision):** `UpsertAsync` (CSV import) updates existing customers' name/email **without** `Update()` → those updates are silently never persisted in production.
- Tests: unit 92/92 (1 re-stubbed + 3 new branch tests); integration 122/122 (new: BrandManager links a globally-existing unmapped customer → 201 + single global row + Manual mapping; second Add → 409).

## 2. [ASK] Email deliverability diagnosis → security incident

- Symptom: distribution emails logged `success=true` in `email_logs` but never arrived.
- `email_logs` proved config was correct (`Environment:Name=pilot` lifted the kill-switch; Gmail SMTP accepted the messages — `success=true` is written only after SMTP accept).
- Root cause (user's screenshots): sender mailbox `nguyentri.thuc@ms-apptech.com` was **compromised** — French CPF phishing spam blast in Sent (Sep 5) + flood of delivery delay/failure DSNs → Google throttled/holds the account's mail.
- Leak vector: the Gmail **app password is committed in git history** (4 commits, found via `git log -S`).
- Fix procedure handed to user (deferred): revoke app password → change account password → check filters/forwarding → new app password via env var (never commit) → rotate other secrets in the same file (DB, ZaloPay, VNPAY, MediaService) → SPF/DKIM/DMARC for ms-apptech.com → 24–72h cool-down → retest to the two authorized addresses.
- Rule recorded: real email test sends only to **lamhong.bac@gmail.com** / **lahoba2026@gmail.com**.
- Doc caveat identified (not yet written): `email_logs.success=true` = SMTP-accepted, **not** delivered; bounces return asynchronously to the sender mailbox.

## 3. [ASK] Redeem walkthrough + doc coverage check

- Verified member wallet (`/my-vouchers`: barcode rendered client-side at tap from the code) and POS flow (`/pos/redeem` web screen for BrandManager: outlet + bill → scan/type → Verify → Lock → Commit / Rollback; or API `POST /api/v1/pos/verify|lock|commit|rollback` with per-outlet `X-API-Key`).
- Doc coverage: `pos-integration-guide.md`, `api-contracts.md`, member guide §4.3 all present. **Gap: brand user guide has no operational section for the `/pos/redeem` screen.**

## 4. [ASK] Voucher-code security assessment → optimization plan

- Design verified: code = `base64({vid,iat,exp=+120s}).base64(HMAC-SHA256(payload, per-voucher 256-bit secret))`; code never persisted (only the secret); constant-time compare; atomic lock→commit with idempotency; transfer soft-lock; outlet scope checks.
- Verdict: forgery/double-spend effectively closed; realistic exposure = password-only member login (no OTP), eager minting of all codes at wallet load, 120s bearer window, dev-grade outlet keys (plaintext `ApiKeyPrefix` match — `integration_partners` has hash+prefix, outlets do not), `[AllowAnonymous]` customer search.
- Optimization plan (sorted easy→hard, user to pick next task): ① rollback owner-outlet check ② remove AllowAnonymous customer search ③ doc gaps ④ rate limiting auth+POS ⑤ outlet key hashing+rotation ⑥ mint-on-tap (also fixes the 120s demo gotcha) ⑦ code TTL 120→60s (after ⑥) ⑧ member OTP login (biggest risk closed) ⑨ optional deep defense.
- Pending non-security smalls: import lost-update bug (see §1), VoucherReceived template rewording, fill-empty-only rule for brand writes on the global customer record (product decision open).

## 5. Process: Work-Type Protocol adopted

- User established a standing rule: every request opens with a signal — `[STORY]` / `[BUG]` / `[CR]` / `[ASK]` — mapped to entry points, actions, and footprints (agent memory + this section).
- Persisted: `BMAD_STRUCTURE.md` gained the "Work-Type Protocol" section; `docs/index.md` Project Guidelines points to it.

## 6. Earlier in session (carried context)

- Admin user guide §9 (email on/off + templates, two-file appsettings model) — done previously.
- BMAD docs updated for the customer identity model: `customer-action-matrix.md` §1 (unique global customer + `brand_customers` ownership, creation-flow tables) and `data-models.md` (`Customer` identity note + `BrandCustomer` entity block).

## 7. [CR-LOG] + [CR] Security assessment persisted to docs; protocol refined

- Protocol refinement (user fine-tune): a CR is born in discussion → `[CR-LOG]` registers it in the source-of-truth docs with a tagged ID (`CR-YYYY-MM-DD-nn`) and explicit status (no code) → a later `[CR]` implements it and flips the status. Persisted in `BMAD_STRUCTURE.md` (new `[CR-LOG]` row + CR lifecycle note) and pointed to from `docs/index.md`.
- `docs/security-hardening-checklist.md`: new §5 "Voucher Code Security Posture (assessed 2026-09-06)" — verified design, verdicts table, and the 9-item optimization backlog registered as CR-2026-09-06-01…09 (easy→hard, with status column).
- `docs/pos-integration-guide.md` §3: corrected the voucher-code format doc (was a fake short `NCF-…` example; now the real signed base64 payload + 120s TTL + never-cache/reuse rule); verify/lock request examples updated to a scan placeholder.
- `docs/user-guides/brand-user-guide.md`: new §6 "Voucher Redemption at POS" (web screen `/pos/redeem` flow, rules/failure modes, API-integration pointer); old §6–§10 renumbered to §7–§11; quick-ref and troubleshooting rows added.
- `docs/user-guides/member-user-guide.md` §4.3: code validity (fresh per view, 2-minute TTL) + bearer-code safety note.
- `docs/user-guides/admin-user-guide.md` §9.6 + troubleshooting: `success=true` = SMTP-accepted, not delivered; bounces return asynchronously to the sender mailbox — plus a matching troubleshooting row.
- CR-2026-09-06-03 (doc gaps) marked Implemented by this pass; the other 8 security CRs await the user's `[CR]` decision.
