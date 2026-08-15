# Environment Setup and Requirements

<cite>
**Referenced Files in This Document**
- [docs/index.md](file://docs/index.md)
- [docs/architecture.md](file://docs/architecture.md)
- [docs/api-contracts.md](file://docs/api-contracts.md)
- [docs/database-setup-guide.md](file://docs/database-setup-guide.md)
- [_bmad/core/config.yaml](file://_bmad/core/config.yaml)
- [_bmad/bmm/config.yaml](file://_bmad/bmm/config.yaml)
- [BMAD_STRUCTURE.md](file://BMAD_STRUCTURE.md)
- [src/NonCash.API/appsettings.json](file://src/NonCash.API/appsettings.json)
- [src/NonCash.API/appsettings.Development.json](file://src/NonCash.API/appsettings.Development.json)
- [src/NonCash.API/Program.cs](file://src/NonCash.API/Program.cs)
- [_bmad-output/session-log-2026-08-15.md](file://_bmad-output/session-log-2026-08-15.md)
</cite>

## Update Summary
**Changes Made**
- Updated database connection string configuration section with post-security incident credentials
- Added security incident response procedures and lessons learned
- Enhanced credential management guidelines based on ransomware attack experience
- Updated troubleshooting section with security-related connectivity issues
- Added emergency response procedures for database security incidents

## Table of Contents
1. [Introduction](#introduction)
2. [Project Structure](#project-structure)
3. [Core Components](#core-components)
4. [Architecture Overview](#architecture-overview)
5. [Detailed Component Analysis](#detailed-component-analysis)
6. [Dependency Analysis](#dependency-analysis)
7. [Performance Considerations](#performance-considerations)
8. [Security Incident Response](#security-incident-response)
9. [Troubleshooting Guide](#troubleshooting-guide)
10. [Conclusion](#conclusion)
11. [Appendices](#appendices)

## Introduction
This document provides a comprehensive environment setup guide for deploying the NonCash platform. It consolidates system requirements, prerequisites, and configuration steps derived from the repository's documentation and configuration files. The platform follows a 3-layer architecture with a C#/.NET Core backend, Blazor frontend, and PostgreSQL as the primary database. Security is enforced via JWT and API keys, and the system emphasizes multi-tenancy and dynamic voucher code logic.

**Updated** Following a ransomware security incident, all database connection strings have been updated with new secure credentials and enhanced security measures have been implemented.

## Project Structure
The repository organizes documentation and BMAD-related configuration under dedicated folders. The most relevant materials for environment setup are:
- docs: High-level architecture, API contracts, and system overview
- _bmad: BMAD configuration files for planning and implementation artifacts
- BMAD_STRUCTURE.md: Describes the three-layer architecture, ORM choice, and security measures

```mermaid
graph TB
Repo["Repository Root"]
Docs["docs/"]
BmadCore["_bmad/core/"]
BmadBmm["_bmad/bmm/"]
BmadOutput["_bmad-output/"]
Repo --> Docs
Repo --> BmadCore
Repo --> BmadBmm
Repo --> BmadOutput
Docs --> ArchDoc["architecture.md"]
Docs --> ApiDoc["api-contracts.md"]
Docs --> IndexDoc["index.md"]
Docs --> DBSetup["database-setup-guide.md"]
BmadCore --> CoreCfg["config.yaml"]
BmadBmm --> BmmCfg["config.yaml"]
BmadOutput --> SessionLog["session-log-2026-08-15.md"]
```

**Diagram sources**
- [docs/index.md:1-41](file://docs/index.md#L1-L41)
- [docs/architecture.md:1-52](file://docs/architecture.md#L1-L52)
- [docs/api-contracts.md:1-109](file://docs/api-contracts.md#L1-L109)
- [docs/database-setup-guide.md:1-267](file://docs/database-setup-guide.md#L1-L267)
- [_bmad/core/config.yaml:1-10](file://_bmad/core/config.yaml#L1-L10)
- [_bmad/bmm/config.yaml:1-17](file://_bmad/bmm/config.yaml#L1-L17)
- [_bmad-output/session-log-2026-08-15.md:1-86](file://_bmad-output/session-log-2026-08-15.md#L1-L86)

**Section sources**
- [docs/index.md:1-41](file://docs/index.md#L1-L41)
- [docs/architecture.md:1-52](file://docs/architecture.md#L1-L52)
- [_bmad/core/config.yaml:1-10](file://_bmad/core/config.yaml#L1-L10)
- [_bmad/bmm/config.yaml:1-17](file://_bmad/bmm/config.yaml#L1-L17)

## Core Components
- Backend runtime and framework: C# / .NET Core (as documented in the architecture)
- Database: PostgreSQL (as documented in the architecture)
- ORM: Entity Framework Core (as documented in the architecture)
- Frontend: Blazor (as documented in the architecture)
- Security: JWT and API Keys (as documented in the architecture and API contracts)
- OS support: Linux and Windows (as documented in the architecture)

These components define the baseline for installing SDKs, databases, and runtime dependencies.

**Section sources**
- [docs/architecture.md:17-52](file://docs/architecture.md#L17-L52)
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)

## Architecture Overview
The NonCash platform uses a 3-layer architecture:
- GUI (Blazor): User interactions and dashboards
- BLL (C#/.NET Core microservices): Business logic and orchestration
- DAL (PostgreSQL via EF Core): Data persistence and abstraction

Security is enforced with JWT and API Keys, and multi-tenancy is implemented via BrandID.

```mermaid
graph TB
subgraph "GUI (Blazor)"
UI["User Interface"]
end
subgraph "BLL (.NET Core Microservices)"
Planning["Planning Service"]
Approval["Approval Service"]
Distribution["Distribution Service"]
Usage["Usage Service"]
Identity["Identity & Tenant Service"]
end
subgraph "DAL (PostgreSQL via EF Core)"
DB["PostgreSQL Database"]
end
UI --> |"Service calls"| Planning
UI --> |"Service calls"| Approval
UI --> |"Service calls"| Distribution
UI --> |"Service calls"| Usage
UI --> |"Service calls"| Identity
Planning --> |"EF Core"| DB
Approval --> |"EF Core"| DB
Distribution --> |"EF Core"| DB
Usage --> |"EF Core"| DB
Identity --> |"EF Core"| DB
```

**Diagram sources**
- [docs/architecture.md:9-35](file://docs/architecture.md#L9-L35)

**Section sources**
- [docs/architecture.md:5-52](file://docs/architecture.md#L5-L52)

## Detailed Component Analysis

### System Requirements
- Backend runtime: C# / .NET Core (version aligned with project's microservices)
- Database: PostgreSQL (primary choice; MongoDB considered but PostgreSQL preferred)
- ORM: Entity Framework Core
- Frontend: Blazor (Server or WebAssembly)
- OS: Linux and Windows supported

These requirements are derived from the architecture and BMAD structure documents.

**Section sources**
- [docs/architecture.md:17-52](file://docs/architecture.md#L17-L52)
- [BMAD_STRUCTURE.md:59-61](file://BMAD_STRUCTURE.md#L59-L61)

### Prerequisite Tools and SDK Installations
- Install the .NET SDK matching the project's backend runtime requirement
- Install PostgreSQL server and client tools
- Install a modern IDE with C# and Blazor support (e.g., Visual Studio, VS Code)
- Install Git for version control

Note: Specific SDK versions are not enumerated in the repository; align with the .NET version used by the backend microservices.

**Section sources**
- [docs/architecture.md:17-19](file://docs/architecture.md#L17-L19)

### Development Environment Configuration
- IDE setup: Configure C# and Blazor projects; enable diagnostics and IntelliSense
- Project layout: Follow the 3-layer architecture (Core, Infrastructure, Web/API) as outlined in the documentation
- Local database: Provision a PostgreSQL instance locally for development and testing

**Section sources**
- [docs/index.md:34-37](file://docs/index.md#L34-L37)
- [docs/architecture.md:28-35](file://docs/architecture.md#L28-L35)

### Environment Variables and Secret Management
- Authentication: API Key header and JWT bearer tokens are used for external integrations and member app interactions
- Secret management: Store sensitive values (e.g., connection strings, API keys) using environment variables or secure secret stores
- Credential setup: Define credentials per deployment stage (development, staging, production) with appropriate isolation

**Updated** Following the security incident, all database connection strings have been updated with new secure credentials. The application supports multiple connection string configurations:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=45.119.87.247;Database=noncash;Username=noncash_app;Password=NonCashMachine@2026;SSL Mode=Require",
  "DevConnection": "Host=45.119.87.247;Database=noncash;Username=noncash_app;Password=NonCashMachine@2026;SSL Mode=Require",
  "PilotConnection": "Host=45.119.87.247;Database=noncash;Username=noncash_app;Password=NonCashMachine@2026;SSL Mode=Require",
  "ProductionConnection": "Host=your-production-db;Database=noncash;Username=noncash_app;Password=CHANGE_ME;SSL Mode=Require"
}
```

The application falls back to environment variables if connection strings are not present in configuration files.

**Section sources**
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)
- [src/NonCash.API/appsettings.json:21-26](file://src/NonCash.API/appsettings.json#L21-L26)
- [src/NonCash.API/appsettings.Development.json:23-25](file://src/NonCash.API/appsettings.Development.json#L23-L25)
- [src/NonCash.API/Program.cs:36-38](file://src/NonCash.API/Program.cs#L36-L38)

### Local Database Provisioning
- Provision PostgreSQL locally for development
- Use EF Core migrations to initialize and update the schema
- Ensure database connectivity from the backend microservices

**Updated** After the security incident, ensure proper network restrictions are configured:

```text
# pg_hba.conf - Restrict access to localhost and specific IPs only
host    noncash    noncash_app    127.0.0.1/32    scram-sha-256
host    noncash    noncash_app    ::1/128         scram-sha-256
host    noncash    noncash_app    45.119.87.247/32    scram-sha-256
```

**Section sources**
- [docs/architecture.md:28-35](file://docs/architecture.md#L28-L35)
- [_bmad-output/session-log-2026-08-15.md:20-28](file://_bmad-output/session-log-2026-08-15.md#L20-L28)

### Deployment Stages and Configuration
- Development: Local PostgreSQL, minimal secrets, debug builds
- Staging: Dedicated PostgreSQL, environment-specific secrets, test deployments
- Production: Managed PostgreSQL, hardened secrets, CI/CD pipeline, monitoring

**Updated** All environments now use the secured database at `45.119.87.247` with updated credentials following the security incident response.

**Section sources**
- [docs/architecture.md:36-41](file://docs/architecture.md#L36-L41)

### Windows Setup (Step-by-Step)
1. Install .NET SDK matching the backend runtime requirement
2. Install PostgreSQL and configure a local database
3. Install an IDE (e.g., Visual Studio or VS Code) with C# and Blazor extensions
4. Clone the repository and restore dependencies
5. Configure environment variables for database connection and API keys
6. Run EF Core migrations to provision the schema
7. Launch the backend microservices and frontend application
8. Validate endpoints using the API contracts

**Updated** Ensure PostgreSQL network access is restricted to authorized IPs only after setup.

**Section sources**
- [docs/architecture.md:17-52](file://docs/architecture.md#L17-L52)
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)

### Linux Setup (Step-by-Step)
1. Install .NET SDK and PostgreSQL server
2. Configure PostgreSQL and create a development database
3. Set up an IDE or editor with C# and Blazor support
4. Clone the repository and restore packages
5. Configure environment variables for secrets and database connection
6. Apply EF Core migrations
7. Start backend microservices and frontend
8. Test API endpoints as defined in the API contracts

**Updated** Implement IP whitelisting in `pg_hba.conf` to prevent unauthorized access.

**Section sources**
- [docs/architecture.md:17-52](file://docs/architecture.md#L17-L52)
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)

### macOS Setup (Step-by-Step)
1. Install .NET SDK and PostgreSQL via Homebrew or official installer
2. Initialize PostgreSQL and create a development database
3. Install an IDE with C# and Blazor support
4. Clone the repository and restore dependencies
5. Configure environment variables for secrets and database connectivity
6. Run EF Core migrations
7. Start backend microservices and frontend
8. Validate API endpoints using the API contracts

**Updated** Verify PostgreSQL SSL configuration and restrict network access appropriately.

**Section sources**
- [docs/architecture.md:17-52](file://docs/architecture.md#L17-L52)
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)

## Dependency Analysis
The platform's dependencies are primarily defined by the 3-layer architecture and security model:
- Backend depends on .NET Core and EF Core
- Database depends on PostgreSQL
- Frontend depends on Blazor
- Security depends on JWT and API Keys

```mermaid
graph LR
DotNet[".NET Core Runtime"]
EF["Entity Framework Core"]
PG["PostgreSQL"]
Blazor["Blazor"]
JWT["JWT"]
APIKey["API Keys"]
DotNet --> EF
EF --> PG
Blazor --> DotNet
Blazor --> JWT
Blazor --> APIKey
```

**Diagram sources**
- [docs/architecture.md:17-52](file://docs/architecture.md#L17-L52)
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)

**Section sources**
- [docs/architecture.md:17-52](file://docs/architecture.md#L17-L52)
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)

## Performance Considerations
- Use PostgreSQL tuning for high-concurrency scenarios (e.g., POS redemptions)
- Optimize EF Core queries and consider connection pooling
- Monitor API latency and throughput for POS verification and redemption endpoints
- Scale microservices independently based on load

[No sources needed since this section provides general guidance]

## Security Incident Response

### Recent Security Incident
On August 15, 2026, the NonCash platform experienced a ransomware attack on the shared DEV PostgreSQL server at `45.119.87.247`. The attacker exploited an open PostgreSQL configuration (`pg_hba.conf` with `0.0.0.0/0`) to gain unauthorized access and dropped the `noncash` database, replacing it with a ransom note database called `readme_to_recover`.

### Immediate Response Actions Taken
- **Database Recovery**: Rebuilt the database from EF migrations and SeedTool
- **Credential Rotation**: Changed both `postgres` superuser password and `noncash_app` user password to `NonCashMachine@2026`
- **Network Restrictions**: Updated `pg_hba.conf` to restrict access to localhost (`127.0.0.1/32`, `::1/128`) and server IP (`45.119.87.247/32`) only
- **Configuration Updates**: Updated all connection strings in `appsettings.json` and `appsettings.Development.json`
- **Service Restoration**: Restarted PostgreSQL service and verified all tests passing (135 tests total)

### Lessons Learned and Prevention Measures
1. **Never expose PostgreSQL to the internet** without proper firewall rules and IP whitelisting
2. **Implement defense-in-depth**: Combine strong passwords with network-level security controls
3. **Regular security audits**: Monitor for open ports and unauthorized access attempts
4. **Backup strategy**: Maintain migration files and seed scripts in source control for disaster recovery
5. **Environment isolation**: Never share credentials across different environments

### Emergency Response Checklist
- [ ] Verify PostgreSQL service is running and accessible
- [ ] Check `pg_hba.conf` for proper network restrictions
- [ ] Confirm all connection strings are updated with current credentials
- [ ] Validate database integrity and run migrations if needed
- [ ] Test API endpoints and health checks
- [ ] Review audit logs for any suspicious activity
- [ ] Update backup schedules and verify backup integrity

**Section sources**
- [_bmad-output/session-log-2026-08-15.md:1-86](file://_bmad-output/session-log-2026-08-15.md#L1-L86)
- [src/NonCash.API/appsettings.json:21-26](file://src/NonCash.API/appsettings.json#L21-L26)
- [src/NonCash.API/appsettings.Development.json:23-25](file://src/NonCash.API/appsettings.Development.json#L23-L25)

## Troubleshooting Guide
- Database connectivity failures: Verify PostgreSQL is running, credentials are correct, and the connection string matches environment variables
- API authentication errors: Confirm API Key header and JWT bearer token are set correctly per the API contracts
- Migration issues: Re-run EF Core migrations and ensure the target database is reachable
- Cross-platform differences: Align .NET SDK versions across Windows, Linux, and macOS

**Updated** Security-related troubleshooting:
- **Unauthorized access errors**: Check `pg_hba.conf` configuration and ensure client IP is whitelisted
- **SSL connection failures**: Verify SSL certificates are properly configured and connection string includes `SSL Mode=Require`
- **Database not found errors**: Ensure database exists and has proper ownership permissions
- **Connection timeouts**: Verify firewall rules allow traffic on port 5432 from authorized sources only

**Section sources**
- [docs/api-contracts.md:5-10](file://docs/api-contracts.md#L5-L10)
- [docs/architecture.md:28-35](file://docs/architecture.md#L28-L35)
- [_bmad-output/session-log-2026-08-15.md:74-79](file://_bmad-output/session-log-2026-08-15.md#L74-L79)

## Conclusion
The NonCash platform requires a .NET Core backend, PostgreSQL database, EF Core ORM, and a Blazor frontend. Security is enforced via JWT and API Keys, and the system supports multi-tenancy. By following the environment setup steps for Windows, Linux, and macOS, configuring environment variables and secrets, and validating API endpoints, teams can establish a reliable development and deployment environment aligned with the documented architecture.

**Updated** Following the security incident, all environments now operate with enhanced security measures including restricted network access, rotated credentials, and improved monitoring capabilities.

[No sources needed since this section summarizes without analyzing specific files]

## Appendices
- BMAD configuration references:
  - Core module configuration
  - BMM module configuration

**Section sources**
- [_bmad/core/config.yaml:1-10](file://_bmad/core/config.yaml#L1-L10)
- [_bmad/bmm/config.yaml:1-17](file://_bmad/bmm/config.yaml#L1-L17)