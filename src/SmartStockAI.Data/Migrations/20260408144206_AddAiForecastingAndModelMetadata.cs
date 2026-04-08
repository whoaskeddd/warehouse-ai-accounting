using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartStockAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiForecastingAndModelMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpectedInboundSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpectedInboundSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpectedInboundSnapshots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForecastSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ScopeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HistoryMonthsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UsesFallback = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceScopeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SourceScopeId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceScopeName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AverageMonthlyDemand = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    ForecastLeadTime = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    SafetyStock = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    ExpectedInbound = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    RecommendedOrder = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    ProjectedDeficit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    ModelQuality = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ArtifactPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelTrainingInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScopeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ScopeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TrainedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TrainingRowsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    QualityMetric = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ArtifactPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTrainingInfos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpectedInboundSnapshots_ProductId",
                table: "ExpectedInboundSnapshots",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForecastSnapshots_ScopeType_ScopeId",
                table: "ForecastSnapshots",
                columns: new[] { "ScopeType", "ScopeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrainingInfos_ModelType_ScopeType_ScopeId",
                table: "ModelTrainingInfos",
                columns: new[] { "ModelType", "ScopeType", "ScopeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpectedInboundSnapshots");

            migrationBuilder.DropTable(
                name: "ForecastSnapshots");

            migrationBuilder.DropTable(
                name: "ModelTrainingInfos");
        }
    }
}
