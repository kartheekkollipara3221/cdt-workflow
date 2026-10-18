using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCdtV2SlaReminderControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DueRecipientsJson",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DueReminderLocalTime",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverdueRecipientsJson",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DueReminderLocalTime",
                schema: "dbo",
                table: "SlaPolicy",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OverdueStartsNextLocalDay",
                schema: "dbo",
                table: "SlaPolicy",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SkipMissedDueReminder",
                schema: "dbo",
                table: "SlaPolicy",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueRecipientsJson",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule");

            migrationBuilder.DropColumn(
                name: "DueReminderLocalTime",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule");

            migrationBuilder.DropColumn(
                name: "OverdueRecipientsJson",
                schema: "dbo",
                table: "WorkflowTaskSlaSchedule");

            migrationBuilder.DropColumn(
                name: "DueReminderLocalTime",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "OverdueStartsNextLocalDay",
                schema: "dbo",
                table: "SlaPolicy");

            migrationBuilder.DropColumn(
                name: "SkipMissedDueReminder",
                schema: "dbo",
                table: "SlaPolicy");
        }
    }
}
