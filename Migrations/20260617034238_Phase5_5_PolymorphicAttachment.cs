using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexCms.InvestPro.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_5_PolymorphicAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LedgerType",
                table: "investpro_ledger_attachments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<Guid>(
                name: "LedgerEntryId",
                table: "investpro_ledger_attachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "investpro_ledger_attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "OwnerType",
                table: "investpro_ledger_attachments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_ledger_attachments_OwnerType_OwnerId",
                table: "investpro_ledger_attachments",
                columns: new[] { "OwnerType", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_investpro_ledger_attachments_OwnerType_OwnerId",
                table: "investpro_ledger_attachments");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "investpro_ledger_attachments");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "investpro_ledger_attachments");

            migrationBuilder.AlterColumn<string>(
                name: "LedgerType",
                table: "investpro_ledger_attachments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "LedgerEntryId",
                table: "investpro_ledger_attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
