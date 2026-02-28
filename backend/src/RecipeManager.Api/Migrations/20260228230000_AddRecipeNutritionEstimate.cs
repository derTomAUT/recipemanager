using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeNutritionEstimate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NutritionEstimatedAtUtc",
                table: "Recipes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NutritionNotes",
                table: "Recipes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NutritionPerServingJson",
                table: "Recipes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NutritionSource",
                table: "Recipes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NutritionTotalJson",
                table: "Recipes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NutritionEstimatedAtUtc",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "NutritionNotes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "NutritionPerServingJson",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "NutritionSource",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "NutritionTotalJson",
                table: "Recipes");
        }
    }
}
