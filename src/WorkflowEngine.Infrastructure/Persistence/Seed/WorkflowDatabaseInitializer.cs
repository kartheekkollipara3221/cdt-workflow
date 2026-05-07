using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Persistence;

namespace WorkflowEngine.Infrastructure.Persistence.Seed;

public sealed class WorkflowDatabaseInitializer(
    WorkflowDbContext dbContext,
    WorkflowSeedService seedService) : IWorkflowDatabaseInitializer
{
    public string Initialize()
    {
        dbContext.Database.Migrate();
        seedService.SeedPortfolioTrade();

        return "Database initialized and Portfolio Trade seed data applied.";
    }
}

