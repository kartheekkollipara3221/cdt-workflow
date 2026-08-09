using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationSlaMinuteRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceDate_SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "SlaPolicy",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaOccurrenceAtUtc",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowTaskSlaSchedule_RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                sql: "[RecurrenceIntervalMinutes] IS NULL OR [RecurrenceIntervalMinutes] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowTaskSlaSchedule_RecurrenceIntervalsMutuallyExclusive",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                sql: "[RecurrenceIntervalMinutes] IS NULL OR [RecurrenceIntervalDays] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SlaPolicy_RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "SlaPolicy",
                sql: "[RecurrenceIntervalMinutes] IS NULL OR [RecurrenceIntervalMinutes] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SlaPolicy_RecurrenceIntervalsMutuallyExclusive",
                schema: "dbo",
                table: "SlaPolicy",
                sql: "[RecurrenceIntervalMinutes] IS NULL OR [RecurrenceIntervalDays] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceAtUtc_SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "WorkflowTaskSlaScheduleId", "SlaOccurrenceAtUtc", "SlaReminderKind" },
                unique: true,
                filter: "[WorkflowTaskSlaScheduleId] IS NOT NULL AND [SlaOccurrenceAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowTaskSlaSchedule_RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowTaskSlaSchedule_RecurrenceIntervalsMutuallyExclusive",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SlaPolicy_RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SlaPolicy_RecurrenceIntervalsMutuallyExclusive",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceAtUtc_SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule");

            migrationBuilder.DropColumn(
                name: "RecurrenceIntervalMinutes",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "SlaOccurrenceAtUtc",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceDate_SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "WorkflowTaskSlaScheduleId", "SlaOccurrenceDate", "SlaReminderKind" },
                unique: true,
                filter: "[WorkflowTaskSlaScheduleId] IS NOT NULL");
        }
    }
}
