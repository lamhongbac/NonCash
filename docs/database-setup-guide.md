# NonCash Database Setup Guide

This guide describes how to provision and configure PostgreSQL databases for the NonCash platform across multiple environments: **Development (DEV)**, **Pilot**, and **Production**.

## 1. Environment Strategy

| Environment | Purpose | Network Scope | SSL |
|---|---|---|---|
| **DEV** | Local development and debugging on the database server or a developer workstation. | Localhost or LAN. | Optional; often disabled for local-only traffic. |
| **Pilot** | Pre-production validation with a small group of real users/stores. | Hosted VPS or cloud VM, accessed over the internet by authorized clients. | Required. |
| **Production** | Live SaaS operation serving all brands, members, and POS devices. | Hosted VPS or cloud VM, accessed over the internet. | Required. |

Each environment must have its own database, credentials, and connection string. Never share credentials across environments.

## 2. Prerequisites

- PostgreSQL 15 or later installed on the target server.
- Administrator access to the PostgreSQL host.
- `psql` command-line client or pgAdmin.
- OpenSSL (for generating self-signed certificates) or a valid TLS certificate.
- Network firewall rules allowing TCP port `5432` from authorized clients only.

## 3. Install PostgreSQL

1. Download the PostgreSQL installer from https://www.postgresql.org/download/.
2. Run the installer and note:
   - Installation directory, e.g. `C:\Program Files\PostgreSQL\18`.
   - Data directory, e.g. `C:\Program Files\PostgreSQL\18\data`.
   - Superuser (`postgres`) password. Store it in a password manager.
3. Complete the installation with the default port `5432`.

## 4. Create the Database and Application User

Connect as the `postgres` superuser and run:

```sql
CREATE DATABASE noncash;
CREATE USER noncash_app WITH PASSWORD 'UseAStrongRandomPassword!';
GRANT ALL PRIVILEGES ON DATABASE noncash TO noncash_app;
```

If the database already exists and you need to grant schema privileges after running migrations:

```sql
\c noncash
GRANT ALL ON SCHEMA public TO noncash_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO noncash_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO noncash_app;
```

Use a unique strong password for each environment.

## 5. Configure PostgreSQL for Remote Access

Edit `postgresql.conf` in the data directory:

```conf
listen_addresses = '*'
port = 5432
```

For DEV running locally, you may keep `listen_addresses = 'localhost'`.

## 6. SSL Certificate Setup

Remote environments (Pilot and Production) must use SSL.

### Option A: Self-Signed Certificate (suitable for Pilot)

Open a terminal in the PostgreSQL data directory and run:

```bash
openssl req -new -x509 -days 365 -nodes -text -out server.crt -keyout server.key -subj "/CN=postgres"
```

Ensure the PostgreSQL service account (e.g. `NT AUTHORITY\NetworkService` on Windows) has **read** access to both files.

### Option B: Valid CA-Signed Certificate (recommended for Production)

Use a certificate issued by a trusted CA. Place the files in the data directory and name them `server.crt` and `server.key`.

### Enable SSL in postgresql.conf

```conf
ssl = on
ssl_cert_file = 'server.crt'
ssl_key_file = 'server.key'
```

Restart PostgreSQL after changing these settings.

## 7. Configure Client Authentication (pg_hba.conf)

Edit `pg_hba.conf` in the data directory.

### DEV (localhost only)

```text
# IPv4 local connections:
host    noncash    noncash_app    127.0.0.1/32    scram-sha-256
# IPv6 local connections:
host    noncash    noncash_app    ::1/128         scram-sha-256
```

### Pilot / Production (remote clients over SSL)

Restrict the address range to the smallest safe CIDR. Example for any internet host:

```text
hostssl    noncash    noncash_app    0.0.0.0/0    scram-sha-256
```

Example for a single client IP:

```text
hostssl    noncash    noncash_app    203.0.113.10/32    scram-sha-256
```

Reload PostgreSQL configuration or restart the service after editing `pg_hba.conf`:

```sql
SELECT pg_reload_conf();
```

## 8. Configure Application Connection Strings

Each environment has its own `appsettings.{Environment}.json` in `src/NonCash.API/`.

