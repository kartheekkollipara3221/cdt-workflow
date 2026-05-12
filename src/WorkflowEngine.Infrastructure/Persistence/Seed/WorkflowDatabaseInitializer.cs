using WorkflowEngine.Application.Persistence;

namespace WorkflowEngine.Infrastructure.Persistence.Seed;

public sealed class WorkflowDatabaseInitializer(
    WorkflowDbContext dbContext,
    WorkflowSeedService seedService) : IWorkflowDatabaseInitializer
{
    public string Initialize()
    {
        WorkflowSchemaInitializer.Apply(dbContext.Database.GetConnectionString()!);
        seedService.SeedPortfolioTrade();

        return "Database initialized and Portfolio Trade seed data applied.";
    }
}

