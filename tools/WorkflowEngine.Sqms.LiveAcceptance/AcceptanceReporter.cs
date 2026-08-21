using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms.Contracts;

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
}
