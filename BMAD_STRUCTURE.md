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
   - ProductionPlan
   - Approval

2. **Data Models:**
   - VoucherModel
   - CustomerModel
   - BusinessModel
   - OrderModel
   - PaymentModel
   - ProductionPlanModel
   - ApprovalModel

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
   - ProductionPlan
   - Approval

3. **Data Access Patterns:**
   - Repository pattern for data abstraction.
   - Dependency injection for loose coupling.

4. **Security Measures:**
   - API Key Authentication
   - JWT Token Management

## Conclusion

This project aims to provide a robust, scalable, and secure voucher production platform that can be easily adopted by businesses. The architecture ensures flexibility and maintainability, allowing for future enhancements and technology upgrades.

## Work-Type Protocol (STATUS / STORY / BUG / CR-LOG / CR / ASK / END)

A session opens with `[STATUS]` and closes with `[END]`; every working request in between carries a **work-type signal** (one word in brackets). If the signal is missing, the agent infers it and states the assumption before acting. Entry-point reading replaces full codebase scans — only the mapped files are read first.

| Signal | Meaning | Read first (entry points) | Actions | Footprints |
|---|---|---|---|---|
| `[STATUS]` | Open a session: show the current board | All CR registries in `docs/` + sprint-status + gate state | Report items pending user verification, pending decisions, in-progress work; suggest the first command | None (read-only) |
| `[STORY]` | Planned feature from an epic | `_bmad-output/implementation-artifacts/sprint-status.yaml` + the story file + only the `docs/` sections it references | Implement per acceptance criteria, with tests; hand over a verification guide at dev-complete | Story task checkboxes, sprint-status transition, `docs/` if contracts change |
| `[BUG]` | Actual behavior ≠ intended behavior | The one `docs/` section governing the behavior + the involved module | Read-only diagnose → root cause → change-proposal table → fix + regression test | Fix + regression test in git history; governing doc fixed if the spec was wrong; pending-decision bugs registered as CR entries. **Escalates to `[CR]` if the root cause is a design disagreement** |
| `[CR-LOG]` | Register a change agreed in discussion — **docs only, no code** | The governing `docs/` sections only | Write the change into the source-of-truth doc as a tagged CR entry (`CR-YYYY-MM-DD-nn`) with an explicit status, plus a changelog row | Doc CR entry + changelog row; no code touched |
| `[CR]` | Implement a previously registered CR | The registered CR entry + governing `docs/` sections + affected module | Implement with tests; set status to *Dev-complete* + hand over a verification guide; flip to *Verified* on user pass; large CRs become stories | Code + tests, CR status flips, changelog row |
| `[ASK]` | Analysis / advice / question | `docs/index.md` + targeted memory/knowledge | Strictly read-only; deliver findings/recommendation | None, unless asked to persist |
| `[END]` | Close a session | Session state + the CR registries | Closing ritual: (1) flip user-confirmed items to *Verified*; (2) sweep any unregistered pending items into CR entries; (3) closing report — Verified today / pending user verification (gate) / pending decisions / uncommitted files; (4) memory check for missed lessons; (5) suggest the first command for the next session | Status flips, new CR entries; session log only for incident narratives or on request |

**CR lifecycle:** discussion → agreement → `[CR-LOG]` registers the change in the source-of-truth docs → a later `[CR]` implements it. Docs must never describe unimplemented behavior without a visible pending status.

**CR status model (two-stage completion):**

| Status | Meaning | Set by |
|---|---|---|
| `Registered — pending decision` | CR recorded in a doc, not yet decided | Agent (on `[CR-LOG]`) |
| `Approved — pending implementation` | User approved, awaiting implementation | User approves → agent sets |
| `Dev-complete (date) — pending user verification` | Code + unit/integration tests green; agent stops and hands over a verification guide | Agent |
| `Verified (date)` | User's real-world test **passed** — closed | User confirms → agent sets |
| `Rejected (date)` | Decided not to do | User |

For docs-only CRs the two stages collapse: the user's review of the doc diff is the verification.

**Completion gate:** while any item sits at `Dev-complete — pending user verification`, no new implementation task starts — unless the user explicitly allows parallel work. The gate also applies to `[BUG]` fixes and `[STORY]` work: a story/fix is done only after user acceptance, not when tests go green. Every dev-complete handover must include a verification guide (what to restart, which screen/endpoint, expected result). A "failed" verdict reopens the work as `[BUG]`.

**Session-log policy:** session logs in `_bmad-output/` are **not** a default footprint anymore — the doc records (CR entries, changelogs, story files, sprint-status) carry the traceable state. Write one only for incident/diagnostic narratives that do not fit a CR entry, or when the user asks. Corollary: any pending item must be registered immediately as a CR entry in its governing doc — nothing may live only in a session log.

**Universal gates:** a change-proposal table (type / target / what / why / risk) precedes every file modification; no git commits unless explicitly requested; configuration changes with demo/production impact are flagged explicitly.
