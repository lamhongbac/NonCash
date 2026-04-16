# Source Tree Analysis - NonCash Project

## Project Structure (Target Architecture)

Based on the project description and key functionalities, the following directory structure is proposed to support the 3-layer architecture and microservices logic.

```text
NonCash/
├── src/
│   ├── NonCash.Core/           # Business Logic Layer (BLL)
│   │   ├── Entities/           # Core business entities (Voucher, Plan, Member)
│   │   ├── Interfaces/         # Repository and Service interfaces
│   │   ├── Services/           # Microservices implementation
│   │   └── Specifications/     # Business rules (e.g., Expiry logic)
│   ├── NonCash.Infrastructure/ # Data Access Layer (DAL)
│   │   ├── Data/               # Entity Framework (EF) DbContext
│   │   ├── Repositories/       # EF implementations of interfaces
│   │   └── Migrations/         # PostgreSQL schema migrations
│   ├── NonCash.Web/            # User Interface (GUI - Blazor)
│   │   ├── Pages/              # Management UI (Planning, Approval)
│   │   ├── Shared/             # UI Components
│   │   └── ViewModels/         # UI state management
│   ├── NonCash.API/            # POS Integration Layer (RESTful)
│   │   ├── Controllers/        # API Endpoints (Usage, Verification)
│   │   ├── Middleware/         # JWT and API Key Auth
│   │   └── DTOs/               # API Request/Response models
│   └── NonCash.Shared/         # Shared libraries
│       └── Models/             # Shared DTOs and Constants
├── docs/                       # Project Documentation
├── tests/                      # Testing Layer
│   ├── NonCash.UnitTests/
│   └── NonCash.IntegrationTests/
└── _bmad/                      # BMAD AI Agent configuration
```

## Critical Folders

| Folder | Purpose |
|:---|:---|
| `NonCash.Core` | Contains all business logic and domain entities. Decoupled from data access. |
| `NonCash.Infrastructure` | Handles all database interactions using Entity Framework and PostgreSQL. |
| `NonCash.Web` | The Blazor application for management staff (SaaS interface). |
| `NonCash.API` | Secure entry point for external POS systems using RESTful APIs. |
| `NonCash.Shared` | Contains code shared between the Web app and the API (e.g., Transfer models). |

## Entry Points

1.  **Management Web Portal**: Located in `NonCash.Web`.
2.  **POS Integration API**: Located in `NonCash.API`.
