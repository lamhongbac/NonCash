# Giga Mall Partnership Discussion Summary

**Date:** June 2026  
**Topic:** Go-to-market strategy for deploying NonCash inside Giga Mall  
**Objective:** Persuade Giga Mall leadership to approve a pilot of the NonCash voucher platform across selected outlets.

---

## 1. Context

The discussion participant is currently a loyalty solution provider for Giga Mall (operating the **Giga Mall App**) and wants to extend the NonCash voucher/loyalty platform to outlets inside the mall. The core challenge is to build a partnership proposal that is compelling from Giga Mall’s perspective and aligned with the mall’s real business goals.

---

## 2. Key Questions Discussed

### Q1: Should the customer portal be separated from the admin web app?

**Conclusion:** Keep the member portal inside `NonCash.Web` for now, but architect it so it can be split later if traffic, team structure, or security requirements demand it. The current unified Blazor app is the fastest path for an MVP/founder stage, while the API remains the long-term boundary for any future separate portal.

### Q2: Why should Giga Mall agree to deploy NonCash?

**Conclusion:** The pitch must start from the mall’s primary KPI: **tenant business effectiveness**. Empty or struggling outlets do not renew leases, which threatens the mall’s main profit. NonCash should be positioned as a tool that helps tenants sell more, which in turn protects and grows rental income.

### Q3: How should the existing Giga Mall App be used?

**Conclusion:** Giga Mall App should not be replaced. It is a strategic asset that already owns the shopper relationship and member data. NonCash should be positioned as the **native monetization and activation layer** inside that app — turning existing data into targeted vouchers, traffic, and measurable tenant revenue.

### Q4: What is the right cooperation / commercial model?

**Conclusion:** A **pilot-first, redemption-based revenue share** is the most attractive and low-risk model for Giga Mall:

- No upfront setup or license fees during the pilot.
- Commission is applied only when vouchers are redeemed at tenant POS.
- Giga Mall keeps the majority of the commission (suggested 60–70%).
- NonCash earns a smaller share (suggested 30–40%).
- Optionally, commission can be applied only to incremental tenant sales vs. baseline.

### Q5: Should NonCash offer promotion support and a profit share?

**Conclusion:** Yes, but with clear boundaries:

- NonCash should co-design the first 3 campaigns and provide templates, targeting recommendations, and analytics.
- Giga Mall owns the channels: app push notifications, in-mall signage, social media, and tenant coordination.
- Promotion support is time-bound to the pilot. Ongoing marketing execution remains Giga Mall’s responsibility.
- The profit/revenue share should be tied to redemptions, not software fees.

---

## 3. Strategic Framing Agreed

The final proposal uses a **native-first, tenant-outcome framing**:

> **Giga Mall Voucher Powered by NonCash**
>
> Giga Mall App brought the customer. NonCash brings the transaction. Together, we make tenants more profitable — and that is the best guarantee of lease renewals and rental income growth for Giga Mall.

Key messages:
- The product is native to Giga Mall App, not an external coupon marketplace.
- Data stays inside the Giga Mall ecosystem.
- Revenue is earned only when real shopper value is created.
- Tenant success is the shared objective.

---

## 4. Proposed Pilot

| Element | Detail |
|---|---|
| **Duration** | 8–10 weeks |
| **Tenants** | 3 anchor outlets: F&B, fashion, service/beauty |
| **Campaigns** | 3 joint campaigns co-designed by Giga Mall and NonCash |
| **Setup fee** | Zero during pilot |
| **Revenue share** | 10% commission on redeemed voucher face value; 60–70% to Giga Mall |
| **Promotion** | NonCash supports first 3 campaigns; Giga Mall owns channels |
| **Data ownership** | Giga Mall owns shopper and transaction data |

Target metrics:
- Voucher redemption rate > 20%
- Incremental tenant sales +15–25% vs. baseline
- 500+ cross-tenant visits per month
- 1,000+ new Giga Mall App activations
- Strong tenant satisfaction / intent to renew

---

## 5. Deliverables Created

The following files were produced during this discussion:

1. **Partnership Proposal**  
   `docs/proposals/giga-mall-voucher-partnership-proposal.md`  
   Full one-page/multi-section proposal with commercial model, pilot plan, roles, and next steps.

2. **Pitch Deck**  
   `docs/proposals/giga-mall-pitch-deck.md`  
   Slide-by-slide deck for presenting to Giga Mall leadership.

3. **Discussion Summary**  
   `docs/proposals/giga-mall-discussion-summary.md`  
   This file.

---

## 6. Recommended Next Steps

1. Review and localize the proposal with Giga Mall-specific tenant names, voucher values, and branding.
2. Prepare a live demo of the NonCash member portal, store, and POS redemption flow.
3. Identify the internal champion at Giga Mall (ideally Marketing / CRM Director).
4. Secure verbal commitment from 1–2 anchor tenants before the leadership meeting.
5. Create a draft pilot MoU once Giga Mall expresses interest.

---

## 7. Open Items

- Finalize exact commission split (e.g., 70/30 vs. 60/40).
- Confirm whether commission applies to all redemptions or only incremental sales.
- Determine which POS systems the pilot tenants use and integration complexity.
- Decide whether NonCash will charge a monthly platform fee after the pilot.

---

*Summary prepared for internal reference and follow-up actions.*
