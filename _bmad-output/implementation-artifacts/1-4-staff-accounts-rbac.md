# Story 1.4: Quan ly Tai khoan Noi bo & Phan Quyen (Staff Accounts & RBAC)

Status: review

## Story

As a System Admin,
I want to create staff user accounts and map them to Brands with Roles,
So that each account operates with the correct permissions and within the correct Brand.

## Acceptance Criteria

**AC1: User Account Creation**
Given a System Admin is on the Permission Management screen
When they create a new User Account with Role and BrandID
Then the system initializes the account with a secure password hash
And stores: `UserID`, `BrandID`, `Username`, `PasswordHash`, `FullName`, `Role`, `Status`

**AC2: Role-Based Access Control**
Given the system has four roles: Admin, Planner, Approver, BrandManager
When a user attempts an action
Then the system enforces:
- `Admin`: full system access, cross-brand user management
- `BrandManager`: manage Outlets, Customers, view plans within their Brand
- `Planner`: create and edit VoucherPlanHeaders within their Brand
- `Approver`: approve/reject plans within their Brand

**AC3: JWT Token Generation**
Given a valid username and password
When the user authenticates via `/api/v1/auth/login`
Then the system issues a JWT containing: `sub` (UserID), `brandId`, `role`, `exp`
And the token is signed with the configured secret key

**AC4: Account Status Management**
Given an existing account
When an Admin locks the account (`Status = Locked`)
Then authentication attempts return 403 / Account Locked
And existing sessions are invalidated on next validation

**AC5: Multi-tenancy Enforcement in Auth**
Given a user belongs to Brand A
When they attempt to access Brand B's data
Then the system returns 403 regardless of their Role
And `BrandID` in JWT is used for all tenant-scoped repository queries

## Tasks / Subtasks

- [x] Task 1: Define UserAccount entity & roles (AC1, AC2)
  - [x] Subtask 1.1: `UserAccount.cs` entity in `NonCash.Core/Entities/`
  - [x] Subtask 1.2: `UserRole` enum: Admin, Planner, Approver, BrandManager
  - [x] Subtask 1.3: `UserStatus` enum: Active, Locked
  - [x] Subtask 1.4: EF FluentAPI config with FK to Brand (nullable for Admin)
- [x] Task 2: Authentication service (AC3)
  - [x] Subtask 2.1: `IAuthService` with `LoginAsync`, `HashPassword`, `VerifyPassword`
  - [x] Subtask 2.2: Use `BCrypt.Net-Next` for password hashing
  - [x] Subtask 2.3: `IJwtTokenService` / `JwtTokenService` generating signed tokens with claims
- [x] Task 3: Authorization infrastructure (AC2, AC5)
  - [x] Subtask 3.1: Custom `[Authorize(Roles = ...)]` attributes on controllers
  - [x] Subtask 3.2: `ICurrentUserService` updated with `GetCurrentUserRole()` from HTTP context claims
  - [x] Subtask 3.3: `BrandScopeMiddleware` enforcing BrandID scoping on every request
- [x] Task 4: API endpoints (AC1, AC3, AC4)
  - [x] Subtask 4.1: `AuthController`: `POST /api/v1/auth/login`
  - [x] Subtask 4.2: `UsersController`: `POST /api/v1/users`, `PUT /api/v1/users/{id}/lock`, `PUT /api/v1/users/{id}/unlock`, `GET /api/v1/users`, `GET /api/v1/users/{id}`
  - [x] Subtask 4.3: DTOs with validation (password min 8 chars, etc.)
- [x] Task 5: Blazor UI (AC1, AC4)
  - [x] Subtask 5.1: Login page with JWT response handling
  - [x] Subtask 5.2: `Users.razor` admin page for account management
  - [x] Subtask 5.3: HttpClient calls with Bearer token (token storage placeholder)
- [x] Task 6: Database migration
  - [x] Subtask 6.1: `user_accounts` table migration (AddUserAccount)
  - [x] Subtask 6.2: Seed admin account (`admin` / `Admin@123`) via DatabaseSeeder
- [x] Task 7: Tests
  - [x] Subtask 7.1: Unit/integration tests for `JwtTokenService` (claim generation, expiry, signing)
  - [x] Subtask 7.2: Integration tests for login (valid, invalid password, locked account)
  - [x] Subtask 7.3: Integration tests for user CRUD (create, lock, unlock, list, duplicate)

## Dev Notes

### Architecture Compliance
- RBAC and JWT are **cross-cutting concerns** used by every subsequent epic.
- Place `ICurrentUserService` in Core so all services can access the current user context without referencing Web/API projects.
- The API Key auth for POS (Epic 4) is separate from JWT. Leave a middleware placeholder for API Keys but do not implement yet.

