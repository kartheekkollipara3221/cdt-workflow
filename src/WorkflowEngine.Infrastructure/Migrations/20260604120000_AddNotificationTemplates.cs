using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyTemplate",
                table: "NotificationRoute",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OverdueSubjectPrefix",
                table: "NotificationRoute",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Overdue: ");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "NotificationOutbox",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NotificationRouteKey",
                table: "WorkflowTask",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationContextJson",
                table: "WorkflowTask",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyTemplate",
                table: "NotificationRoute");

            migrationBuilder.DropColumn(
                name: "OverdueSubjectPrefix",
                table: "NotificationRoute");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "NotificationRouteKey",
                table: "WorkflowTask");

            migrationBuilder.DropColumn(
                name: "NotificationContextJson",
                table: "WorkflowTask");
        }
    }
}
