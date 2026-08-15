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
9. ☐ Implement rate limiting on login/password-reset endpoints
10. ☐ Set up alerting for failed login attempts

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

**Next review date:** 2026-09-15  
**Owner:** Dev Team  
**Approved by:** _TBD_
