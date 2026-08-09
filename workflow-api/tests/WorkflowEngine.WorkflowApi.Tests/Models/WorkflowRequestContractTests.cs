using System.Text.Json;
using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Tests.Models;

public sealed class WorkflowRequestContractTests
{
    [Fact]
    public void WorkflowStartRequestExposesOnlyTheOptimizedPublicFields()
    {
        Assert.Equal(
            ["ActionKey", "BusinessObjectId", "Notification", "SlaTargetDate", "WorkflowKey"],
            PublicPropertyNames<WorkflowStartRequest>());
    }

    [Fact]
    public void WorkflowActionRequestExposesOnlyTheOptimizedPublicFields()
    {
        Assert.Equal(
            ["ActionKey", "AssignedRoleKey", "Notification", "SlaTargetDate", "WorkflowInstanceId"],
            PublicPropertyNames<WorkflowActionRequest>());
    }

    [Fact]
    public void OptimizedStartJsonDeserializesNamedRecipientInputsWithoutChangingOpaqueAddresses()
    {
        const string json = """
            {
              "workflowKey": "PortfolioTrade",
              "businessObjectId": "PT-1001",
              "actionKey": "SubmitForChecker",
              "slaTargetDate": "2030-01-20",
              "notification": {
                "recipientInputs": {
                  "originalMaker": [
                    {
                      "userId": "maker-1",
                      "emailAddress": " opaque-address-value ",
                      "displayName": "Maker One"
                    }
                  ]
                },
                "templateData": {
                  "tradeId": "PT-1001"
                }
              }
            }
            """;

        var request = JsonSerializer.Deserialize<WorkflowStartRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("SubmitForChecker", request.ActionKey);
        Assert.Equal(new DateOnly(2030, 1, 20), request.SlaTargetDate);
        var maker = Assert.Single(request.Notification?.RecipientInputs?["originalMaker"] ?? []);
        Assert.Equal(" opaque-address-value ", maker.EmailAddress);
        Assert.Equal("PT-1001", request.Notification?.TemplateData?["tradeId"]);
    }

    [Theory]
    [InlineData("businessObjectType")]
    [InlineData("initialActionKey")]
    [InlineData("idempotencyKey")]
    [InlineData("slaInput")]
    [InlineData("notificationContext")]
    [InlineData("notificationRecipients")]
    public void WorkflowStartRequestRejectsRemovedPublicFields(string removedField)
    {
        var json = $$"""
            {
              "workflowKey": "PortfolioTrade",
              "businessObjectId": "PT-1001",
              "{{removedField}}": null
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowStartRequest>(json));
    }

    [Theory]
    [InlineData("idempotencyKey")]
    [InlineData("slaInput")]
    [InlineData("notificationContext")]
    [InlineData("notificationRecipients")]
    public void WorkflowActionRequestRejectsRemovedPublicFields(string removedField)
    {
        var json = $$"""
            {
              "workflowInstanceId": "WF-PT-1001",
              "actionKey": "SubmitForChecker",
              "{{removedField}}": null
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowActionRequest>(json));
    }

    [Fact]
    public void NotificationRequestRejectsUnknownNestedFields()
    {
        const string json = """
            {
              "workflowInstanceId": "WF-PT-1001",
              "actionKey": "SubmitForChecker",
              "notification": {
                "to": ["maker@example.test"]
              }
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowActionRequest>(json));
    }

    [Fact]
    public void RecipientRequestRejectsUnknownNestedFields()
    {
        const string json = """
            {
              "workflowInstanceId": "WF-PT-1001",
              "actionKey": "SubmitForChecker",
              "notification": {
                "recipientInputs": {
                  "selectedCcUsers": [
                    {
                      "emailAddress": "opaque",
                      "role": "Maker"
                    }
                  ]
                }
              }
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowActionRequest>(json));
    }

    private static string[] PublicPropertyNames<T>()
    {
        return typeof(T)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
