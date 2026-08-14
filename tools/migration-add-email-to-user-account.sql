START TRANSACTION;

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
COMMIT;

