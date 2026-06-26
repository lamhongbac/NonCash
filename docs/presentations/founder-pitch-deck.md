# NonCash Founder Pitch Deck Content

Use this document to compose your investor / co-founder pitch slides and prepare for Q&A.

---

## Slide-by-Slide Content

### Slide 1 — Title

**Title:** NonCash
**Subtitle:** The brand-owned incentive infrastructure for vouchers, gift cards, and loyalty.
**Tagline:** "Issue, distribute, redeem, and transfer digital incentives — securely, scalably, and measurably."

**Your details:**
- Founder name
- Contact information
- Date

---

### Slide 2 — The Problem

**Title:** Brands Lose Money and Customers on Broken Incentives

**Bullets:**
- Static promo codes are copied, leaked, and redeemed fraudulently.
- Campaigns are still run on spreadsheets — slow, error-prone, and hard to audit.
- Customers receive vouchers they never use, with no safe way to pass them on.
- Franchises lack centralized control over which outlets honor which promotions.
- Marketers cannot prove campaign ROI because redemption data is fragmented.

**Killer line:**
> "Every unused voucher is a lost sale. Every leaked code is lost margin."

---

### Slide 3 — The Solution

**Title:** NonCash — One Platform for the Full Incentive Lifecycle

**Bullets:**
- Multi-tenant SaaS for brands to create, approve, distribute, and track vouchers.
- Dynamic codes and API-key POS integration stop fraud before it happens.
- Peer-to-peer transfer turns customers into a zero-cost referral channel.
- Role-based workflow matches how real enterprises operate.
- Extensible architecture supports vouchers today, gift cards and loyalty points tomorrow.

**Visual suggestion:** A simple lifecycle diagram:

```text
Create Plan → Generate Vouchers → Distribute → Transfer/Gift → Redeem at POS → Report
```

---

### Slide 4 — Product Demo Modules

**Title:** Product Modules

| Module | What It Does |
| --- | --- |
| **Brand & Tenant Management** | Onboard brands, manage staff roles, approve business registrations. |
| **Campaign Planning** | Create voucher plans, define value/validity, submit for approval. |
| **Distribution** | Batch promotion by phone list, self-purchase (B2C/B2B), customer import. |
| **Redemption** | POS verify and commit with dynamic codes and outlet authorization. |
| **Social Transfer** | Peer-to-peer voucher gifting with sender/recipient confirmation. |
| **Reporting** | Real-time tracking of issuance, distribution, redemption, and expiry. |

**Speaker note:** Mention that the transfer module is already built and tested with 13 passing acceptance tests.

---

### Slide 5 — Business Value for Brands

**Title:** Real Business Outcomes

| Outcome | How NonCash Delivers It |
| --- | --- |
| **Higher revenue** | Unused vouchers are transferred and redeemed instead of expiring. |
| **Lower CAC** | Peer-to-peer gifting turns customers into organic referrers. |
| **Less fraud** | Dynamic codes and POS verification prevent counterfeiting and reuse. |
| **Higher retention** | Stored vouchers drive expiry-driven repeat visits. |
| **Better ROI** | Full audit trail from issuance to redemption. |
| **Faster execution** | Self-service campaign creation and approval workflows. |

**Killer line:**
> "NonCash turns voucher campaigns from a cost center into a measurable growth channel."

---

### Slide 6 — Business Value for Customers

**Title:** Customers Win Too

**Bullets:**
- Reuse unused vouchers by gifting them to friends or family.
- Send digital gifts securely by phone number — no risky screenshots.
- See voucher validity, expiry, and authorized outlets in one place.
- Buy vouchers directly from the brand store and receive them instantly.
- Trust the redemption process with transparent status and history.

**Killer line:**
> "Customers keep vouchers alive, and brands acquire new customers for free."

---

### Slide 7 — Market Opportunity

**Title:** A Large and Growing Market

**Bullets:**
- Digital gift cards and promotional vouchers are a global, recession-resilient market.
- Southeast Asia has high mobile penetration and voucher-driven consumer behavior.
- F&B, retail, hospitality, telco, and employee benefits are all target verticals.
- Brands currently rely on fragmented tools or expensive custom development.

