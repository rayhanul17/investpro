using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexCms.InvestPro.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_Investments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investpro_investments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LifecycleStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_investpro_investments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_investment_partners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PartnerRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AgreedCapital = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AgreedLaborValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProfitSharePercent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    LossSharePercent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SpecialTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_investment_partners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investpro_investment_partners_investpro_investments_Investm~",
                        column: x => x.InvestmentId,
                        principalTable: "investpro_investments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_investpro_investment_partners_investpro_partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "investpro_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investment_partners_InvestmentId_PartnerId",
                table: "investpro_investment_partners",
                columns: new[] { "InvestmentId", "PartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investment_partners_PartnerId",
                table: "investpro_investment_partners",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investments_Code",
                table: "investpro_investments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investpro_investments_LifecycleStatus",
                table: "investpro_investments",
                column: "LifecycleStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investpro_investment_partners");

            migrationBuilder.DropTable(
                name: "investpro_investments");
        }
    }
}
