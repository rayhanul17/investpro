using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexCms.InvestPro.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_Reopen_SnapshotVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_investpro_investment_snapshots_InvestmentId",
                table: "investpro_investment_snapshots");

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmount",
                table: "investpro_snapshot_partner_details",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousSettlementAmount",
                table: "investpro_snapshot_partner_details",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "investpro_payouts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdjustment",
                table: "investpro_payouts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousSnapshotId",
                table: "investpro_investment_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotStatus",
                table: "investpro_investment_snapshots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SupersededAt",
                table: "investpro_investment_snapshots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupersededReason",
                table: "investpro_investment_snapshots",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "investpro_investment_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing pre-Phase7 rows so they look like a normal v1
            // Active snapshot with Outgoing payouts. Without this, the snapshot
            // viewer (which filters by SnapshotStatus = Active) hides them.
            migrationBuilder.Sql(@"
                UPDATE investpro_investment_snapshots
                   SET ""Version"" = 1,
                       ""SnapshotStatus"" = 'Active'
                 WHERE ""Version"" = 0 OR ""SnapshotStatus"" = '';

                UPDATE investpro_payouts
                   SET ""Direction"" = 'Outgoing'
                 WHERE ""Direction"" = '';
            ");

            migrationBuilder.CreateTable(
                name: "investpro_reopen_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequestStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_reopen_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_reopen_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReopenRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_reopen_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investpro_reopen_approvals_investpro_reopen_requests_Reopen~",
                        column: x => x.ReopenRequestId,
                        principalTable: "investpro_reopen_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investment_snapshots_InvestmentId",
                table: "investpro_investment_snapshots",
                column: "InvestmentId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investment_snapshots_InvestmentId_SnapshotStatus",
                table: "investpro_investment_snapshots",
                columns: new[] { "InvestmentId", "SnapshotStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_investpro_reopen_approvals_ReopenRequestId",
                table: "investpro_reopen_approvals",
                column: "ReopenRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_reopen_requests_InvestmentId",
                table: "investpro_reopen_requests",
                column: "InvestmentId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_reopen_requests_RequestStatus",
                table: "investpro_reopen_requests",
                column: "RequestStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investpro_reopen_approvals");

            migrationBuilder.DropTable(
                name: "investpro_reopen_requests");

            migrationBuilder.DropIndex(
                name: "IX_investpro_investment_snapshots_InvestmentId",
                table: "investpro_investment_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_investpro_investment_snapshots_InvestmentId_SnapshotStatus",
                table: "investpro_investment_snapshots");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmount",
                table: "investpro_snapshot_partner_details");

            migrationBuilder.DropColumn(
                name: "PreviousSettlementAmount",
                table: "investpro_snapshot_partner_details");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "investpro_payouts");

            migrationBuilder.DropColumn(
                name: "IsAdjustment",
                table: "investpro_payouts");

            migrationBuilder.DropColumn(
                name: "PreviousSnapshotId",
                table: "investpro_investment_snapshots");

            migrationBuilder.DropColumn(
                name: "SnapshotStatus",
                table: "investpro_investment_snapshots");

            migrationBuilder.DropColumn(
                name: "SupersededAt",
                table: "investpro_investment_snapshots");

            migrationBuilder.DropColumn(
                name: "SupersededReason",
                table: "investpro_investment_snapshots");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "investpro_investment_snapshots");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investment_snapshots_InvestmentId",
                table: "investpro_investment_snapshots",
                column: "InvestmentId",
                unique: true);
        }
    }
}
