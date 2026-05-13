using Microsoft.Data.SqlClient;

namespace WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

public sealed class WorkflowSqlConnectionFactory(string connectionString) : IWorkflowSqlConnectionFactory
{
    public SqlConnection CreateConnection()
    {
        return new SqlConnection(connectionString);
    }
}
