using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTaskAssignmentRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowTaskAssignmentRoleDefinition",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    TaskKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_BIN2"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTaskAssignmentRoleDefinition", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTaskAssignmentRoleDefinition_WorkflowDefinitionVersionId_TaskKey_RoleKey",
                table: "WorkflowTaskAssignmentRoleDefinition",
                columns: new[] { "WorkflowDefinitionVersionId", "TaskKey", "RoleKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowTaskAssignmentRoleDefinition");
        }
    }
}
