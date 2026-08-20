using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BusinessRegistrationRequestFirstBrandInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "submitted_by_user_id",
                schema: "public",
                table: "business_registration_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "brand_id",
                schema: "public",
                table: "business_registration_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_id",
                schema: "public",
                table: "business_registration_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_name",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_brand_name",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manager_password_hash",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manager_username",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "representative_name",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Backfill business/brand/user information for requests created under the old model.
            migrationBuilder.Sql(@"
                WITH backfill_data AS (
                    SELECT r.id AS request_id,
                           b.business_name,
                           b.tax_code,
                           b.address,
                           b.contact_email,
                           b.phone_number,
                           b.id AS business_uuid,
                           br.name AS brand_name,
                           u.full_name,
                           u.username,
                           u.password_hash
                    FROM public.business_registration_requests r
                    JOIN public.brands br ON br.id = r.brand_id
                    LEFT JOIN public.businesses b ON b.id = br.business_id
                    LEFT JOIN public.user_accounts u ON u.id = r.submitted_by_user_id
                    WHERE r.business_name IS NULL OR r.business_name = ''
                )
                UPDATE public.business_registration_requests r
                SET business_name = COALESCE(d.business_name, ''),
                    tax_code = COALESCE(d.tax_code, ''),
                    address = d.address,
                    contact_email = d.contact_email,
                    phone_number = d.phone_number,
                    representative_name = COALESCE(d.full_name, ''),
                    first_brand_name = d.brand_name,
                    manager_username = d.username,
                    manager_password_hash = d.password_hash,
                    business_id = d.business_uuid
                FROM backfill_data d
                WHERE r.id = d.request_id;
            ");

            // Make required columns non-nullable.
            migrationBuilder.AlterColumn<string>(
                name: "business_name",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "representative_name",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "tax_code",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_registration_requests_business_id",
                schema: "public",
                table: "business_registration_requests",
                column: "business_id");

            migrationBuilder.AddForeignKey(
                name: "FK_business_registration_requests_businesses_business_id",
                schema: "public",
                table: "business_registration_requests",
                column: "business_id",
                principalSchema: "public",
                principalTable: "businesses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_business_registration_requests_businesses_business_id",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropIndex(
                name: "IX_business_registration_requests_business_id",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "address",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "business_id",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "business_name",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "contact_email",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "first_brand_name",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "manager_password_hash",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "manager_username",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "phone_number",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "representative_name",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "tax_code",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "submitted_by_user_id",
                schema: "public",
                table: "business_registration_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "brand_id",
                schema: "public",
                table: "business_registration_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
