using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAnchoredRecurringSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_WorkflowTaskId_NotificationType",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationOutbox_Status",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.AlterColumn<int>(
                name: "DurationMinutes",
                schema: "dbo",
                table: "SlaPolicy",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CalculationMode",
                schema: "dbo",
                table: "SlaPolicy",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DueNotificationRouteKey",
                schema: "dbo",
                table: "SlaPolicy",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverdueNotificationRouteKey",
                schema: "dbo",
                table: "SlaPolicy",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverdueStartOffsetDays",
                schema: "dbo",
                table: "SlaPolicy",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceIntervalDays",
                schema: "dbo",
                table: "SlaPolicy",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReminderLocalTime",
                schema: "dbo",
                table: "SlaPolicy",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendDueDateReminder",
                schema: "dbo",
                table: "SlaPolicy",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "dbo",
                table: "SlaPolicy",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAtUtc",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SlaOccurrenceDate",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkflowTaskSlaScheduleId",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowTaskSlaSchedule",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowTaskId = table.Column<long>(type: "bigint", nullable: false),
                    ScheduleVersion = table.Column<int>(type: "int", nullable: false),
                    SlaPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueReminderAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OverdueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextReminderAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CalculationMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReminderLocalTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    OverdueStartOffsetDays = table.Column<int>(type: "int", nullable: false),
                    RecurrenceIntervalDays = table.Column<int>(type: "int", nullable: true),
                    DueNotificationRouteKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OverdueNotificationRouteKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupersededAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SupersededByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTaskSlaSchedule", x => x.Id);
                    table.CheckConstraint("CK_WorkflowTaskSlaSchedule_RecurrenceIntervalDays", "[RecurrenceIntervalDays] IS NULL OR [RecurrenceIntervalDays] > 0");
                    table.CheckConstraint("CK_WorkflowTaskSlaSchedule_ScheduleVersion", "[ScheduleVersion] > 0");
                    table.CheckConstraint("CK_WorkflowTaskSlaSchedule_Status", "[Status] IN ('Active', 'Superseded', 'Completed')");
                    table.ForeignKey(
                        name: "FK_WorkflowTaskSlaSchedule_WorkflowTask_WorkflowTaskId",
                        column: x => x.WorkflowTaskId,
                        principalSchema: "dbo",
                        principalTable: "WorkflowTask",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                "UPDATE [dbo].[SlaPolicy] SET [CalculationMode] = N'DurationFromTaskCreation' WHERE [CalculationMode] = N'';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SlaPolicy_CalculationMode",
                schema: "dbo",
                table: "SlaPolicy",
                sql: "[CalculationMode] IN ('DurationFromTaskCreation', 'ApplicationTargetDate')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SlaPolicy_OverdueStartOffsetDays",
                schema: "dbo",
                table: "SlaPolicy",
                sql: "[OverdueStartOffsetDays] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SlaPolicy_RecurrenceIntervalDays",
                schema: "dbo",
                table: "SlaPolicy",
                sql: "[RecurrenceIntervalDays] IS NULL OR [RecurrenceIntervalDays] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceDate_SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "WorkflowTaskSlaScheduleId", "SlaOccurrenceDate", "SlaReminderKind" },
                unique: true,
                filter: "[WorkflowTaskSlaScheduleId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationOutbox_Status",
                schema: "dbo",
                table: "NotificationOutbox",
                sql: "[Status] IN ('Pending', 'Processing', 'Sent', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTaskSlaSchedule_WorkflowTaskId",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                column: "WorkflowTaskId",
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTaskSlaSchedule_WorkflowTaskId_ScheduleVersion",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                columns: new[] { "WorkflowTaskId", "ScheduleVersion" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationOutbox_WorkflowTaskSlaSchedule_WorkflowTaskSlaScheduleId",
                schema: "dbo",
                table: "NotificationOutbox",
                column: "WorkflowTaskSlaScheduleId",
                principalSchema: "dbo",
                principalTable: "WorkflowTaskSlaSchedule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationOutbox_WorkflowTaskSlaSchedule_WorkflowTaskSlaScheduleId",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropTable(
                name: "WorkflowTaskSlaSchedule",
                schema: "dbo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SlaPolicy_CalculationMode",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SlaPolicy_OverdueStartOffsetDays",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SlaPolicy_RecurrenceIntervalDays",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceDate_SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationOutbox_Status",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "CalculationMode",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "DueNotificationRouteKey",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "OverdueNotificationRouteKey",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "OverdueStartOffsetDays",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "RecurrenceIntervalDays",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "ReminderLocalTime",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "SendDueDateReminder",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "SlaOccurrenceDate",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "SlaReminderKind",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "WorkflowTaskSlaScheduleId",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.AlterColumn<int>(
                name: "DurationMinutes",
                schema: "dbo",
                table: "SlaPolicy",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_WorkflowTaskId_NotificationType",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "WorkflowTaskId", "NotificationType" },
                unique: true,
                filter: "[NotificationType] = 'SlaTaskOverdue'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationOutbox_Status",
                schema: "dbo",
                table: "NotificationOutbox",
                sql: "[Status] IN ('Pending', 'Processing', 'Sent')");
        }
    }
}
