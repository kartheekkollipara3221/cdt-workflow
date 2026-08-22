using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms.Contracts;
using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Sqms.LiveAcceptance;

public sealed class AcceptanceReporter(TextWriter output)
{
    private int sequence;

    public void WriteManifest(AcceptanceManifest manifest)
    {
        output.WriteLine($"runMarker={AcceptanceConstants.RunMarker}");
        foreach (var row in manifest.OpportunityIds.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"opportunity.{row.Key}.id={row.Value}");
        }

        foreach (var row in manifest.BidIds.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"bid.{row.Key}.id={row.Value}");
        }
    }

    public int WriteInput(
        string entityType,
        int targetId,
        string method,
        string? actionKey,
        int? expectedInputVersion,
        SqmsActor actor,
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> recipients,
        string expectedState)
    {
        var current = ++sequence;
        output.WriteLine($"[{current:000}] INPUT");
        output.WriteLine($"entityType={entityType}");
        output.WriteLine($"targetId={targetId}");
        output.WriteLine($"method={method}");
        output.WriteLine($"actionKey={actionKey ?? "START"}");
        output.WriteLine($"expectedVersion={expectedInputVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NONE"}");
        output.WriteLine($"actor={actor.UserId}|{actor.DisplayName}|{string.Join(',', actor.Roles)}");
        output.WriteLine($"recipientInputs={JsonSerializer.Serialize(recipients)}");
        output.WriteLine($"expectedState={expectedState}");
        return current;
    }

    public void WriteResult(
        int current,
        SqmsOperationResult result,
        string? targetStatus,
        string? targetWorkflowState,
        bool parity)
    {
        var workflow = result.Context?.WorkflowContext.Workflow;
        output.WriteLine($"[{current:000}] RESULT");
        output.WriteLine($"success={result.Success}");
        output.WriteLine($"code={result.ResultCode}");
        output.WriteLine($"message={result.Message}");
        output.WriteLine($"workflowState={workflow?.CurrentState}");
        output.WriteLine($"workflowVersion={workflow?.WorkflowInstanceVersion}");
        output.WriteLine($"targetStatus={targetStatus}");
        output.WriteLine($"targetWorkflowState={targetWorkflowState}");
        output.WriteLine($"parity={(parity ? "PASS" : "FAIL")}");
    }

    public void WriteScheduled(string stage, SqmsScheduledRunResult result)
    {
        output.WriteLine($"SCHEDULE stage={stage} now={result.Now:O}");
        output.WriteLine(
            $"closed={result.OpportunitiesClosed} ipg={result.BidsMovedToIpgReview} " +
            $"notAllocated={result.BidsNotAllocated} open={result.OpenNotificationsQueued} " +
            $"reminder={result.ReminderNotificationsQueued} alreadyQueued={result.AlreadyQueued} " +
            $"failures={result.Failures.Count}");
        foreach (var failure in result.Failures)
        {
            output.WriteLine(
                $"SCHEDULE_FAILURE entity={failure.EntityType}:{failure.EntityId} " +
                $"operation={failure.Operation} error={failure.Error}");
        }
    }

    public void WriteProjectionOwnership(ProjectionOwnershipEvidence evidence)
    {
        output.WriteLine(
            $"OWNERSHIP kind={evidence.EvidenceKind} entity={evidence.EntityType}:{evidence.TargetId} " +
            $"operation={evidence.Operation} state={evidence.ReturnedState} " +
            $"version={evidence.ReturnedVersion} unchanged={evidence.Unchanged}");
    }

    public void WriteOutbox(ProcessNotificationOutboxResult result)
    {
        output.WriteLine(
            $"OUTBOX sender={result.SenderName} pending={result.PendingCount} " +
            $"processed={result.Processed.Count} warnings={result.Warnings.Count} " +
            $"failure={(result.Failure is null ? "NONE" : result.Failure.DisplayReference)}");
    }

    public void WriteSummary(AcceptanceManifest manifest)
    {
        output.WriteLine("SQMS LIVE ACCEPTANCE PASS");
        output.WriteLine($"database={AcceptanceConstants.DatabaseName}");
        output.WriteLine($"marker={AcceptanceConstants.RunMarker}");
        output.WriteLine($"opportunities={manifest.OpportunityIds.Count}");
        output.WriteLine($"bids={manifest.BidIds.Count}");
        output.WriteLine($"emails={AcceptanceConstants.ExpectedEmailCount}");
        output.WriteLine($"smtp4dev={AcceptanceConstants.Smtp4DevBaseAddress}");
    }
}
