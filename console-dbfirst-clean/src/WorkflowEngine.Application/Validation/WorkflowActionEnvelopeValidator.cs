using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Validation;

public sealed class WorkflowActionEnvelopeValidator
{
    public IReadOnlyList<WorkflowValidationError> Validate(WorkflowActionEnvelope? envelope)
    {
        var errors = new List<WorkflowValidationError>();
        if (envelope is null)
        {
            errors.Add(new WorkflowValidationError("$", "Envelope is required."));
            return errors;
        }

        ValidateRequest(envelope.Request, errors);
        ValidateBusinessObject(envelope.BusinessObject, errors);
        ValidateActor(envelope.Actor, errors);
        ValidateUiAction(envelope.UiAction, errors);
        ValidateDataSave(envelope.DataSave, errors);
        ValidateWorkflow(envelope.Workflow, errors);
        ValidateMakerPrimary(envelope, errors);

        return errors;
    }

    private static void ValidateRequest(WorkflowRequest? request, ICollection<WorkflowValidationError> errors)
    {
        if (request is null)
        {
            errors.Add(new WorkflowValidationError("request", "request block is required."));
            return;
        }

        RequireText(request.CommandType, "request.commandType", errors);
        RequireText(request.ClientActionId, "request.clientActionId", errors);
        RequireText(request.CorrelationId, "request.correlationId", errors);
    }

    private static void ValidateBusinessObject(WorkflowBusinessObject? businessObject, ICollection<WorkflowValidationError> errors)
    {
        if (businessObject is null)
        {
            errors.Add(new WorkflowValidationError("businessObject", "businessObject block is required."));
            return;
        }

        RequireText(businessObject.ObjectType, "businessObject.objectType", errors);
        RequireText(businessObject.ObjectId, "businessObject.objectId", errors);
    }

    private static void ValidateActor(WorkflowActor? actor, ICollection<WorkflowValidationError> errors)
    {
        if (actor is null)
        {
            errors.Add(new WorkflowValidationError("actor", "actor block is required."));
            return;
        }

        RequireText(actor.UserId, "actor.userId", errors);
        if (actor.Roles is null)
        {
            errors.Add(new WorkflowValidationError("actor.roles", "actor.roles is required."));
            return;
        }

        if (actor.Roles.Count == 0 || actor.Roles.All(string.IsNullOrWhiteSpace))
        {
            errors.Add(new WorkflowValidationError("actor.roles", "actor.roles must contain at least one role."));
        }
    }

    private static void ValidateUiAction(WorkflowUiAction? uiAction, ICollection<WorkflowValidationError> errors)
    {
        if (uiAction is null)
        {
            errors.Add(new WorkflowValidationError("uiAction", "uiAction block is required."));
            return;
        }

        RequireText(uiAction.SectionKey, "uiAction.sectionKey", errors);
        RequireText(uiAction.ButtonKey, "uiAction.buttonKey", errors);
    }

    private static void ValidateDataSave(WorkflowDataSave? dataSave, ICollection<WorkflowValidationError> errors)
    {
        if (dataSave is null)
        {
            errors.Add(new WorkflowValidationError("dataSave", "dataSave block is required."));
            return;
        }

        RequireText(dataSave.SectionKey, "dataSave.sectionKey", errors);
        if (dataSave.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            errors.Add(new WorkflowValidationError("dataSave.payload", "dataSave.payload is required."));
            return;
        }

        if (dataSave.Payload.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new WorkflowValidationError("dataSave.payload", "dataSave.payload must be a JSON object."));
        }
    }

    private static void ValidateWorkflow(WorkflowActionBlock? workflow, ICollection<WorkflowValidationError> errors)
    {
        if (workflow is null)
        {
            errors.Add(new WorkflowValidationError("workflow", "workflow block is required."));
            return;
        }

        RequireText(workflow.WorkflowKey, "workflow.workflowKey", errors);
        RequireText(workflow.WorkflowInstanceId, "workflow.workflowInstanceId", errors);
        RequireText(workflow.ActionKey, "workflow.actionKey", errors);
    }

    private static void ValidateMakerPrimary(WorkflowActionEnvelope envelope, ICollection<WorkflowValidationError> errors)
    {
        var actionKey = envelope.Workflow?.ActionKey;
        if (!string.Equals(envelope.UiAction?.ButtonKey, "MakerPrimary", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(actionKey))
        {
            return;
        }

        var uiAction = envelope.UiAction;
        if (uiAction is null)
        {
            return;
        }

        if (string.Equals(actionKey, "SaveDraft", StringComparison.Ordinal)
            && uiAction.SaveAsDraft != true)
        {
            errors.Add(new WorkflowValidationError("uiAction.saveAsDraft", "SaveDraft requires uiAction.saveAsDraft to be true."));
        }

        if ((string.Equals(actionKey, "SubmitForChecker", StringComparison.Ordinal)
                || string.Equals(actionKey, "ResubmitForChecker", StringComparison.Ordinal))
            && uiAction.SaveAsDraft != false)
        {
            errors.Add(new WorkflowValidationError("uiAction.saveAsDraft", $"{actionKey} requires uiAction.saveAsDraft to be false."));
        }
    }

    private static void RequireText(string? value, string field, ICollection<WorkflowValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new WorkflowValidationError(field, $"{field} is required."));
        }
    }
}
