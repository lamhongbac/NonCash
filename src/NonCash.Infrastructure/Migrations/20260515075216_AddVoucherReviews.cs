using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voucher_reviews",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publish_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_reviews_user_accounts_approver_id",
                        column: x => x.approver_id,
                        principalSchema: "public",
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_reviews_voucher_plan_headers_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "public",
                        principalTable: "voucher_plan_headers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_reviews_approver_id",
                schema: "public",
                table: "voucher_reviews",
                column: "approver_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_reviews_plan_id",
                schema: "public",
                table: "voucher_reviews",
                column: "plan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_reviews",
                schema: "public");
        }
    }
}
