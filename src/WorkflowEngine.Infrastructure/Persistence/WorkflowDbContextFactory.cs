using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WorkflowEngine.Domain.Workflow;

namespace WorkflowEngine.Infrastructure.Persistence;

public sealed class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    private const string FallbackConnectionString =
        "Server=DOLLY;Database=WorkflowEngineDesignTime;Integrated Security=true;TrustServerCertificate=True;Encrypt=False";

    public WorkflowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable($"ConnectionStrings__{WorkflowKeys.DefaultConnectionName}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    WorkflowDbContext.DefaultSchema))
            .Options;

        return new WorkflowDbContext(options);
    }
}
