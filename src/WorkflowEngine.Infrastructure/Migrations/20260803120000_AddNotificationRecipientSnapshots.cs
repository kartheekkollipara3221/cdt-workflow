using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRecipientSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [dbo].[NotificationOutbox];");
            migrationBuilder.Sql("DELETE FROM [dbo].[WorkflowTask];");

            migrationBuilder.AddColumn<string>(
                name: "NotificationRecipientsJson",
                schema: "dbo",
                table: "WorkflowTask",
                type: "nvarchar(max)",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "NotificationRecipientsJson",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(max)",
                nullable: false);

            migrationBuilder.DropColumn(
                name: "RecipientRoleKey",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                schema: "dbo",
                table: "NotificationOutbox");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationRecipientsJson",
                schema: "dbo",
                table: "WorkflowTask");

            migrationBuilder.DropColumn(
                name: "NotificationRecipientsJson",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.AddColumn<string>(
                name: "RecipientRoleKey",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientUserId",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
