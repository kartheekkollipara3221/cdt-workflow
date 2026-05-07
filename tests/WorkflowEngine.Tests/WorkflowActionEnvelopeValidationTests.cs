using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Tests;

public sealed class WorkflowActionEnvelopeValidationTests
{
    private readonly WorkflowActionEnvelopeValidator validator = new();

    [Fact]
    public void ValidEnvelopeHasNoValidationErrors()
    {
        var errors = validator.Validate(WorkflowActionEnvelopeTestData.DeserializeValid());

        Assert.Empty(errors);
    }

    [Fact]
    public void MissingActorReturnsValidationFailedError()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Actor = null;

        var errors = validator.Validate(envelope);

        Assert.Contains(errors, error => error.Field == "actor");
    }

    [Fact]
    public void EmptyActorRolesReturnValidationFailedError()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Actor!.Roles = [];

        var errors = validator.Validate(envelope);

        Assert.Contains(errors, error => error.Field == "actor.roles");
    }

    [Fact]
    public void MissingDataSaveReturnsValidationFailedError()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.DataSave = null;

        var errors = validator.Validate(envelope);

        Assert.Contains(errors, error => error.Field == "dataSave");
    }

    [Fact]
    public void NonObjectPayloadReturnsValidationFailedError()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.DataSave!.Payload = JsonDocument.Parse("\"not-object\"").RootElement.Clone();

        var errors = validator.Validate(envelope);

        Assert.Contains(errors, error => error.Field == "dataSave.payload");
    }

    [Fact]
    public void SaveDraftRequiresSaveAsDraftTrue()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Workflow!.ActionKey = "SaveDraft";
        envelope.UiAction!.SaveAsDraft = false;

        var errors = validator.Validate(envelope);

        Assert.Contains(errors, error => error.Field == "uiAction.saveAsDraft");
    }

    [Theory]
    [InlineData("SubmitForChecker")]
    [InlineData("ResubmitForChecker")]
    public void MakerSubmitActionsRequireSaveAsDraftFalse(string actionKey)
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Workflow!.ActionKey = actionKey;
        envelope.UiAction!.SaveAsDraft = true;

        var errors = validator.Validate(envelope);

        Assert.Contains(errors, error => error.Field == "uiAction.saveAsDraft");
    }
}

