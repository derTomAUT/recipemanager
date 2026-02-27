using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RecipeManager.Api.Data;

#nullable disable

namespace RecipeManager.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260227193000_AddAiDebugLogs")]
    public partial class AddAiDebugLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiDebugLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    RequestJsonSanitized = table.Column<string>(type: "text", nullable: false),
                    ResponseJsonSanitized = table.Column<string>(type: "text", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiDebugLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiDebugLogs_CreatedAtUtc",
                table: "AiDebugLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiDebugLogs_Provider_Operation",
                table: "AiDebugLogs",
                columns: new[] { "Provider", "Operation" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDebugLogs_Success",
                table: "AiDebugLogs",
                column: "Success");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiDebugLogs");
        }
    }
}