### DEV

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=noncash;Username=noncash_app;Password=UseAStrongRandomPassword!;SSL Mode=Disable"
}
```

### Pilot / Production

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=45.119.87.247;Database=noncash;Username=noncash_app;Password=UseAStrongRandomPassword!;SSL Mode=Require"
}
```

Replace `45.119.87.247` with the actual database server IP or hostname.

For production secrets, prefer environment variables over checked-in JSON:

```powershell
$env:NONCASH_CONNECTION_STRING="Host=prod-db.example.com;Database=noncash;Username=noncash_app;Password=...;SSL Mode=Require"
```

The application falls back to the environment variable in `Program.cs` if `DefaultConnection` is not present.

## 9. Apply EF Core Migrations

From the solution root, run:

```bash
dotnet ef database update --project src/NonCash.Infrastructure --startup-project src/NonCash.API
```

Repeat for each environment by changing the connection string before running the command.

To create a new migration after model changes:

```bash
dotnet ef migrations add MigrationName --project src/NonCash.Infrastructure --startup-project src/NonCash.API
```

## 10. Seed Initial Data

The API seeds a default admin account on startup via `DatabaseSeeder.SeedAdminAsync` in `Program.cs`.

Default DEV credentials:

- Username: `admin`
- Password: `Admin@123`

For Pilot/Production, change the seed password or remove seeding and create the admin account through a secure onboarding process.

## 11. Verify Connectivity

From the application host, test with `psql`:

```bash
psql "host=45.119.87.247 port=5432 dbname=noncash user=noncash_app sslmode=require" -c "SELECT current_database();"
```

If SSL is disabled on DEV:

```bash
psql "host=localhost port=5432 dbname=noncash user=noncash_app sslmode=disable" -c "SELECT current_database();"
```

## 12. Backup and Maintenance

### Automated Backups

Schedule daily logical backups with `pg_dump`:

```bash
pg_dump -h localhost -U postgres -d noncash -F c -f "noncash_backup_$(date +%Y%m%d).dump"
```

Store backups off-site and test restore procedures regularly.

### Health Checks

The API exposes a health check endpoint:

```text
GET /health
```

It verifies the PostgreSQL connection through `AddDbContextCheck<ApplicationDbContext>`.

## 13. Troubleshooting

### Service starts then stops immediately

- Check PostgreSQL logs in `<data_directory>\log\`.
- If SSL is enabled, verify `server.crt` and `server.key` exist and the service account has read access.
- Verify there are no syntax errors in `postgresql.conf` or `pg_hba.conf`.

### Timeout or connection refused from remote client

- Verify `listen_addresses = '*'` and the service is restarted.
- Verify the firewall allows inbound TCP `5432`.
- Verify `pg_hba.conf` contains an entry for the client's source IP.
- If using `SSL Mode=Require`, confirm `ssl = on` in `postgresql.conf`.

### Password authentication failed

- Confirm the password in the connection string matches the database user.
- Confirm the user has `LOGIN` privilege and the password is not expired.

### SSL errors

- Self-signed certificates work with `SSL Mode=Require` but not with `VerifyCA` or `VerifyFull`.
- For full validation, use a CA-signed certificate and set the connection string to `SSL Mode=VerifyCA` or `SSL Mode=VerifyFull`.

## 14. Environment Checklist

### DEV

- [ ] PostgreSQL installed locally.
- [ ] Database `noncash` created.
- [ ] User `noncash_app` created with strong password.
- [ ] `appsettings.Development.json` uses `Host=localhost` and `SSL Mode=Disable`.
- [ ] Migrations applied.
- [ ] API starts and `/health` returns healthy.

### Pilot / Production

- [ ] PostgreSQL installed on dedicated server or VPS.
- [ ] Database `noncash` created.
- [ ] User `noncash_app` created with unique strong password.
- [ ] SSL certificate installed (`server.crt` / `server.key`).
- [ ] `ssl = on` in `postgresql.conf`.
- [ ] `pg_hba.conf` allows only authorized client IPs via `hostssl`.
- [ ] Firewall restricted to required source IPs.
- [ ] `appsettings.{Environment}.json` uses public IP/hostname and `SSL Mode=Require`.
- [ ] Migrations applied.
- [ ] Backups configured.
- [ ] API `/health` returns healthy.
