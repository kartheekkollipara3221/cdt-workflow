using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.WorkflowContext;
using WorkflowEngine.Infrastructure.WorkflowExecution;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowOptionsConflictTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public void CloseCurrentTask_FalseOnTaskClosingTransition_PrintsWarning_StillClosesTask()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstanceWithTask(dbContext, "CheckerReview", 2, "CheckerReviewTask", "Checker");

        var result = ExecuteWithCapturedConsole(
            dbContext,
            Envelope("CheckerReturnToMaker", "Checker", "CheckerPrimary", "CheckerReview", 2, "CheckerReviewTask", ["Checker"], "checker1", new() { CloseCurrentTask = false }));

        Assert.Contains("WARN: workflow.options.closeCurrentTask = false conflicts with metadata; using metadata", result.Stderr);
        Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "CheckerReviewTask" && row.Status == "Completed");
    }

    [Fact]
    public void CloseCurrentTask_TrueOnNonTaskClosingTransition_PrintsWarning_DoesNotCreateClose()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope(options: new() { CloseCurrentTask = true }));

        Assert.Contains("WARN: workflow.options.closeCurrentTask = true conflicts with metadata; using metadata", result.Stderr);
        Assert.Empty(dbContext.WorkflowTasks.Where(row => row.Status == "Completed"));
    }

    [Fact]
    public void CreateNextTask_FalseOnTaskCreatingTransition_PrintsWarning_StillCreatesTask()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope(options: new() { CreateNextTask = false }));

        Assert.Contains("WARN: workflow.options.createNextTask = false conflicts with metadata; using metadata", result.Stderr);
        Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "CheckerReviewTask" && row.Status == "Pending");
    }

    [Fact]
    public void CreateNextTask_TrueOnNonTaskCreatingTransition_PrintsWarning_DoesNotCreate()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope("SaveDraft", saveAsDraft: true, options: new() { CreateNextTask = true }));

        Assert.Contains("WARN: workflow.options.createNextTask = true conflicts with metadata; using metadata", result.Stderr);
        Assert.Empty(dbContext.WorkflowTasks);
    }

    [Fact]
    public void QueueNotifications_FalseOnNotificationTransition_PrintsWarning_StillQueues()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope(options: new() { QueueNotifications = false }));

        Assert.Contains("WARN: workflow.options.queueNotifications = false conflicts with metadata; using metadata", result.Stderr);
        Assert.Single(dbContext.NotificationOutbox);
    }

    [Fact]
    public void QueueNotifications_TrueOnNoNotificationTransition_PrintsWarning_DoesNotQueue()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope("SaveDraft", saveAsDraft: true, options: new() { QueueNotifications = true }));

        Assert.Contains("WARN: workflow.options.queueNotifications = true conflicts with metadata; using metadata", result.Stderr);
        Assert.Empty(dbContext.NotificationOutbox);
    }

    [Fact]
    public void EvaluateSla_FalseOnSlaTask_PrintsWarning_StillSetsDueAtUtc()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope(options: new() { EvaluateSla = false }));

        Assert.Contains("WARN: workflow.options.evaluateSla = false conflicts with metadata; using metadata", result.Stderr);
        Assert.NotNull(dbContext.WorkflowTasks.Single(row => row.TaskKey == "CheckerReviewTask").DueAtUtc);
    }

    [Fact]
    public void EvaluateSla_TrueOnNoSlaTask_PrintsWarning_DueAtUtcStillNull()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstanceWithTask(dbContext, "CheckerReview", 2, "CheckerReviewTask", "Checker");

        var result = ExecuteWithCapturedConsole(
            dbContext,
            Envelope("CheckerReturnToMaker", "Checker", "CheckerPrimary", "CheckerReview", 2, "CheckerReviewTask", ["Checker"], "checker1", new() { EvaluateSla = true }));

        Assert.Contains("WARN: workflow.options.evaluateSla = true conflicts with metadata; using metadata", result.Stderr);
        Assert.Null(dbContext.WorkflowTasks.Single(row => row.TaskKey == "MakerReworkTask").DueAtUtc);
    }

    [Fact]
    public void WriteAudit_False_PrintsWarning_StillWritesAudit()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope("SaveDraft", saveAsDraft: true, options: new() { WriteAudit = false }));

        Assert.Contains("WARN: workflow.options.writeAudit = false conflicts with metadata; using metadata", result.Stderr);
        Assert.Single(dbContext.WorkflowAudits);
    }

    [Fact]
    public void WriteAudit_True_NoWarning()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope("SaveDraft", saveAsDraft: true, options: new() { WriteAudit = true }));

        Assert.DoesNotContain("workflow.options.writeAudit", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void AllOptionsMatchMetadata_NoWarnings()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope(options: new()
        {
            CloseCurrentTask = false,
            CreateNextTask = true,
            QueueNotifications = true,
            EvaluateSla = true,
            WriteAudit = true
        }));

        Assert.DoesNotContain("WARN: workflow.options.", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleConflicts_AllPrintWarnings()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope("SaveDraft", saveAsDraft: true, options: new()
        {
            CloseCurrentTask = true,
            CreateNextTask = true,
            QueueNotifications = true
        }));

        var warnings = result.Stderr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, warnings.Length);
        Assert.Contains("WARN: workflow.options.closeCurrentTask = true conflicts with metadata; using metadata", warnings);
        Assert.Contains("WARN: workflow.options.createNextTask = true conflicts with metadata; using metadata", warnings);
        Assert.Contains("WARN: workflow.options.queueNotifications = true conflicts with metadata; using metadata", warnings);
    }

    [Fact]
    public void WarningsGoToStderr_NotStdout()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope(options: new() { CreateNextTask = false }));

        Assert.Contains("WARN: workflow.options.createNextTask = false conflicts with metadata; using metadata", result.Stderr);
        Assert.DoesNotContain("WARN: workflow.options.", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void WarningsDoNotAffectResultCode()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "Draft", 1);
        dbContext.SaveChanges();

        var result = ExecuteWithCapturedConsole(dbContext, Envelope(options: new() { CreateNextTask = false }));

        Assert.True(result.Result.Result.Success);
        Assert.Equal("Success", result.Result.Result.ResultCode);
    }

    private static CapturedExecution ExecuteWithCapturedConsole(WorkflowDbContext dbContext, WorkflowActionEnvelope envelope)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var normalized = new WorkflowActionEnvelopeNormalizer().Normalize(envelope, null, null);
            var result = CreateService(dbContext).Execute(new WorkflowExecutionRequest(
                normalized,
                new CanonicalRequestHasher().ComputeHash(normalized.Envelope),
                AdminOverride: false));
            return new CapturedExecution(result, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static WorkflowExecutionService CreateService(WorkflowDbContext dbContext)
    {
        return new WorkflowExecutionService(
            dbContext,
            new WorkflowContextReader(dbContext, new FixedTimeProvider(Now)),
            new FixedTimeProvider(Now),
            new WorkflowActionResultBuilder(),
            new CanonicalWorkflowResultSerializer(),
            new NotificationTemplateRenderer());
    }

    private static WorkflowActionEnvelope Envelope(
        string actionKey = "SubmitForChecker",
        string sectionKey = "Maker",
        string buttonKey = "MakerPrimary",
        string expectedState = "Draft",
        int expectedVersion = 1,
        string? expectedTaskKey = null,
        string[]? roles = null,
        string userId = "maker1",
        WorkflowActionOptions? options = null,
        bool saveAsDraft = false)
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Actor!.UserId = userId;
        envelope.Actor.Roles = roles?.ToList() ?? ["Maker"];
        envelope.UiAction!.SectionKey = sectionKey;
        envelope.UiAction.ButtonKey = buttonKey;
        envelope.UiAction.SaveAsDraft = saveAsDraft;
        envelope.DataSave!.SectionKey = sectionKey;
        envelope.DataSave.Payload = JsonDocument.Parse("""{"comment":"options test"}""").RootElement.Clone();
        envelope.Workflow!.ActionKey = actionKey;
        envelope.Workflow.ExpectedCurrentState = expectedState;
        envelope.Workflow.ExpectedInstanceVersion = expectedVersion;
        envelope.Workflow.ExpectedCurrentTaskKey = expectedTaskKey;
        envelope.Workflow.IdempotencyKey = $"options-{Guid.NewGuid():N}";
        envelope.Workflow.Options = options;
        envelope.Workflow.NotificationContext = NotificationContext();
        return envelope;
    }

    private static Dictionary<string, string?> NotificationContext()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["referenceNumber"] = "1050",
            ["submitterDisplayName"] = "Kollipara, Kartheek",
            ["returnedByDisplayName"] = "Kollipara, Kartheek",
            ["recipientDisplayName"] = "Kollipara, Kartheek",
            ["traderDisplayName"] = "Kollipara, Kartheek",
            ["confirmerDisplayName"] = "Kollipara, Kartheek",
            ["deskName"] = "Taxable",
            ["securityType"] = "Other",
            ["accountShortName"] = "EFG",
            ["accountDisplayName"] = "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
            ["submittedDate"] = "4/24/2026",
            ["tradeLink"] = "https://sharepoint.example.test/trades/1050"
        };
    }

    private static void AddInstanceWithTask(
        WorkflowDbContext dbContext,
        string currentState,
        int version,
        string taskKey,
        string assignedRole)
    {
        AddInstance(dbContext, currentState, version);
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = "WF-PT-1001",
            TaskKey = taskKey,
            AssignedRoleKey = assignedRole,
            Status = "Pending",
            CreatedAtUtc = Now,
            DueAtUtc = Now.AddMinutes(2)
        });
        dbContext.SaveChanges();
    }

    private static void AddInstance(WorkflowDbContext dbContext, string currentState, int version)
    {
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = "WF-PT-1001",
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001",
            CurrentStateKey = currentState,
            Status = "Active",
            StartedAtUtc = Now.AddMinutes(-30),
            StartedByUserId = "maker1",
            Version = version
        });
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }

    private sealed record CapturedExecution(WorkflowActionResultEnvelope Result, string Stdout, string Stderr);
}
