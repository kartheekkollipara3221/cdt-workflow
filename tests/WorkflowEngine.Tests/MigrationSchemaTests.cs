using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Tests;

public sealed class MigrationSchemaTests
{
    [Fact]
    public void MigrationScriptQualifiesWorkflowTablesWithDboSchema()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE [dbo].[__EFMigrationsHistory]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[WorkflowDefinition]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[WorkflowInstance]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[NotificationOutbox]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE [WorkflowDefinition]", script, StringComparison.Ordinal);
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=WorkflowSchemaScriptTest;Integrated Security=true;TrustServerCertificate=True;Encrypt=False",
                sqlOptions => sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo"))
            .Options;

        return new WorkflowDbContext(options);
    }
}
