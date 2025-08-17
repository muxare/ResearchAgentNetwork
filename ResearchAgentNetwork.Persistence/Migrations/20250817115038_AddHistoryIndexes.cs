using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResearchAgentNetwork.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CreatedAtUtc",
                table: "Tasks",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvents_TimestampUtc",
                table: "TaskEvents",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_CreatedAtUtc",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_TaskEvents_TimestampUtc",
                table: "TaskEvents");
        }
    }
}
