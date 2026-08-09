using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.WorkflowStart;

public sealed class WorkflowParticipantCaptureService(
    WorkflowDbContext dbContext,
    TimeProvider timeProvider)
{
    public WorkflowParticipantCaptureResult Capture(
        long workflowDefinitionVersionId,
        string workflowInstanceId,
        WorkflowNotificationInput? notificationInput)
    {
        var input = notificationInput ?? new WorkflowNotificationInput();
        var definitions = dbContext.WorkflowParticipantDefinitions
            .Where(row =>
                row.WorkflowDefinitionVersionId == workflowDefinitionVersionId
                && row.CaptureStage == WorkflowParticipantCaptureStages.Start
                && row.IsActive)
            .OrderBy(row => row.ParticipantKey)
            .ToList();
        var participantKeys = definitions
            .Select(row => row.ParticipantKey)
            .ToHashSet(StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var definition in definitions)
        {
            input.RecipientInputs.TryGetValue(definition.ParticipantKey, out var supplied);
            supplied ??= [];

            var existing = dbContext.WorkflowParticipants.Any(row =>
                row.WorkflowInstanceId == workflowInstanceId
                && row.ParticipantKey == definition.ParticipantKey);
            if (existing)
            {
                errors.Add(definition.IsImmutable
                    ? $"Workflow participant '{definition.ParticipantKey}' is immutable and has already been captured."
                    : $"Workflow participant '{definition.ParticipantKey}' has already been captured.");
                continue;
            }

            if (definition.Cardinality == WorkflowParticipantCardinalities.Single)
            {
                if (definition.IsRequired && supplied.Count == 0)
                {
                    errors.Add($"Required workflow participant '{definition.ParticipantKey}' is missing.");
                }
                else if (supplied.Count > 1)
                {
                    errors.Add($"Workflow participant '{definition.ParticipantKey}' must contain exactly one value.");
                }
            }
            else if (definition.Cardinality == WorkflowParticipantCardinalities.Many
                     && definition.IsRequired
                     && supplied.Count == 0)
            {
                errors.Add($"Required workflow participant '{definition.ParticipantKey}' is missing.");
            }
        }

        var actionInputs = input.RecipientInputs
            .Where(pair => !participantKeys.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var actionNotificationInput = new WorkflowNotificationInput
        {
            RecipientInputs = actionInputs,
            TemplateData = input.TemplateData
        };

        if (errors.Count > 0)
        {
            return new WorkflowParticipantCaptureResult(actionNotificationInput, errors);
        }

        var createdAtUtc = timeProvider.GetUtcNow();
        foreach (var definition in definitions)
        {
            if (!input.RecipientInputs.TryGetValue(definition.ParticipantKey, out var supplied))
            {
                continue;
            }

            for (var index = 0; index < supplied.Count; index++)
            {
                var value = supplied[index];
                dbContext.WorkflowParticipants.Add(new WorkflowParticipant
                {
                    WorkflowInstanceId = workflowInstanceId,
                    ParticipantKey = definition.ParticipantKey,
                    UserId = value.UserId,
                    EmailAddress = value.EmailAddress ?? string.Empty,
                    DisplayName = value.DisplayName,
                    SortOrder = index + 1,
                    CreatedAtUtc = createdAtUtc
                });
            }
        }

        return new WorkflowParticipantCaptureResult(actionNotificationInput, []);
    }
}

public sealed record WorkflowParticipantCaptureResult(
    WorkflowNotificationInput ActionNotificationInput,
    IReadOnlyList<string> Errors);
