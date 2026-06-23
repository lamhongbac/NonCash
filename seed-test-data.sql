-- =============================================================================
-- NonCash Test Data Seed Script
-- Run this in pgAdmin Query Tool after migrations are applied
-- =============================================================================

-- Use a transaction so we can rollback if anything fails
BEGIN;

-- =============================================================================
-- 1. Create a Brand
-- =============================================================================
INSERT INTO public.brands (id, created_at, name, tax_code, contact_email, status)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    NOW(),
    'Test Coffee Shop',
    'TAX-TEST-001',
    'admin@testcoffee.com',
    'Active'
)
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- 2. Create an Outlet
-- =============================================================================
INSERT INTO public.outlets (id, created_at, brand_id, name, address, status, api_key_prefix)
VALUES (
    'b0000000-0000-0000-0000-000000000001',
    NOW(),
    'a0000000-0000-0000-0000-000000000001',
    'Main Street Store',
    '123 Main Street, HCMC',
    'Active',
    'TEST'
)
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- 3. Create 2 Members (sender and recipient)
-- =============================================================================
-- Member 1: Alice (sender - has vouchers)
INSERT INTO public.customers (id, created_at, phone_number, full_name, email, status)
VALUES (
    'c0000000-0000-0000-0000-000000000001',
    NOW(),
    '0909111111',
    'Alice Sender',
    'alice@test.com',
    'Active'
)
ON CONFLICT (id) DO NOTHING;

-- Member 2: Bob (recipient - will receive transfers)
INSERT INTO public.customers (id, created_at, phone_number, full_name, email, status)
VALUES (
    'c0000000-0000-0000-0000-000000000002',
    NOW(),
    '0909222222',
    'Bob Receiver',
    'bob@test.com',
    'Active'
)
ON CONFLICT (id) DO NOTHING;

-- Member 3: Carol (another recipient)
INSERT INTO public.customers (id, created_at, phone_number, full_name, email, status)
VALUES (
    'c0000000-0000-0000-0000-000000000003',
    NOW(),
    '0909333333',
    'Carol Third',
    'carol@test.com',
    'Active'
)
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- 4. Create UserAccounts for Members (for login and JWT auth)
-- Password for all: Test@123 (bcrypt hash)
-- =============================================================================
-- Alice's user account (sender)
INSERT INTO public.user_accounts (id, created_at, brand_id, username, password_hash, full_name, role, status)
VALUES (
    'd0000000-0000-0000-0000-000000000001',
    NOW(),
    'a0000000-0000-0000-0000-000000000001',
    'alice',
    '$2a$11$K8X3v9Y5J6z7Q8mN2pL3rOQ4s5t6u7v8w9x0y1z2A3B4C5D6E7F8G', -- Test@123 (placeholder hash)
    'Alice Sender',
    'BrandManager',
    'Active'
)
ON CONFLICT (id) DO NOTHING;

-- Bob's user account (recipient)
INSERT INTO public.user_accounts (id, created_at, brand_id, username, password_hash, full_name, role, status)
VALUES (
    'd0000000-0000-0000-0000-000000000002',
    NOW(),
    'a0000000-0000-0000-0000-000000000001',
    'bob',
    '$2a$11$K8X3v9Y5J6z7Q8mN2pL3rOQ4s5t6u7v8w9x0y1z2A3B4C5D6E7F8G', -- Test@123 (placeholder hash)
    'Bob Receiver',
    'BrandManager',
    'Active'
)
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- 5. Create a Voucher Plan Header (approved)
-- =============================================================================
INSERT INTO public.voucher_plan_headers (
    id, created_at, plan_date, creator_id, brand_id, voucher_type, value_type,
    face_value, net_value, expiry_date, publish_date, valid_from, valid_to,
    target_quantity, budget, target_distributed, target_used, approval_status,
    version_number
)
VALUES (
    'e0000000-0000-0000-0000-000000000001',
    NOW(),
    NOW(),
    'd0000000-0000-0000-0000-000000000001', -- Alice as creator
    'a0000000-0000-0000-0000-000000000001',
    'Complimentary',
    'Value',
    100000.00, -- 100,000 VND face value
    100000.00, -- 100,000 VND net value
    NOW() + INTERVAL '1 year', -- expires in 1 year
    NOW(),
    NOW(),
    NOW() + INTERVAL '1 year',
    10, -- target 10 vouchers
    1000000.00, -- 1,000,000 VND budget
    0,
    0,
    'Approved',
    1
)
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- 6. Create Voucher Plan Details (vouchers distributed to Alice)
-- =============================================================================
-- Voucher 1: Given to Alice
INSERT INTO public.voucher_plan_details (
    id, created_at, parent_id, serial_no, voucher_code_secret, member_id, usage_status
)
VALUES (
    'f0000000-0000-0000-0000-000000000001',
    NOW(),
    'e0000000-0000-0000-0000-000000000001',
    'VC-TEST-2026-00000001',
    'secret-key-alice-1',
    'c0000000-0000-0000-0000-000000000001', -- Alice's customer ID
    'Pending'
)
ON CONFLICT (id) DO NOTHING;

-- Voucher 2: Given to Alice
INSERT INTO public.voucher_plan_details (
    id, created_at, parent_id, serial_no, voucher_code_secret, member_id, usage_status
)
VALUES (
    'f0000000-0000-0000-0000-000000000002',
    NOW(),
    'e0000000-0000-0000-0000-000000000001',
    'VC-TEST-2026-00000002',
    'secret-key-alice-2',
    'c0000000-0000-0000-0000-000000000001', -- Alice's customer ID
    'Pending'
)
ON CONFLICT (id) DO NOTHING;

-- Voucher 3: Given to Bob (so Bob can also test sending)
INSERT INTO public.voucher_plan_details (
    id, created_at, parent_id, serial_no, voucher_code_secret, member_id, usage_status
)
VALUES (
    'f0000000-0000-0000-0000-000000000003',
    NOW(),
    'e0000000-0000-0000-0000-000000000001',
    'VC-TEST-2026-00000003',
    'secret-key-bob-1',
    'c0000000-0000-0000-0000-000000000002', -- Bob's customer ID
    'Pending'
)
ON CONFLICT (id) DO NOTHING;

COMMIT;

-- =============================================================================
-- Verification Queries
-- =============================================================================
SELECT 'Brands' as entity, COUNT(*) as count FROM public.brands
UNION ALL
SELECT 'Outlets', COUNT(*) FROM public.outlets
UNION ALL
SELECT 'Customers', COUNT(*) FROM public.customers
UNION ALL
SELECT 'UserAccounts', COUNT(*) FROM public.user_accounts
UNION ALL
SELECT 'VoucherPlanHeaders', COUNT(*) FROM public.voucher_plan_headers
UNION ALL
SELECT 'VoucherPlanDetails', COUNT(*) FROM public.voucher_plan_details;
