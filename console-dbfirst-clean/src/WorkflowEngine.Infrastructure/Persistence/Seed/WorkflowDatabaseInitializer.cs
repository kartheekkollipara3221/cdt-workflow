using WorkflowEngine.Application.Persistence;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

namespace WorkflowEngine.Infrastructure.Persistence.Seed;

public sealed class WorkflowDatabaseInitializer(
    DatabaseFirstSchemaInitializer schemaInitializer,
    WorkflowSeedService seedService) : IWorkflowDatabaseInitializer
{
    public string Initialize()
    {
        schemaInitializer.InitializeSchema();
        seedService.SeedPortfolioTrade();

        return "Database initialized from database-first schema/stored procedure artifacts and Portfolio Trade seed data applied.";
    }
}
