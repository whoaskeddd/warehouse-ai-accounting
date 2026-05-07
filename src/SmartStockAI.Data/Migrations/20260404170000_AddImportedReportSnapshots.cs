using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartStockAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedReportSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportedReportSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReportName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RowsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedReportSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedReportSnapshots_ImportedAtUtc",
                table: "ImportedReportSnapshots",
                column: "ImportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedReportSnapshots_ReportKey",
                table: "ImportedReportSnapshots",
                column: "ReportKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportedReportSnapshots");
        }
    }
}
