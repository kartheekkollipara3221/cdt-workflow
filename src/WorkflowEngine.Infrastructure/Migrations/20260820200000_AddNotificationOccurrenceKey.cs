using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOccurrenceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WorkflowInstanceId",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<string>(
                name: "NotificationOccurrenceKey",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_NotificationOutbox_NotificationOccurrenceKey",
                schema: "dbo",
                table: "NotificationOutbox",
                column: "NotificationOccurrenceKey",
                unique: true,
                filter: "[NotificationOccurrenceKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationOutbox_Owner",
                schema: "dbo",
                table: "NotificationOutbox",
                sql: "[WorkflowInstanceId] IS NOT NULL OR [NotificationOccurrenceKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_NotificationOutbox_NotificationOccurrenceKey",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationOutbox_Owner",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "NotificationOccurrenceKey",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowInstanceId",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);
        }
    }
}
