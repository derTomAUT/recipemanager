using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdAiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiApiKeyEncrypted",
                table: "Households",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiModel",
                table: "Households",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiProvider",
                table: "Households",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiApiKeyEncrypted",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "AiModel",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "AiProvider",
                table: "Households");
        }
    }
}
