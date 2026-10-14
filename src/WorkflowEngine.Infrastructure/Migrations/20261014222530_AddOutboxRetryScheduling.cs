using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                schema: "dbo",
                table: "NotificationOutbox",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_RetryEligibility",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_RetryEligibility",
                schema: "dbo",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                schema: "dbo",
                table: "NotificationOutbox");
        }
    }
}
