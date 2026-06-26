using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitMemberIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TEST COMMENT
            // 1. Drop old FKs so data can be freely migrated.
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_customers_member_id",
                schema: "public",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_user_accounts_customers_customer_id",
                schema: "public",
                table: "user_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_distributions_customers_member_id",
                schema: "public",
                table: "voucher_distributions");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_user_accounts_recipient_id",
                schema: "public",
                table: "voucher_transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_user_accounts_sender_id",
                schema: "public",
                table: "voucher_transfers");

            // 2. Create the new member_accounts table.
            migrationBuilder.CreateTable(
                name: "member_accounts",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_member_accounts_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_member_accounts_customer_id",
                schema: "public",
                table: "member_accounts",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_accounts_username",
                schema: "public",
                table: "member_accounts",
                column: "username",
                unique: true);

            // 3. Migrate existing Member rows from user_accounts, preserving Id.
            migrationBuilder.Sql(@"
                INSERT INTO member_accounts (id, customer_id, username, password_hash, full_name, status, created_at, updated_at)
                SELECT id, customer_id, username, password_hash, full_name, status, created_at, updated_at
                FROM user_accounts
                WHERE role = 'Member';
            ");

            // 4. Create placeholder MemberAccount rows for any Customer.Id referenced as MemberId
            //    in voucher_plan_details, voucher_distributions, or purchase_orders that does not
            //    yet have a member account.
            migrationBuilder.Sql(@"
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
            ");

            // 5. Fix inconsistent MemberId values: repoint values that currently reference
            //    customers.id to the corresponding member_accounts.id.
            migrationBuilder.Sql(@"
                UPDATE voucher_plan_details vpd
                SET member_id = ma.id
                FROM member_accounts ma
                WHERE vpd.member_id = ma.customer_id;
            ");

            migrationBuilder.Sql(@"
                UPDATE voucher_distributions vd
                SET member_id = ma.id
                FROM member_accounts ma
                WHERE vd.member_id = ma.customer_id;
            ");

            migrationBuilder.Sql(@"
                UPDATE purchase_orders po
                SET member_id = ma.id
                FROM member_accounts ma
                WHERE po.member_id = ma.customer_id;
            ");

            // 6. Delete migrated Member rows from user_accounts.
            migrationBuilder.Sql(@"
                DELETE FROM user_accounts WHERE role = 'Member';
            ");

            // 7. Drop customer_id column/index from user_accounts.
            migrationBuilder.DropIndex(
                name: "IX_user_accounts_customer_id",
                schema: "public",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "public",
                table: "user_accounts");

            // 8. Null out any remaining ownership references that do not map to a
            //    valid member account (e.g. vouchers assigned to staff accounts).
            migrationBuilder.Sql(@"
                UPDATE voucher_plan_details SET member_id = NULL WHERE member_id NOT IN (SELECT id FROM member_accounts);
            ");

            migrationBuilder.Sql(@"
                UPDATE voucher_distributions SET member_id = NULL WHERE member_id NOT IN (SELECT id FROM member_accounts);
            ");

            migrationBuilder.Sql(@"
                UPDATE purchase_orders SET member_id = NULL WHERE member_id NOT IN (SELECT id FROM member_accounts);
            ");

            migrationBuilder.Sql(@"
                UPDATE voucher_transfers SET sender_id = NULL WHERE sender_id NOT IN (SELECT id FROM member_accounts);
            ");

            migrationBuilder.Sql(@"
                UPDATE voucher_transfers SET recipient_id = NULL WHERE recipient_id NOT IN (SELECT id FROM member_accounts);
            ");

            // 9. Add new FKs/indexes pointing to member_accounts.
            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_member_accounts_member_id",
                schema: "public",
                table: "purchase_orders",
                column: "member_id",
                principalSchema: "public",
                principalTable: "member_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_distributions_member_accounts_member_id",
                schema: "public",
                table: "voucher_distributions",
                column: "member_id",
                principalSchema: "public",
                principalTable: "member_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_plan_details_member_accounts_member_id",
                schema: "public",
                table: "voucher_plan_details",
                column: "member_id",
                principalSchema: "public",
                principalTable: "member_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_member_accounts_recipient_id",
                schema: "public",
                table: "voucher_transfers",
                column: "recipient_id",
                principalSchema: "public",
                principalTable: "member_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_member_accounts_sender_id",
                schema: "public",
                table: "voucher_transfers",
                column: "sender_id",
                principalSchema: "public",
                principalTable: "member_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_member_accounts_member_id",
                schema: "public",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_distributions_member_accounts_member_id",
                schema: "public",
                table: "voucher_distributions");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_plan_details_member_accounts_member_id",
                schema: "public",
                table: "voucher_plan_details");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_member_accounts_recipient_id",
                schema: "public",
                table: "voucher_transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_member_accounts_sender_id",
                schema: "public",
                table: "voucher_transfers");

            migrationBuilder.DropTable(
                name: "member_accounts",
                schema: "public");

            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "public",
                table: "user_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_customer_id",
                schema: "public",
                table: "user_accounts",
                column: "customer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_customers_member_id",
                schema: "public",
                table: "purchase_orders",
                column: "member_id",
                principalSchema: "public",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_accounts_customers_customer_id",
                schema: "public",
                table: "user_accounts",
                column: "customer_id",
                principalSchema: "public",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_distributions_customers_member_id",
                schema: "public",
                table: "voucher_distributions",
                column: "member_id",
                principalSchema: "public",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_user_accounts_recipient_id",
                schema: "public",
                table: "voucher_transfers",
                column: "recipient_id",
                principalSchema: "public",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_user_accounts_sender_id",
                schema: "public",
                table: "voucher_transfers",
                column: "sender_id",
                principalSchema: "public",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
