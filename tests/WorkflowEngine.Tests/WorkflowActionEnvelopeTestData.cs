using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Tests;

internal static class WorkflowActionEnvelopeTestData
{
    public static string ValidSubmitJson => """
        {
          "request": {
            "commandType": "ExecuteWorkflowAction",
            "commandVersion": 1,
            "clientActionId": "client-submit-1",
            "correlationId": "corr-submit-1",
            "requestedAtUtc": "2030-01-15T15:30:00Z",
            "sourceSystem": "PortfolioTradeConsoleTest"
          },
          "businessObject": {
            "objectType": "PortfolioTrade",
            "objectId": "PT-1001",
            "objectVersion": 7
          },
          "actor": {
            "userId": "maker1",
            "displayName": "Maker User One",
            "roles": ["Maker"]
          },
          "uiAction": {
            "sectionKey": "Maker",
            "buttonKey": "MakerPrimary",
            "buttonLabelShown": "Submit for Checker",
            "saveAsDraft": false
          },
          "dataSave": {
            "sectionKey": "Maker",
            "payload": {
              "customerName": "ABC Capital",
              "portfolioId": "PORT-123",
              "tradeAmount": 1000000
            }
          },
          "workflow": {
            "workflowKey": "PortfolioTrade",
            "workflowInstanceId": "WF-PT-1001",
            "workflowDefinitionVersion": 1,
            "expectedCurrentState": "Draft",
            "expectedCurrentTaskId": null,
            "expectedCurrentTaskKey": null,
            "expectedInstanceVersion": 1,
            "actionKey": "SubmitForChecker",
            "actionReason": "Maker submitted the portfolio trade for Checker review.",
            "notificationRecipients": {
              "toEmailAddresses": [
                "checker.primary@example.com",
                "checker.secondary@example.com"
              ],
              "ccEmailAddresses": [
                "maker.manager@example.com",
                "workflow.operations@example.com"
              ]
            },
            "idempotencyKey": "idem-submit-1"
          }
        }
        """;

    public static WorkflowActionEnvelope DeserializeValid()
    {
        return JsonSerializer.Deserialize<WorkflowActionEnvelope>(ValidSubmitJson, WorkflowJson.Options)
            ?? throw new InvalidOperationException("Test JSON failed to deserialize.");
    }

    public static string WriteTempEnvelope(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}

