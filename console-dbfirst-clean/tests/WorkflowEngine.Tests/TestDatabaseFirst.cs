using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

namespace WorkflowEngine.Tests;

internal static class TestDatabaseFirst
{
    public static void InitializeSchema(WorkflowDbContext dbContext)
    {
        new DatabaseFirstSchemaInitializer(dbContext).InitializeSchema();
    }
}
