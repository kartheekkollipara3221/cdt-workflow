using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeWorkflowApiEnvelope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [dbo].[NotificationOutbox];");
            migrationBuilder.Sql("DELETE FROM [dbo].[WorkflowTaskSlaSchedule];");
            migrationBuilder.Sql("DELETE FROM [dbo].[WorkflowTask];");
            migrationBuilder.Sql("DELETE FROM [dbo].[WorkflowAudit];");
            migrationBuilder.Sql("DELETE FROM [dbo].[IdempotencyRecord];");
            migrationBuilder.Sql("DELETE FROM [dbo].[WorkflowInstance];");

            migrationBuilder.DropColumn(
                name: "RecipientRoleKey",
                schema: "dbo",
                table: "NotificationRoute");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                schema: "dbo",
                table: "NotificationRoute");

            migrationBuilder.AddColumn<string>(
                name: "BusinessObjectType",
                schema: "dbo",
                table: "WorkflowDefinition",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "NotificationRecipientRule",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationRouteId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientKind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StaticAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRecipientRule", x => x.Id);
                    table.CheckConstraint("CK_NotificationRecipientRule_RecipientKind", "[RecipientKind] IN ('To', 'Cc')");
                    table.CheckConstraint("CK_NotificationRecipientRule_SortOrder", "[SortOrder] > 0");
                    table.CheckConstraint("CK_NotificationRecipientRule_SourceFields", "([SourceType] = 'StaticAddress' AND [StaticAddress] IS NOT NULL AND [SourceKey] IS NULL) OR ([SourceType] IN ('WorkflowParticipant', 'RequestInput') AND [SourceKey] IS NOT NULL AND [StaticAddress] IS NULL)");
                    table.CheckConstraint("CK_NotificationRecipientRule_SourceType", "[SourceType] IN ('StaticAddress', 'WorkflowParticipant', 'RequestInput')");
                    table.ForeignKey(
                        name: "FK_NotificationRecipientRule_NotificationRoute_NotificationRouteId",
                        column: x => x.NotificationRouteId,
                        principalSchema: "dbo",
                        principalTable: "NotificationRoute",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowParticipant",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ParticipantKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowParticipant", x => x.Id);
                    table.CheckConstraint("CK_WorkflowParticipant_SortOrder", "[SortOrder] > 0");
                    table.ForeignKey(
                        name: "FK_WorkflowParticipant_WorkflowInstance_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "dbo",
                        principalTable: "WorkflowInstance",
                        principalColumn: "WorkflowInstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowParticipantDefinition",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    ParticipantKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cardinality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CaptureStage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsImmutable = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowParticipantDefinition", x => x.Id);
                    table.CheckConstraint("CK_WorkflowParticipantDefinition_CaptureStage", "[CaptureStage] IN ('Start')");
                    table.CheckConstraint("CK_WorkflowParticipantDefinition_Cardinality", "[Cardinality] IN ('Single', 'Many')");
                    table.ForeignKey(
                        name: "FK_WorkflowParticipantDefinition_WorkflowDefinitionVersion_WorkflowDefinitionVersionId",
                        column: x => x.WorkflowDefinitionVersionId,
                        principalSchema: "dbo",
                        principalTable: "WorkflowDefinitionVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipientRule_NotificationRouteId_SortOrder",
                schema: "dbo",
                table: "NotificationRecipientRule",
                columns: new[] { "NotificationRouteId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowParticipant_WorkflowInstanceId_ParticipantKey_SortOrder",
                schema: "dbo",
                table: "WorkflowParticipant",
                columns: new[] { "WorkflowInstanceId", "ParticipantKey", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowParticipantDefinition_WorkflowDefinitionVersionId_ParticipantKey",
                schema: "dbo",
                table: "WorkflowParticipantDefinition",
                columns: new[] { "WorkflowDefinitionVersionId", "ParticipantKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationRecipientRule",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WorkflowParticipant",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WorkflowParticipantDefinition",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "BusinessObjectType",
                schema: "dbo",
                table: "WorkflowDefinition");

            migrationBuilder.AddColumn<string>(
                name: "RecipientRoleKey",
                schema: "dbo",
                table: "NotificationRoute",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientUserId",
                schema: "dbo",
                table: "NotificationRoute",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
