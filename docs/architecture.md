# System Architecture - NonCash Project

This document describes the high-level architecture of the NonCash voucher platform.

## Architecture Pattern: 3-Layer SaaS

The system is designed as a Software as a Service (SaaS) platform using a robust 3-layer architecture to ensure scalability and maintainability.

### 1. User Interface (GUI) - Frontend
- **Technology**: Blazor Server or WebAssembly.
- **Responsibilities**:
    - Manage user interactions for business admins and marketing staff.
    - Provide dashboards for production planning and approval tracking.
    - Visualize voucher usage and performance metrics.
- **Communication**: Communicates with the Business Logic Layer (BLL) via service-to-service calls or internal APIs.

### 2. Business Logic Layer (BLL) - Core
- **Technology**: C# / .NET Core.
- **Organization**: Structured as **Microservices** for loose coupling and independent scalability.
- **Key Services**:
    - **Planning Service**: Manages voucher plan creation, budgeting, and targets.
    - **Approval Service**: Handles the routing and state management of plan reviews.
    - **Distribution Service**: Manages voucher sales, batch promotions, and inbox delivery.
    - **Usage Service**: Orchestrates the POS redemption workflow (Lock -> Commit/Rollback).
    - **Billing Service**: Manages the prepaid credit ledger (usage-based fee) — charges 1 credit per voucher at its value moment (Gift at sale, Complimentary at redemption), enforces balance guards on upstream operations, and handles admin top-ups.
    - **Identity & Tenant Service**: Handles RBAC for `UserAccount`, multi-tenancy for `Brand` & `Outlet`, and profile management for `Customer`.
- **Security**: Implements JWT-based authentication and specialized logic for dynamic voucher code generation.

### 3. Data Access Layer (DAL) - Infrastructure
- **Technology**: Entity Framework (EF) Core with **PostgreSQL**.
- **Pattern**: Repository Pattern for data abstraction.
- **Responsibilities**:
    - Handles all database CRUD operations.
    - Decoupled from BLL, allowing for easy schema updates or technology changes.
    - Manages database consistency through transactions, especially for POS usage.

## Security Architecture

- **Multi-tenancy**: Uses `BrandID` strictly to isolate data between different businesses sharing the SaaS platform, ensuring staff users and their outlets only access authorized tenant spaces.
- **Dynamic Security**: Vouchers use a rotating dynamic code (similar to JWT logic) to prevent reuse and unauthorized scanning.
- **Integration Security**: POS systems are authenticated via API Keys and locked to specific ranges defined in the planning phase.

---

## External Integration Boundary: Loyalty App Partnership Model

NonCash is designed as a **generic voucher engine** that integrates with **any brand Loyalty App** — not just one specific partner. Examples include Giga Mall App, Coffee House App, Golden Gate App, or any brand-operated mobile loyalty application.

### Architectural Principle

```
┌─────────────────────────┐         ┌──────────────────────────┐
│    Brand Loyalty App    │         │        NonCash           │
│  (Giga Mall, etc.)      │◄───────►│  (Voucher Engine)        │
│                         │   API   │                          │
│  • Customer master data │         │  • Voucher production    │
│  • Visit/purchase history│        │  • Distribution execution│
│  • Segmentation/analytics│        │  • POS redemption        │
│  • Marketing planning   │         │  • Fraud protection      │
│  • Push notifications   │         │  • Event history/audit   │
│  • Voucher wallet UI    │         │  • Cross-tenant settlement│
└─────────────────────────┘         └──────────────────────────┘
```

### Responsibility Split

| Domain | Loyalty App | NonCash |
|---|:---:|:---:|
| Customer profiles and master data | Primary | Reference only (phone/MemberID) |
| Visit history and purchase behavior | Primary | — |
| Segmentation and targeting | Primary | — |
| Campaign marketing decisions | Primary | — |
| Push notifications | Primary | — |
| Voucher wallet display | Primary (consumes NonCash data) | Data provider |
| Voucher production and lifecycle | — | Primary |
| Distribution execution | Triggers via API | Primary |
| POS redemption | — | Primary |
| Fraud prevention | — | Primary |
| Event history (issued, redeemed, transferred, expired) | Consumer | Primary (emitter) |
| Cross-tenant settlement tracking | Consumer | Primary |

### Integration Mechanism

NonCash exposes a **Loyalty App Integration API** (see `docs/api-contracts.md`) providing:

1. **Segment distribution** — Loyalty App pushes a member segment, NonCash distributes vouchers.
2. **Wallet query** — Loyalty App pulls a member's voucher state for in-app display.
3. **Event history** — Loyalty App pulls full lifecycle events for analytics and notifications.
4. **Webhooks (push)** — NonCash pushes real-time lifecycle events to the Loyalty App.
5. **Campaign performance** — Loyalty App queries aggregated redemption and ROI data.

### Key Design Decisions

- **NonCash never owns the customer relationship.** It is infrastructure, not a consumer-facing app.
- **Any Loyalty App can integrate** using the same API, with partner-specific API keys for isolation.
- **Event-driven architecture** ensures the Loyalty App receives real-time updates without polling.
- **No data duplication** — NonCash stores only the minimum member reference (phone number) needed for voucher ownership. Full customer profiles remain in the Loyalty App.

## Technical Stack Summary

| Layer | Technology |
|:---|:---|
| **Frontend** | Blazor App |
| **Backend** | C# / .NET Core (Microservices) |
| **Database** | PostgreSQL |
| **ORM** | Entity Framework Core |
| **Auth** | JWT + API Keys |
| **OS** | Linux / Windows (SaaS Cloud Optimized) |
