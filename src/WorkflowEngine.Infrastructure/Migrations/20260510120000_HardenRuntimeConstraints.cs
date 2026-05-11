using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkflowEngine.Infrastructure.Persistence;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    [DbContext(typeof(WorkflowDbContext))]
    [Migration("20260510120000_HardenRuntimeConstraints")]
    public partial class HardenRuntimeConstraints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "NotificationOutbox",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingToken",
                table: "NotificationOutbox",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingStartedAtUtc",
                table: "NotificationOutbox",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "NotificationOutbox",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationOutbox_Status",
                table: "NotificationOutbox",
                sql: "[Status] IN ('Pending', 'Processing', 'Sent')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowInstance_Status",
                table: "WorkflowInstance",
                sql: "[Status] IN ('Active', 'Completed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowTask_Status",
                table: "WorkflowTask",
                sql: "[Status] IN ('Pending', 'Completed')");

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId",
                table: "IdempotencyRecord",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId",
                table: "NotificationOutbox",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowAudit",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowTask",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstance",
                principalColumn: "WorkflowInstanceId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId",
                table: "IdempotencyRecord");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId",
                table: "NotificationOutbox");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowAudit");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowTask");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationOutbox_Status",
                table: "NotificationOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowInstance_Status",
                table: "WorkflowInstance");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowTask_Status",
                table: "WorkflowTask");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAtUtc",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "ProcessingToken",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "NotificationOutbox");
        }
    }
}
