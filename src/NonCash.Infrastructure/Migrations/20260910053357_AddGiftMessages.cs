using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gift_messages",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_gift_messages_member_accounts_author_member_id",
                        column: x => x.author_member_id,
                        principalSchema: "public",
                        principalTable: "member_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_messages_member_accounts_recipient_member_id",
                        column: x => x.recipient_member_id,
                        principalSchema: "public",
                        principalTable: "member_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gift_messages_voucher_transfers_transfer_id",
                        column: x => x.transfer_id,
                        principalSchema: "public",
                        principalTable: "voucher_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gift_messages_author_member_id",
                schema: "public",
                table: "gift_messages",
                column: "author_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_gift_messages_recipient_member_id_is_read",
                schema: "public",
                table: "gift_messages",
                columns: new[] { "recipient_member_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "IX_gift_messages_transfer_id_sent_at",
                schema: "public",
                table: "gift_messages",
                columns: new[] { "transfer_id", "sent_at" });

            // CR-2026-09-10-31: gifts sent before this change kept their words in three columns on
            // voucher_transfers. Those columns stay (the gift lists read them as a summary), and their
            // contents become the first rows of each gift's conversation, dated as they really happened.
            // Backfilled rows start read — they are history nobody is waiting on.
            migrationBuilder.Sql(@"
INSERT INTO public.gift_messages
    (id, transfer_id, author_member_id, recipient_member_id, direction, kind, body, sent_at, is_read, read_at, created_at, updated_at)
SELECT gen_random_uuid(), t.id, t.sender_id, t.recipient_id, 'FromSender', 'GiftNote',
       btrim(t.note), t.initiated_at, TRUE, t.initiated_at, t.initiated_at, NULL
FROM public.voucher_transfers t
WHERE t.note IS NOT NULL AND btrim(t.note) <> '';

INSERT INTO public.gift_messages
    (id, transfer_id, author_member_id, recipient_member_id, direction, kind, body, sent_at, is_read, read_at, created_at, updated_at)
SELECT gen_random_uuid(), t.id, t.recipient_id, t.sender_id, 'FromRecipient', 'ThankYou',
       btrim(t.recipient_note), COALESCE(t.responded_at, t.initiated_at), TRUE, COALESCE(t.responded_at, t.initiated_at),
       COALESCE(t.responded_at, t.initiated_at), NULL
FROM public.voucher_transfers t
WHERE t.recipient_note IS NOT NULL AND btrim(t.recipient_note) <> '';

INSERT INTO public.gift_messages
    (id, transfer_id, author_member_id, recipient_member_id, direction, kind, body, sent_at, is_read, read_at, created_at, updated_at)
SELECT gen_random_uuid(), t.id, t.recipient_id, t.sender_id, 'FromRecipient', 'DeclineReason',
       btrim(t.reject_reason), COALESCE(t.responded_at, t.initiated_at), TRUE, COALESCE(t.responded_at, t.initiated_at),
       COALESCE(t.responded_at, t.initiated_at), NULL
FROM public.voucher_transfers t
WHERE t.reject_reason IS NOT NULL AND btrim(t.reject_reason) <> '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gift_messages",
                schema: "public");
        }
    }
}
