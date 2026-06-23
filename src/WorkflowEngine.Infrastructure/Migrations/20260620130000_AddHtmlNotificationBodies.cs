using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WorkflowEngine.Infrastructure.Persistence;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(WorkflowDbContext))]
    [Migration("20260620130000_AddHtmlNotificationBodies")]
    public partial class AddHtmlNotificationBodies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HtmlBodyTemplate",
                schema: "dbo",
                table: "NotificationRoute",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HtmlBody",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HtmlBodyTemplate",
                schema: "dbo",
                table: "NotificationRoute");

            migrationBuilder.DropColumn(
                name: "HtmlBody",
                schema: "dbo",
                table: "NotificationOutbox");
        }
    }
}
