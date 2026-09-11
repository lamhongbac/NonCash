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
| API | `MediaServiceConfig__ApiKey`, `MediaServiceConfig__AppCode` | MSA media-service credentials |
| API | `ZaloPay__Key1`, `ZaloPay__Key2` | ZaloPay HMAC keys |
| API | `VNPAY__TmnCode`, `VNPAY__HashSecret` | VNPay terminal code and hash secret |
| Web | `Environment__Name` | `production` |
| Web | `ApiBaseUrls__production` | `https://api.yourdomain.com/` |
| Web | `MediaServiceConfig__ApiKey`, `MediaServiceConfig__AppCode` | MSA media-service credentials |
| Web | `ZaloPay__Key1`, `ZaloPay__Key2` | ZaloPay HMAC keys |

> Use `SSL Mode=Require` only if Postgres is configured for TLS. For a localhost DB without TLS, use `SSL Mode=Prefer` or `Disable`.

You can set these as **IIS environment variables** in each app's `web.config` (see §4) or in the deployed `appsettings.json` (see §4.3). Prefer env vars for secrets.

> **Since CR-2026-09-06-13 every credential in the repository's `appsettings*.json` is an empty
> string.** The keys are kept so the shape of the config is documented, but no value is committed.
> A server that supplies nothing therefore starts with no database, no signing key and no mail —
> the values must come from the deployed `appsettings.json` or from the env vars above.
>
> **The API now refuses to start without a usable `Jwt:Key`.** `JwtSigningKey.Resolve` throws during
> startup if the key is missing or shorter than 32 bytes, and the message states both the cause and
> how to fix it. There is deliberately no built-in default: a fallback key committed to the repo let
> anyone reading it forge an Admin token.

### 1.1 Local development: user-secrets instead of committed values

