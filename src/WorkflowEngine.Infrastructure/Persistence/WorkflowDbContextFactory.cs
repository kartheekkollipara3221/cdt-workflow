using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkflowEngine.Infrastructure.Persistence;

public sealed class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=WorkflowEngineDesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new WorkflowDbContext(options);
    }
}

