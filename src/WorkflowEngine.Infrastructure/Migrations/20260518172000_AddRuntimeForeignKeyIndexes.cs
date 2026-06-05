using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkflowEngine.Infrastructure.Persistence;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    [DbContext(typeof(WorkflowDbContext))]
    [Migration("20260518172000_AddRuntimeForeignKeyIndexes")]
    public partial class AddRuntimeForeignKeyIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecord_WorkflowInstanceId",
                table: "IdempotencyRecord",
                schema: "dbo",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_WorkflowInstanceId",
                table: "NotificationOutbox",
                schema: "dbo",
                column: "WorkflowInstanceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecord_WorkflowInstanceId",
                table: "IdempotencyRecord",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_WorkflowInstanceId",
                table: "NotificationOutbox",
                schema: "dbo");
        }
    }
}