On a developer machine the same values live in the .NET user-secrets store, which is outside the
repository (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`) and is loaded **only** when
`ASPNETCORE_ENVIRONMENT=Development`. Each app has its own store:

| App | `UserSecretsId` |
|---|---|
| `NonCash.API` | `745a52d9-40d1-474d-85e5-d4887115db02` |
| `NonCash.Web` | `ffbda5fb-66e1-4e28-827a-8846ded9fe0e` |

After cloning, populate them once (values come from the password store, not from this file):

```powershell
dotnet user-secrets set "Jwt:Key" "<jwt-key-min-32-bytes>" --project src\NonCash.API\NonCash.API.csproj
dotnet user-secrets set "ConnectionStrings:DevConnection" "<connection-string>" --project src\NonCash.API\NonCash.API.csproj
dotnet user-secrets set "Smtp:Password" "<app-password>" --project src\NonCash.API\NonCash.API.csproj
dotnet user-secrets list --project src\NonCash.API\NonCash.API.csproj
```

The full key list is the credential rows of the §1 table. `dotnet user-secrets set` merges into the
existing file, so setting one key never drops the others.

A clone with no user-secrets fails fast and says why — the API stops during startup with
*"Jwt:Key is not configured … Set a random value of at least 32 bytes"* rather than quietly running
on a key published in the repository.

The test suite does not use user-secrets: `NonCash.IntegrationTests` supplies its own signing key
(`TestJwtConfig.SigningKey`) through `builder.UseSetting`, and replaces the PostgreSQL registration
with SQLite in-memory, so tests run on a clean clone with nothing configured.

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
   - **API:** `"Environment": { "Name": "production" }`, then fill every credential from the §4.3 server example — `ConnectionStrings:ProductionConnection`, `Jwt:Key`, `Smtp:*`, `MediaServiceConfig:*`, `ZaloPay:Key1/Key2`, `VNPAY:TmnCode/HashSecret`. The repository ships all of them empty, so a partial fill leaves mail, media upload or a payment gateway silently unconfigured.
   - **Web:** `"Environment": { "Name": "production" }`, set `ApiBaseUrls:production` to the public API URL, and fill `MediaServiceConfig:*` and `ZaloPay:Key1/Key2` per §4.3.
7. Recycle the app pools and verify (§6).

> Prefer **Folder** over **Web Server (IIS)**: the IIS target requires Web Deploy (MSDeploy) installed on the server with port 8172 open. Folder + copy needs nothing extra.

---

## 4. Deploy path B — CI/CD (recommended)

Use **GitHub Actions with a self-hosted Windows runner installed directly on the server**:

### 4.1 One-time runner setup (on the server)

1. In the GitHub repo: **Settings → Actions → Runners → New self-hosted runner → Windows**.
2. Download and run the `config.cmd` commands shown by GitHub (as Administrator).
3. Register the runner with labels `self-hosted, windows` (default) or add a custom label like `noncash-server`.
4. Install as a Windows service so it starts automatically:
   ```powershell
   .\svc.cmd install
   .\svc.cmd start
   ```
5. The runner service account must be able to:
   - Read/write `C:\inetpub\noncash`.
   - Manage IIS app pools (membership in local `Administrators` or `IIS_IUSRS` plus PowerShell WebAdministration module).
   - Connect to the local PostgreSQL database.

### 4.2 GitHub secrets

Add these in the repo: **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value |
|---|---|
| `NONCASH_DB_CONNECTION` | `Host=localhost;Database=noncash;Username=noncash_app;Password=<pwd>;SSL Mode=Require` |

> `<pwd>` is the real `noncash_app` password. It belongs only in the GitHub secret store and on the
> server — never in this file, which is committed to git.

### 4.3 Protect production config from being overwritten

Each `dotnet publish` replaces `appsettings.json` with the repository copy, whose credential values are all empty since CR-2026-09-06-13. To avoid re-applying production settings after every deploy, configure production values directly in the deployed `appsettings.json` files on the server. The CI/CD workflow automatically **backs up and restores** these files, so you only need to set them once.

Create/edit this file once on the server: `C:\Projects\NonCashAPI\appsettings.json`

```json
{
  "Environment": { "Name": "production" },
  "ConnectionStrings": {
    "ProductionConnection": "Host=localhost;Database=noncash;Username=noncash_app;Password=<pwd>;SSL Mode=Require"
  },
  "Jwt": { "Key": "<jwt-key-min-32-bytes>" },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "<sender-email>",
    "Password": "<app-password>",
    "FromAddress": "<sender-email>",
    "FromDisplayName": "NonCash"
  },
  "Notifications": { "EmailEnabled": true },
  "MediaServiceConfig": { "ApiKey": "<msa-api-key>", "AppCode": "<msa-app-code>" },
  "ZaloPay": { "Key1": "<zalo-key1>", "Key2": "<zalo-key2>" },
  "VNPAY": { "TmnCode": "<vnpay-tmn-code>", "HashSecret": "<vnpay-hash-secret>" }
}
```

Create/edit this file once on the server: `C:\Projects\NonCashWeb\appsettings.json`

```json
{
  "Environment": { "Name": "production" },
  "ApiBaseUrls": {
    "production": "http://45.119.87.247:8668/"
  },
  "MediaServiceConfig": { "ApiKey": "<msa-api-key>", "AppCode": "<msa-app-code>" },
  "ZaloPay": { "Key1": "<zalo-key1>", "Key2": "<zalo-key2>" }
}
```

> **Two limits of the backup/restore step — both have bitten this project before:**
>
> 1. The restore only runs when `C:\Projects\<App>_appsettings.json` already exists. On a **first**
>    deploy to a fresh server there is no backup yet, so the repository file — with every credential
>    empty — lands in production and the API stops at startup with *"Jwt:Key is not configured"*.
>    Create both server files **before** the first deploy.
> 2. The restore copies the whole file back, so a config key added to the repository later never
>    reaches production until the server file is edited by hand. After adding any new key, update both
>    server files in the same change.

> **Never commit these production values to Git.** The repo `appsettings.json` files should keep their default/placeholder values.
>
> The workflow keeps backup copies at `C:\Projects\NonCashAPI_appsettings.json` and `C:\Projects\NonCashWeb_appsettings.json` and restores them after each deploy.

### 4.4 Trigger a deploy

Push to `main` or a branch matching `deploy/*`:

```bash
git checkout -b deploy/2026-08-18
# merge your feature branch or commit your changes
git push origin deploy/2026-08-18
```

The workflow (`.github/workflows/deploy-iis.yml`) will:

1. Build + test.
2. Publish API to `C:\Projects\_stage\api` and Web to `C:\Projects\_stage\web`.
3. Stop `NonCashAPI` and `NonCashWeb` app pools.
4. Swap `C:\Projects\NonCashAPI` → `C:\Projects\NonCashAPI_prev` and `C:\Projects\NonCashWeb` → `C:\Projects\NonCashWeb_prev`, then copy the staged folders into place.
5. Apply EF migrations using `NONCASH_DB_CONNECTION`.
6. Start the app pools.
7. Smoke-test `http://localhost:8668/health`.

Because the runner is on the server, there is **no need for WinRM/MSDeploy or open inbound ports** — the runner only dials out to GitHub.

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
- Every credential — `Jwt:Key`, `ConnectionStrings:*`, `Smtp:*`, `MediaServiceConfig:ApiKey/AppCode`, `ZaloPay:Key1/Key2`, `VNPAY:TmnCode/HashSecret` — comes from env vars, the deployed `appsettings.json`, or (locally) user-secrets. None is committed.
- The API refuses to start when `Jwt:Key` is missing or under 32 bytes; there is no fallback key in code.
- `ASPNETCORE_ENVIRONMENT` ≠ `Development` on the server (disable Swagger & detailed errors in prod).
- HTTPS-only bindings; redirect 80 → 443.

> **Outstanding risk — rotation still owed.** Blanking the repository files (CR-2026-09-06-13) stops
> the values being published from now on, but it does **not** remove them from git history: every
> credential committed before that change — the database passwords, the JWT signing key, the SMTP app
> password, the MSA media key, and the ZaloPay/VNPAY keys — is still recoverable by anyone with read
> access to this repository and must be treated as exposed. Each one needs to be rotated at its
> provider, and the new value entered only in user-secrets / the deployed config. Scrubbing history
> (`git filter-repo` plus a force-push and a fresh clone for every contributor) is the only way to
> remove the old values, and it is a separate, disruptive decision.

---

## 9. Email troubleshooting on the server

On approval, one email goes to the business contact: `ActiveBusiness` (welcome + welcome-credit policy). On rejection: `RegistrationRejected` to the applicant. If nothing arrives:

1. **`email_logs` triage:** `SELECT sent_at, to_address, template_name, success, error_message FROM email_logs ORDER BY sent_at DESC LIMIT 20;`
   - 0 rows → no send attempted → console fallback (step 2). `success=false` → SMTP error (step 5). `success=true` → check spam.
2. **Deployed SMTP config (most common cause):** the repository ships an empty `Smtp` section in *every* tracked `appsettings*.json` — since CR-2026-09-06-13 the developer credentials live in user-secrets, which only load under `ASPNETCORE_ENVIRONMENT=Development`, so a deployed app inherits nothing from the repo. Fill the `Smtp` section in the deployed `appsettings.json` (or `Smtp__*` env vars in `web.config`) and **recycle the app pool** (the email/console choice is made at startup).
3. **Flow ran?** `credit_batches` row with `batch_type = 1` (WelcomeGrant) for the brand; `brand_registration_requests.status` approved.
4. **Default welcome policy template exists?** Approval requires at least one active default template in `welcome_grant_policy_templates` (seeded by the `WelcomePolicyTemplates` migration). If missing or deactivated, approval fails with *"No default welcome policy template is configured."*
5. **Contact email present?** `brands.contact_email` or `businesses.contact_email` empty → welcome email silently skipped.
6. **Network:** `Test-NetConnection smtp.gmail.com -Port 587`; app password without spaces.
7. **stdout logs:** set `stdoutLogEnabled="true"` in `web.config`; console fallback prints `[NOTIFICATION]` lines, failures log "Failed to send welcome-credit notification".
8. **Retest** after fix + recycle (register + approve, or forgot-password), then re-check `email_logs`.
