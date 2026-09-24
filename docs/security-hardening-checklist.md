# Security Hardening Checklist — NonCash Platform

**Created:** 2026-08-15  
**Trigger:** Ransomware attack on DEV PostgreSQL server (2026-08-15)  
**Root cause:** PostgreSQL exposed to internet with `0.0.0.0/0` in `pg_hba.conf`

---

## 1. Network / Server

### 1.1 Firewall & Network Isolation

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 1.1.1 | Block all inbound traffic except required ports (443, 3389) | ☐ | P0 | Use Windows Firewall or cloud security group |
| 1.1.2 | Never expose database port (5432) to `0.0.0.0/0` | ☐ | P0 | Already fixed — verify in `pg_hba.conf` |
| 1.1.3 | Restrict RDP (3389) to known admin IPs only | ☐ | P0 | Or use VPN/bastion host |
| 1.1.4 | Set up WireGuard/OpenVPN for developer access | ☐ | P1 | When team grows beyond 1-2 devs |
| 1.1.5 | Enable IDS/IPS on network perimeter | ☐ | P2 | Detect port scanning and brute force attempts |
| 1.1.6 | Implement network segmentation (DB in private subnet) | ☐ | P2 | If moving to cloud infrastructure |

### 1.2 Server Hardening

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 1.2.1 | Disable unused Windows services | ☐ | P1 | Reduce attack surface |
| 1.2.2 | Enable Windows Firewall on all profiles | ☐ | P0 | Domain, Private, Public |
| 1.2.3 | Configure Windows Update for automatic security patches | ☐ | P1 | Or scheduled monthly patching |
| 1.2.4 | Enable audit logging for logon events | ☐ | P1 | Track who accesses the server |
| 1.2.5 | Disable password authentication for RDP, use RDP + MFA | ☐ | P1 | Or certificate-based auth |
| 1.2.6 | Set up fail2ban equivalent for Windows (e.g., IPBan) | ☐ | P2 | Block IPs after failed login attempts |
| 1.2.7 | Disable SMBv1, enforce SMB signing | ☐ | P1 | Prevent lateral movement |
| 1.2.8 | Enable BitLocker for disk encryption | ☐ | P2 | Protect data at rest |

### 1.3 Monitoring & Alerting

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 1.3.1 | Set up centralized logging (SIEM or cloud log service) | ☐ | P1 | Aggregate logs from all sources |
| 1.3.2 | Alert on failed login attempts (>5 in 5 min) | ☐ | P1 | Possible brute force |
| 1.3.3 | Alert on unusual outbound traffic from DB server | ☐ | P1 | Data exfiltration indicator |
| 1.3.4 | Alert on PostgreSQL service stop/restart | ☐ | P1 | Unexpected downtime |
| 1.3.5 | Monitor disk space usage with threshold alerts | ☐ | P2 | Prevent service crashes |
| 1.3.6 | Set up uptime monitoring (ping/health check) | ☐ | P2 | Detect outages quickly |

---

## 2. Database (PostgreSQL)

### 2.1 Authentication & Access Control

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 2.1.1 | `pg_hba.conf` restricted to localhost + server IP only | ✅ | P0 | Done — verify periodically |
| 2.1.2 | `postgres` superuser password changed to strong password | ✅ | P0 | Done |
| 2.1.3 | Application user (`noncash_app`) uses strong password | ✅ | P0 | Done |
| 2.1.4 | Application user has minimal required privileges | ☐ | P0 | No CREATE on public schema in prod |
| 2.1.5 | Separate users for app, migrations, and backups | ☐ | P1 | Principle of least privilege |
| 2.1.6 | Rotate database passwords every 90 days | ☐ | P2 | Automate with secrets manager |
| 2.1.7 | Disable `postgres` user remote login entirely | ☐ | P1 | Only local admin access |
| 2.1.8 | Use SCRAM-SHA-256 instead of MD5 for password hashing | ☐ | P1 | Stronger auth method |

