using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEpic7And8Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "redeem_brand_id",
                schema: "public",
                table: "voucher_usages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sponsor_brand_id",
                schema: "public",
                table: "voucher_usages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brand_color",
                schema: "public",
                table: "voucher_plan_headers",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_image_url",
                schema: "public",
                table: "voucher_plan_headers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                schema: "public",
                table: "voucher_plan_headers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_description",
                schema: "public",
                table: "voucher_plan_headers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sponsor_brand_id",
                schema: "public",
                table: "voucher_plan_headers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terms_and_conditions",
                schema: "public",
                table: "voucher_plan_headers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "valid_days_of_week",
                schema: "public",
                table: "voucher_plan_headers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_member_id",
                schema: "public",
                table: "voucher_distributions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_usages_redeem_brand_id",
                schema: "public",
                table: "voucher_usages",
                column: "redeem_brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_usages_sponsor_brand_id",
                schema: "public",
                table: "voucher_usages",
                column: "sponsor_brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_headers_sponsor_brand_id",
                schema: "public",
                table: "voucher_plan_headers",
                column: "sponsor_brand_id");

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_plan_headers_brands_sponsor_brand_id",
                schema: "public",
                table: "voucher_plan_headers",
                column: "sponsor_brand_id",
                principalSchema: "public",
                principalTable: "brands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_usages_brands_redeem_brand_id",
                schema: "public",
                table: "voucher_usages",
                column: "redeem_brand_id",
                principalSchema: "public",
                principalTable: "brands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_usages_brands_sponsor_brand_id",
                schema: "public",
                table: "voucher_usages",
                column: "sponsor_brand_id",
                principalSchema: "public",
                principalTable: "brands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voucher_plan_headers_brands_sponsor_brand_id",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_usages_brands_redeem_brand_id",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_usages_brands_sponsor_brand_id",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropIndex(
                name: "IX_voucher_usages_redeem_brand_id",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropIndex(
                name: "IX_voucher_usages_sponsor_brand_id",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropIndex(
                name: "IX_voucher_plan_headers_sponsor_brand_id",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "redeem_brand_id",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropColumn(
                name: "sponsor_brand_id",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropColumn(
                name: "brand_color",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "cover_image_url",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "display_name",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "short_description",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "sponsor_brand_id",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "terms_and_conditions",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "valid_days_of_week",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "external_member_id",
                schema: "public",
                table: "voucher_distributions");
        }
    }
}
