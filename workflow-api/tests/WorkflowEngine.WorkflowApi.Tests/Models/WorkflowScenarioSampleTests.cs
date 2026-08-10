using System.Text.Json;
using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Tests.Models;

public sealed class WorkflowScenarioSampleTests
{
    private static readonly ScenarioExpectation[] Expectations =
    [
        new("01-start-only", ScenarioEndpoint.Start, null, null, false, "Maker"),
        new("02-start-save-draft", ScenarioEndpoint.Start, "SaveDraft", null, false, "Maker"),
        new("03-start-submit-for-checker", ScenarioEndpoint.Start, "SubmitForChecker", null, false, "Maker"),
        new("04-draft-save-draft", ScenarioEndpoint.Action, "SaveDraft", null, false, "Maker"),
        new("05-draft-submit-for-checker", ScenarioEndpoint.Action, "SubmitForChecker", null, false, "Maker"),
        new("06-returned-to-maker-save-draft", ScenarioEndpoint.Action, "SaveDraft", null, false, "Maker"),
        new("07-returned-to-maker-resubmit-for-checker", ScenarioEndpoint.Action, "ResubmitForChecker", null, false, "Maker"),
        new("08-checker-return-to-maker", ScenarioEndpoint.Action, "CheckerReturnToMaker", null, false, "Checker"),
        new("09-checker-send-to-trading-taxable", ScenarioEndpoint.Action, "CheckerSendToTradingDesk", "TradingDeskTaxable", true, "Checker"),
        new("10-checker-send-to-trading-tax-exempt", ScenarioEndpoint.Action, "CheckerSendToTradingDesk", "TradingDeskTaxExempt", true, "Checker"),
        new("11-checker-send-to-trading-equity", ScenarioEndpoint.Action, "CheckerSendToTradingDesk", "TradingDeskEquity", true, "Checker"),
        new("12-trading-desk-sla-sync-no-change", ScenarioEndpoint.Action, null, null, true, "TradingDesk"),
        new("13-trading-desk-sla-reschedule", ScenarioEndpoint.Action, null, null, true, "TradingDesk"),
        new("14-trading-desk-return-to-maker", ScenarioEndpoint.Action, "TradingReturnToMaker", null, false, "TradingDesk"),
        new("15-trading-desk-trade-executed", ScenarioEndpoint.Action, "TradingTradeExecuted", null, false, "TradingDesk"),
        new("16-bdrm-trade-fulfilled", ScenarioEndpoint.Action, "BdrmTradeFulfilled", null, false, "BDRMClientService"),
        new("17-bdrm-trade-not-executed-properly", ScenarioEndpoint.Action, "BdrmTradeNotExecutedProperly", null, false, "BDRMClientService"),
        new("18-trading-desk-rework-trade-executed", ScenarioEndpoint.Action, "TradingTradeExecuted", null, false, "TradingDesk")
    ];

    private static readonly HashSet<string> ForbiddenProperties = new(StringComparer.Ordinal)
    {
        "businessObjectType",
        "initialActionKey",
        "idempotencyKey",
        "slaInput",
        "dueAtUtc",
        "notificationContext",
        "notificationRecipients",
        "toState",
        "workflow",
        "options"
    };

    [Fact]
    public void ScenarioLibraryCoversEveryApprovedPublicRequestShape()
    {
        var scenarioRoot = ScenarioRoot();
        Assert.True(File.Exists(Path.Combine(scenarioRoot, "README.md")), "The scenario-library root README is missing.");

        var actualDirectories = Directory
            .GetDirectories(scenarioRoot)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expectedDirectories = Expectations
            .Select(expectation => expectation.DirectoryName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedDirectories, actualDirectories);

        foreach (var expectation in Expectations)
        {
            VerifyScenarioPack(scenarioRoot, expectation);
        }
    }

    [Theory]
    [InlineData("15-trading-desk-trade-executed")]
    [InlineData("18-trading-desk-rework-trade-executed")]
    public void TradingExecutionScenariosSupplySelectedCcUsers(string directoryName)
    {
        var json = File.ReadAllText(Path.Combine(ScenarioRoot(), directoryName, "request.json"));
        var request = JsonSerializer.Deserialize<WorkflowActionRequest>(json);

        var recipients = request?.Notification?.RecipientInputs?["selectedCcUsers"] ?? [];
        Assert.Equal(2, recipients.Count);
        Assert.All(recipients, recipient =>
        {
            Assert.False(string.IsNullOrWhiteSpace(recipient.UserId));
            Assert.False(string.IsNullOrWhiteSpace(recipient.EmailAddress));
            Assert.False(string.IsNullOrWhiteSpace(recipient.DisplayName));
        });
    }

