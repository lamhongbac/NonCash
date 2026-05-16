# Story 0.1: Project Foundation & Solution Setup

Status: review

## Story

As a Dev Agent,
I want to initialize the complete .NET solution structure, shared dependencies, and foundational tooling for the NonCash voucher platform,
So that all subsequent epics have a consistent, buildable, and testable codebase to extend.

## Acceptance Criteria

**AC1: Solution Structure**
Given the project is greenfield
When the dev agent scaffolds the solution
Then the following projects are created under `src/`:
- `NonCash.Core` (Class Library) — Entities, Interfaces, Services, Specifications
- `NonCash.Infrastructure` (Class Library) — EF Core DbContext, Repositories, Migrations
- `NonCash.Shared` (Class Library) — Shared DTOs, constants, enums, helpers
- `NonCash.Web` (Blazor Server / WASM) — Management portal
- `NonCash.API` (ASP.NET Core Web API) — POS and Member app endpoints
- `NonCash.UnitTests` (xUnit) — Unit test project
- `NonCash.IntegrationTests` (xUnit) — Integration test project

**AC2: Dependency Wiring**
Given the projects exist
When `dotnet build` is executed
Then there are zero compilation errors and project references follow:
- `NonCash.Infrastructure` -> `NonCash.Core`
- `NonCash.Web` -> `NonCash.Core`, `NonCash.Infrastructure`, `NonCash.Shared`
- `NonCash.API` -> `NonCash.Core`, `NonCash.Infrastructure`, `NonCash.Shared`
- Test projects -> all relevant source projects

**AC3: Core NuGet Packages**
Given the solution builds
When inspecting package references
Then the following are installed in correct projects:
- `Npgsql.EntityFrameworkCore.PostgreSQL` (Infrastructure)
- `Microsoft.EntityFrameworkCore.Design` (Infrastructure + API as startup)
- `Microsoft.AspNetCore.Authentication.JwtBearer` (API + Web)
- `AutoMapper` or `Mapperly` (Core or Shared — team choice, document it)
- `FluentValidation` (Core or API)
- `Blazorise` or `MudBlazor` (Web — UI component library, pick one and lock it)

**AC4: Base Entity & Repository Contract**
Given `NonCash.Core` exists
When a base entity class is created
Then it exposes `Id : Guid`, `CreatedAt : DateTime`, `UpdatedAt : DateTime?`
And `IRepository<T>` interface exists with: `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update`, `Delete`, `SaveChangesAsync`

**AC5: DbContext Skeleton**
Given `NonCash.Infrastructure` exists
When `ApplicationDbContext` is created
Then it inherits `DbContext`, exposes `DbSet` placeholders for all core tables (even if empty), and is configured for PostgreSQL via `OnConfiguring` / `Program.cs`

**AC6: CI-Ready Build**
Given the repository is clean
When `dotnet test` is run
Then the test runner executes with zero tests (baseline passing) and no build failures

## Tasks / Subtasks

- [x] Task 1: Scaffold solution and 7 projects (AC1)
  - [x] Subtask 1.1: Create root `.sln` file
  - [x] Subtask 1.2: Create each project with correct SDK type
  - [x] Subtask 1.3: Wire project references
- [x] Task 2: Install and verify NuGet packages (AC3)
  - [x] Subtask 2.1: Install packages with compatible versions for .NET 9
  - [x] Subtask 2.2: Restore and build
- [x] Task 3: Define base domain primitives (AC4)
  - [x] Subtask 3.1: `BaseEntity.cs` in `NonCash.Core/Entities/`
  - [x] Subtask 3.2: `IRepository<T>` in `NonCash.Core/Interfaces/`
  - [x] Subtask 3.3: `IUnitOfWork` in `NonCash.Core/Interfaces/`
- [x] Task 4: Implement Infrastructure skeleton (AC5)
  - [x] Subtask 4.1: `ApplicationDbContext.cs` in `NonCash.Infrastructure/Data/`
  - [x] Subtask 4.2: Generic `Repository<T>` in `NonCash.Infrastructure/Repositories/`
  - [x] Subtask 4.3: Design-time factory or connection-string config for migrations
- [x] Task 5: Configure API startup (AC2, AC6)
  - [x] Subtask 5.1: `Program.cs` with DI registration for DbContext, Repositories
  - [x] Subtask 5.2: Health check endpoint `/health`
  - [x] Subtask 5.3: Swagger / OpenAPI wired
- [x] Task 6: Configure Blazor startup
  - [x] Subtask 6.1: `Program.cs` with MudBlazor services
  - [x] Subtask 6.2: A default landing page that compiles

