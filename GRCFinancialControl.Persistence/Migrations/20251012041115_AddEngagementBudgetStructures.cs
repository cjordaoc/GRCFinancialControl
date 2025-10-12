using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRCFinancialControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementBudgetStructures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualHours",
                table: "Engagements",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialHoursBudget",
                table: "Engagements",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "EngagementRankBudgets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    RankName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Hours = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementRankBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngagementRankBudgets_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRankBudgets_EngagementId_RankName",
                table: "EngagementRankBudgets",
                columns: new[] { "EngagementId", "RankName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EngagementRankBudgets");

            migrationBuilder.DropColumn(
                name: "ActualHours",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "InitialHoursBudget",
                table: "Engagements");
        }
    }
}
