using Microsoft.Data.SqlClient;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

namespace WorkflowEngine.Infrastructure.Sla;

public sealed class RunSlaService(DatabaseFirstWorkflowStore store) : IRunSlaService
{
    public Task<RunSlaResult> RunAsync(RunSlaRequest request, CancellationToken cancellationToken)
    {
        var now = request.Now.ToUniversalTime();
        var summary = store.GetSlaScanSummary(now);
        var overdueRows = store.GetSlaOverdueCandidates(now);

        var queued = new List<RunSlaQueuedNotification>();
        var warnings = new List<string>();

        foreach (var row in overdueRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var inserted = store.InsertSlaOverdueNotification(row.WorkflowTaskId, now);
                if (inserted is not null)
                {
                    queued.Add(inserted);
                }
            }
            catch (SqlException exception) when (IsSlaOutboxUniqueViolation(exception))
            {
                warnings.Add($"task {row.WorkflowTaskId} already has an SlaTaskOverdue notification");
            }
        }

        return Task.FromResult(new RunSlaResult(now, summary.PendingTasksWithSla, summary.AlreadyQueued, queued, warnings));
    }

    private static bool IsSlaOutboxUniqueViolation(SqlException exception)
    {
        return exception.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627);
    }
}
