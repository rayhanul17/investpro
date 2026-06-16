using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexCms.InvestPro.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investpro_approval_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutoApproveBelow = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequireApprovalAbove = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequireAllPartnersAbove = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_approval_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_expense_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_expense_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investpro_partners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankAccountNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MfsProvider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MfsNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NomineeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NomineePhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NomineeRelation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investpro_partners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investpro_approval_configs_LedgerType",
                table: "investpro_approval_configs",
                column: "LedgerType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investpro_expense_categories_Name",
                table: "investpro_expense_categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investpro_partners_Nid",
                table: "investpro_partners",
                column: "Nid");

            migrationBuilder.CreateIndex(
                name: "IX_investpro_partners_Phone",
                table: "investpro_partners",
                column: "Phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investpro_approval_configs");

            migrationBuilder.DropTable(
                name: "investpro_expense_categories");

            migrationBuilder.DropTable(
                name: "investpro_partners");
        }
    }
}
