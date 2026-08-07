using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.WorkflowContext;
using WorkflowEngine.Infrastructure.WorkflowExecution;
using WorkflowEngine.Infrastructure.WorkflowStart;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowStartInitialActionTests
{
    [Fact]
    public void StartWithInitialSubmitMovesToCheckerReviewWithoutMockSectionData()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var service = CreateService(dbContext);

        var result = service.Start(new StartWorkflowRequest(
            "PortfolioTrade",
            "PortfolioTrade",
            "PT-START-SUBMIT",
            "maker1",
            Reset: false,
            InitialActionKey: "SubmitForChecker",
            StartedByDisplayName: "Maker One",
            StartedByRoles: ["Maker"],
            NotificationContext: NotificationContext(),
            NotificationRecipients: new NotificationRecipients
            {
                ToEmailAddresses = [" checker.one@example.test ", "checker.one@example.test"],
                CcEmailAddresses = ["manager@example.test", "operations@example.test"]
            }));

        Assert.True(result.Envelope.Result.Success);
        Assert.Equal("SubmitForChecker", result.Envelope.Result.ActionKey);
        Assert.Equal("Draft", result.Envelope.Result.OldState);
        Assert.Equal("CheckerReview", result.Envelope.Result.NewState);
        Assert.NotNull(result.Envelope.WorkflowContext);

        var instance = Assert.Single(dbContext.WorkflowInstances);
        Assert.Equal("CheckerReview", instance.CurrentStateKey);
        Assert.Equal(2, instance.Version);

        var task = Assert.Single(dbContext.WorkflowTasks);
        Assert.Equal("CheckerReviewTask", task.TaskKey);
        Assert.Equal("Pending", task.Status);
        var recipients = JsonSerializer.Deserialize<NotificationRecipients>(
            task.NotificationRecipientsJson,
            WorkflowJson.Options);
        Assert.NotNull(recipients);
        Assert.Equal(
            [" checker.one@example.test ", "checker.one@example.test"],
            recipients.ToEmailAddresses);
        Assert.Equal(
            ["manager@example.test", "operations@example.test"],
            recipients.CcEmailAddresses);

        var notification = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal("WorkflowTaskAssigned", notification.NotificationType);
        Assert.Equal(task.NotificationRecipientsJson, notification.NotificationRecipientsJson);

        Assert.Single(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartWithInitialSubmitPreservesExplicitNullRecipientArray(bool nullToEmailAddresses)
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var service = CreateService(dbContext);
        var recipients = new NotificationRecipients
        {
            ToEmailAddresses = nullToEmailAddresses
                ? null!
                : [" first@example.test ", "first@example.test"],
            CcEmailAddresses = nullToEmailAddresses
                ? [" manager@example.test ", "manager@example.test"]
                : null!
        };
        var expectedSnapshot = JsonSerializer.Serialize(recipients, WorkflowJson.Options);

        var result = service.Start(new StartWorkflowRequest(
            "PortfolioTrade",
            "PortfolioTrade",
            nullToEmailAddresses ? "PT-START-NULL-TO" : "PT-START-NULL-CC",
            "maker1",
            Reset: false,
            InitialActionKey: "SubmitForChecker",
            StartedByDisplayName: "Maker One",
            StartedByRoles: ["Maker"],
            NotificationContext: NotificationContext(),
            NotificationRecipients: recipients));

        Assert.True(result.Envelope.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks);
        var notification = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal(expectedSnapshot, task.NotificationRecipientsJson);
        Assert.Equal(task.NotificationRecipientsJson, notification.NotificationRecipientsJson);
        var persistedRecipients = JsonSerializer.Deserialize<NotificationRecipients>(
            task.NotificationRecipientsJson,
            WorkflowJson.Options);
        Assert.NotNull(persistedRecipients);
        if (nullToEmailAddresses)
        {
            Assert.Null(persistedRecipients.ToEmailAddresses);
            Assert.Equal(
                [" manager@example.test ", "manager@example.test"],
                persistedRecipients.CcEmailAddresses);
        }
        else
        {
            Assert.Equal(
                [" first@example.test ", "first@example.test"],
                persistedRecipients.ToEmailAddresses);
            Assert.Null(persistedRecipients.CcEmailAddresses);
        }
    }

    [Fact]
    public void StartWithInvalidInitialActionRollsBackCreatedInstance()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var service = CreateService(dbContext);

        var result = service.Start(new StartWorkflowRequest(
            "PortfolioTrade",
            "PortfolioTrade",
            "PT-BAD-ACTION",
            "maker1",
            Reset: false,
            InitialActionKey: "CheckerReturnToMaker",
            StartedByDisplayName: "Maker One",
            StartedByRoles: ["Maker"]));

        Assert.False(result.Envelope.Result.Success);
        Assert.Equal("InvalidTransition", result.Envelope.Result.ResultCode);
        Assert.Empty(dbContext.WorkflowInstances);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.IdempotencyRecords);
    }

    [Fact]
    public void StartWithInitialActionMergesDuplicateNotificationContextKeysAfterTrim()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var service = CreateService(dbContext);

        var notificationContext = NotificationContext();
        notificationContext["deskName"] = "Original";
        notificationContext[" deskName "] = " Taxable ";
        notificationContext["emptyValue"] = " ";

        var result = service.Start(new StartWorkflowRequest(
            "PortfolioTrade",
            "PortfolioTrade",
            "PT-START-CONTEXT",
            "maker1",
            Reset: false,
            InitialActionKey: "SubmitForChecker",
            StartedByDisplayName: "Maker One",
            StartedByRoles: ["Maker"],
            NotificationContext: notificationContext));

        Assert.True(result.Envelope.Result.Success);
        Assert.Equal("SubmitForChecker", result.Envelope.Result.ActionKey);
        Assert.Single(dbContext.WorkflowInstances);
        Assert.Single(dbContext.WorkflowTasks);
    }

    [Fact]
    public void StartInitialActionCopiesSlaInputIntoCreatedTaskSchedule()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var checkerPolicy = dbContext.SlaPolicies.Single(row => row.SlaPolicyKey == "CheckerReviewSla");
        checkerPolicy.CalculationMode = "ApplicationTargetDate";
        checkerPolicy.DurationMinutes = null;
        checkerPolicy.SendDueDateReminder = true;
        checkerPolicy.ReminderLocalTime = new TimeOnly(9, 0);
        checkerPolicy.TimeZoneId = "America/New_York";
        checkerPolicy.OverdueStartOffsetDays = 1;
        checkerPolicy.RecurrenceIntervalDays = 1;
        checkerPolicy.DueNotificationRouteKey = "RouteSlaTaskDueToday";
        checkerPolicy.OverdueNotificationRouteKey = "RouteSlaTaskOverdue";
        dbContext.SaveChanges();
        var service = CreateService(dbContext);

        var result = service.Start(new StartWorkflowRequest(
            "PortfolioTrade",
            "PortfolioTrade",
            "PT-START-SLA",
            "maker1",
            Reset: false,
            InitialActionKey: "SubmitForChecker",
            StartedByDisplayName: "Maker One",
            StartedByRoles: ["Maker"],
            NotificationContext: NotificationContext(),
            SlaInput: new WorkflowSlaInput
            {
                TargetDate = new DateOnly(2030, 1, 20)
            }));

        Assert.True(result.Envelope.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks);
        Assert.Equal(DateTimeOffset.Parse("2030-01-21T14:00:00Z"), task.DueAtUtc);
        var schedule = Assert.Single(dbContext.WorkflowTaskSlaSchedules);
        Assert.Equal(new DateOnly(2030, 1, 20), schedule.TargetDate);
    }

    private static StartWorkflowService CreateService(WorkflowDbContext dbContext)
    {
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2030-01-15T15:30:00Z"));
        var resultBuilder = new WorkflowActionResultBuilder();
        var contextReader = new WorkflowContextReader(dbContext, timeProvider);
        var executionService = new WorkflowExecutionService(
            dbContext,
            contextReader,
            timeProvider,
            resultBuilder,
            new CanonicalWorkflowResultSerializer(),
            new NotificationTemplateRenderer());

        return new StartWorkflowService(
            dbContext,
            timeProvider,
            resultBuilder,
            new WorkflowActionEnvelopeNormalizer(),
            new CanonicalRequestHasher(),
            executionService);
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
}
