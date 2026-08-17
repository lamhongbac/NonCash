# NonCash — Deployment Guide (Windows Server + IIS)

> **Target:** On-prem Windows Server with IIS and a local PostgreSQL `noncash` database.
> **Apps to host:** `NonCash.API` (ASP.NET Core 9) and `NonCash.Web` (Blazor).
> **Recommendation:** one-time manual server prep, then **CI/CD via GitHub Actions self-hosted runner** for every deploy.

---

## 1. How the apps pick production config

Both apps switch on the custom key `Environment:Name`:

- **API** (`src/NonCash.API/Program.cs`): `Environment:Name = production` → uses connection string `ConnectionStrings:ProductionConnection` (falls back to `NONCASH_CONNECTION_STRING`, then `DefaultConnection`).
- **Web** (`src/NonCash.Web/Program.cs`): `Environment:Name = production` → uses `ApiBaseUrls:production`.

So on the server you must set, per app:

| App | Environment variable | Value |
|-----|---------------------|-------|
| API | `Environment__Name` | `production` |
| API | `ConnectionStrings__ProductionConnection` | `Host=localhost;Database=noncash;Username=noncash_app;Password=<pwd>;SSL Mode=<Require or Disable>` |
| API | `Jwt__Key` | a strong ≥32-byte secret |
| API | `Smtp__*` | your SMTP sender config |
| Web | `Environment__Name` | `production` |
| Web | `ApiBaseUrls__production` | `https://api.yourdomain.com/` |

> Use `SSL Mode=Require` only if Postgres is configured for TLS. For a localhost DB without TLS, use `SSL Mode=Prefer` or `Disable`.

You can set these as **IIS environment variables** in each app's `web.config` (see §4) or via `appsettings.Production.json`. Prefer env vars for secrets.

---

## 2. One-time server preparation (manual)

1. **Install IIS** (Server Manager → Web Server (IIS)).
2. **Install the .NET 9 Hosting Bundle** (not just the runtime) — it includes the ASP.NET Core Module (ANCM) for IIS.
   - After install: `net stop was /y` then `net start w3svc` (or `iisreset`).
3. **Create app pools** (both `.NET CLR Version = No Managed Code`):
   - `NonCashAPI`, `NonCashWeb`.
4. **Create two sites** (or one site + two apps). Suggested:
   - `NonCash.API` → physical path `C:\inetpub\noncash\api`, binding `https://api.yourdomain.com:443`.
   - `NonCash.Web` → physical path `C:\inetpub\noncash\web`, binding `https://yourdomain.com:443`.
5. **HTTPS certificate** — bind a CA-issued cert (or an internal/self-signed cert for intranet). IIS terminates TLS; the apps run in-process over HTTP.
6. **Permissions** — grant `IIS AppPool\NonCashAPI` and `IIS AppPool\NonCashWeb` Read & Execute on their folders.
7. **Database** — already on the server. Ensure schema is current (see §5).
8. **Firewall** — keep Postgres (5432) bound to localhost only; expose only 80/443.

`tools/deploy/setup-iis.ps1` automates steps 3–6.

---

## 3. Deploy path A — scripted manual (first deploy / no CI yet)

1. On a build machine (or the server): run `tools/deploy/publish.ps1`.
   - It runs `dotnet publish -c Release` for API and Web into `artifacts/api` and `artifacts/web`.
2. Copy the two folders to the server's site paths (`C:\inetpub\noncash\api`, `...\web`).
3. Apply migrations (§5).
4. Recycle the app pools (`Import-Module WebAdministration; Restart-WebAppPool NonCashAPI, NonCashWeb`).
5. Verify (§6).

This is "manual" but repeatable. Once it works, move to path B.

### 3.1 Traditional Visual Studio "Folder" publish

**Why your Publish wizard looks different:** Visual Studio detected `.github/workflows/deploy-iis.yml` and registered it as a *GitHub Actions publish profile*, so right-click → Publish opens that profile page instead of the "Where are you publishing today?" wizard. Nothing is broken — just create a classic profile:

1. In the Publish window, click **+ New profile** (top-left, next to Refresh).
2. The target wizard appears → select **Folder** → **Next**.
3. Choose a target location, e.g. `C:\publish\noncash-web` → **Finish** → **Publish**.
4. Repeat for the **other project** (`NonCash.API` → right-click → Publish → + New profile → Folder → `C:\publish\noncash-api`). The wizard publishes one project at a time; you need both.
5. Copy the two output folders to the server: `C:\inetpub\noncash\web` and `C:\inetpub\noncash\api`.
6. On the server, edit the deployed `appsettings.json` of each app (or set IIS env vars per §1):
   - **API:** `"Environment": { "Name": "production" }`, fill `ConnectionStrings:ProductionConnection` (`Host=localhost;...`), `Jwt:Key`, `Smtp:*`.
   - **Web:** `"Environment": { "Name": "production" }`, set `ApiBaseUrls:production` to the public API URL.
