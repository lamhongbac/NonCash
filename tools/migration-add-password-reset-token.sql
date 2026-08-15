CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427071051_InitialBrand') THEN
    CREATE TABLE public.brands (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        tax_code character varying(50) NOT NULL,
        contact_email character varying(255),
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_brands" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427071051_InitialBrand') THEN
    CREATE INDEX "IX_brands_status" ON public.brands (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427071051_InitialBrand') THEN
    CREATE UNIQUE INDEX "IX_brands_tax_code" ON public.brands (tax_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427071051_InitialBrand') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260427071051_InitialBrand', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427104737_AddOutlet') THEN
    CREATE TABLE public.outlets (
        id uuid NOT NULL,
        brand_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        address text,
        status character varying(20) NOT NULL,
        api_key_prefix character varying(16),
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_outlets" PRIMARY KEY (id),
        CONSTRAINT "FK_outlets_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427104737_AddOutlet') THEN
    CREATE INDEX "IX_outlets_api_key_prefix" ON public.outlets (api_key_prefix);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427104737_AddOutlet') THEN
    CREATE INDEX "IX_outlets_brand_id" ON public.outlets (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427104737_AddOutlet') THEN
    CREATE INDEX "IX_outlets_status" ON public.outlets (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427104737_AddOutlet') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260427104737_AddOutlet', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427113444_AddCustomer') THEN
    CREATE TABLE public.customers (
        id uuid NOT NULL,
        phone_number character varying(20) NOT NULL,
        full_name character varying(200) NOT NULL,
        email character varying(255),
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_customers" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427113444_AddCustomer') THEN
    CREATE UNIQUE INDEX "IX_customers_phone_number" ON public.customers (phone_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427113444_AddCustomer') THEN
    CREATE INDEX "IX_customers_status" ON public.customers (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427113444_AddCustomer') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260427113444_AddCustomer', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427143503_AddUserAccount') THEN
    CREATE TABLE public.user_accounts (
        id uuid NOT NULL,
        brand_id uuid,
        username character varying(100) NOT NULL,
        password_hash character varying(255) NOT NULL,
        full_name character varying(200) NOT NULL,
        role character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_user_accounts" PRIMARY KEY (id),
        CONSTRAINT "FK_user_accounts_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427143503_AddUserAccount') THEN
    CREATE INDEX "IX_user_accounts_brand_id" ON public.user_accounts (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427143503_AddUserAccount') THEN
    CREATE UNIQUE INDEX "IX_user_accounts_username" ON public.user_accounts (username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427143503_AddUserAccount') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260427143503_AddUserAccount', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428034723_AddBrandRegistrationRequest') THEN
    CREATE TABLE public.brand_registration_requests (
        id uuid NOT NULL,
        brand_id uuid NOT NULL,
        submitted_by_user_id uuid NOT NULL,
        submitted_at timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        review_notes character varying(1000),
        reviewed_at timestamp with time zone,
        reviewed_by_user_id uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_brand_registration_requests" PRIMARY KEY (id),
        CONSTRAINT "FK_brand_registration_requests_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE CASCADE,
        CONSTRAINT "FK_brand_registration_requests_user_accounts_reviewed_by_user_~" FOREIGN KEY (reviewed_by_user_id) REFERENCES public.user_accounts (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_brand_registration_requests_user_accounts_submitted_by_user~" FOREIGN KEY (submitted_by_user_id) REFERENCES public.user_accounts (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428034723_AddBrandRegistrationRequest') THEN
    CREATE INDEX "IX_brand_registration_requests_brand_id" ON public.brand_registration_requests (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428034723_AddBrandRegistrationRequest') THEN
    CREATE INDEX "IX_brand_registration_requests_reviewed_by_user_id" ON public.brand_registration_requests (reviewed_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428034723_AddBrandRegistrationRequest') THEN
    CREATE INDEX "IX_brand_registration_requests_status" ON public.brand_registration_requests (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428034723_AddBrandRegistrationRequest') THEN
    CREATE INDEX "IX_brand_registration_requests_submitted_by_user_id" ON public.brand_registration_requests (submitted_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428034723_AddBrandRegistrationRequest') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260428034723_AddBrandRegistrationRequest', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    CREATE TABLE public.voucher_plan_headers (
        id uuid NOT NULL,
        plan_date timestamp with time zone NOT NULL,
        creator_id uuid NOT NULL,
        approver_id uuid,
        brand_id uuid NOT NULL,
        voucher_type character varying(20) NOT NULL,
        image_url text,
        icon_url text,
        value_type character varying(20) NOT NULL,
        face_value numeric(18,2) NOT NULL,
        net_value numeric(18,2) NOT NULL,
        expiry_date timestamp with time zone NOT NULL,
        publish_date timestamp with time zone NOT NULL,
        valid_from timestamp with time zone,
        valid_to timestamp with time zone,
        target_quantity integer NOT NULL,
        budget numeric(18,2) NOT NULL,
        target_distributed integer NOT NULL,
        target_used integer NOT NULL,
        approval_status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_voucher_plan_headers" PRIMARY KEY (id),
        CONSTRAINT "FK_voucher_plan_headers_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE CASCADE,
        CONSTRAINT "FK_voucher_plan_headers_user_accounts_approver_id" FOREIGN KEY (approver_id) REFERENCES public.user_accounts (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_voucher_plan_headers_user_accounts_creator_id" FOREIGN KEY (creator_id) REFERENCES public.user_accounts (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    CREATE TABLE public.plan_outlets (
        plan_id uuid NOT NULL,
        outlet_id uuid NOT NULL,
        CONSTRAINT "PK_plan_outlets" PRIMARY KEY (plan_id, outlet_id),
        CONSTRAINT "FK_plan_outlets_outlets_outlet_id" FOREIGN KEY (outlet_id) REFERENCES public.outlets (id) ON DELETE CASCADE,
        CONSTRAINT "FK_plan_outlets_voucher_plan_headers_plan_id" FOREIGN KEY (plan_id) REFERENCES public.voucher_plan_headers (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    CREATE INDEX "IX_plan_outlets_outlet_id" ON public.plan_outlets (outlet_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    CREATE INDEX "IX_voucher_plan_headers_approval_status" ON public.voucher_plan_headers (approval_status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    CREATE INDEX "IX_voucher_plan_headers_approver_id" ON public.voucher_plan_headers (approver_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    CREATE INDEX "IX_voucher_plan_headers_brand_id" ON public.voucher_plan_headers (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    CREATE INDEX "IX_voucher_plan_headers_creator_id" ON public.voucher_plan_headers (creator_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428090417_AddVoucherPlanHeaders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260428090417_AddVoucherPlanHeaders', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428102328_AddVoucherPlanDetails') THEN
    CREATE TABLE public.voucher_plan_details (
        id uuid NOT NULL,
        parent_id uuid NOT NULL,
        serial_no character varying(50) NOT NULL,
        voucher_code_secret character varying(255) NOT NULL,
        member_id uuid,
        usage_status character varying(20) NOT NULL,
        used_date timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_voucher_plan_details" PRIMARY KEY (id),
        CONSTRAINT "FK_voucher_plan_details_voucher_plan_headers_parent_id" FOREIGN KEY (parent_id) REFERENCES public.voucher_plan_headers (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428102328_AddVoucherPlanDetails') THEN
    CREATE INDEX "IX_voucher_plan_details_member_id" ON public.voucher_plan_details (member_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428102328_AddVoucherPlanDetails') THEN
    CREATE INDEX "IX_voucher_plan_details_parent_id" ON public.voucher_plan_details (parent_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428102328_AddVoucherPlanDetails') THEN
    CREATE UNIQUE INDEX "IX_voucher_plan_details_serial_no" ON public.voucher_plan_details (serial_no);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428102328_AddVoucherPlanDetails') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260428102328_AddVoucherPlanDetails', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515075216_AddVoucherReviews') THEN
    CREATE TABLE public.voucher_reviews (
        id uuid NOT NULL,
        plan_id uuid NOT NULL,
        approver_id uuid NOT NULL,
        review_date timestamp with time zone NOT NULL,
        review_notes text,
        decision character varying(20) NOT NULL,
        publish_date timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_voucher_reviews" PRIMARY KEY (id),
        CONSTRAINT "FK_voucher_reviews_user_accounts_approver_id" FOREIGN KEY (approver_id) REFERENCES public.user_accounts (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_voucher_reviews_voucher_plan_headers_plan_id" FOREIGN KEY (plan_id) REFERENCES public.voucher_plan_headers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515075216_AddVoucherReviews') THEN
    CREATE INDEX "IX_voucher_reviews_approver_id" ON public.voucher_reviews (approver_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515075216_AddVoucherReviews') THEN
    CREATE INDEX "IX_voucher_reviews_plan_id" ON public.voucher_reviews (plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515075216_AddVoucherReviews') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260515075216_AddVoucherReviews', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515081942_AddPlanVersioning') THEN
    ALTER TABLE public.voucher_plan_headers ADD previous_version_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515081942_AddPlanVersioning') THEN
    ALTER TABLE public.voucher_plan_headers ADD version_number integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515081942_AddPlanVersioning') THEN
    CREATE INDEX "IX_voucher_plan_headers_previous_version_id" ON public.voucher_plan_headers (previous_version_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515081942_AddPlanVersioning') THEN
    ALTER TABLE public.voucher_plan_headers ADD CONSTRAINT "FK_voucher_plan_headers_voucher_plan_headers_previous_version_~" FOREIGN KEY (previous_version_id) REFERENCES public.voucher_plan_headers (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515081942_AddPlanVersioning') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260515081942_AddPlanVersioning', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515095153_AddVoucherDistributions') THEN
    CREATE TABLE public.voucher_distributions (
        id uuid NOT NULL,
        voucher_id uuid NOT NULL,
        member_id uuid NOT NULL,
        method character varying(20) NOT NULL,
        distribution_date timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_voucher_distributions" PRIMARY KEY (id),
        CONSTRAINT "FK_voucher_distributions_customers_member_id" FOREIGN KEY (member_id) REFERENCES public.customers (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_voucher_distributions_voucher_plan_details_voucher_id" FOREIGN KEY (voucher_id) REFERENCES public.voucher_plan_details (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515095153_AddVoucherDistributions') THEN
    CREATE INDEX "IX_voucher_distributions_member_id" ON public.voucher_distributions (member_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515095153_AddVoucherDistributions') THEN
    CREATE INDEX "IX_voucher_distributions_voucher_id" ON public.voucher_distributions (voucher_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515095153_AddVoucherDistributions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260515095153_AddVoucherDistributions', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515103527_AddPurchaseOrders') THEN
    CREATE TABLE public.purchase_orders (
        id uuid NOT NULL,
        member_id uuid NOT NULL,
        status character varying(20) NOT NULL,
        invoice_company_name character varying(200),
        invoice_tax_code character varying(50),
        total_amount numeric(18,2) NOT NULL,
        paid_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_purchase_orders" PRIMARY KEY (id),
        CONSTRAINT "FK_purchase_orders_customers_member_id" FOREIGN KEY (member_id) REFERENCES public.customers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515103527_AddPurchaseOrders') THEN
    CREATE TABLE public.order_details (
        id uuid NOT NULL,
        order_id uuid NOT NULL,
        plan_id uuid NOT NULL,
        quantity integer NOT NULL,
        unit_price numeric(18,2) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_order_details" PRIMARY KEY (id),
        CONSTRAINT "FK_order_details_purchase_orders_order_id" FOREIGN KEY (order_id) REFERENCES public.purchase_orders (id) ON DELETE CASCADE,
        CONSTRAINT "FK_order_details_voucher_plan_headers_plan_id" FOREIGN KEY (plan_id) REFERENCES public.voucher_plan_headers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515103527_AddPurchaseOrders') THEN
    CREATE INDEX "IX_order_details_order_id" ON public.order_details (order_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515103527_AddPurchaseOrders') THEN
    CREATE INDEX "IX_order_details_plan_id" ON public.order_details (plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515103527_AddPurchaseOrders') THEN
    CREATE INDEX "IX_purchase_orders_member_id" ON public.purchase_orders (member_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515103527_AddPurchaseOrders') THEN
    CREATE INDEX "IX_purchase_orders_status" ON public.purchase_orders (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515103527_AddPurchaseOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260515103527_AddPurchaseOrders', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516044105_AddVoucherLockColumns') THEN
    ALTER TABLE public.voucher_plan_details ADD bill_number character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516044105_AddVoucherLockColumns') THEN
    ALTER TABLE public.voucher_plan_details ADD lock_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516044105_AddVoucherLockColumns') THEN
    ALTER TABLE public.voucher_plan_details ADD locked_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516044105_AddVoucherLockColumns') THEN
    ALTER TABLE public.voucher_plan_details ADD locked_outlet_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516044105_AddVoucherLockColumns') THEN
    CREATE INDEX "IX_voucher_plan_details_lock_id" ON public.voucher_plan_details (lock_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516044105_AddVoucherLockColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260516044105_AddVoucherLockColumns', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516045046_AddVoucherUsages') THEN
    CREATE TABLE public.voucher_usages (
        id uuid NOT NULL,
        voucher_id uuid NOT NULL,
        pos_id uuid NOT NULL,
        transaction_id character varying(100) NOT NULL,
        usage_date timestamp with time zone NOT NULL,
        amount_used numeric(18,2) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_voucher_usages" PRIMARY KEY (id),
        CONSTRAINT "FK_voucher_usages_voucher_plan_details_voucher_id" FOREIGN KEY (voucher_id) REFERENCES public.voucher_plan_details (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516045046_AddVoucherUsages') THEN
    CREATE INDEX "IX_voucher_usages_pos_id" ON public.voucher_usages (pos_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516045046_AddVoucherUsages') THEN
    CREATE UNIQUE INDEX "IX_voucher_usages_transaction_id" ON public.voucher_usages (transaction_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516045046_AddVoucherUsages') THEN
    CREATE INDEX "IX_voucher_usages_voucher_id" ON public.voucher_usages (voucher_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516045046_AddVoucherUsages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260516045046_AddVoucherUsages', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    ALTER TABLE public.voucher_plan_details ADD transfer_lock_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    ALTER TABLE public.voucher_plan_details ADD transfer_locked_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    CREATE TABLE public.voucher_transfers (
        id uuid NOT NULL,
        sender_id uuid NOT NULL,
        recipient_id uuid NOT NULL,
        voucher_id uuid NOT NULL,
        status character varying(30) NOT NULL,
        transfer_type character varying(20) NOT NULL,
        initiated_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        note character varying(500),
        reject_reason character varying(500),
        responded_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_voucher_transfers" PRIMARY KEY (id),
        CONSTRAINT "FK_voucher_transfers_customers_recipient_id" FOREIGN KEY (recipient_id) REFERENCES public.customers (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_voucher_transfers_customers_sender_id" FOREIGN KEY (sender_id) REFERENCES public.customers (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_voucher_transfers_voucher_plan_details_voucher_id" FOREIGN KEY (voucher_id) REFERENCES public.voucher_plan_details (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    CREATE INDEX "IX_voucher_plan_details_transfer_lock_id" ON public.voucher_plan_details (transfer_lock_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    CREATE INDEX "IX_voucher_transfers_expires_at" ON public.voucher_transfers (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    CREATE INDEX "IX_voucher_transfers_recipient_id" ON public.voucher_transfers (recipient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    CREATE INDEX "IX_voucher_transfers_sender_id" ON public.voucher_transfers (sender_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    CREATE INDEX "IX_voucher_transfers_status" ON public.voucher_transfers (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    CREATE INDEX "IX_voucher_transfers_voucher_id" ON public.voucher_transfers (voucher_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623030118_AddVoucherTransfers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260623030118_AddVoucherTransfers', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623050142_FixVoucherTransferFkToUserAccounts') THEN
    ALTER TABLE public.voucher_transfers DROP CONSTRAINT "FK_voucher_transfers_customers_recipient_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623050142_FixVoucherTransferFkToUserAccounts') THEN
    ALTER TABLE public.voucher_transfers DROP CONSTRAINT "FK_voucher_transfers_customers_sender_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623050142_FixVoucherTransferFkToUserAccounts') THEN
    ALTER TABLE public.voucher_transfers ADD CONSTRAINT "FK_voucher_transfers_user_accounts_recipient_id" FOREIGN KEY (recipient_id) REFERENCES public.user_accounts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623050142_FixVoucherTransferFkToUserAccounts') THEN
    ALTER TABLE public.voucher_transfers ADD CONSTRAINT "FK_voucher_transfers_user_accounts_sender_id" FOREIGN KEY (sender_id) REFERENCES public.user_accounts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623050142_FixVoucherTransferFkToUserAccounts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260623050142_FixVoucherTransferFkToUserAccounts', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623062018_AddCustomerIdToUserAccount') THEN
    ALTER TABLE public.user_accounts ADD customer_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623062018_AddCustomerIdToUserAccount') THEN
    CREATE INDEX "IX_user_accounts_customer_id" ON public.user_accounts (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623062018_AddCustomerIdToUserAccount') THEN
    ALTER TABLE public.user_accounts ADD CONSTRAINT "FK_user_accounts_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES public.customers (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260623062018_AddCustomerIdToUserAccount') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260623062018_AddCustomerIdToUserAccount', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.purchase_orders DROP CONSTRAINT "FK_purchase_orders_customers_member_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.user_accounts DROP CONSTRAINT "FK_user_accounts_customers_customer_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.voucher_distributions DROP CONSTRAINT "FK_voucher_distributions_customers_member_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.voucher_transfers DROP CONSTRAINT "FK_voucher_transfers_user_accounts_recipient_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.voucher_transfers DROP CONSTRAINT "FK_voucher_transfers_user_accounts_sender_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    CREATE TABLE public.member_accounts (
        id uuid NOT NULL,
        customer_id uuid NOT NULL,
        username character varying(100) NOT NULL,
        password_hash character varying(255) NOT NULL,
        full_name character varying(200) NOT NULL,
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_member_accounts" PRIMARY KEY (id),
        CONSTRAINT "FK_member_accounts_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES public.customers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    CREATE UNIQUE INDEX "IX_member_accounts_customer_id" ON public.member_accounts (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    CREATE UNIQUE INDEX "IX_member_accounts_username" ON public.member_accounts (username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    INSERT INTO member_accounts (id, customer_id, username, password_hash, full_name, status, created_at, updated_at)
                    SELECT id, customer_id, username, password_hash, full_name, status, created_at, updated_at
                    FROM user_accounts
                    WHERE role = 'Member';
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    INSERT INTO member_accounts (id, customer_id, username, password_hash, full_name, status, created_at, updated_at)
                    SELECT gen_random_uuid(), c.id, c.phone_number || '_' || c.id::text, '', c.full_name, 'Active', NOW(), NOW()
                    FROM customers c
                    WHERE c.id IN (
                        SELECT member_id FROM voucher_plan_details WHERE member_id IS NOT NULL
                        UNION
                        SELECT member_id FROM voucher_distributions WHERE member_id IS NOT NULL
                        UNION
                        SELECT member_id FROM purchase_orders WHERE member_id IS NOT NULL
                    )
                    AND c.id NOT IN (SELECT customer_id FROM member_accounts);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE voucher_plan_details vpd
                    SET member_id = ma.id
                    FROM member_accounts ma
                    WHERE vpd.member_id = ma.customer_id;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE voucher_distributions vd
                    SET member_id = ma.id
                    FROM member_accounts ma
                    WHERE vd.member_id = ma.customer_id;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE purchase_orders po
                    SET member_id = ma.id
                    FROM member_accounts ma
                    WHERE po.member_id = ma.customer_id;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    DELETE FROM user_accounts WHERE role = 'Member';
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    DROP INDEX public."IX_user_accounts_customer_id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.user_accounts DROP COLUMN customer_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE voucher_plan_details SET member_id = NULL WHERE member_id NOT IN (SELECT id FROM member_accounts);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE voucher_distributions SET member_id = NULL WHERE member_id NOT IN (SELECT id FROM member_accounts);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE purchase_orders SET member_id = NULL WHERE member_id NOT IN (SELECT id FROM member_accounts);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE voucher_transfers SET sender_id = NULL WHERE sender_id NOT IN (SELECT id FROM member_accounts);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN

                    UPDATE voucher_transfers SET recipient_id = NULL WHERE recipient_id NOT IN (SELECT id FROM member_accounts);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.purchase_orders ADD CONSTRAINT "FK_purchase_orders_member_accounts_member_id" FOREIGN KEY (member_id) REFERENCES public.member_accounts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.voucher_distributions ADD CONSTRAINT "FK_voucher_distributions_member_accounts_member_id" FOREIGN KEY (member_id) REFERENCES public.member_accounts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.voucher_plan_details ADD CONSTRAINT "FK_voucher_plan_details_member_accounts_member_id" FOREIGN KEY (member_id) REFERENCES public.member_accounts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.voucher_transfers ADD CONSTRAINT "FK_voucher_transfers_member_accounts_recipient_id" FOREIGN KEY (recipient_id) REFERENCES public.member_accounts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    ALTER TABLE public.voucher_transfers ADD CONSTRAINT "FK_voucher_transfers_member_accounts_sender_id" FOREIGN KEY (sender_id) REFERENCES public.member_accounts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260624121015_SplitMemberIdentity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260624121015_SplitMemberIdentity', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085600_AddPaymentTransactions') THEN
    CREATE TABLE public.payment_transactions (
        id uuid NOT NULL,
        purchase_order_id uuid NOT NULL,
        gateway character varying(50) NOT NULL,
        gateway_transaction_id character varying(100) NOT NULL,
        amount numeric(18,2) NOT NULL,
        currency character varying(10) NOT NULL,
        status character varying(20) NOT NULL,
        request_payload text,
        response_payload text,
        webhook_payload text,
        gateway_response_code character varying(50),
        completed_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_payment_transactions" PRIMARY KEY (id),
        CONSTRAINT "FK_payment_transactions_purchase_orders_purchase_order_id" FOREIGN KEY (purchase_order_id) REFERENCES public.purchase_orders (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085600_AddPaymentTransactions') THEN
    CREATE INDEX "IX_payment_transactions_gateway_transaction_id" ON public.payment_transactions (gateway_transaction_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085600_AddPaymentTransactions') THEN
    CREATE INDEX "IX_payment_transactions_purchase_order_id" ON public.payment_transactions (purchase_order_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085600_AddPaymentTransactions') THEN
    CREATE INDEX "IX_payment_transactions_status" ON public.payment_transactions (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085600_AddPaymentTransactions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260626085600_AddPaymentTransactions', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN
    ALTER TABLE public.brands ADD business_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN
    CREATE TABLE public.businesses (
        id uuid NOT NULL,
        business_name character varying(200) NOT NULL,
        tax_code character varying(50) NOT NULL,
        address character varying(500) NOT NULL,
        contact_email character varying(255),
        phone_number character varying(50),
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_businesses" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN

                    INSERT INTO public.businesses (id, business_name, tax_code, address, contact_email, phone_number, is_active, created_at, updated_at)
                    SELECT (md5(random()::text || clock_timestamp()::text)::uuid), name, tax_code, '', contact_email, '', true, now(), NULL
                    FROM public.brands;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN

                    UPDATE public.brands b
                    SET business_id = bu.id
                    FROM public.businesses bu
                    WHERE bu.tax_code = b.tax_code;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN
    ALTER TABLE public.brands ALTER COLUMN business_id SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN
    CREATE INDEX "IX_brands_business_id" ON public.brands (business_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN
    CREATE UNIQUE INDEX "IX_businesses_tax_code" ON public.businesses (tax_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN
    ALTER TABLE public.brands ADD CONSTRAINT "FK_brands_businesses_business_id" FOREIGN KEY (business_id) REFERENCES public.businesses (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706072057_AddBusinessEntity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260706072057_AddBusinessEntity', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_usages ADD redeem_brand_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_usages ADD sponsor_brand_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD brand_color character varying(7);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD cover_image_url text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD display_name character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD short_description character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD sponsor_brand_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD terms_and_conditions text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD valid_days_of_week character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_distributions ADD external_member_id character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    CREATE INDEX "IX_voucher_usages_redeem_brand_id" ON public.voucher_usages (redeem_brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    CREATE INDEX "IX_voucher_usages_sponsor_brand_id" ON public.voucher_usages (sponsor_brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    CREATE INDEX "IX_voucher_plan_headers_sponsor_brand_id" ON public.voucher_plan_headers (sponsor_brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_plan_headers ADD CONSTRAINT "FK_voucher_plan_headers_brands_sponsor_brand_id" FOREIGN KEY (sponsor_brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_usages ADD CONSTRAINT "FK_voucher_usages_brands_redeem_brand_id" FOREIGN KEY (redeem_brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    ALTER TABLE public.voucher_usages ADD CONSTRAINT "FK_voucher_usages_brands_sponsor_brand_id" FOREIGN KEY (sponsor_brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727134213_AddEpic7And8Schema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727134213_AddEpic7And8Schema', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE TABLE public.settlement_entries (
        id uuid NOT NULL,
        sponsor_brand_id uuid,
        issuing_brand_id uuid NOT NULL,
        redeem_brand_id uuid,
        redeem_outlet_id uuid,
        voucher_usage_id uuid NOT NULL,
        face_value numeric(18,2) NOT NULL,
        status integer NOT NULL,
        settled_at timestamp with time zone,
        settled_by uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_settlement_entries" PRIMARY KEY (id),
        CONSTRAINT "FK_settlement_entries_brands_issuing_brand_id" FOREIGN KEY (issuing_brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_settlement_entries_brands_redeem_brand_id" FOREIGN KEY (redeem_brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_settlement_entries_brands_sponsor_brand_id" FOREIGN KEY (sponsor_brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_settlement_entries_outlets_redeem_outlet_id" FOREIGN KEY (redeem_outlet_id) REFERENCES public.outlets (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_settlement_entries_voucher_usages_voucher_usage_id" FOREIGN KEY (voucher_usage_id) REFERENCES public.voucher_usages (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE INDEX "IX_settlement_entries_created_at" ON public.settlement_entries (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE INDEX "IX_settlement_entries_issuing_brand_id" ON public.settlement_entries (issuing_brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE INDEX "IX_settlement_entries_redeem_brand_id" ON public.settlement_entries (redeem_brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE INDEX "IX_settlement_entries_redeem_outlet_id" ON public.settlement_entries (redeem_outlet_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE INDEX "IX_settlement_entries_sponsor_brand_id" ON public.settlement_entries (sponsor_brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE INDEX "IX_settlement_entries_status" ON public.settlement_entries (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    CREATE UNIQUE INDEX "IX_settlement_entries_voucher_usage_id" ON public.settlement_entries (voucher_usage_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727135443_AddSettlementEntries') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727135443_AddSettlementEntries', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE TABLE public.integration_partners (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        contact_email character varying(200) NOT NULL,
        callback_url character varying(500) NOT NULL,
        api_key_prefix character varying(16) NOT NULL,
        api_key_hash character varying(200) NOT NULL,
        webhook_secret character varying(200) NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_integration_partners" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE TABLE public.voucher_events (
        id uuid NOT NULL,
        event_type character varying(100) NOT NULL,
        voucher_id uuid,
        member_phone character varying(20),
        brand_id uuid,
        payload_json text NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_voucher_events" PRIMARY KEY (id),
        CONSTRAINT "FK_voucher_events_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_voucher_events_voucher_plan_details_voucher_id" FOREIGN KEY (voucher_id) REFERENCES public.voucher_plan_details (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE TABLE public.partner_brands (
        partner_id uuid NOT NULL,
        brand_id uuid NOT NULL,
        CONSTRAINT "PK_partner_brands" PRIMARY KEY (partner_id, brand_id),
        CONSTRAINT "FK_partner_brands_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE CASCADE,
        CONSTRAINT "FK_partner_brands_integration_partners_partner_id" FOREIGN KEY (partner_id) REFERENCES public.integration_partners (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE TABLE public.webhook_deliveries (
        id uuid NOT NULL,
        partner_id uuid NOT NULL,
        event_id uuid NOT NULL,
        http_status integer,
        retry_count integer NOT NULL,
        delivered_at timestamp with time zone,
        next_retry_at timestamp with time zone,
        last_error character varying(1000),
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_webhook_deliveries" PRIMARY KEY (id),
        CONSTRAINT "FK_webhook_deliveries_integration_partners_partner_id" FOREIGN KEY (partner_id) REFERENCES public.integration_partners (id) ON DELETE CASCADE,
        CONSTRAINT "FK_webhook_deliveries_voucher_events_event_id" FOREIGN KEY (event_id) REFERENCES public.voucher_events (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE UNIQUE INDEX "IX_integration_partners_api_key_prefix" ON public.integration_partners (api_key_prefix);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_integration_partners_name" ON public.integration_partners (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_partner_brands_brand_id" ON public.partner_brands (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_voucher_events_brand_id" ON public.voucher_events (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_voucher_events_created_at" ON public.voucher_events (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_voucher_events_event_type" ON public.voucher_events (event_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_voucher_events_member_phone" ON public.voucher_events (member_phone);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_voucher_events_voucher_id" ON public.voucher_events (voucher_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_webhook_deliveries_delivered_at" ON public.webhook_deliveries (delivered_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE UNIQUE INDEX "IX_webhook_deliveries_event_partner" ON public.webhook_deliveries (event_id, partner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_webhook_deliveries_next_retry_at" ON public.webhook_deliveries (next_retry_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    CREATE INDEX "IX_webhook_deliveries_partner_id" ON public.webhook_deliveries (partner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727140551_AddIntegrationPartnerAndWebhooks') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727140551_AddIntegrationPartnerAndWebhooks', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728092248_AddCreditLedger') THEN
    CREATE TABLE public.credit_ledger_entries (
        id uuid NOT NULL,
        brand_id uuid NOT NULL,
        entry_type integer NOT NULL,
        amount integer NOT NULL,
        reference character varying(500),
        voucher_detail_id uuid,
        created_by uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_credit_ledger_entries" PRIMARY KEY (id),
        CONSTRAINT "FK_credit_ledger_entries_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728092248_AddCreditLedger') THEN
    CREATE INDEX "IX_credit_ledger_entries_brand_id_created_at" ON public.credit_ledger_entries (brand_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728092248_AddCreditLedger') THEN
    CREATE UNIQUE INDEX "IX_credit_ledger_entries_voucher_detail_id" ON public.credit_ledger_entries (voucher_detail_id) WHERE voucher_detail_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728092248_AddCreditLedger') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260728092248_AddCreditLedger', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE TABLE public.brand_groups (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        description character varying(1000),
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_brand_groups" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE TABLE public.brand_group_members (
        id uuid NOT NULL,
        brand_group_id uuid NOT NULL,
        brand_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_brand_group_members" PRIMARY KEY (id),
        CONSTRAINT "FK_brand_group_members_brand_groups_brand_group_id" FOREIGN KEY (brand_group_id) REFERENCES public.brand_groups (id) ON DELETE CASCADE,
        CONSTRAINT "FK_brand_group_members_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE TABLE public.credit_pricing_policies (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        scope integer NOT NULL,
        brand_group_id uuid,
        brand_id uuid,
        price_per_credit_vnd numeric(18,2) NOT NULL,
        credit_expiry_months integer,
        welcome_credits integer NOT NULL,
        welcome_credit_expiry_months integer,
        low_balance_warning_pct integer,
        expiry_warning_days integer,
        adjustment_approval_threshold integer,
        effective_from timestamp with time zone NOT NULL,
        effective_to timestamp with time zone,
        is_active boolean NOT NULL,
        created_by uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_credit_pricing_policies" PRIMARY KEY (id),
        CONSTRAINT "FK_credit_pricing_policies_brand_groups_brand_group_id" FOREIGN KEY (brand_group_id) REFERENCES public.brand_groups (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_credit_pricing_policies_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE TABLE public.credit_adjustment_requests (
        id uuid NOT NULL,
        brand_id uuid NOT NULL,
        adjustment_type integer NOT NULL,
        amount integer NOT NULL,
        related_batch_id uuid,
        reason_text character varying(1000) NOT NULL,
        evidence_note character varying(1000),
        evidence_image_url character varying(1000),
        status integer NOT NULL,
        requires_approval boolean NOT NULL,
        approval_threshold integer,
        policy_id uuid,
        requested_by uuid NOT NULL,
        requested_at timestamp with time zone NOT NULL,
        reviewed_by uuid,
        reviewed_at timestamp with time zone,
        review_note character varying(1000),
        applied_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_credit_adjustment_requests" PRIMARY KEY (id),
        CONSTRAINT "FK_credit_adjustment_requests_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_credit_adjustment_requests_credit_pricing_policies_policy_id" FOREIGN KEY (policy_id) REFERENCES public.credit_pricing_policies (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE TABLE public.credit_batches (
        id uuid NOT NULL,
        brand_id uuid NOT NULL,
        policy_id uuid,
        batch_type integer NOT NULL,
        original_amount integer NOT NULL,
        remaining_amount integer NOT NULL,
        price_per_credit_vnd numeric(18,2) NOT NULL,
        total_paid_vnd numeric(18,2) NOT NULL,
        expires_at timestamp with time zone,
        evidence_image_url character varying(1000),
        reference character varying(500),
        adjustment_request_id uuid,
        created_by uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_credit_batches" PRIMARY KEY (id),
        CONSTRAINT "FK_credit_batches_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_credit_batches_credit_adjustment_requests_adjustment_reques~" FOREIGN KEY (adjustment_request_id) REFERENCES public.credit_adjustment_requests (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_credit_batches_credit_pricing_policies_policy_id" FOREIGN KEY (policy_id) REFERENCES public.credit_pricing_policies (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE TABLE public.credit_consumptions (
        id uuid NOT NULL,
        batch_id uuid NOT NULL,
        brand_id uuid NOT NULL,
        voucher_detail_id uuid NOT NULL,
        reference character varying(500),
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_credit_consumptions" PRIMARY KEY (id),
        CONSTRAINT "FK_credit_consumptions_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_credit_consumptions_credit_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES public.credit_batches (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE TABLE public.credit_expiry_logs (
        id uuid NOT NULL,
        batch_id uuid NOT NULL,
        brand_id uuid NOT NULL,
        expired_credits integer NOT NULL,
        expired_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_credit_expiry_logs" PRIMARY KEY (id),
        CONSTRAINT "FK_credit_expiry_logs_brands_brand_id" FOREIGN KEY (brand_id) REFERENCES public.brands (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_credit_expiry_logs_credit_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES public.credit_batches (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_brand_group_members_brand_id" ON public.brand_group_members (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE UNIQUE INDEX "IX_brand_group_members_group_brand" ON public.brand_group_members (brand_group_id, brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE UNIQUE INDEX "IX_brand_groups_name" ON public.brand_groups (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_adjustment_requests_brand_id_created_at" ON public.credit_adjustment_requests (brand_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_adjustment_requests_policy_id" ON public.credit_adjustment_requests (policy_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_adjustment_requests_related_batch_id" ON public.credit_adjustment_requests (related_batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_adjustment_requests_status_requested_at" ON public.credit_adjustment_requests (status, requested_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_batches_adjustment_request_id" ON public.credit_batches (adjustment_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_batches_brand_id_created_at" ON public.credit_batches (brand_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_batches_brand_id_expires_at" ON public.credit_batches (brand_id, expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_batches_policy_id" ON public.credit_batches (policy_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_consumptions_batch_id" ON public.credit_consumptions (batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_consumptions_brand_id_created_at" ON public.credit_consumptions (brand_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE UNIQUE INDEX "IX_credit_consumptions_voucher_detail_id" ON public.credit_consumptions (voucher_detail_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE UNIQUE INDEX "IX_credit_expiry_logs_batch_id" ON public.credit_expiry_logs (batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_expiry_logs_brand_id" ON public.credit_expiry_logs (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_pricing_policies_brand_group_id" ON public.credit_pricing_policies (brand_group_id) WHERE brand_group_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_pricing_policies_brand_id" ON public.credit_pricing_policies (brand_id) WHERE brand_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    CREATE INDEX "IX_credit_pricing_policies_scope_active_from" ON public.credit_pricing_policies (scope, is_active, effective_from);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    ALTER TABLE public.credit_adjustment_requests ADD CONSTRAINT "FK_credit_adjustment_requests_credit_batches_related_batch_id" FOREIGN KEY (related_batch_id) REFERENCES public.credit_batches (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729063438_AddCreditPolicyBatchAdjustment') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260729063438_AddCreditPolicyBatchAdjustment', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729065824_AddCreditBatchExpiryWarningSentAt') THEN
    ALTER TABLE public.credit_batches ADD expiry_warning_sent_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729065824_AddCreditBatchExpiryWarningSentAt') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260729065824_AddCreditBatchExpiryWarningSentAt', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
    ALTER TABLE public.credit_batches ADD welcome_policy_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
    CREATE INDEX "IX_credit_batches_welcome_policy_id" ON public.credit_batches (welcome_policy_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
    CREATE INDEX "IX_welcome_grant_policies_business_active_from" ON public.welcome_grant_policies (business_id, is_active, effective_from);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
    ALTER TABLE public.credit_batches ADD CONSTRAINT "FK_credit_batches_welcome_grant_policies_welcome_policy_id" FOREIGN KEY (welcome_policy_id) REFERENCES public.welcome_grant_policies (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN

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
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
    ALTER TABLE public.credit_pricing_policies DROP COLUMN welcome_credit_expiry_months;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
    ALTER TABLE public.credit_pricing_policies DROP COLUMN welcome_credits;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814050918_SplitWelcomePolicy') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814050918_SplitWelcomePolicy', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814090258_AddEmailToUserAccount') THEN
    ALTER TABLE public.user_accounts ADD email character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814090258_AddEmailToUserAccount') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814090258_AddEmailToUserAccount', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814110418_AddEmailLog') THEN
    CREATE TABLE public.email_logs (
        id uuid NOT NULL,
        to_address character varying(255) NOT NULL,
        subject character varying(500) NOT NULL,
        template_name character varying(100) NOT NULL,
        notification_type character varying(100) NOT NULL,
        related_entity_id uuid,
        success boolean NOT NULL,
        error_message character varying(2000),
        retry_count integer NOT NULL,
        sent_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_email_logs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814110418_AddEmailLog') THEN
    CREATE INDEX "IX_email_logs_notification_type" ON public.email_logs (notification_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814110418_AddEmailLog') THEN
    CREATE INDEX "IX_email_logs_sent_at" ON public.email_logs (sent_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814110418_AddEmailLog') THEN
    CREATE INDEX "IX_email_logs_success" ON public.email_logs (success);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814110418_AddEmailLog') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814110418_AddEmailLog', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814114913_AddPasswordResetToken') THEN
    ALTER TABLE public.user_accounts ADD password_reset_token character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814114913_AddPasswordResetToken') THEN
    ALTER TABLE public.user_accounts ADD password_reset_token_expiry timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814114913_AddPasswordResetToken') THEN
    CREATE INDEX "IX_user_accounts_password_reset_token" ON public.user_accounts (password_reset_token) WHERE password_reset_token IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814114913_AddPasswordResetToken') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814114913_AddPasswordResetToken', '9.0.4');
    END IF;
END $EF$;
COMMIT;

