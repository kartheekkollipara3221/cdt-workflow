using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class Phase5OutboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");
    private const string RecipientSnapshot = """{"toEmailAddresses":["first@example.test","second@example.test"],"ccEmailAddresses":["manager@example.test","operations@example.test"]}""";

    [Fact]
    public async Task ProcessNotificationOutboxSendsPendingRowsInCreatedThenIdOrder()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-PT-2", "Second", Now.AddMinutes(-1));
        AddNotification(dbContext, "WF-PT-1", "First", Now.AddMinutes(-2));
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();

        var result = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Equal(2, result.PendingCount);
        Assert.Equal(["First", "Second"], sender.SentRows.Select(row => row.Subject));
        Assert.All(sender.SentRows, row =>
        {
            Assert.Equal(["first@example.test", "second@example.test"], row.ToEmailAddresses);
            Assert.Equal(["manager@example.test", "operations@example.test"], row.CcEmailAddresses);
        });
        dbContext.ChangeTracker.Clear();
        Assert.All(dbContext.NotificationOutbox, row =>
        {
            Assert.Equal("Sent", row.Status);
            Assert.Equal(Now, row.SentAtUtc);
        });
    }

    [Fact]
    public async Task ProcessNotificationOnlyOccurrenceUsesOccurrenceDisplayReference()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = null,
            NotificationOccurrenceKey = "SQMS:OpportunityOpen:2030:Q1:2030-01-15",
            BusinessObjectType = "SqmsOpportunityGroup",
            BusinessObjectId = "2030-Q1",
            NotificationType = "OpportunityOpen",
            NotificationRecipientsJson = RecipientSnapshot,
            Subject = "Open opportunities",
            Body = "body",
            HtmlBody = "<p>body</p>",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = "corr-notification-only",
            CreatedAtUtc = Now
        });
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();

        var result = await new ProcessNotificationOutboxService(
            dbContext,
            sender,
            new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.Null(result.Failure);
        var delivery = Assert.Single(sender.SentRows);
        Assert.Null(delivery.WorkflowInstanceId);
        Assert.Equal("SQMS:OpportunityOpen:2030:Q1:2030-01-15", delivery.NotificationOccurrenceKey);
        Assert.Equal(delivery.NotificationOccurrenceKey, delivery.DisplayReference);
        var processed = Assert.Single(result.Processed);
        Assert.Null(processed.WorkflowInstanceId);
        Assert.Equal(delivery.NotificationOccurrenceKey, processed.NotificationOccurrenceKey);
        Assert.Equal(delivery.NotificationOccurrenceKey, processed.DisplayReference);
    }

    [Fact]
    public async Task ProcessNotificationOutboxRetainsFailedRowAndContinuesWhenSenderThrows()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-PT-1", "First", Now.AddMinutes(-3));
        AddNotification(dbContext, "WF-PT-2", "Second", Now.AddMinutes(-2));
        AddNotification(dbContext, "WF-PT-3", "Third", Now.AddMinutes(-1));
        dbContext.SaveChanges();
        var secondId = dbContext.NotificationOutbox.Single(row => row.Subject == "Second").Id;
        var sender = new RecordingNotificationSender(secondId);

        var result = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.NotNull(result.Failure);
        Assert.Equal(secondId, result.Failure.Id);
        Assert.Equal(["first@example.test", "second@example.test"], result.Failure.ToEmailAddresses);
        Assert.Equal(["manager@example.test", "operations@example.test"], result.Failure.CcEmailAddresses);
        Assert.Equal(2, result.Processed.Count);
        Assert.Equal(0, result.NotAttemptedCount);
        dbContext.ChangeTracker.Clear();
        Assert.Equal("Sent", dbContext.NotificationOutbox.Single(row => row.Subject == "First").Status);
        Assert.Equal("Pending", dbContext.NotificationOutbox.Single(row => row.Subject == "Second").Status);
        Assert.Equal(1, dbContext.NotificationOutbox.Single(row => row.Subject == "Second").RetryCount);
        Assert.Contains("send failed", dbContext.NotificationOutbox.Single(row => row.Subject == "Second").LastError, StringComparison.Ordinal);
        Assert.Equal("Sent", dbContext.NotificationOutbox.Single(row => row.Subject == "Third").Status);
    }

    [Fact]
    public async Task MalformedRecipientSnapshotDoesNotBlockHealthyRowsAcrossRepeatedRuns()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var notification = AddNotification(dbContext, "WF-PT-1", "Malformed", Now);
        notification.NotificationRecipientsJson = "{";
        AddNotification(dbContext, "WF-PT-2", "Healthy", Now.AddMinutes(1));
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();

        var result = await new ProcessNotificationOutboxService(
            dbContext,
            sender,
            new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.NotNull(result.Failure);
        Assert.Empty(result.Failure.ToEmailAddresses);
        Assert.Empty(result.Failure.CcEmailAddresses);
        Assert.Equal("Healthy", Assert.Single(sender.SentRows).Subject);
        dbContext.ChangeTracker.Clear();
        var row = dbContext.NotificationOutbox.Single(item => item.Subject == "Malformed");
        Assert.Equal("Pending", row.Status);
        Assert.Equal(1, row.RetryCount);
        Assert.Contains("recipient snapshot is invalid", row.LastError, StringComparison.Ordinal);
        Assert.Null(row.ProcessingToken);
        Assert.Null(row.ProcessingStartedAtUtc);

        AddNotification(dbContext, "WF-PT-3", "Later healthy", Now.AddMinutes(2));
        dbContext.SaveChanges();
        var retry = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now))
            .ProcessAsync(CancellationToken.None);
        Assert.NotNull(retry.Failure);
        Assert.Equal(0, retry.NotAttemptedCount);
        Assert.Equal(["Healthy", "Later healthy"], sender.SentRows.Select(item => item.Subject));
        dbContext.ChangeTracker.Clear();
        Assert.Equal(2, dbContext.NotificationOutbox.Single(item => item.Subject == "Malformed").RetryCount);
    }

    [Fact]
    public async Task MalformedMailboxAddressThroughSmtpSenderReleasesWholeOutboxRow()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var notification = AddNotification(dbContext, "WF-PT-1", "Malformed address", Now);
        var originalSnapshot = JsonSerializer.Serialize(
            new NotificationRecipients
            {
                ToEmailAddresses = ["first@example.test", "second@example.test"],
                CcEmailAddresses = ["<"]
            },
            WorkflowJson.Options);
        notification.NotificationRecipientsJson = originalSnapshot;
        dbContext.SaveChanges();
        var sender = new SmtpNotificationSender(SmtpNotificationSenderTests.Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "1",
            ["Smtp:From"] = "workflow@example.test"
        }));

        var result = await new ProcessNotificationOutboxService(
            dbContext,
            sender,
            new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.NotNull(result.Failure);
        Assert.IsType<ParseException>(result.Failure.Exception);
        Assert.Equal(
            ["first@example.test", "second@example.test"],
            result.Failure.ToEmailAddresses);
        Assert.Equal(["<"], result.Failure.CcEmailAddresses);
        Assert.Empty(result.Processed);
        dbContext.ChangeTracker.Clear();
        var row = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal("Pending", row.Status);
        Assert.Equal(1, row.RetryCount);
        Assert.False(string.IsNullOrWhiteSpace(row.LastError));
        Assert.Null(row.ProcessingToken);
        Assert.Null(row.ProcessingStartedAtUtc);
        Assert.Equal(originalSnapshot, row.NotificationRecipientsJson);
    }

    [Fact]
    public async Task ConcurrentProcessorsClaimBeforeSendingSoOnlyOneSenderRuns()
    {
        using var setupContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(setupContext, "WF-PT-1", "Only", Now);
        setupContext.SaveChanges();
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var firstSender = new BlockingNotificationSender();
        var secondSender = new RecordingNotificationSender();
        var firstProcessor = new ProcessNotificationOutboxService(firstContext, firstSender, new FixedTimeProvider(Now));
        var secondProcessor = new ProcessNotificationOutboxService(secondContext, secondSender, new FixedTimeProvider(Now));

        var firstTask = firstProcessor.ProcessAsync(CancellationToken.None);
        await firstSender.WaitForSendAsync();
        var secondResult = await secondProcessor.ProcessAsync(CancellationToken.None);
        firstSender.Release();
        var firstResult = await firstTask;

        Assert.Null(firstResult.Failure);
        Assert.Null(secondResult.Failure);
        Assert.Single(firstSender.SentRows);
        Assert.Empty(secondSender.SentRows);
        Assert.Contains(secondResult.Warnings, warning => warning.Contains("already claimed", StringComparison.Ordinal));
        setupContext.ChangeTracker.Clear();
        var row = setupContext.NotificationOutbox.Single();
        Assert.Equal("Sent", row.Status);
        Assert.Null(row.ProcessingToken);
        Assert.Null(row.ProcessingStartedAtUtc);
    }

    [Fact]
    public async Task ClaimedSlaOccurrenceForInactiveScheduleIsCancelledBeforeSenderRuns()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var instance = AddInstance(dbContext, "WF-PT-STALE-SLA", Now);
        var task = new WorkflowTask
        {
            WorkflowInstanceId = instance.WorkflowInstanceId,
            TaskKey = "TradingDeskReviewTask",
            AssignedRoleKey = "TradingDeskTaxable",
            Status = "Pending",
            CreatedAtUtc = Now.AddDays(-1),
            NotificationRecipientsJson = RecipientSnapshot
        };
        dbContext.WorkflowTasks.Add(task);
        dbContext.SaveChanges();
        var schedule = new WorkflowTaskSlaSchedule
        {
            WorkflowTaskId = task.Id,
            ScheduleVersion = 1,
            SlaPolicyKey = "TradingDeskReviewSla",
            TargetDate = new DateOnly(2030, 1, 15),
            DueReminderAtUtc = Now,
            OverdueAtUtc = Now.AddDays(1),
            NextReminderAtUtc = null,
            CalculationMode = "ApplicationTargetDate",
            TimeZoneId = "America/New_York",
            ReminderLocalTime = new TimeOnly(9, 0),
            OverdueStartOffsetDays = 1,
            RecurrenceIntervalDays = 1,
            DueNotificationRouteKey = "RouteSlaTaskDueToday",
            OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
            Status = "Superseded",
            CreatedAtUtc = Now.AddDays(-1),
            CreatedByUserId = "maker1",
            CorrelationId = "corr-stale",
            IdempotencyKey = "idem-stale",
            SupersededAtUtc = Now,
            SupersededByUserId = "maker1"
        };
        dbContext.WorkflowTaskSlaSchedules.Add(schedule);
        dbContext.SaveChanges();
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowTaskId = task.Id,
            WorkflowTaskSlaScheduleId = schedule.Id,
            SlaOccurrenceDate = new DateOnly(2030, 1, 15),
            SlaReminderKind = "DueToday",
            BusinessObjectType = instance.BusinessObjectType,
            BusinessObjectId = instance.BusinessObjectId,
            NotificationType = "SlaTaskDueToday",
            NotificationRecipientsJson = RecipientSnapshot,
            Subject = "Stale SLA reminder",
            Body = "body",
            HtmlBody = "<p>body</p>",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = "sla-stale",
            CreatedAtUtc = Now
        });
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();

        var result = await new ProcessNotificationOutboxService(
            dbContext,
            sender,
            new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Empty(sender.SentRows);
        dbContext.ChangeTracker.Clear();
        var row = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal("Cancelled", row.Status);
        Assert.Equal("SlaScheduleInactive", row.CancellationReason);
        Assert.Equal(Now, row.CancelledAtUtc);
        Assert.Null(row.ProcessingToken);
        Assert.Null(row.ProcessingStartedAtUtc);
    }

    [Fact]
    public async Task CommandReportsEveryFailedRowAndStillSendsHealthyMessage()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-PT-1", "Bad first", Now).NotificationRecipientsJson = "{";
        AddNotification(dbContext, "WF-PT-2", "Bad second", Now.AddMinutes(1)).NotificationRecipientsJson = "{";
        AddNotification(dbContext, "WF-PT-3", "Healthy", Now.AddMinutes(2));
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();
        var processor = new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now));

        var result = await new ProcessNotificationOutboxCommandHandler(processor, sender)
            .RunAsync([], CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Bad first", result.Message, StringComparison.Ordinal);
        Assert.Contains("Bad second", result.Message, StringComparison.Ordinal);
        Assert.Contains("1 notification processed, 2 failed, 0 not attempted.", result.Message, StringComparison.Ordinal);
        Assert.Equal("Healthy", Assert.Single(sender.SentRows).Subject);
        dbContext.ChangeTracker.Clear();
        Assert.All(dbContext.NotificationOutbox.Where(row => row.Status == "Pending"), row => Assert.Equal(1, row.RetryCount));
    }

    [Fact]
    public async Task CancellationReleasesCurrentClaimAndDoesNotAttemptRemainingMessages()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-PT-1", "Cancelled", Now);
        AddNotification(dbContext, "WF-PT-2", "Remaining", Now.AddMinutes(1));
        dbContext.SaveChanges();
        using var cancellation = new CancellationTokenSource();
        var sender = new CancellingNotificationSender(cancellation);

        var result = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now))
            .ProcessAsync(cancellation.Token);

        Assert.IsAssignableFrom<OperationCanceledException>(Assert.Single(result.Failures).Exception);
        Assert.Same(result.Failure, result.Failures[0]);
        Assert.Equal(1, result.NotAttemptedCount);
        Assert.Equal(1, sender.Attempts);
        dbContext.ChangeTracker.Clear();
        Assert.All(dbContext.NotificationOutbox, row =>
        {
            Assert.Equal("Pending", row.Status);
            Assert.Null(row.ProcessingToken);
            Assert.Null(row.ProcessingStartedAtUtc);
        });
        Assert.Equal(0, dbContext.NotificationOutbox.Single(row => row.Subject == "Remaining").RetryCount);
    }

    private sealed class CancellingNotificationSender(CancellationTokenSource cancellation) : INotificationSender
    {
        public string SenderName => "Cancelling";
        public int Attempts { get; private set; }
        public Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
        {
            Attempts++;
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private static NotificationOutbox AddNotification(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string subject,
        DateTimeOffset createdAtUtc)
    {
        if (!dbContext.WorkflowInstances.Any(row => row.WorkflowInstanceId == workflowInstanceId))
        {
            dbContext.WorkflowInstances.Add(new WorkflowInstance
            {
                WorkflowInstanceId = workflowInstanceId,
                WorkflowKey = "PortfolioTrade",
                WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
                BusinessObjectType = "PortfolioTrade",
                BusinessObjectId = workflowInstanceId,
                CurrentStateKey = "Draft",
                Status = "Active",
                StartedAtUtc = createdAtUtc,
                StartedByUserId = "maker1",
                Version = 1
            });
        }

        var notification = new NotificationOutbox
        {
            WorkflowInstanceId = workflowInstanceId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = workflowInstanceId,
            NotificationType = "WorkflowTaskAssigned",
            NotificationRecipientsJson = RecipientSnapshot,
            Subject = subject,
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = subject,
            CreatedAtUtc = createdAtUtc
        };
        dbContext.NotificationOutbox.Add(notification);
        return notification;
    }

    private static WorkflowInstance AddInstance(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        DateTimeOffset createdAtUtc)
    {
        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = workflowInstanceId,
            CurrentStateKey = "TradingDeskReview",
            Status = "Active",
            StartedAtUtc = createdAtUtc,
            StartedByUserId = "maker1",
            Version = 1
        };
        dbContext.WorkflowInstances.Add(instance);
        return instance;
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_phase4_execution_tests"))
            .Options;

        return new WorkflowDbContext(options);
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }

    private sealed class BlockingNotificationSender : INotificationSender
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string SenderName => "Blocking";

        public List<NotificationDelivery> SentRows { get; } = [];

        public async Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
        {
            SentRows.Add(notification);
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
        }

        public Task WaitForSendAsync()
        {
            return entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Release()
        {
            release.SetResult();
        }
    }
}