**Visual suggestion:** A TAM/SAM/SOM chart:

| Level | Definition |
| --- | --- |
| TAM | Digital incentives and loyalty software in Southeast Asia |
| SAM | Mid-market F&B, retail, and hospitality brands in Vietnam |
| SOM | First 50 brand clients in year one |

---

### Slide 8 — Competitive Landscape

**Title:** Where NonCash Wins

| Capability | Generic Coupons | Wallet Vouchers | In-House Systems | NonCash |
| --- | --- | --- | --- | --- |
| Brand-owned customer data | Limited | No | Yes | Yes |
| Multi-tenant brand isolation | No | No | Custom | Yes |
| Dynamic code security | No | Yes | Sometimes | Yes |
| Peer-to-peer transfer | No | No | No | Yes |
| Atomic POS redemption | No | No | Sometimes | Yes |
| Open POS integration | Weak | Closed | Expensive | API-based |
| Extensible product line | No | No | Slow | Yes |

**Killer line:**
> "We sit between closed wallets and custom systems: brand-owned, secure, and fast to deploy."

---

### Slide 9 — Platform Extensibility

**Title:** Built to Expand Beyond Vouchers

**Bullets:**
- Core domain: issuance → ownership → transfer → redemption → audit.
- Same engine can power:
  - Gift cards
  - Loyalty points
  - Cashback credits
  - Discount coupons
  - Membership passes
  - Employee benefits
  - Event tickets

**Killer line:**
> "We are not building a voucher app. We are building the infrastructure for brand-owned incentives."

---

### Slide 10 — Technology & Architecture

**Title:** Enterprise-Ready Foundation

**Bullets:**
- **Backend:** .NET 9 Web API, EF Core 9, PostgreSQL.
- **Frontend:** Blazor with MudBlazor.
- **Security:** JWT authentication, API keys for POS, BCrypt password hashing.
- **Architecture:** Three-layer domain-driven design with repository pattern.
- **Testing:** xUnit integration tests with SQLite in-memory, 13/13 transfer acceptance tests passing.
- **Deployment:** Cloud-ready, container-friendly.

**Visual suggestion:** A simple architecture diagram showing Core / Infrastructure / API / Web layers.

---

### Slide 11 — Traction & Progress

**Title:** What We Have Built

**Bullets:**
- Core domain model and database schema implemented.
- JWT + RBAC with Admin, BrandManager, Planner, and Approver roles.
- Brand, Outlet, Customer, and UserAccount modules complete and tested.
- Peer-to-peer transfer feature (Epic 5) complete with full acceptance test coverage.
- User guides created for Admin, Brand, and Member roles.
- Clean architecture ready for rapid feature expansion.

**Metrics to show:**
- 13/13 transfer acceptance tests passing
- 28/28 unit tests passing
- 3 role-based user guides completed

---

### Slide 12 — Business Model

**Title:** How We Make Money

| Revenue Stream | Description |
| --- | --- |
| **SaaS subscription** | Monthly/annual tiers by brand size or voucher volume. |
| **Transaction fee** | Percentage on self-purchase and B2B voucher sales. |
| **Premium features** | Advanced analytics, white-label branding, API access, custom integrations. |
| **Implementation services** | Onboarding, POS integration, and training for enterprise clients. |

---

### Slide 13 — Go-to-Market Strategy

**Title:** How We Reach Customers

**Phase 1 — Prove:**
- Pilot with 3–5 F&B or retail brands in Vietnam.
- Use the transfer feature as the unique hook.

**Phase 2 — Scale:**
- Target mid-market chains and franchise groups.
- Partner with POS providers for easy integration.

**Phase 3 — Expand:**
- Add gift cards and loyalty points.
- Enter adjacent verticals: hospitality, telco, employee benefits.

---

### Slide 14 — Roadmap

**Title:** Product Roadmap