### 2.2 Encryption

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 2.2.1 | Enable SSL/TLS for all database connections | ☐ | P0 | `sslmode=Require` in connection strings |
| 2.2.2 | Configure PostgreSQL with valid SSL certificate | ☐ | P0 | Use Let's Encrypt or internal CA |
| 2.2.3 | Enable Transparent Data Encryption (TDE) | ☐ | P2 | Protect data at rest |
| 2.2.4 | Encrypt backups at rest | ☐ | P1 | GPG or AES-256 encryption |

### 2.3 Backup & Recovery

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 2.3.1 | Automated daily backups to separate storage | ☐ | P0 | Not on same server! |
| 2.3.2 | Test backup restoration monthly | ☐ | P1 | Verify backups actually work |
| 2.3.3 | Keep 30 days of incremental backups | ☐ | P1 | Point-in-time recovery |
| 2.3.4 | Keep 4 weekly full backups (offsite) | ☐ | P1 | Disaster recovery |
| 2.3.5 | Document and test DR procedure | ☐ | P1 | Runbook for rebuild scenario |
| 2.3.6 | Use `pg_basebackup` for physical backups | ☐ | P2 | Faster than logical dumps |
| 2.3.7 | Enable WAL archiving for PITR | ☐ | P2 | Point-in-time recovery |

### 2.4 PostgreSQL Configuration

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 2.4.1 | Set `listen_addresses = 'localhost'` in postgresql.conf | ☐ | P0 | Don't bind to all interfaces |
| 2.4.2 | Change default PostgreSQL port (optional) | ☐ | P2 | Security through obscurity |
| 2.4.3 | Set `log_connections = on` | ☐ | P1 | Track who connects |
| 2.4.4 | Set `log_disconnections = on` | ☐ | P1 | Track session duration |
| 2.4.5 | Set `log_statement = 'ddl'` | ☐ | P1 | Log schema changes |
| 2.4.6 | Set `log_line_prefix` to include user/db/host | ☐ | P1 | Better audit trail |
| 2.4.7 | Configure `statement_timeout` to prevent long queries | ☐ | P2 | Prevent DoS via queries |
| 2.4.8 | Set `max_connections` appropriately | ☐ | P2 | Prevent connection exhaustion |
| 2.4.9 | Remove `readme_to_recover` database if it reappears | ☐ | P0 | Immediate alert + drop |

---

## 3. Application

### 3.1 Secrets Management

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 3.1.1 | No secrets in source code / Git | ✅ | P0 | Using .NET User Secrets |
| 3.1.2 | Use Azure Key Vault / AWS Secrets Manager for prod | ☐ | P0 | Centralized secrets management |
| 3.1.3 | Rotate SMTP credentials periodically | ☐ | P2 | Every 90 days |
| 3.1.4 | Rotate JWT signing keys annually | ☐ | P2 | With zero-downtime rotation |
| 3.1.5 | Scan Git history for leaked secrets (git-secrets/trufflehog) | ☐ | P1 | Check for accidental commits |

### 3.2 Authentication & Authorization

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 3.2.1 | JWT tokens have reasonable expiry (15-60 min) | ☐ | P1 | Short-lived access tokens |
| 3.2.2 | Implement refresh token rotation | ☐ | P1 | Detect token theft |
| 3.2.3 | Enforce MFA for admin users | ☐ | P1 | Critical for production |
| 3.2.4 | Implement account lockout after failed logins | ☐ | P1 | 5 attempts → 15 min lockout |
| 3.2.5 | Rate limit login endpoint | ☐ | P1 | Prevent brute force |
| 3.2.6 | Validate password complexity (min 12 chars, mixed case, numbers, symbols) | ☐ | P1 | Enforce at registration |
| 3.2.7 | Implement password history (prevent reuse of last 5) | ☐ | P2 | When changing password |
| 3.2.8 | Log all authentication events | ☐ | P1 | Success and failure |

