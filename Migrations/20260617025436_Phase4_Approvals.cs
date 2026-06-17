using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexCms.InvestPro.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_Approvals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investpro_approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequiredApproverMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RequestStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_approval_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_approval_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_investpro_approval_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investpro_approval_decisions_investpro_approval_requests_Ap~",
                        column: x => x.ApprovalRequestId,
                        principalTable: "investpro_approval_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investpro_approval_decisions_ApprovalRequestId",
                table: "investpro_approval_decisions",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_approval_requests_InvestmentId",
                table: "investpro_approval_requests",
                column: "InvestmentId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_approval_requests_LedgerType_LedgerEntryId",
                table: "investpro_approval_requests",
                columns: new[] { "LedgerType", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investpro_approval_requests_RequestStatus",
                table: "investpro_approval_requests",
                column: "RequestStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investpro_approval_decisions");

            migrationBuilder.DropTable(
                name: "investpro_approval_requests");
        }
    }
}
