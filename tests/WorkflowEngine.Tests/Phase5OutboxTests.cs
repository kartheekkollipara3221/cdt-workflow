using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Infrastructure.DependencyInjection;
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
    public async Task ConfiguredBatchLeavesRemainingRowsForTheNextRun()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        for (var i = 0; i < 5; i++)
        {
            AddNotification(dbContext, $"WF-BATCH-{i}", $"Message {i}", Now.AddMinutes(i));
        }
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();
        using var provider = ConfiguredProvider(dbContext, sender);
        var processor = provider.GetRequiredService<IProcessNotificationOutboxService>();

        var first = await processor.ProcessAsync(CancellationToken.None);
        Assert.Equal(2, first.PendingCount);
        Assert.Equal(["Message 0", "Message 1"], sender.SentRows.Select(row => row.Subject));
        Assert.Equal(3, dbContext.NotificationOutbox.Count(row => row.Status == "Pending"));
        await processor.ProcessAsync(CancellationToken.None);
        await processor.ProcessAsync(CancellationToken.None);
        Assert.Equal(5, sender.SentRows.Count);
    }

    [Fact]
    public async Task ConfiguredRetryDoesNotRetryPoisonRowOnImmediateNextRun()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-RETRY", "Poison", Now).NotificationRecipientsJson = "{";
        dbContext.SaveChanges();
        using var provider = ConfiguredProvider(dbContext, new RecordingNotificationSender());
        var processor = provider.GetRequiredService<IProcessNotificationOutboxService>();
        Assert.Single((await processor.ProcessAsync(CancellationToken.None)).Failures);
        var second = await processor.ProcessAsync(CancellationToken.None);
        Assert.Empty(second.Failures);
        Assert.Equal(0, second.PendingCount);
    }

    private static ServiceProvider ConfiguredProvider(WorkflowDbContext dbContext, INotificationSender sender)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = dbContext.Database.GetConnectionString(),
            ["Notifications:Sender"] = "NoOp",
            ["Notifications:Outbox:BatchSize"] = "2",
            ["Notifications:Outbox:RetryInitialDelaySeconds"] = "60",
            ["Notifications:Outbox:RetryMaxDelaySeconds"] = "3600"
        }).Build();
        var services = new ServiceCollection().AddWorkflowInfrastructure(configuration);
        services.AddSingleton(dbContext);
        services.AddSingleton(sender);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RetryDelayDoublesCapsAndSurvivesNewProcessorThenRecovers()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var row = AddNotification(dbContext, "WF-BACKOFF", "Poison", Now);
        row.NotificationRecipientsJson = "{";
        dbContext.SaveChanges();
        var options = new OutboxProcessingOptions(1, 60, 3600);
        var clock = Now;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await using var context = CreateDbContext();
            var processor = new ProcessNotificationOutboxService(context, new RecordingNotificationSender(), new FixedTimeProvider(clock), options);
            Assert.Single((await processor.ProcessAsync(CancellationToken.None)).Failures);
            var persisted = context.NotificationOutbox.Single();
            var next = clock.AddSeconds(Math.Min(60 * Math.Pow(2, attempt), 3600));
            Assert.Equal(next, persisted.NextAttemptAtUtc);
            Assert.Equal(attempt + 1, persisted.RetryCount);
            Assert.Equal("Pending", persisted.Status);
            Assert.Null(persisted.ProcessingToken);
            var early = new ProcessNotificationOutboxService(context, new RecordingNotificationSender(), new FixedTimeProvider(next.AddTicks(-1)));
            Assert.Equal(0, (await early.ProcessAsync(CancellationToken.None)).PendingCount);
            clock = next;
        }
        dbContext.ChangeTracker.Clear();
        var repaired = dbContext.NotificationOutbox.Single();
        repaired.NotificationRecipientsJson = RecipientSnapshot;
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();
        await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(clock), options).ProcessAsync(CancellationToken.None);
        Assert.Single(sender.SentRows);
        dbContext.ChangeTracker.Clear();
        Assert.Null(dbContext.NotificationOutbox.Single().NextAttemptAtUtc);
        Assert.Equal("Sent", dbContext.NotificationOutbox.Single().Status);
        Assert.Equal(8, dbContext.NotificationOutbox.Single().RetryCount);
    }

    [Fact]
    public async Task DeferredPoisonRowDoesNotStarveLaterHealthyRowsInSingleRowBatches()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-BAD", "Poison", Now).NotificationRecipientsJson = "{";
        AddNotification(dbContext, "WF-GOOD", "Healthy", Now);
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();
        var processor = new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now), new OutboxProcessingOptions(1, 60));
        Assert.Single((await processor.ProcessAsync(CancellationToken.None)).Failures);
        Assert.Single((await processor.ProcessAsync(CancellationToken.None)).Processed);
        Assert.Equal("Healthy", Assert.Single(sender.SentRows).Subject);
    }

    [Fact]
    public async Task AtomicClaimRechecksRetryTimeChangedAfterCandidateSelection()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-RACE-1", "First", Now);
        var second = AddNotification(dbContext, "WF-RACE-2", "Second", Now);
        dbContext.SaveChanges();
        var sender = new BlockingNotificationSender();
        var run = new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now), new OutboxProcessingOptions(2, 60))
            .ProcessAsync(CancellationToken.None);
        await sender.WaitForSendAsync();
        try
        {
            await using var other = CreateDbContext();
            await other.NotificationOutbox.Where(row => row.Id == second.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.NextAttemptAtUtc, Now.AddMinutes(1)));
        }
        finally { sender.Release(); }
        var result = await run;
        Assert.Single(result.Processed);
        Assert.Single(sender.SentRows);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task BatchBoundsActiveClaimWarningsAndExpiredLeaseStillRecovers()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        for (var i = 0; i < 5; i++)
        {
            var row = AddNotification(dbContext, $"WF-CLAIM-{i}", $"Claim {i}", Now);
            row.Status = "Processing";
            row.ProcessingToken = $"owner-{i}";
            row.ProcessingStartedAtUtc = Now;
        }
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();
        var options = new OutboxProcessingOptions(2, 60);
        var active = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now), options).ProcessAsync(CancellationToken.None);
        Assert.Equal(2, active.Warnings.Count);
        Assert.Empty(sender.SentRows);
        var expired = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now.AddMinutes(15)), options).ProcessAsync(CancellationToken.None);
        Assert.Equal(2, expired.Processed.Count);
        Assert.Equal(3, dbContext.NotificationOutbox.Count(row => row.Status == "Processing"));
    }

    [Fact]
    public async Task UncertainDeliveryCanRepeatOnlyWhenRetryBecomesDue()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-UNCERTAIN", "Uncertain", Now);
        dbContext.SaveChanges();
        var sender = new AcceptedThenFailedSender();
        var options = new OutboxProcessingOptions(1, 60);
        var first = new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now), options);
        Assert.Single((await first.ProcessAsync(CancellationToken.None)).Failures);
        Assert.Equal(0, (await first.ProcessAsync(CancellationToken.None)).PendingCount);
        await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now.AddMinutes(1)), options).ProcessAsync(CancellationToken.None);
        Assert.Equal(2, sender.Accepted.Count);
        Assert.Equal(sender.Accepted[0], sender.Accepted[1]);
    }

    private sealed class AcceptedThenFailedSender : INotificationSender
    {
        public string SenderName => "Uncertain";
        public List<long> Accepted { get; } = [];
        public Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
        {
            Accepted.Add(notification.Id);
            if (Accepted.Count == 1)
                throw new IOException("Simulated connection loss after acceptance");
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdditiveRetryUpgradePreservesExistingActiveWorkflowsAndOutbox(bool manualSql)
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var row = AddNotification(dbContext, "WF-UPGRADE", "Preserved", Now);
        row.RetryCount = 3;
        row.LastError = "Previous failure";
        dbContext.SaveChanges();
        var id = row.Id;
        await dbContext.GetService<IMigrator>().MigrateAsync("20260820200000_AddNotificationOccurrenceKey");
        if (manualSql)
        {
            var path = Path.Combine(SqmsDatabasePackageTests.RepositoryRoot(), "sql", "upgrades", "20261014_outbox_retry_scheduling.sql");
            await SqmsDatabasePackageTests.ExecuteScriptAsync(dbContext.Database.GetConnectionString()!, path);
            await SqmsDatabasePackageTests.ExecuteScriptAsync(dbContext.Database.GetConnectionString()!, path);
        }
        else
        {
            await dbContext.Database.MigrateAsync();
        }
        dbContext.ChangeTracker.Clear();
        var preserved = dbContext.NotificationOutbox.Single();
        Assert.Equal(id, preserved.Id);
        Assert.Equal("Pending", preserved.Status);
        Assert.Equal("Preserved", preserved.Subject);
        Assert.Equal(RecipientSnapshot, preserved.NotificationRecipientsJson);
        Assert.Equal(3, preserved.RetryCount);
        Assert.Equal("Previous failure", preserved.LastError);
        Assert.Null(preserved.NextAttemptAtUtc);
        Assert.Equal("Active", dbContext.WorkflowInstances.Single().Status);
        var result = await new ProcessNotificationOutboxService(dbContext, new RecordingNotificationSender(), new FixedTimeProvider(Now), new OutboxProcessingOptions(100, 60))
            .ProcessAsync(CancellationToken.None);
        Assert.Single(result.Processed);
    }

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
