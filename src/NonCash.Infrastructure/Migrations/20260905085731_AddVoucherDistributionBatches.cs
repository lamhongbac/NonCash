using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherDistributionBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "batch_id",
                schema: "public",
                table: "voucher_distributions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "voucher_distribution_batches",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notify_channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_count = table.Column<int>(type: "integer", nullable: false),
                    distributed_count = table.Column<int>(type: "integer", nullable: false),
                    skipped_count = table.Column<int>(type: "integer", nullable: false),
                    skipped_records = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_distribution_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_distribution_batches_user_accounts_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "public",
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_distribution_batches_voucher_plan_headers_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "public",
                        principalTable: "voucher_plan_headers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_distributions_batch_id",
                schema: "public",
                table: "voucher_distributions",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_distribution_batches_brand_id",
                schema: "public",
                table: "voucher_distribution_batches",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_distribution_batches_created_by_id",
                schema: "public",
                table: "voucher_distribution_batches",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_distribution_batches_plan_id",
                schema: "public",
                table: "voucher_distribution_batches",
                column: "plan_id");

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_distributions_voucher_distribution_batches_batch_id",
                schema: "public",
                table: "voucher_distributions",
                column: "batch_id",
                principalSchema: "public",
                principalTable: "voucher_distribution_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voucher_distributions_voucher_distribution_batches_batch_id",
                schema: "public",
                table: "voucher_distributions");

            migrationBuilder.DropTable(
                name: "voucher_distribution_batches",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_voucher_distributions_batch_id",
                schema: "public",
                table: "voucher_distributions");

            migrationBuilder.DropColumn(
                name: "batch_id",
                schema: "public",
                table: "voucher_distributions");
        }
    }
}
