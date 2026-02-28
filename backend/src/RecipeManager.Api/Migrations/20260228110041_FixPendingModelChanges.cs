using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperCardImportDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    NutritionJson = table.Column<string>(type: "text", nullable: true),
                    IngredientsByServingsJson = table.Column<string>(type: "text", nullable: false),
                    StepsJson = table.Column<string>(type: "text", nullable: false),
                    HeroImageUrl = table.Column<string>(type: "text", nullable: false),
                    StepImageUrlsJson = table.Column<string>(type: "text", nullable: false),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    ConfidenceJson = table.Column<string>(type: "text", nullable: true),
                    RawExtractedTextFront = table.Column<string>(type: "text", nullable: true),
                    RawExtractedTextBack = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsCommitted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperCardImportDrafts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperCardImportDrafts_ExpiresAtUtc",
                table: "PaperCardImportDrafts",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaperCardImportDrafts_HouseholdId_CreatedAtUtc",
                table: "PaperCardImportDrafts",
                columns: new[] { "HouseholdId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperCardImportDrafts");
        }
    }
}