    private static void VerifyScenarioPack(string scenarioRoot, ScenarioExpectation expectation)
    {
        var directory = Path.Combine(scenarioRoot, expectation.DirectoryName);
        var requestPath = Path.Combine(directory, "request.json");
        var httpPath = Path.Combine(directory, "request.http");
        var readmePath = Path.Combine(directory, "README.md");

        Assert.True(File.Exists(requestPath), $"Missing request body: {requestPath}");
        Assert.True(File.Exists(httpPath), $"Missing executable request: {httpPath}");
        Assert.True(File.Exists(readmePath), $"Missing scenario documentation: {readmePath}");

        var json = File.ReadAllText(requestPath);
        using var document = JsonDocument.Parse(json);
        var forbiddenProperty = PropertyNames(document.RootElement)
            .FirstOrDefault(ForbiddenProperties.Contains);
        Assert.Null(forbiddenProperty);

        if (expectation.Endpoint == ScenarioEndpoint.Start)
        {
            VerifyStartRequest(json, expectation);
        }
        else
        {
            VerifyActionRequest(json, expectation);
        }

        var http = File.ReadAllText(httpPath);
        var endpoint = expectation.Endpoint == ScenarioEndpoint.Start
            ? "WorkflowStart"
            : "WorkflowAction";
        Assert.Contains($"POST {{{{baseUrl}}}}/v1/{endpoint}", http, StringComparison.Ordinal);
        Assert.Contains("X-Workflow-UserId:", http, StringComparison.Ordinal);
        Assert.Contains($"X-Workflow-Roles: {expectation.ActorRole}", http, StringComparison.Ordinal);
        Assert.Contains("X-Workflow-DisplayName:", http, StringComparison.Ordinal);
        Assert.Contains("< ./request.json", http, StringComparison.Ordinal);

        var readme = File.ReadAllText(readmePath);
        Assert.Contains("## Prerequisite", readme, StringComparison.Ordinal);
        Assert.Contains("## Expected effects", readme, StringComparison.Ordinal);
    }

    private static void VerifyStartRequest(string json, ScenarioExpectation expectation)
    {
        var request = JsonSerializer.Deserialize<WorkflowStartRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("PortfolioTrade", request.WorkflowKey);
        Assert.False(string.IsNullOrWhiteSpace(request.BusinessObjectId));
        Assert.Equal(expectation.ActionKey, request.ActionKey);
        Assert.Equal(expectation.HasSlaTargetDate, request.SlaTargetDate is not null);
        var maker = Assert.Single(request.Notification?.RecipientInputs?["originalMaker"] ?? []);
        Assert.False(string.IsNullOrWhiteSpace(maker.UserId));
        Assert.False(string.IsNullOrWhiteSpace(maker.EmailAddress));
    }

    private static void VerifyActionRequest(string json, ScenarioExpectation expectation)
    {
        var request = JsonSerializer.Deserialize<WorkflowActionRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("{{workflowInstanceId}}", request.WorkflowInstanceId);
        Assert.Equal(expectation.ActionKey, request.ActionKey);
        Assert.Equal(expectation.AssignedRoleKey, request.AssignedRoleKey);
        Assert.Equal(expectation.HasSlaTargetDate, request.SlaTargetDate is not null);
    }

    private static IEnumerable<string> PropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nestedProperty in PropertyNames(property.Value))
                {
                    yield return nestedProperty;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nestedProperty in PropertyNames(item))
                {
                    yield return nestedProperty;
                }
            }
        }
    }

    private static string ScenarioRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PROJECT.md")))
        {
            directory = directory.Parent;
        }

        var repositoryRoot = directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
        return Path.Combine(repositoryRoot, "workflow-api", "samples", "scenarios");
    }

    private enum ScenarioEndpoint
    {
        Start,
        Action
    }

    private sealed record ScenarioExpectation(
        string DirectoryName,
        ScenarioEndpoint Endpoint,
        string? ActionKey,
        string? AssignedRoleKey,
        bool HasSlaTargetDate,
        string ActorRole);
}