### 3.3 Input Validation & Injection Prevention

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 3.3.1 | All user inputs validated and sanitized | ☐ | P0 | Prevent SQL injection |
| 3.3.2 | Use parameterized queries (EF Core does this) | ✅ | P0 | Already using EF Core |
| 3.3.3 | Validate file uploads (type, size, content) | ☐ | P1 | Prevent malicious uploads |
| 3.3.4 | Implement CORS policy (restrict origins) | ☐ | P1 | Only allow known frontends |
| 3.3.5 | Set Content-Security-Policy headers | ☐ | P2 | Prevent XSS |
| 3.3.6 | Enable request size limits | ☐ | P1 | Prevent DoS via large payloads |

### 3.4 API Security

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 3.4.1 | Rate limiting on all API endpoints | ☐ | P1 | Prevent abuse |
| 3.4.2 | Rate limiting on sensitive endpoints (login, register, password reset) | ☐ | P0 | Stricter limits |
| 3.4.3 | Implement API versioning | ☐ | P2 | Allow safe evolution |
| 3.4.4 | Disable Swagger/OpenAPI in production | ☐ | P0 | Information disclosure |
| 3.4.5 | Validate Content-Type headers | ☐ | P2 | Prevent content-type confusion |
| 3.4.6 | Implement request signing for critical operations | ☐ | P2 | Prevent replay attacks |

### 3.5 Logging & Audit

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 3.5.1 | Log all authentication events (success/failure) | ☐ | P1 | Security audit trail |
| 3.5.2 | Log all authorization failures | ☐ | P1 | Detect privilege escalation |
| 3.5.3 | Log all data modification operations | ☐ | P1 | Who changed what, when |
| 3.5.4 | Include correlation IDs in logs | ☐ | P2 | Trace requests across services |
| 3.5.5 | Ship logs to centralized logging (not just local files) | ☐ | P1 | Prevent log tampering |
| 3.5.6 | Set up alerts for suspicious patterns | ☐ | P2 | Multiple failures, unusual access |
| 3.5.7 | Retain logs for 90 days minimum | ☐ | P2 | Compliance and forensics |

### 3.6 HTTPS & Transport Security

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 3.6.1 | Enforce HTTPS on all endpoints | ☐ | P0 | Redirect HTTP → HTTPS |
| 3.6.2 | Use strong TLS ciphers only (TLS 1.2+) | ☐ | P0 | Disable TLS 1.0/1.1 |
| 3.6.3 | Enable HSTS (HTTP Strict Transport Security) | ☐ | P1 | Prevent downgrade attacks |
| 3.6.4 | Set Secure flag on all cookies | ☐ | P1 | HTTPS-only cookies |
| 3.6.5 | Set HttpOnly flag on auth cookies | ☐ | P1 | Prevent XSS cookie theft |
| 3.6.6 | Set SameSite=Strict on cookies | ☐ | P1 | Prevent CSRF |

### 3.7 Dependency Security

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 3.7.1 | Run `dotnet list package --vulnerable` regularly | ☐ | P1 | Check for vulnerable NuGet packages |
| 3.7.2 | Enable Dependabot or similar for automated alerts | ☐ | P1 | Auto-detect vulnerable deps |
| 3.7.3 | Pin dependency versions (no floating versions) | ☐ | P2 | Prevent supply chain attacks |
| 3.7.4 | Scan Docker images for vulnerabilities (if containerized) | ☐ | P2 | Use Trivy or Snyk |

---

## 4. Operational Procedures

### 4.1 Incident Response

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 4.1.1 | Document incident response procedure | ☐ | P1 | Who to call, what to do |
| 4.1.2 | Define escalation matrix | ☐ | P1 | Severity levels and contacts |
| 4.1.3 | Conduct tabletop exercise quarterly | ☐ | P2 | Practice incident response |
| 4.1.4 | Maintain contact list for ISPs, hosting, law enforcement | ☐ | P2 | Quick access during incident |

