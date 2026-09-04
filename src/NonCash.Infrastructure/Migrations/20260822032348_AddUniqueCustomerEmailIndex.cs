using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCustomerEmailIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail fast with a clear message if legacy data already contains duplicate emails
            // (compared case-insensitively) — the unique index cannot be created otherwise.
            migrationBuilder.Sql("""
                DO $$
                DECLARE dup_count integer;
                BEGIN
                    SELECT COUNT(*) INTO dup_count
                    FROM (
                        SELECT lower(email)
                        FROM public.customers
                        WHERE email IS NOT NULL
                        GROUP BY lower(email)
                        HAVING COUNT(*) > 1
                    ) duplicates;

                    IF dup_count > 0 THEN
                        RAISE EXCEPTION 'Cannot create unique email index: % duplicate email(s) exist in customers. Resolve them first.', dup_count;
                    END IF;
                END $$;
                """);

            // Case-insensitive unique index: email is optional (NULLs excluded),
            // but a used email cannot be shared by two customers.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_customers_email"
                    ON public.customers (lower(email))
                    WHERE email IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customers_email",
                schema: "public",
                table: "customers");
        }
    }
}
