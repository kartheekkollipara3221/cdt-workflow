using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkflowEngine.Infrastructure.Persistence;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    [DbContext(typeof(WorkflowDbContext))]
    [Migration("20260512100000_WorkflowInstanceAlternateKey")]
    public partial class WorkflowInstanceAlternateKey : Migration
    {
        // The hand-written HardenRuntimeConstraints migration added FK constraints that use
        // WorkflowInstance.WorkflowInstanceId as principal key (HasPrincipalKey), which in EF Core
        // means an alternate key. The initial migration only created a unique index there. EF Core 10
        // treats that as a pending model change and hard-errors on Migrate(). This migration promotes
        // the unique index to a named unique constraint (alternate key) so the model and DB align.
        //
        // SQL Server requires FKs that reference a column to be dropped before the underlying
        // index/constraint on that column can be replaced, so all four FKs are dropped and re-added.

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId",
                table: "IdempotencyRecord",
                schema: "dbo");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId",
                table: "NotificationOutbox",
                schema: "dbo");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowAudit",
                schema: "dbo");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowTask",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowInstance",
                schema: "dbo");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowInstance",
                schema: "dbo",
                column: "WorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId",
                table: "IdempotencyRecord",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId",
                table: "NotificationOutbox",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowAudit",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowTask",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId",
                table: "IdempotencyRecord",
                schema: "dbo");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId",
                table: "NotificationOutbox",
                schema: "dbo");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowAudit",
                schema: "dbo");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowTask",
                schema: "dbo");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowInstance",
                schema: "dbo");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowInstance",
                schema: "dbo",
                column: "WorkflowInstanceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId",
                table: "IdempotencyRecord",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId",
                table: "NotificationOutbox",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowAudit",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowTask",
                schema: "dbo",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalSchema: "dbo",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