### 4.2 Regular Maintenance

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 4.2.1 | Monthly security patch review | ☐ | P1 | Windows + PostgreSQL updates |
| 4.2.2 | Quarterly penetration test | ☐ | P2 | External security assessment |
| 4.2.3 | Annual security audit | ☐ | P2 | Comprehensive review |
| 4.2.4 | Review and update this checklist quarterly | ☐ | P1 | Keep it current |

### 4.3 Access Review

| # | Item | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 4.3.1 | Quarterly review of database user accounts | ☐ | P1 | Remove unused accounts |
| 4.3.2 | Quarterly review of admin user access | ☐ | P1 | Principle of least privilege |
| 4.3.3 | Immediate revocation on employee/contractor departure | ☐ | P0 | Offboarding checklist |
| 4.3.4 | Review firewall rules quarterly | ☐ | P1 | Remove unnecessary openings |

---

## 5. Voucher Code Security Posture (assessed 2026-09-06, extended 2026-09-24)

Assessment of the voucher-code solution against forgery, theft, and misuse. Verified against code (`VoucherCodeService`, `PosService`, `MembersController`, `ApiKeyMiddleware`).

### 5.1 Verified design

- Code format: `base64(payload: voucher id + issued-at + expiry) . base64(HMAC-SHA256(payload, perVoucherSecret))` — a long opaque string, **not** a short human-readable code.
- Per-voucher random 256-bit secret stored server-side; the code itself is **never persisted** — minted fresh on each wallet view, dead after **120 seconds**.
- Constant-time HMAC comparison; forged attempts are logged with verify result `Forged`.
- Spend integrity: atomic `Pending → InUse → Complete` transitions with idempotency keys, compensating rollback, and auto-expiring 10-minute locks; a pending member-to-member transfer soft-locks the voucher against redemption.
- Verify enforces outlet scope (the outlet must belong to the plan's brand or be in scope via a sponsored campaign) and plan expiry.
- POS auth: per-outlet API keys (`X-API-Key` header, outlet resolved from the key). Wallet auth: member JWT with an ownership check.
- Identity & tenant isolation (extended 2026-09-24): JWTs are validated on issuer/audience/lifetime/signing key with no hardcoded defaults (`JwtSigningKey.Resolve` fails fast); roles are separated (Admin / BrandManager / StoreStaff / member); `BrandScopeMiddleware` + `ICurrentUserService` enforce brand scoping at the query layer, so a BrandManager of brand A cannot read brand B's data; POS identity is resolved from the outlet API key and cross-checked against the request body (`TerminalKeyMismatch`).
- Secrets hygiene (extended 2026-09-24): git-tracked `appsettings*.json` keep empty values — real values come from the per-environment key → env-var chain or user-secrets; `Smtp:Password` was removed from tracked files (CR-2026-09-06-13); email has an environment kill-switch; all credentials present in pre-2026-09-11 git history are treated as exposed and must be rotated at their providers (Immediate Actions #11).
- Error contract (extended 2026-09-24): every failure states cause + next action (CR-2026-09-09-24), and business outcomes return HTTP 200 with a machine reason while genuine transport errors are 4xx/5xx — this prevents retry-storms against rate-limited endpoints and panic commits on ambiguous errors.

### 5.2 Verdicts

| Threat | Posture |
|---|---|
| Forgery | Strong — per-voucher secret + HMAC + constant-time compare |
| Double-spend | Strong — atomic status transitions + idempotency |
| Code theft (shoulder-surf / screenshot within the 120s window) | Moderate in absolute terms — the code is a bearer token; mitigated only by the TTL. Relative to market practice this is a strength, not a weakness: static codes used by typical voucher platforms stay valid indefinitely once copied, while a copied NonCash token self-destructs within its ≤120 s window and is single-use |
| Member account takeover | Weak — password-only member login, no OTP |
| Outlet key compromise | Weak — outlet keys are matched on plaintext prefix (dev-grade; `integration_partners` keys are stored hashed, outlet keys are not) and there is no rotation procedure |
| Customer data enumeration | Moderate — customer search requires BrandManager/Admin (CR-2026-09-06-02), and the anonymous auth endpoints plus every POS call are now rate limited (CR-2026-09-06-04). Residual: `staff-login` and `member-auth` are one *global* bucket rather than per-IP, so they throttle an abuser and every legitimate user alike |

### 5.3 Optimization backlog (CR registry)

Sorted by implementation ease (easy → hard), decoupled from risk ranking. Each item is a registered CR — implementation starts only on the `[CR]` command (see the Work-Type Protocol in `BMAD_STRUCTURE.md`).

| CR ID | Item | Effort | Status |
|---|---|---|---|
| CR-2026-09-06-01 | Restrict POS rollback to the lock-owning outlet (closes cross-outlet DoS) | ~1–2h | ✅ Implemented 2026-09-11 — `RollbackAsync(lockId, outletId, …)` puts the outlet condition inside the atomic `ExecuteUpdateAsync` WHERE clause, so a commit landing between check and release cannot be undone; a foreign key gets `OutletMismatch` and the voucher keeps its lock. Pinned by 2 integration facts in `PosPipelineTests`. See `customer-action-matrix.md` §9 |
| CR-2026-09-06-02 | Remove `[AllowAnonymous]` from customer search (require an authenticated brand/admin role) | ~2h | ✅ Verified 2026-09-07 (docs-only re-check: no `[AllowAnonymous]` remains on any customer endpoint — `CustomersController` is class-level `[Authorize(Roles = "BrandManager,Admin")]`; remaining anonymous endpoints are login / self-register / public catalog / payment callbacks by design) |
| CR-2026-09-06-03 | Doc gaps: brand-guide POS-redeem section + admin-guide "success = SMTP-accepted, not delivered" caveat | ~1–2h | ✅ Verified 2026-09-06 (docs-only; user review = verification) |
| CR-2026-09-06-04 | Rate limiting on `/api/v1/auth/*` + `/api/v1/pos/verify` (ASP.NET Core RateLimiter middleware) | ~0.5d | ✅ Implemented 2026-09-11 — two **partitioned** policies: `pos-outlet` (120/min, declared on the `PosController` class so a new POS endpoint is throttled by default) and `auth-recovery` (5/min per client IP) covering `login`, `forgot-password`, `reset-password` and `magic-link`, which had no limiter at all. POS partitions on the SHA-256 of the raw `X-API-Key` header, not on the IP: every terminal in a store sits behind one NAT address, and `UseRateLimiter` runs before `ApiKeyMiddleware` so the outlet is not resolved yet. 429 bodies state the wait and the next action; `PosApiClient` now maps 401/429 to `TerminalRejected` / `RateLimited` / `ServiceRejected` instead of deserializing an error body into an all-null result and showing the cashier a blank screen. Pinned by 5 integration facts. **Still open:** `staff-login` and `member-auth` use `AddFixedWindowLimiter`, i.e. one *global* bucket, so their "per client IP" comment is wrong — see CR-2026-09-06-04's row in `customer-action-matrix.md` §9 |
| CR-2026-09-06-05 | Outlet API key hashing + rotation endpoint (align with the `integration_partners` hash+prefix pattern) | ~1d | Registered — pending decision |
| CR-2026-09-06-06 | Mint-on-tap: mint the code on demand per tap instead of eagerly for the whole wallet (also fixes the 120s demo gotcha) | ~1d | Registered — pending decision |
| CR-2026-09-06-07 | Shrink code TTL 120s → 60s (only after CR-2026-09-06-06) | ~2h | Registered — pending decision |
| CR-2026-09-06-08 | Member OTP login (closes the biggest residual risk; needs SMS/email OTP infrastructure) | ~2–3d | Registered — pending decision |
| CR-2026-09-06-09 | Optional deep defense (device binding, per-outlet velocity checks, alerting on bursts of `Forged` results) | TBD | Registered — pending decision |

### 5.4 Security as a selling point — positioning vs market practice (owner-approved 2026-09-24)

The owner-approved lead pair is **code self-destruct** and **invoice-level traceability**. Frame every B2B security conversation as: threat → typical market practice → NonCash design → the sentence to sell with.

| Threat | Market practice (static-code platforms, printed vouchers) | NonCash design | The sentence to sell |
|---|---|---|---|
| Leaked / copied code | A copied or leaked code stays valid until someone redeems it — often months | Rotating HMAC-signed token, single-use, self-destructs ≤ 120 s | "Mã lộ không giết chiến dịch của anh — ảnh chụp tự vô hiệu trong 2 phút." |
| Double-spend at the counter | Two tills can both succeed with a static code; the brand eats the dispute | Atomic `Pending → InUse → Complete` inside a single UPDATE; 10-min lock TTL with auto-cleanup | "Hai thu ngân không bao giờ cùng thắng — không tranh chấp tiền giữa các cửa hàng." |
| Campaign blindness | Provider reports only "used / not used" | Every redemption binds bill number + POS No + operator + outlet; the brand redemption report separates face value from the amount the bill actually absorbed (breakage) | "Brand lần đầu tiên thấy voucher kéo bao nhiêu doanh thu thật — tới tận số hóa đơn." |

Honest limit — never over-promise: we sell "**copy also dies**", never "cannot be copied". Bearer-within-120 s is a registered residual risk (CR-2026-09-06-06/07/08/09); identity-bound redemption would close it entirely at the cost of the forward-as-a-gift property — a product decision recorded in §5.3.

---

## Priority Legend

| Priority | Meaning | Timeline |
|----------|---------|----------|
| **P0** | Critical — immediate action required | Within 24 hours |
| **P1** | High — significant security impact | Within 1 week |
| **P2** | Medium — defense in depth | Within 1 month |

---

## Immediate Actions (Do Today)

Based on the ransomware incident, these P0 items should be verified/completed immediately:

1. ✅ `pg_hba.conf` restricted (already done)
2. ✅ Database passwords changed (already done)
3. ☐ Verify `listen_addresses = 'localhost'` in `postgresql.conf`
4. ☐ Enable SSL/TLS for database connections
5. ☐ Set up automated daily backups to **separate storage**
6. ☐ Verify Windows Firewall blocks port 5432 from internet
7. ☐ Disable Swagger in production
8. ☐ Enforce HTTPS on all endpoints
9. ✅ Rate limiting on login/password-reset endpoints (CR-2026-09-06-04, 2026-09-11): `auth-recovery` 5/min per client IP on `login` / `forgot-password` / `reset-password` / `magic-link`, `pos-outlet` 120/min per outlet key on every POS call
10. ☐ Set up alerting for failed login attempts
11. ☐ **Rotate every credential committed before 2026-09-11** (CR-2026-09-06-13). The values are out of the working tree — all four tracked `appsettings*.json` files, the CI workflow, four docs, a generated wiki page and `tools/DbQuery` — but they remain in git history, so the DB passwords, the JWT signing key, the SMTP app password, the MSA media key and the ZaloPay/VNPAY keys must all be treated as exposed and replaced at their providers

---

## Verification Commands

```powershell
# Check PostgreSQL is only listening on localhost
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -c "SHOW listen_addresses;"

# Check pg_hba.conf rules
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -c "SELECT * FROM pg_hba_file_rules;"

# Check SSL is enabled
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -c "SHOW ssl;"

# Check active connections
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -c "SELECT usename, client_addr, state FROM pg_stat_activity;"

# Check Windows Firewall rules
Get-NetFirewallRule | Where-Object { $_.DisplayName -like "*postgres*" -or $_.DisplayName -like "*5432*" }

# Check for listening ports
Get-NetTCPConnection -LocalPort 5432 | Select-Object LocalAddress, State, OwningProcess
```

---

**Next review date:** 2026-12-24  
**Owner:** Dev Team  
**Approved by:** _TBD_
