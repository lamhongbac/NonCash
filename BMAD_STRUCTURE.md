# Business Model and Architecture Design (BMAD) Structure

## Business

1. **Primary Business Objectives:**
   - Provide a platform for businesses to produce vouchers for promotional purposes or sales through various channels such as B2B, B2C, etc.
   - Allow businesses to outsource voucher production to avoid investing in human resources.
   - Drive sales by targeting specific customer segments through targeted marketing campaigns using vouchers.

2. **Target Users:**
   - Businesses, particularly those in the retail sector (e.g., restaurants, hotels).

3. **Compliance and Regulatory Requirements:**
   - Ensure that vouchers and other forms of digital assets are distinct from traditional wallets to avoid regulatory issues.
   - Report revenue generated from voucher sales as part of the business's taxable income.

## Model

1. **Business Objects:**
   - Voucher
   - Customer
   - Business (Tenant)
   - Order
   - Payment

2. **Data Models:**
   - VoucherModel
   - CustomerModel
   - BusinessModel
   - OrderModel
   - PaymentModel

## Architecture

1. **Three-Layer Architecture:**
   - Data Access Layer (DAL)
   - Business Logic Layer (BLL)
   - User Interface (GUI)

2. **DAL:**
   - Handles database interactions using Entity Framework (EF).
   - Uses repositories for data abstraction.
   - Decoupled from other layers to support changes in the underlying data model or technology.

3. **BLL:**
   - Contains business logic and interacts with DAL through repositories.
   - Organized into microservices for loose coupling between components.

4. **GUI:**
   - Manages user interactions using Blazor framework.
   - Communicates with BLL for processing business logic.

## Data

1. **Database Choice:**
   - PostgreSQL or MongoDB (PostgreSQL preferred due to cost and performance).

2. **Data Models:**
   - Voucher
   - Customer
   - Business
   - Order
   - Payment

3. **Data Access Patterns:**
   - Repository pattern for data abstraction.
   - Dependency injection for loose coupling.

4. **Security Measures:**
   - API Key Authentication
   - JWT Token Management

## Conclusion

This project aims to provide a robust, scalable, and secure voucher production platform that can be easily adopted by businesses. The architecture ensures flexibility and maintainability, allowing for future enhancements and technology upgrades.
