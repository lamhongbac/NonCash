-- Migration: SplitWelcomePolicy (20260814050918)
-- Run this script as a PostgreSQL user that has CREATE privilege on the 'public' schema
-- (e.g. the database owner or a superuser).

START TRANSACTION;

CREATE TABLE public.welcome_grant_policies (
    id uuid NOT NULL,
    name character varying(200) NOT NULL,
    business_id uuid NOT NULL,
    welcome_credits integer NOT NULL,
    welcome_credit_expiry_months integer,
    effective_from timestamp with time zone NOT NULL,
    effective_to timestamp with time zone,
    is_active boolean NOT NULL,
    created_by uuid,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    CONSTRAINT "PK_welcome_grant_policies" PRIMARY KEY (id),
    CONSTRAINT "FK_welcome_grant_policies_businesses_business_id" FOREIGN KEY (business_id) REFERENCES public.businesses (id) ON DELETE RESTRICT
);

ALTER TABLE public.credit_batches ADD welcome_policy_id uuid;

CREATE INDEX "IX_credit_batches_welcome_policy_id" ON public.credit_batches (welcome_policy_id);

CREATE INDEX "IX_welcome_grant_policies_business_active_from" ON public.welcome_grant_policies (business_id, is_active, effective_from);

ALTER TABLE public.credit_batches ADD CONSTRAINT "FK_credit_batches_welcome_grant_policies_welcome_policy_id" FOREIGN KEY (welcome_policy_id) REFERENCES public.welcome_grant_policies (id) ON DELETE RESTRICT;

-- Seed: migrate brand-scoped welcome credits into business-scoped welcome policies.
-- scope = 2 = Brand. Global/BrandGroup welcome defaults remain in CreditConfig.
INSERT INTO welcome_grant_policies
    (id, name, business_id, welcome_credits, welcome_credit_expiry_months,
        effective_from, effective_to, is_active, created_by, created_at, updated_at)
SELECT
    gen_random_uuid(),
    'Migrated: ' || p.name,
    b.business_id,
    p.welcome_credits,
    p.welcome_credit_expiry_months,
    COALESCE(p.effective_from, p.created_at),
    p.effective_to,
    p.is_active,
    p.created_by,
    NOW(),
    NOW()
FROM credit_pricing_policies p
JOIN brands b ON b.id = p.brand_id
WHERE p.scope = 2
    AND p.welcome_credits > 0
    AND b.business_id IS NOT NULL;

ALTER TABLE public.credit_pricing_policies DROP COLUMN welcome_credit_expiry_months;

ALTER TABLE public.credit_pricing_policies DROP COLUMN welcome_credits;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260814050918_SplitWelcomePolicy', '9.0.4');

COMMIT;
