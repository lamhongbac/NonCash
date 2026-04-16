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
    - **Member Service**: Manages B2B/B2C profiles and transfer logic.
- **Security**: Implements JWT-based authentication and specialized logic for dynamic voucher code generation.

### 3. Data Access Layer (DAL) - Infrastructure
- **Technology**: Entity Framework (EF) Core with **PostgreSQL**.
- **Pattern**: Repository Pattern for data abstraction.
- **Responsibilities**:
    - Handles all database CRUD operations.
    - Decoupled from BLL, allowing for easy schema updates or technology changes.
    - Manages database consistency through transactions, especially for POS usage.

## Security Architecture

- **Multi-tenancy**: Uses `BrandID` and `OrganizationID` to isolate data between different businesses sharing the SaaS platform.
- **Dynamic Security**: Vouchers use a rotating dynamic code (similar to JWT logic) to prevent reuse and unauthorized scanning.
- **Integration Security**: POS systems are authenticated via API Keys and locked to specific ranges defined in the planning phase.

## Technical Stack Summary

| Layer | Technology |
|:---|:---|
| **Frontend** | Blazor App |
| **Backend** | C# / .NET Core (Microservices) |
| **Database** | PostgreSQL |
| **ORM** | Entity Framework Core |
| **Auth** | JWT + API Keys |
| **OS** | Linux / Windows (SaaS Cloud Optimized) |
