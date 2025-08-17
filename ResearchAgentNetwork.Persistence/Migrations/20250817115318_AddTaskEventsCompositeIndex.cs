using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResearchAgentNetwork.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskEventsCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TaskEvents_TaskId_TimestampUtc",
                table: "TaskEvents",
                columns: new[] { "TaskId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskEvents_TaskId_TimestampUtc",
                table: "TaskEvents");
        }
    }
}
