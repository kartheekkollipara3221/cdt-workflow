using Microsoft.Data.SqlClient;

namespace WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

public interface IWorkflowSqlConnectionFactory
{
    SqlConnection CreateConnection();
}
