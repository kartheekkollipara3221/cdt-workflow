using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Persistence;

namespace WorkflowEngine.Infrastructure.Persistence.Seed;

public sealed class WorkflowDatabaseInitializer(
    WorkflowDbContext dbContext,
    WorkflowSeedService seedService) : IWorkflowDatabaseInitializer, IPortfolioTradeVersionInitializer
{
    public string Initialize()
    {
        dbContext.Database.Migrate();
        seedService.SeedPortfolioTrade();

        return "Database initialized and Portfolio Trade seed data applied.";
    }

    public string Initialize(int activePortfolioTradeVersion)
    {
        if (activePortfolioTradeVersion is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activePortfolioTradeVersion),
                "Supported Portfolio Trade versions are 1 and 2.");
        }

        dbContext.Database.Migrate();
        seedService.SeedPortfolioTrade();
        seedService.SeedPortfolioTradeVersion2(activate: false);
        seedService.ActivatePortfolioTradeVersion(activePortfolioTradeVersion);

        return $"Database initialized and Portfolio Trade seed data applied; active version {activePortfolioTradeVersion}.";
    }
}

