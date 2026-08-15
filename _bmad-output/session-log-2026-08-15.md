# Session Log — 2026-08-15

## Summary

Emergency response to a **ransomware attack** on the shared DEV PostgreSQL server at 45.119.87.247. The `noncash` database was dropped by an attacker and replaced with a `readme_to_recover` ransom database. Server was secured, database rebuilt from EF migrations + SeedTool, and all tests verified passing.

## Incident Timeline

### Discovery
- User reported database disappeared suddenly in the morning
- API startup showed: `3D000: database "noncash" does not exist`
- Server at 45.119.87.247 was **reachable** and authenticating, but the database was gone
- Found `readme_to_recover` database — classic PostgreSQL ransomware pattern

### Root Cause
- `pg_hba.conf` had `0.0.0.0/0` (open to the entire internet)
- Attacker scanned for open port 5432, gained access via `postgres` superuser
- Dropped all user databases, left ransom note

## Security Hardening (done by user on the server)

| Action | Detail |
|--------|--------|
| `postgres` superuser password | Changed via pgAdmin |
| `noncash_app` password | Changed to `NonCashMachine@2026` |
| `pg_hba.conf` | Restricted to `127.0.0.1/32`, `::1/128`, `45.119.87.247/32` only |
| `readme_to_recover` database | Dropped via pgAdmin Query Tool (as postgres superuser) |
| PostgreSQL service | Restarted via services.msc |

## Database Rebuild

| Step | Result |
|------|--------|
| Connection strings updated | Both `appsettings.json` and `appsettings.Development.json` — new `noncash_app` password |
| Database created | `noncash` created via pgAdmin (owner: `noncash_app`) |
| EF Migrations applied | **29 migrations** applied via `dotnet ef database update` with explicit connection string |
| SeedTool fixed | Added missing `INotificationService` DI registration (`ConsoleNotificationService`) |
| SeedTool executed | Business, Brand, Outlet, 3 Customers, 2 MemberAccounts, 1 Staff UserAccount, 1 Voucher Plan, 3 Vouchers |
| Admin user seeded | `admin` / `Admin@123` (auto-seeded by API startup via `DatabaseSeeder.SeedAdminAsync`) |

## Files Modified

| File | Change |
|------|--------|
| `src/NonCash.API/appsettings.Development.json` | New `noncash_app` password in connection string |
| `src/NonCash.API/appsettings.json` | New password in `DefaultConnection`, `DevConnection`, `PilotConnection` |
| `src/NonCash.SeedTool/Program.cs` | Added `INotificationService` → `ConsoleNotificationService` DI registration |

## Test Credentials

| Role | Username | Password |
|------|----------|----------|
| Admin | `admin` | `Admin@123` |
| Brand Manager | `brandmanager` | `Test@123` |
| Member (Alice) | `alice` | `Test@123` |
| Member (Bob) | `bob` | `Test@123` |

## Build & Test Results

| Metric | Value |
|--------|-------|
| Build errors | 0 |
| Build warnings | 22 (non-breaking: MudBlazor PanelClass + nullable annotations) |
| Unit tests | **61 passed** |
| Integration tests | **74 passed** |
| **Total** | **135 tests passing, 0 failed** |

## Key Decisions

1. **pg_hba.conf strategy**: Only allow localhost (`127.0.0.1/32`, `::1/128`) and server's own IP (`45.119.87.247/32`). Developer machines connect via RDP to the server, so no remote IP whitelisting needed for individual laptops.
2. **VPN for future team scaling**: When 10+ devs need direct access, set up WireGuard VPN instead of managing individual IPs in pg_hba.conf.
3. **SeedTool over SQL script**: The C# SeedTool (`src/NonCash.SeedTool`) is the authoritative seeding mechanism — the old `seed-test-data.sql` is outdated (missing `business_id` FK on brands).

## Lessons Learned

- PostgreSQL exposed to the internet with `0.0.0.0/0` in pg_hba.conf is an immediate attack vector
- Password-only security is insufficient for public-facing database servers
- IP whitelisting provides critical defense-in-depth alongside authentication
- Always maintain migration files and seed scripts in source control — enables full disaster recovery

## Next Steps

- **Epic 6 (Loyalty App Integration)** is next — stories 6.1 through 6.5 are in backlog
- User will continue development from their laptop
- Consider setting up WireGuard VPN when team grows beyond 1-2 developers
