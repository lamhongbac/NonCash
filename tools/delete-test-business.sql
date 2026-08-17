-- ============================================================================
-- Delete a self-registered test business (brand + user + welcome credits)
-- so you can re-run registration -> approval and verify the email service.
--
-- SAFE ONLY FOR A FRESH TEST BUSINESS (no outlets, plans, vouchers, partners).
--
-- IDs for the current test cycle:
--   brand_id    = 01a00e07-5a2b-7db9-a746-c87ca44030f5
--   business_id = 01a00e07-59ac-7c39-8fc4-b26057cb7112
-- For the next cycle, refresh them with the Step 0 query below.
-- ============================================================================

-- Step 0: find the IDs of the business you just created
-- SELECT biz.id AS business_id, biz.business_name, b.id AS brand_id, b.name AS brand_name
-- FROM businesses biz
-- LEFT JOIN brands b ON b.business_id = biz.id
-- ORDER BY biz.created_at DESC
-- LIMIT 5;

BEGIN;

-- 1. Credit consumption (none on a fresh brand; kept for safety) — brand-scoped
DELETE FROM credit_consumptions         WHERE brand_id = '01a00e07-5a2b-7db9-a746-c87ca44030f5';

-- 2. Ledger entries (welcome grant) — brand-scoped
DELETE FROM credit_ledger_entries       WHERE brand_id = '01a00e07-5a2b-7db9-a746-c87ca44030f5';

-- 3. Credit batches (welcome grant) — brand-scoped; MUST go before the brand delete
DELETE FROM credit_batches              WHERE brand_id = '01a00e07-5a2b-7db9-a746-c87ca44030f5';

-- 4. Registration request (references brand AND user -> delete before user)
DELETE FROM brand_registration_requests WHERE brand_id = '01a00e07-5a2b-7db9-a746-c87ca44030f5';

-- 5. Applicant user account created by the registration
DELETE FROM user_accounts               WHERE brand_id = '01a00e07-5a2b-7db9-a746-c87ca44030f5';

-- 6. Brand
DELETE FROM brands                      WHERE id       = '01a00e07-5a2b-7db9-a746-c87ca44030f5';

-- 7. Business
DELETE FROM businesses                  WHERE id       = '01a00e07-59ac-7c39-8fc4-b26057cb7112';

COMMIT;