| Timeline | Milestones |
| --- | --- |
| **Q1** | Complete Epic 2–4: plan approval, batch promotion, POS redemption. |
| **Q2** | Pilot with 3–5 brands, iterate based on feedback. |
| **Q3** | Launch gift card module and reporting dashboard. |
| **Q4** | Loyalty points, mobile app, and first enterprise client. |

---

### Slide 15 — Team & Ask

**Title:** Join Us

**Team slide content:**
- Your background and role.
- Any co-founders or key advisors.
- Why this team can execute.

**The Ask:**
- Be specific. Examples:
  - "We are raising $X seed to complete product-market fit and onboard our first 10 paying brands."
  - "We are looking for a co-founder with enterprise SaaS sales experience."
  - "We are seeking introductions to F&B and retail chain decision-makers."

---

### Slide 16 — Closing

**Title:** NonCash Makes Incentives Work Harder

**Closing statement:**
> "Brands already spend billions on incentives. NonCash makes that spend secure, measurable, and social — and turns every customer into a potential referrer."

**Call to action:**
- Schedule a follow-up.
- Share contact details.
- Offer a demo.

---

## Investor Q&A Preparation

### Q: Why would a brand choose you over Momo, ZaloPay, or Grab vouchers?

**Answer:**
> "Wallet vouchers are powerful but they are closed ecosystems. The brand does not own the customer relationship or the redemption data. NonCash is brand-owned and white-label. The brand controls the rules, sees the analytics, and can integrate with any POS."

### Q: What stops a bigger player from copying this?

**Answer:**
> "Three things. First, our atomic POS redemption and peer-to-peer transfer are not standard features. Second, our architecture is designed for extensibility — the same platform handles vouchers, gift cards, points, and passes. Third, we integrate with existing POS systems rather than trying to replace them, which makes us easier to adopt."

### Q: How do you make money?

**Answer:**
> "SaaS subscriptions based on brand size and volume, plus transaction fees on self-purchase and B2B sales. Premium tiers include advanced analytics, white-label branding, and API access."

### Q: What is your initial target market?

**Answer:**
> "Mid-market F&B and retail chains in Vietnam with multiple outlets. They run frequent voucher campaigns, suffer from fraud and reconciliation pain, and need multi-outlet control."

### Q: How long to onboard a brand?

**Answer:**
> "A standard brand can be set up in hours. POS integration depends on the outlet system but our API-first design with API-key authentication keeps it lightweight."

### Q: What is your moat?

**Answer:**
> "Our moat is the combination of dynamic code security, atomic redemption, peer-to-peer transfer, true multi-tenancy, and an extensible domain. Competitors usually have two or three of these. We also build data moat over time: the more brands and vouchers on the platform, the better our analytics and optimization."

### Q: What if brands already have their own system?

**Answer:**
> "Custom systems are expensive to maintain and slow to change. We offer a faster, cheaper, and more secure alternative. Migration is straightforward because our API and data model map directly to standard voucher operations."

### Q: How do you handle fraud?

**Answer:**
> "We use dynamic voucher codes that are generated per transaction or per voucher, verified at POS through API-key authenticated endpoints. We also apply a prepare/lock/commit/rollback pattern to prevent double-spending."

### Q: Why peer-to-peer transfer?

**Answer:**
> "Because it solves two problems at once. Customers stop wasting unused vouchers, and brands get warm referrals for free. It also differentiates us from every closed-wallet and generic coupon solution."

### Q: What is the biggest risk?

**Answer:**
> "The biggest risk is execution speed and POS integration complexity. We mitigate this by targeting brands with standard POS systems first, offering clear API documentation, and using pilot programs to prove value before scaling."

### Q: What do you need from investors beyond money?

**Answer:**
> " introductions to decision-makers at F&B and retail chains, help with enterprise SaaS go-to-market strategy, and experience scaling a regional software company."

---

## Speaker Notes Tips

- Start with the customer pain point, not the technology.
- Use the transfer feature as a live demo because it is complete and tested.
- Show numbers whenever possible: fraud reduction potential, CAC savings, unused voucher waste.
- Be ready to explain why closed wallets are partners or competitors depending on the brand's size and maturity.
- End every answer by connecting back to revenue, growth, or defensibility.