### File Structure Requirements
```
src/NonCash.Core/Entities/UserAccount.cs
src/NonCash.Core/Enums/UserRole.cs
src/NonCash.Core/Enums/UserStatus.cs
src/NonCash.Core/Interfaces/IAuthService.cs
src/NonCash.Core/Interfaces/IJwtTokenService.cs
src/NonCash.Core/Services/AuthService.cs
src/NonCash.Infrastructure/Data/Configurations/UserAccountConfiguration.cs
src/NonCash.API/Controllers/AuthController.cs
src/NonCash.API/Controllers/UsersController.cs
src/NonCash.API/Services/CurrentUserService.cs
src/NonCash.API/Middleware/BrandScopeMiddleware.cs
src/NonCash.Web/Pages/Login.razor
src/NonCash.Web/Pages/Admin/Users.razor
```

### Database Schema
- Table: `user_accounts`
- Columns: `user_id` (uuid PK), `brand_id` (uuid FK nullable), `username` (varchar 100 unique not null), `password_hash` (varchar 255 not null), `full_name` (varchar 200), `role` (varchar 20 not null), `status` (varchar 20), `created_at` (timestamptz), `updated_at` (timestamptz)
- Index: `IX_user_accounts_username` (unique), `IX_user_accounts_brand_id`
- FK: `FK_user_accounts_brands_brand_id` (RESTRICT)

### API Contracts
- `POST /api/v1/auth/login` -> `{ username, password }` => `{ token, expiresAt, user { userId, fullName, role, brandId } }`
- `POST /api/v1/users` -> Admin only
- All other endpoints require `Authorization: Bearer <jwt>`

### Security & NFR
- NFR3 (RBAC): This story is the RBAC foundation. Roles MUST be enforced on every controller action.
- NFR4 (Multi-tenancy): BrandID in JWT overrides any request-body BrandID for tenant-scoped endpoints.
- Passwords must be hashed with salt (BCrypt or Identity).
- JWT secret key must be >= 32 characters; store in environment variables, never in source code.

### Testing Standards
- Use `WebApplicationFactory` for integration tests.
- Generate a test JWT in integration tests to simulate different roles.

### References
- [Source: docs/data-models.md#UserAccount] — Entity definition.
- [Source: docs/architecture.md#Security Architecture] — JWT + API Keys.
- [Source: Key Functionalities.txt#I] — Role and right setup context.

## Dev Agent Record

### Agent Model Used

Qoder AI Assistant

### Debug Log References

- Moved JwtTokenService from Core to API layer (Core cannot reference JWT/System.IdentityModel packages)
- Fixed MudBlazor MaxWidth.Smaller -> MaxWidth.Small (not available in 9.x)
- Added FakeCurrentUserService.GetCurrentUserRole() to integration test mock
- Added Microsoft.Extensions.DependencyInjection using to DatabaseSeeder for CreateScope()

### Completion Notes List

- BCrypt.Net-Next 4.1.0 installed in Core project for password hashing
- IJwtTokenService interface in Core, JwtTokenService implementation in API (proper layer separation)
- BrandScopeMiddleware validates non-admin users have brand_id claim
- Admin seed account: username=`admin`, password=`Admin@123`
- All 66 tests pass (12 Brand + 18 Outlet + 19 Customer + 17 Auth/User)
- user_accounts table created in PostgreSQL with unique index on username

### File List

- src/NonCash.Core/Entities/UserAccount.cs (entity + UserRole/UserStatus enums)
- src/NonCash.Core/Interfaces/IAuthService.cs (auth interface + AuthResult record)
- src/NonCash.Core/Interfaces/IJwtTokenService.cs (JWT interface)
- src/NonCash.Core/Interfaces/IUserAccountRepository.cs (user repository interface)
- src/NonCash.Core/Interfaces/ICurrentUserService.cs (added GetCurrentUserRole)
- src/NonCash.Core/Services/AuthService.cs (BCrypt login + password hashing)
- src/NonCash.Core/Services/UserService.cs (CRUD + lock/unlock)
- src/NonCash.Infrastructure/Data/Configurations/UserAccountConfiguration.cs (FluentAPI)
- src/NonCash.Infrastructure/Data/ApplicationDbContext.cs (DbSet<UserAccount>)
- src/NonCash.Infrastructure/Data/DatabaseSeeder.cs (seed admin account)
- src/NonCash.Infrastructure/Repositories/UserAccountRepository.cs
- src/NonCash.API/Services/JwtTokenService.cs (JWT generation)
- src/NonCash.API/Services/CurrentUserService.cs (added GetCurrentUserRole)
- src/NonCash.API/Middleware/BrandScopeMiddleware.cs (brand scope enforcement)
- src/NonCash.API/Controllers/AuthController.cs (POST /api/v1/auth/login)
- src/NonCash.API/Controllers/UsersController.cs (Admin-only CRUD)
- src/NonCash.API/DTOs/AuthDtos.cs (request/response DTOs)
- src/NonCash.API/Program.cs (DI registrations, middleware pipeline, seeder)
- src/NonCash.Web/Components/Pages/Login.razor
- src/NonCash.Web/Components/Pages/Admin/Users.razor
- tests/NonCash.IntegrationTests/Controllers/AuthControllerTests.cs (17 tests)

