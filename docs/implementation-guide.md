# NonCash Implementation Guide

This guide explains how to build, configure, and deploy the NonCash platform from source. It covers environment setup, database provisioning, dependency configuration, running the application locally, and preparing Pilot/Production deployments.

## 1. Overview

NonCash is a SaaS voucher production and management platform built with:

- **Backend:** .NET 9 Web API, EF Core 9, PostgreSQL
- **Frontend:** Blazor WebAssembly (NonCash.Web)
- **Database:** PostgreSQL 15+
- **Architecture:** 3-layer (Core, Infrastructure, Web/API)

Solution file: [`NonCash.sln`](file:///c:/MSA/Sources/NonCashSol/NonCash.sln)

## 2. Environments

| Environment | Purpose | Database Host | SSL |
|---|---|---|---|
| **DEV** | Local development and debugging. | `localhost` | Disabled |
| **Pilot** | Limited pre-production validation. | Hosted VPS / cloud VM | Required |
| **Production** | Live operation. | Hosted VPS / cloud VM | Required |

Each environment must have its own database, credentials, JWT secret, and payment provider configuration.

## 3. Prerequisites

### 3.1 Required Software

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- (Optional) [Entity Framework Core tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet):
  ```bash
  dotnet tool install --global dotnet-ef
  ```
- (Optional) Visual Studio 2022 or JetBrains Rider for debugging.
- (Optional) Git for source control.

### 3.2 Verify Installation

```bash
dotnet --version
psql --version
dotnet ef --version
```

## 4. Clone and Build

### 4.1 Clone the Repository

```bash
git clone <repository-url> NonCashSol
cd NonCashSol
```

### 4.2 Restore and Build

```bash
dotnet restore
dotnet build
```

To build only the API:

```bash
dotnet build src/NonCash.API/NonCash.API.csproj
```

To build only the Web frontend:

```bash
dotnet build src/NonCash.Web/NonCash.Web.csproj
```

## 5. Database Setup

See the detailed [`Database Setup Guide`](./database-setup-guide.md) for PostgreSQL installation, user creation, SSL certificates, and firewall rules.

Summary for DEV:

1. Install PostgreSQL.
2. Create database `noncash`.
3. Create user `noncash_app` with a strong password.
4. Keep SSL disabled in DEV.
5. Allow local connections in `pg_hba.conf`.

## 6. Configure Application Settings

Configuration files are in `src/NonCash.API/` and `src/NonCash.Web/`.

### 6.1 API Connection String

Edit `src/NonCash.API/appsettings.Development.json` for DEV:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=noncash;Username=noncash_app;Password=YourStrongPassword;SSL Mode=Disable"
}
```

For Pilot/Production, create or edit `appsettings.Pilot.json` / `appsettings.Production.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=your-db-host;Database=noncash;Username=noncash_app;Password=YourStrongPassword;SSL Mode=Require"
}
```

The application also reads from the environment variable `NONCASH_CONNECTION_STRING` if the JSON value is missing:

```powershell
$env:NONCASH_CONNECTION_STRING="Host=..."
```

### 6.2 JWT Secret

Update `Jwt:Key` to a random string of at least 32 characters. Do not use the default key in Production:

```json
"Jwt": {
  "Issuer": "NonCash",
  "Audience": "NonCash.Users",
  "Key": "your-random-secret-key-must-be-at-least-32-bytes-long!"
}
```

### 6.3 Payment Providers

#### ZaloPay (Sandbox)

```json
"ZaloPay": {
  "Endpoint": "https://sb-openapi.zalopay.vn/v2/create",
  "AppId": 2554,
  "Key1": "your-sandbox-key-1",
  "Key2": "your-sandbox-key-2",
  "CallbackUrl": "https://your-api/api/v1/payments/webhook",
  "RedirectUrl": "https://your-web/payment-result",
  "AppUserPrefix": "noncash"
}
```

#### VNPAY (Sandbox)

```json
"VNPAY": {
  "TmnCode": "YOUR_TMN_CODE",
  "HashSecret": "YOUR_HASH_SECRET",
  "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
  "ApiUrl": "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction",
  "Version": "2.1.0",
  "Command": "pay",
  "CurrCode": "VND",
  "Locale": "vn",
  "ReturnUrl": "https://your-api/api/v1/payments/vnpay-return",
  "IpnUrl": "https://your-api/api/v1/payments/vnpay-ipn"
}
```

### 6.4 Web Frontend API Base URL

Edit `src/NonCash.Web/appsettings.Development.json`:

```json
"ApiBaseUrl": "https://localhost:7107/"
```

For Pilot/Production:

```json
"ApiBaseUrl": "https://your-api-domain/"
```

## 7. Apply EF Core Migrations

From the solution root:

```bash
dotnet ef database update --project src/NonCash.Infrastructure --startup-project src/NonCash.API
```

To target a specific environment, set the environment variable first:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet ef database update --project src/NonCash.Infrastructure --startup-project src/NonCash.API
```

### Create a New Migration

```bash
dotnet ef migrations add MigrationName --project src/NonCash.Infrastructure --startup-project src/NonCash.API
```

