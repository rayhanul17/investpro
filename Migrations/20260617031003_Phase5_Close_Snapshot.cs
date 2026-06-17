using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexCms.InvestPro.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_Close_Snapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investpro_close_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_close_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_investment_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InvestmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InvestmentStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GrossRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossExpense = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetPL = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCapital = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalLaborValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PartnerCount = table.Column<int>(type: "integer", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_investment_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerDetailId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_payouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_close_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CloseRequestId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_investpro_close_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investpro_close_approvals_investpro_close_requests_CloseReq~",
                        column: x => x.CloseRequestId,
                        principalTable: "investpro_close_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investpro_snapshot_partner_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PartnerNid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PartnerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PartnerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContractTypeAtClose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PartnerRoleAtClose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CapitalContributed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LaborValueContributed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProfitSharePercent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    LossSharePercent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    ProfitShareAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LossShareAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WithdrawalsDuringInvestment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FinalSettlementAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ZakatEligibleAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_snapshot_partner_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investpro_snapshot_partner_details_investpro_investment_sna~",
                        column: x => x.SnapshotId,
                        principalTable: "investpro_investment_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investpro_close_approvals_CloseRequestId",
                table: "investpro_close_approvals",
                column: "CloseRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_close_requests_InvestmentId",
                table: "investpro_close_requests",
                column: "InvestmentId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_close_requests_RequestStatus",
                table: "investpro_close_requests",
                column: "RequestStatus");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investment_snapshots_InvestmentId",
                table: "investpro_investment_snapshots",
                column: "InvestmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investpro_payouts_SnapshotId",
                table: "investpro_payouts",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_snapshot_partner_details_SnapshotId_PartnerId",
                table: "investpro_snapshot_partner_details",
                columns: new[] { "SnapshotId", "PartnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investpro_close_approvals");

            migrationBuilder.DropTable(
                name: "investpro_payouts");

            migrationBuilder.DropTable(
                name: "investpro_snapshot_partner_details");

            migrationBuilder.DropTable(
                name: "investpro_close_requests");

            migrationBuilder.DropTable(
                name: "investpro_investment_snapshots");
        }
    }
}