## Dev Notes

### Architecture Compliance
- **Pattern**: 3-Layer SaaS — Core (BLL), Infrastructure (DAL), Web/API (GUI).
- **DO NOT** put business logic in Controllers or Razor code-behind. All logic belongs in Core Services.
- **DO NOT** reference Infrastructure directly from Core. Use interfaces + DI.
- **Multi-tenancy**: Every DbSet query must eventually filter by `BrandID`. Build `BrandId` into `BaseEntity` now or ensure all entities implement `IBrandScoped`.

### File Structure Requirements
```
src/
  NonCash.Core/
    Entities/
    Interfaces/
    Services/
    Specifications/
  NonCash.Infrastructure/
    Data/
    Repositories/
    Migrations/
  NonCash.Web/
    Pages/
    Shared/
    ViewModels/
  NonCash.API/
    Controllers/
    Middleware/
    DTOs/
  NonCash.Shared/
    Models/
    Constants/
    Enums/
tests/
  NonCash.UnitTests/
  NonCash.IntegrationTests/
```

### Database & PostgreSQL
- Use snake_case naming convention for PostgreSQL tables/columns via EF Core naming strategy.
- Connection string should read from `appsettings.Development.json` and environment variables for production.
- Enable sensitive data logging only in Development.

### Security Foundations
- JWT configuration section in `appsettings.json`: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` (min 32 bytes).
- API Key middleware placeholder (empty pipeline slot) in `NonCash.API`.

### Testing Standards
- Use xUnit + FluentAssertions + NSubstitute (or Moq).
- Every Core service must be testable with mocked `IRepository<T>`.

### References
- [Source: docs/architecture.md] — 3-Layer SaaS pattern and microservices organization.
- [Source: docs/data-models.md] — Entity list informs DbContext placeholder sets.
- [Source: docs/source-tree-analysis.md] — Target directory structure.
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-04-17.md] — Greenfield setup recommendation.

## Dev Agent Record

### Agent Model Used
Qoder

### Debug Log References
- EF Core version compatibility: Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4 depends on EF Core 9.0.1. Explicit Microsoft.EntityFrameworkCore 9.0.4 caused assembly conflicts. Removed explicit package to let Npgsql resolve transitive dependencies.
- MudBlazor requires `@using MudBlazor` in `_Imports.razor` for component resolution across all .razor files.

### Completion Notes List
- Solution NonCash.sln scaffolded with 7 projects under `src/` and `tests/`
- Project references wired per AC2 requirements
- NuGet packages installed with .NET 9 compatible versions:
  - Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4
  - Microsoft.EntityFrameworkCore.Design 9.0.1
  - Microsoft.AspNetCore.Authentication.JwtBearer 9.0.4
  - Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.0.4
  - FluentValidation 12.1.1
  - MudBlazor 9.4.0
  - Swashbuckle.AspNetCore 10.1.7
- BaseEntity implements Id, CreatedAt, UpdatedAt with automatic assignment in SaveChangesAsync
- IRepository<T> and IUnitOfWork contracts defined in Core
- Generic Repository<T> implemented with async CRUD operations
- ApplicationDbContext includes snake_case naming convention for PostgreSQL and automatic audit timestamps
- API Program.cs configures DbContext, repository DI, JWT auth placeholder, Swagger, and health checks
- Web Program.cs configures MudBlazor services with default layout and landing page
- `dotnet build` passes with zero errors
- `dotnet test` passes with zero tests (baseline)

### File List
- NonCash.sln
- src/NonCash.Core/Entities/BaseEntity.cs
- src/NonCash.Core/Interfaces/IBrandScoped.cs
- src/NonCash.Core/Interfaces/IRepository.cs
- src/NonCash.Core/Interfaces/IUnitOfWork.cs
- src/NonCash.Infrastructure/Data/ApplicationDbContext.cs
- src/NonCash.Infrastructure/Repositories/Repository.cs
- src/NonCash.API/Program.cs
- src/NonCash.API/appsettings.json
- src/NonCash.API/appsettings.Development.json
- src/NonCash.Web/Program.cs
- src/NonCash.Web/Components/App.razor
- src/NonCash.Web/Components/_Imports.razor
- src/NonCash.Web/Components/Layout/MainLayout.razor
- src/NonCash.Web/Components/Routes.razor
- src/NonCash.Web/Components/Pages/Home.razor
- src/NonCash.Web/NonCash.Web.csproj
- src/NonCash.API/NonCash.API.csproj
- src/NonCash.Infrastructure/NonCash.Infrastructure.csproj