## 8. Seed Test Data (DEV Only)

Use the seed tool to create sample brands, outlets, customers, accounts, and vouchers:

```bash
dotnet run --project src/NonCash.SeedTool
```

The tool uses the `NONCASH_CONNECTION_STRING` environment variable or defaults to `localhost` with user `postgres`.

Default test credentials:

- Admin: `admin` / `Admin@123`
- Brand Manager: `brandmanager` / `Test@123`
- Member Alice: `alice` / `Test@123`
- Member Bob: `bob` / `Test@123`

## 9. Run the Applications

### 9.1 Run the API

```bash
dotnet run --project src/NonCash.API --launch-profile https
```

Default URLs:

- HTTPS: `https://localhost:7107`
- HTTP: `http://localhost:5200`

Swagger UI is available at `https://localhost:7107/swagger` in Development mode.

### 9.2 Run the Web Frontend

```bash
dotnet run --project src/NonCash.Web
```

Default URL: `https://localhost:7026` (check `launchSettings.json` for the exact port).

### 9.3 Visual Studio Multi-Startup

Configure Visual Studio to start both projects simultaneously:

1. Right-click the solution → **Properties**.
2. Select **Multiple startup projects**.
3. Set **NonCash.API** and **NonCash.Web** to **Start**.
4. Save and run.

## 10. Verify the Deployment

### 10.1 Health Check

```bash
curl https://localhost:7107/health
```

Expected response: `Healthy`

### 10.2 API Endpoints

Test authentication:

```bash
curl -X POST https://localhost:7107/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}'
```

## 11. Pilot / Production Deployment

### 11.1 Server Requirements

- Windows Server 2019+ or Linux (Ubuntu 22.04 LTS recommended)
- PostgreSQL 15+ on a separate database server or managed service
- .NET 9 Runtime or SDK
- Reverse proxy (IIS, Nginx, or cloud load balancer) with HTTPS

### 11.2 Publish the API

```bash
dotnet publish src/NonCash.API/NonCash.API.csproj -c Release -o ./publish/api
```

### 11.3 Publish the Web Frontend

```bash
dotnet publish src/NonCash.Web/NonCash.Web.csproj -c Release -o ./publish/web
```

### 11.4 Deploy Steps

1. Provision the database and apply migrations.
2. Copy published API files to the host.
3. Configure `appsettings.Production.json` with production secrets.
4. Set environment variables for secrets instead of storing them in JSON where possible.
5. Configure HTTPS certificate.
6. Start the API as a Windows Service or Linux systemd service.
7. Deploy the Web frontend to the web server.
8. Verify `/health` and smoke-test key flows.

### 11.5 Example systemd Service (Linux)

```ini
[Unit]
Description=NonCash API
After=network.target

[Service]
WorkingDirectory=/var/noncash/api
ExecStart=/usr/bin/dotnet /var/noncash/api/NonCash.API.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=NONCASH_CONNECTION_STRING=Host=...;Database=noncash;Username=noncash_app;Password=...;SSL Mode=Require
Restart=always
RestartSec=10
User=noncash

[Install]
WantedBy=multi-user.target
```

## 12. Environment Checklist

### DEV

- [ ] .NET 9 SDK installed.
- [ ] PostgreSQL installed locally.
- [ ] Database `noncash` and user `noncash_app` created.
- [ ] `appsettings.Development.json` configured.
- [ ] Migrations applied.
- [ ] Seed tool executed.
- [ ] API and Web start successfully.
- [ ] `/health` returns healthy.

### Pilot / Production

- [ ] Dedicated server or cloud VM provisioned.
- [ ] PostgreSQL provisioned with SSL and restricted firewall.
- [ ] Database `noncash` and user `noncash_app` created.
- [ ] Production `appsettings` or environment variables configured.
- [ ] JWT key is random and at least 32 characters.
- [ ] Payment provider credentials configured.
- [ ] Migrations applied.
- [ ] API published and deployed.
- [ ] Web frontend published and deployed.
- [ ] HTTPS certificate installed.
- [ ] Backups configured.
- [ ] `/health` returns healthy.

## 13. Troubleshooting

### Build Errors

- Ensure the .NET 9 SDK is installed: `dotnet --version`
- Restore packages: `dotnet restore`

### Database Timeout on Startup

- Verify the connection string host, port, and credentials.
- For localhost DEV, use `SSL Mode=Disable`.
- For remote hosts, ensure `SSL Mode=Require` and the server has SSL enabled.
- Check `pg_hba.conf` allows the client source IP.

### EF Core Migration Errors

- Ensure the startup project is `src/NonCash.API`.
- Ensure the connection string points to an existing database.

### HTTPS Certificate Errors

- Trust the development certificate: `dotnet dev-certs https --trust`
- For Production, use a valid certificate from a trusted CA.

## 14. Additional Resources

- [Database Setup Guide](./database-setup-guide.md)
- [Architecture Overview](./architecture.md)
- [Data Models](./data-models.md)
- [API Contracts](./api-contracts.md)
- [POS Integration Guide](./pos-integration-guide.md)
