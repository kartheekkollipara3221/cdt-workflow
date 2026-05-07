using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkflowSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyRecord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResultHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MockSectionData",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessObjectType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessObjectId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SectionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SavedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SavedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockSectionData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationOutbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    WorkflowTaskId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessObjectType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessObjectId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientRoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecipientUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRoute",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    RouteKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientRoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecipientUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubjectTemplate = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRoute", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    SlaPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowActionDefinition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SectionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ButtonKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ButtonLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Intent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequiresReason = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowActionDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowAudit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BusinessObjectType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessObjectId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FromStateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToStateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PerformedByRoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    WorkflowKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessObjectType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessObjectId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CurrentStateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRoleDefinition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    RoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRoleDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStateDefinition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    StateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStateDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTask",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TaskKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssignedRoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssignedUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClosedByActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTask", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTaskDefinition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    TaskKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerRoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SlaPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTaskDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionDefinition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    FromStateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AllowedRoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToStateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TaskToCloseKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaskToCreateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotificationRouteKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuditLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionVersion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitionVersion_WorkflowDefinition_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecord_IdempotencyKey",
                table: "IdempotencyRecord",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MockSectionData_BusinessObjectType_BusinessObjectId_SectionKey_SavedAtUtc_Id",
                table: "MockSectionData",
                columns: new[] { "BusinessObjectType", "BusinessObjectId", "SectionKey", "SavedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_WorkflowTaskId_NotificationType",
                table: "NotificationOutbox",
                columns: new[] { "WorkflowTaskId", "NotificationType" },
                unique: true,
                filter: "[NotificationType] = 'SlaTaskOverdue'");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRoute_WorkflowDefinitionVersionId_RouteKey",
                table: "NotificationRoute",
                columns: new[] { "WorkflowDefinitionVersionId", "RouteKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicy_WorkflowDefinitionVersionId_SlaPolicyKey",
                table: "SlaPolicy",
                columns: new[] { "WorkflowDefinitionVersionId", "SlaPolicyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActionDefinition_WorkflowDefinitionVersionId_ActionKey",
                table: "WorkflowActionDefinition",
                columns: new[] { "WorkflowDefinitionVersionId", "ActionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAudit_WorkflowInstanceId",
                table: "WorkflowAudit",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinition_WorkflowKey",
                table: "WorkflowDefinition",
                column: "WorkflowKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersion_WorkflowDefinitionId_VersionNumber",
                table: "WorkflowDefinitionVersion",
                columns: new[] { "WorkflowDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_WorkflowInstanceId",
                table: "WorkflowInstance",
                column: "WorkflowInstanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_WorkflowKey_BusinessObjectType_BusinessObjectId",
                table: "WorkflowInstance",
                columns: new[] { "WorkflowKey", "BusinessObjectType", "BusinessObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRoleDefinition_WorkflowDefinitionVersionId_RoleKey",
                table: "WorkflowRoleDefinition",
                columns: new[] { "WorkflowDefinitionVersionId", "RoleKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStateDefinition_WorkflowDefinitionVersionId_StateKey",
                table: "WorkflowStateDefinition",
                columns: new[] { "WorkflowDefinitionVersionId", "StateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTask_WorkflowInstanceId",
                table: "WorkflowTask",
                column: "WorkflowInstanceId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTaskDefinition_WorkflowDefinitionVersionId_TaskKey",
                table: "WorkflowTaskDefinition",
                columns: new[] { "WorkflowDefinitionVersionId", "TaskKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionDefinition_WorkflowDefinitionVersionId_FromStateKey_ActionKey_AllowedRoleKey",
                table: "WorkflowTransitionDefinition",
                columns: new[] { "WorkflowDefinitionVersionId", "FromStateKey", "ActionKey", "AllowedRoleKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecord");

            migrationBuilder.DropTable(
                name: "MockSectionData");

            migrationBuilder.DropTable(
                name: "NotificationOutbox");

            migrationBuilder.DropTable(
                name: "NotificationRoute");

            migrationBuilder.DropTable(
                name: "SlaPolicy");

            migrationBuilder.DropTable(
                name: "WorkflowActionDefinition");

            migrationBuilder.DropTable(
                name: "WorkflowAudit");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionVersion");

            migrationBuilder.DropTable(
                name: "WorkflowInstance");

            migrationBuilder.DropTable(
                name: "WorkflowRoleDefinition");

            migrationBuilder.DropTable(
                name: "WorkflowStateDefinition");

            migrationBuilder.DropTable(
                name: "WorkflowTask");

            migrationBuilder.DropTable(
                name: "WorkflowTaskDefinition");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionDefinition");

            migrationBuilder.DropTable(
                name: "WorkflowDefinition");
        }
    }
}