7. Recycle the app pools and verify (§6).

> Prefer **Folder** over **Web Server (IIS)**: the IIS target requires Web Deploy (MSDeploy) installed on the server with port 8172 open. Folder + copy needs nothing extra.

---

## 4. Deploy path B — CI/CD (recommended)

Use **GitHub Actions with a self-hosted Windows runner on the server**:

1. On the server: Settings → Actions → Runners → add a self-hosted runner (windows-x64); install as a service.
2. Store secrets in the repo (Settings → Secrets): not strictly needed if the runner is on the server and config lives in IIS env vars; but keep `JWT_KEY`, `DB_PASSWORD`, `SMTP_PASSWORD` as secrets if you inject them.
3. Push to `main` (or `deploy/*`) → `.github/workflows/deploy-iis.yml` runs:
   - checkout → `dotnet build` → `dotnet test` → `dotnet publish` (API + Web)
   - copy publish output to the IIS site folders
   - apply EF migrations
   - recycle app pools
   - smoke-test the health endpoints

The workflow is provided at `.github/workflows/deploy-iis.yml`. Because the runner is on the server, there is **no need for WinRM/MSDeploy or open inbound ports** — the runner only dials out to GitHub.

---

## 5. Database migrations on the server

The DB exists; keep schema in sync on each deploy:

```powershell
# From the repo root on the server (runner has the full checkout):
dotnet tool install --global dotnet-ef   # once
dotnet ef database update `
  --project src\NonCash.Infrastructure `
  --startup-project src\NonCash.API `
  --connection "Host=localhost;Database=noncash;Username=noncash_app;Password=<pwd>;SSL Mode=Prefer"
```

Migrations are idempotent-safe (EF tracks applied migrations in `__EFMigrationsHistory`), so re-running is harmless.

---

## 6. Post-deploy verification

- API health: `https://api.yourdomain.com/health` (or `/swagger` in non-prod).
- Web loads and can log in; check it reaches the API (login exercises the HTTP client).
- Confirm `email_logs` gets a row on a test notification (SMTP configured).
- Check Windows Event Log / stdout for startup errors. Enable stdout logging in `web.config` temporarily if needed.

---

## 7. Rollback

- Keep the previous publish in `C:\inetpub\noncash\api_prev` and `web_prev`.
- On failure, swap folders and recycle pools. `publish.ps1`/the workflow can maintain a `previous` copy automatically.

---

## 8. Security checklist

- Postgres bound to localhost; strong `noncash_app` password; never `0.0.0.0/0` in `pg_hba.conf`.
- `Jwt__Key` and SMTP/ZaloPay/VNPAY secrets via env vars, not committed files.
- `ASPNETCORE_ENVIRONMENT` ≠ `Development` on the server (disable Swagger & detailed errors in prod).
- HTTPS-only bindings; redirect 80 → 443.

---

## 9. Email troubleshooting on the server

On approval, one email goes to the business contact: `ActiveBusiness` (welcome + welcome-credit policy). On rejection: `RegistrationRejected` to the applicant. If nothing arrives:

1. **`email_logs` triage:** `SELECT sent_at, to_address, template_name, success, error_message FROM email_logs ORDER BY sent_at DESC LIMIT 20;`
   - 0 rows → no send attempted → console fallback (step 2). `success=false` → SMTP error (step 5). `success=true` → check spam.
2. **Deployed SMTP config (most common cause):** the repo `appsettings.json` ships with empty `Smtp:Host`; credentials live only in `appsettings.Development.json`, which IIS does not load. Fill the `Smtp` section in the deployed `appsettings.json` (or `Smtp__*` env vars in `web.config`) and **recycle the app pool** (the email/console choice is made at startup).
3. **Flow ran?** `credit_batches` row with `batch_type = 1` (WelcomeGrant) for the brand; `brand_registration_requests.status` approved.
4. **Default welcome policy template exists?** Approval requires at least one active default template in `welcome_grant_policy_templates` (seeded by the `WelcomePolicyTemplates` migration). If missing or deactivated, approval fails with *"No default welcome policy template is configured."*
5. **Contact email present?** `brands.contact_email` or `businesses.contact_email` empty → welcome email silently skipped.
6. **Network:** `Test-NetConnection smtp.gmail.com -Port 587`; app password without spaces.
7. **stdout logs:** set `stdoutLogEnabled="true"` in `web.config`; console fallback prints `[NOTIFICATION]` lines, failures log "Failed to send welcome-credit notification".
8. **Retest** after fix + recycle (register + approve, or forgot-password), then re-check `email_logs`.
