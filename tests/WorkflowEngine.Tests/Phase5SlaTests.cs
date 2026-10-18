using System.Text.Json;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class Phase5SlaTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");
    private const string RecipientSnapshot = """{"toEmailAddresses":["primary@example.test","secondary@example.test"],"ccEmailAddresses":["cc.one@example.test","cc.two@example.test"]}""";

    [Fact]
    public async Task C14DefaultScanUsesBoundedIndependentContextsAndProcessesAllPages()
    {
        using var setup = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(setup);
        for (var i = 0; i < 103; i++)
        {
            var id = $"WF-C14-{i}";
            AddInstance(setup, id, id, "Active", versionId);
            AddTask(setup, id, "CheckerReviewTask", "Checker", Now.AddMinutes(-1));
        }
        setup.SaveChanges();
        var probe = new C14TrackingProbe();
        using var context = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(setup.Database.GetConnectionString()).AddInterceptors(probe).Options);
        var result = await new RunSlaService(context, new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);
        Assert.Equal(103, result.QueuedNotifications.Count);
        Assert.Equal(103, result.PendingTasksWithSla);
        Assert.Equal(2, probe.ContextIds.Count);
        Assert.DoesNotContain(context.ContextId.InstanceId, probe.ContextIds);
        Assert.InRange(probe.MaxTracked, 1, 400);
        Assert.Empty(context.ChangeTracker.Entries());
        var repeated = await new RunSlaService(context, new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);
        Assert.Empty(repeated.QueuedNotifications);
    }

    private sealed class C14TrackingProbe : SaveChangesInterceptor
    {
        public HashSet<Guid> ContextIds { get; } = [];
        public int MaxTracked { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            ContextIds.Add(context.ContextId.InstanceId);
            MaxTracked = Math.Max(MaxTracked, context.ChangeTracker.Entries().Count());
            return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task C14BatchContextsPreserveRuntimeConnectionAndCallerOwnership(int connectionMode)
    {
        using var setup = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(setup);
        for (var i = 0; i < 3; i++)
        {
            var id = $"WF-C14-CONNECTION-{i}";
            AddInstance(setup, id, id, "Active", versionId);
            AddTask(setup, id, "CheckerReviewTask", "Checker", Now.AddMinutes(-1));
        }
        setup.SaveChanges();
        using var connection = new SqlConnection(setup.Database.GetConnectionString());
        var probe = new C14CommandProbe();
        var options = new DbContextOptionsBuilder<WorkflowDbContext>().AddInterceptors(probe);
        if (connectionMode == 1)
            options.UseSqlServer(connection, contextOwnsConnection: true);
        else
            options.UseSqlServer("Server=localhost,65530;Database=invalid;Integrated Security=true;Connect Timeout=1");
        using var context = new WorkflowDbContext(options.Options);
        if (connectionMode == 0)
            context.Database.SetConnectionString(setup.Database.GetConnectionString());
        else if (connectionMode == 2)
            context.Database.SetDbConnection(connection);
        context.Database.SetCommandTimeout(17);
        var result = await new RunSlaService(context, new SlaScheduleCalculator(), new SlaProcessingOptions(1))
            .RunAsync(new(Now), CancellationToken.None);
        Assert.Equal(3, result.QueuedNotifications.Count);
        Assert.Equal(3, await context.WorkflowTasks.CountAsync());
        Assert.False(string.IsNullOrEmpty(context.Database.GetDbConnection().ConnectionString));
        Assert.NotEmpty(probe.Timeouts);
        Assert.All(probe.Timeouts, timeout => Assert.Equal(17, timeout));
    }

    private sealed class C14CommandProbe : DbCommandInterceptor
    {
        public List<int> Timeouts { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Timeouts.Add(command.CommandTimeout);
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task C14DoesNotOverwriteUndetectedCallerScheduleEdits()
    {
        using var setup = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(setup, "WF-C14-EDIT", "C14-EDIT", "Active", PortfolioTradeVersionId(setup));
        AddTask(setup, "WF-C14-EDIT", "CheckerReviewTask", "Checker", Now.AddMinutes(-1));
        setup.SaveChanges();
        using var context = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(setup.Database.GetConnectionString()).Options);
        var schedule = context.WorkflowTaskSlaSchedules.Single();
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        schedule.TimeZoneId = "caller-unsaved-zone";
        Assert.Equal(EntityState.Unchanged, context.Entry(schedule).State);

        var result = await new RunSlaService(context, new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Single(result.QueuedNotifications);
        Assert.Equal("caller-unsaved-zone", schedule.TimeZoneId);
        Assert.Equal(EntityState.Modified, context.Entry(schedule).State);
        Assert.False(context.ChangeTracker.AutoDetectChangesEnabled);
        Assert.Null(setup.WorkflowTaskSlaSchedules.AsNoTracking().Single().TimeZoneId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task C14ConfiguredPagesPassPoisonRowsPreserveOrderAndLeaveCallerChangesAlone(int batchSize)
    {
        using var setup = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(setup);
        for (var i = 0; i < 8; i++)
        {
            var id = $"WF-C14-FAIR-{i}";
            AddInstance(setup, id, id, "Active", versionId);
            AddTask(setup, id, "CheckerReviewTask", "Checker", i == 7 ? Now.AddHours(1) : Now.AddMinutes(-10 + i / 2),
                notificationRecipientsJson: i == 0 ? "{" : RecipientSnapshot);
        }
        setup.SaveChanges();
        var probe = new C14TrackingProbe();
        using var context = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(setup.Database.GetConnectionString()).AddInterceptors(probe).Options);
        var callerInstance = context.WorkflowInstances.Single(row => row.WorkflowInstanceId == "WF-C14-FAIR-7");
        callerInstance.StartedByUserId = "unsaved-caller-change";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = setup.Database.GetConnectionString(),
            ["Sla:BatchSize"] = batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        using var provider = new ServiceCollection().AddWorkflowInfrastructure(configuration)
            .AddSingleton(context).BuildServiceProvider();
        var service = provider.GetRequiredService<IRunSlaService>();
        var result = await service.RunAsync(new(Now), CancellationToken.None);
        Assert.Equal(8, result.PendingTasksWithSla);
        Assert.Single(result.Warnings);
        Assert.Equal(Enumerable.Range(1, 6).Select(i => $"WF-C14-FAIR-{i}"),
            result.QueuedNotifications.Select(row => row.WorkflowInstanceId));
        Assert.InRange(probe.MaxTracked, 1, batchSize * 4);
        Assert.DoesNotContain(context.ContextId.InstanceId, probe.ContextIds);
        Assert.Same(callerInstance, Assert.Single(context.ChangeTracker.Entries()).Entity);
        Assert.Equal(EntityState.Modified, context.Entry(callerInstance).State);
        Assert.NotEqual("unsaved-caller-change", setup.WorkflowInstances.AsNoTracking()
            .Single(row => row.WorkflowInstanceId == callerInstance.WorkflowInstanceId).StartedByUserId);
        var retry = await service.RunAsync(new(Now), CancellationToken.None);
        Assert.Single(retry.Warnings);
        Assert.Empty(retry.QueuedNotifications);
        Assert.Equal(6, setup.NotificationOutbox.Count(row => row.SlaReminderKind != null));
    }

    [Theory]
    [InlineData("duration-metadata")]
    [InlineData("application-metadata")]
    [InlineData("invalid-zone")]
    [InlineData("missing-route")]
    [InlineData("inactive-route")]
    [InlineData("current-dst-gap")]
    [InlineData("next-dst-gap")]
    [InlineData("advance-overflow")]
    [InlineData("display-overflow")]
    public async Task C06RunSlaIsolatesInvalidScheduleAndRetriesWithoutDuplicatingHealthyReminder(string failure)
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var now = DateTimeOffset.Parse(failure == "current-dst-gap"
            ? "2030-03-10T17:00:00Z"
            : "2030-03-09T17:00:00Z");
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-C06-BAD", "C06-BAD", "Active", versionId);
        AddInstance(dbContext, "WF-C06-GOOD", "C06-GOOD", "Active", versionId);
        var badTask = AddTask(dbContext, "WF-C06-BAD", "CheckerReviewTask", "Checker", now.AddMinutes(-10));
        var goodTask = AddTask(dbContext, "WF-C06-GOOD", "CheckerReviewTask", "Checker", now.AddMinutes(-5));
        dbContext.SaveChanges();
        var badSchedule = dbContext.WorkflowTaskSlaSchedules.Single(row => row.WorkflowTaskId == badTask.Id);
        switch (failure)
        {
            case "duration-metadata":
                badSchedule.RecurrenceIntervalDays = 1;
                break;
            case "application-metadata":
                badSchedule.CalculationMode = "ApplicationTargetDate";
                break;
            case "invalid-zone":
                badSchedule.RecurrenceIntervalDays = 1;
                badSchedule.ReminderLocalTime = new TimeOnly(9, 0);
                badSchedule.TimeZoneId = "C06/UnknownTimeZone";
                break;
            case "missing-route":
                badSchedule.OverdueNotificationRouteKey = "C06MissingRoute";
                break;
            case "inactive-route":
                badSchedule.OverdueNotificationRouteKey = "RouteCheckerAssignedOnSubmit";
                dbContext.NotificationRoutes.Single(row => row.WorkflowDefinitionVersionId == versionId
                    && row.RouteKey == "RouteCheckerAssignedOnSubmit").IsActive = false;
                break;
            case "current-dst-gap":
                badSchedule.CalculationMode = "ApplicationTargetDate";
                badSchedule.TargetDate = new DateOnly(2030, 3, 9);
                badSchedule.RecurrenceIntervalDays = 1;
                badSchedule.ReminderLocalTime = new TimeOnly(2, 30);
                badSchedule.TimeZoneId = "Eastern Standard Time";
                break;
            case "next-dst-gap":
                badSchedule.RecurrenceIntervalDays = 1;
                badSchedule.ReminderLocalTime = new TimeOnly(2, 30);
                badSchedule.TimeZoneId = "Eastern Standard Time";
                break;
            case "advance-overflow":
                badSchedule.RecurrenceIntervalDays = int.MaxValue;
                badSchedule.ReminderLocalTime = new TimeOnly(9, 0);
                badSchedule.TimeZoneId = "UTC";
                break;
            case "display-overflow":
                badSchedule.OverdueAtUtc = DateTimeOffset.MinValue;
                break;
        }
        dbContext.SaveChanges();
        var originalSchedule = JsonSerializer.Serialize(badSchedule);
        var originalTask = JsonSerializer.Serialize(badTask);
        var originalInstance = JsonSerializer.Serialize(dbContext.WorkflowInstances.Single(row => row.WorkflowInstanceId == badTask.WorkflowInstanceId));
        dbContext.ChangeTracker.Clear();
        var service = new RunSlaService(dbContext, new SlaScheduleCalculator());

        var first = await service.RunAsync(new(now), CancellationToken.None);
        var second = await service.RunAsync(new(now), CancellationToken.None);

        Assert.Equal([goodTask.Id], first.QueuedNotifications.Select(row => row.WorkflowTaskId));
        Assert.Empty(second.QueuedNotifications);
        foreach (var result in new[] { first, second })
        {
            var warning = Assert.Single(result.Warnings);
            Assert.Contains($"schedule {badSchedule.Id}", warning, StringComparison.Ordinal);
            Assert.Contains($"task {badTask.Id}", warning, StringComparison.Ordinal);
            Assert.Contains(badTask.WorkflowInstanceId, warning, StringComparison.Ordinal);
        }
        // Read persisted data, not tracked objects. A failed next-reminder calculation occurs
        // after an outbox insert is staged; the later healthy SaveChanges must not save it.
        var unchangedSchedule = dbContext.WorkflowTaskSlaSchedules.AsNoTracking().Single(row => row.Id == badSchedule.Id);
        Assert.Equal(originalSchedule, JsonSerializer.Serialize(unchangedSchedule));
        Assert.Equal(originalTask, JsonSerializer.Serialize(dbContext.WorkflowTasks.AsNoTracking().Single(row => row.Id == badTask.Id)));
        Assert.Equal(originalInstance, JsonSerializer.Serialize(dbContext.WorkflowInstances.AsNoTracking().Single(row => row.WorkflowInstanceId == badTask.WorkflowInstanceId)));
        var notification = Assert.Single(dbContext.NotificationOutbox.AsNoTracking().Where(row => row.SlaReminderKind != null));
        Assert.Equal(goodTask.Id, notification.WorkflowTaskId);
        Assert.Equal("Overdue: Original CheckerReviewTask subject", notification.Subject);
        Assert.Equal("Original CheckerReviewTask plain body", notification.Body);
        Assert.Equal("<p>Original CheckerReviewTask HTML body</p>", notification.HtmlBody);
        Assert.Equal(RecipientSnapshot, notification.NotificationRecipientsJson);
        Assert.DoesNotContain(dbContext.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified);

        // A later, separately authorized metadata repair can recover this same
        // active task; no workflow closure or reminder deletion is required.
        var repaired = dbContext.WorkflowTaskSlaSchedules.Single(row => row.Id == badSchedule.Id);
        repaired.CalculationMode = "DurationFromTaskCreation";
        repaired.RecurrenceIntervalDays = null;
        repaired.OverdueAtUtc = badTask.DueAtUtc!.Value;
        repaired.OverdueNotificationRouteKey = "RouteSlaTaskOverdue";
        dbContext.SaveChanges();
        var recovered = await service.RunAsync(new(now), CancellationToken.None);
        Assert.Empty(recovered.Warnings);
        Assert.Equal(badTask.Id, Assert.Single(recovered.QueuedNotifications).WorkflowTaskId);
        Assert.Equal(2, dbContext.NotificationOutbox.Count(row => row.SlaReminderKind != null));
        Assert.Empty((await service.RunAsync(new(now), CancellationToken.None)).QueuedNotifications);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("database")]
    [InlineData("unexpected")]
    public async Task C06RunSlaPropagatesFailuresOutsideScheduleData(string failure)
    {
        using var setup = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(setup, "WF-C06-FATAL", "C06-FATAL", "Active", PortfolioTradeVersionId(setup));
        var task = AddTask(setup, "WF-C06-FATAL", "CheckerReviewTask", "Checker", Now.AddMinutes(-5));
        setup.SaveChanges();
        Exception expected = failure switch
        {
            "cancel" => new OperationCanceledException("C06 injected cancellation"),
            "database" => new DbUpdateException("C06 injected database failure"),
            _ => new InvalidOperationException("C06 injected unexpected failure")
        };
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(setup.Database.GetConnectionString())
            .AddInterceptors(new C06SaveFailureInterceptor(expected))
            .Options;
        using var dbContext = new WorkflowDbContext(options);

        var actual = await Record.ExceptionAsync(() => new RunSlaService(dbContext, new SlaScheduleCalculator())
            .RunAsync(new(Now), CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Empty(setup.NotificationOutbox.AsNoTracking().Where(row => row.SlaReminderKind != null));
        Assert.Equal(task.DueAtUtc, setup.WorkflowTaskSlaSchedules.AsNoTracking().Single().NextReminderAtUtc);
    }

    private sealed class C06SaveFailureInterceptor(Exception failure) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) => throw failure;
    }

    [Fact]
    public async Task RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-1", "PT-1", "Active", versionId);
        AddInstance(dbContext, "WF-PT-2", "PT-2", "Active", versionId);
        AddInstance(dbContext, "WF-PT-3", "PT-3", "Active", versionId);
        AddInstance(dbContext, "WF-PT-4", "PT-4", "Active", versionId);
        AddInstance(dbContext, "WF-PT-5", "PT-5", "Active", versionId);
        AddInstance(dbContext, "WF-PT-6", "PT-6", "Completed", versionId);
        AddInstance(dbContext, "WF-PT-7", "PT-7", "Active", versionId);
        dbContext.SaveChanges();

        var fixedOffsetBoundary = DateTimeOffset.Parse("2030-01-15T02:00:00Z");
        var returned = AddTask(dbContext, "WF-PT-7", "TradingDeskReviewTask", "TradingDesk", Now.AddMinutes(-6));
        var overdue = AddTask(dbContext, "WF-PT-1", "CheckerReviewTask", "Checker", fixedOffsetBoundary);
        var alreadyQueued = AddTask(dbContext, "WF-PT-2", "CheckerReviewTask", "Checker", Now.AddMinutes(-4));
        AddTask(dbContext, "WF-PT-3", "CheckerReviewTask", "Checker", Now.AddMinutes(1));
        AddTask(dbContext, "WF-PT-4", "MakerReworkTask", "Maker", null);
        AddTask(dbContext, "WF-PT-5", "CheckerReviewTask", "Checker", Now.AddMinutes(-3), status: "Completed");
        AddTask(dbContext, "WF-PT-6", "CheckerReviewTask", "Checker", Now.AddMinutes(-2));
        dbContext.SaveChanges();
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = "WF-PT-2",
            WorkflowTaskId = alreadyQueued.Id,
            WorkflowTaskSlaScheduleId = dbContext.WorkflowTaskSlaSchedules.Single(row => row.WorkflowTaskId == alreadyQueued.Id).Id,
            SlaOccurrenceAtUtc = alreadyQueued.DueAtUtc,
            SlaOccurrenceDate = DateOnly.FromDateTime(alreadyQueued.DueAtUtc!.Value.UtcDateTime),
            SlaReminderKind = "Overdue",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-2",
            NotificationType = "SlaTaskOverdue",
            NotificationRecipientsJson = RecipientSnapshot,
            Subject = "Task overdue",
            PayloadJson = "{}",
            Status = "Sent",
            CorrelationId = "existing",
            CreatedAtUtc = Now.AddMinutes(-1),
            SentAtUtc = Now.AddMinutes(-1)
        });
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Equal(4, result.PendingTasksWithSla);
        Assert.Equal(1, result.AlreadyQueued);
        Assert.Equal([overdue.Id, returned.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
        Assert.All(result.QueuedNotifications, row => Assert.Equal("Active", dbContext.WorkflowInstances.Single(instance => instance.WorkflowInstanceId == row.WorkflowInstanceId).Status));
        Assert.Equal(3, dbContext.NotificationOutbox.Count(row => row.NotificationType == "SlaTaskOverdue"));
        var overdueNotification = dbContext.NotificationOutbox.Single(row =>
            row.WorkflowTaskId == overdue.Id && row.NotificationType == "SlaTaskOverdue");
        Assert.Equal("Overdue: Original CheckerReviewTask subject", overdueNotification.Subject);
        Assert.Equal("Original CheckerReviewTask plain body", overdueNotification.Body);
        Assert.Equal("<p>Original CheckerReviewTask HTML body</p>", overdueNotification.HtmlBody);
        Assert.Equal(RecipientSnapshot, overdueNotification.NotificationRecipientsJson);

        using var payload = JsonDocument.Parse(overdueNotification.PayloadJson);
        Assert.False(payload.RootElement.TryGetProperty("ccEmailAddresses", out _));
        Assert.Equal(fixedOffsetBoundary.ToString("O"), payload.RootElement.GetProperty("dueAtUtc").GetString());
        Assert.Equal(fixedOffsetBoundary.ToString("O"), payload.RootElement.GetProperty("overdueAtUtc").GetString());

        var second = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Empty(second.QueuedNotifications);
    }

    [Fact]
    public async Task RunSlaMinuteRecurrenceQueuesSameDaySlotsAndSkipsMissedOccurrences()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-CHECKER-RECUR", "PT-CHECKER-RECUR", "Active", versionId);
        var task = AddTask(
            dbContext,
            "WF-PT-CHECKER-RECUR",
            "CheckerReviewTask",
            "Checker",
            Now,
            recurrenceIntervalMinutes: 2);
        dbContext.SaveChanges();
        var service = new RunSlaService(
            dbContext,
            new SlaScheduleCalculator());

        var first = await service.RunAsync(new(Now), CancellationToken.None);
        var duplicate = await service.RunAsync(new(Now), CancellationToken.None);
        var second = await service.RunAsync(new(Now.AddMinutes(2)), CancellationToken.None);
        var delayed = await service.RunAsync(new(Now.AddMinutes(11)), CancellationToken.None);
        var delayedDuplicate = await service.RunAsync(new(Now.AddMinutes(11)), CancellationToken.None);

        Assert.Single(first.QueuedNotifications);
        Assert.Empty(duplicate.QueuedNotifications);
        Assert.Single(second.QueuedNotifications);
        Assert.Single(delayed.QueuedNotifications);
        Assert.Empty(delayedDuplicate.QueuedNotifications);

        var reminders = dbContext.NotificationOutbox
            .Where(row => row.WorkflowTaskId == task.Id && row.SlaReminderKind == "Overdue")
            .OrderBy(row => row.SlaOccurrenceAtUtc)
            .ToArray();
        Assert.Equal(3, reminders.Length);
        Assert.All(reminders, row => Assert.Equal(new DateOnly(2030, 1, 15), row.SlaOccurrenceDate));
        Assert.Equal(
            [Now, Now.AddMinutes(2), Now.AddMinutes(10)],
            reminders.Select(row => row.SlaOccurrenceAtUtc!.Value).ToArray());
        Assert.Equal(3, reminders.Select(row => row.CorrelationId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(reminders, row =>
        {
            using var payload = JsonDocument.Parse(row.PayloadJson);
            Assert.Equal(
                row.SlaOccurrenceAtUtc!.Value.ToString("O"),
                payload.RootElement.GetProperty("slaOccurrenceAtUtc").GetString());
        });
        Assert.Equal(
            Now.AddMinutes(12),
            dbContext.WorkflowTaskSlaSchedules.Single(row => row.WorkflowTaskId == task.Id).NextReminderAtUtc);
    }

    [Fact]
    public async Task ConcurrentMinuteScansQueueOneExactOccurrence()
    {
        using var setupContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(setupContext);
        AddInstance(setupContext, "WF-PT-CHECKER-CONCURRENT", "PT-CHECKER-CONCURRENT", "Active", versionId);
        var task = AddTask(
            setupContext,
            "WF-PT-CHECKER-CONCURRENT",
            "CheckerReviewTask",
            "Checker",
            Now,
            recurrenceIntervalMinutes: 2);
        setupContext.SaveChanges();
        var scheduleId = setupContext.WorkflowTaskSlaSchedules
            .Single(row => row.WorkflowTaskId == task.Id)
            .Id;
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_phase4_execution_tests"))
            .Options;

        async Task<RunSlaResult> ScanAsync()
        {
            await using var scanContext = new WorkflowDbContext(options);
            return await new RunSlaService(
                scanContext,
                new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ScanAsync()));

        setupContext.ChangeTracker.Clear();
        var occurrence = Assert.Single(setupContext.NotificationOutbox.Where(row =>
            row.WorkflowTaskSlaScheduleId == scheduleId
            && row.SlaOccurrenceAtUtc == Now
            && row.SlaReminderKind == "Overdue"));
        Assert.Equal(Now, occurrence.SlaOccurrenceAtUtc);
        Assert.Equal(1, results.Sum(result => result.QueuedNotifications.Count));
        Assert.Equal(
            Now.AddMinutes(2),
            setupContext.WorkflowTaskSlaSchedules.Single(row => row.Id == scheduleId).NextReminderAtUtc);
    }

    [Fact]
    public async Task RunSlaDurationRecurrenceStartsAtDeadlineThenRunsDailyAtNineEastern()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-BDRM-RECUR", "PT-BDRM-RECUR", "Active", versionId);
        var task = AddTask(
            dbContext,
            "WF-PT-BDRM-RECUR",
            "BdrmClientServiceReviewTask",
            "BDRMClientService",
            Now.AddMinutes(-2),
            recurrenceIntervalDays: 1,
            reminderLocalTime: new TimeOnly(9, 0),
            timeZoneId: "America/New_York");
        dbContext.SaveChanges();

        var service = new RunSlaService(
            dbContext,
            new SlaScheduleCalculator());

        var first = await service.RunAsync(new(Now), CancellationToken.None);
        var duplicate = await service.RunAsync(new(Now), CancellationToken.None);
        var beforeNextBoundary = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-16T13:59:00Z")),
            CancellationToken.None);
        var nextDay = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-16T14:00:00Z")),
            CancellationToken.None);
        var delayed = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-19T15:00:00Z")),
            CancellationToken.None);

        Assert.Single(first.QueuedNotifications);
        Assert.Empty(duplicate.QueuedNotifications);
        Assert.Empty(beforeNextBoundary.QueuedNotifications);
        Assert.Single(nextDay.QueuedNotifications);
        Assert.Single(delayed.QueuedNotifications);

        var reminders = dbContext.NotificationOutbox
            .Where(row => row.WorkflowTaskId == task.Id && row.SlaReminderKind == "Overdue")
            .OrderBy(row => row.SlaOccurrenceDate)
            .ToArray();
        Assert.Equal(
            [new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16), new DateOnly(2030, 1, 19)],
            reminders.Select(row => row.SlaOccurrenceDate!.Value).ToArray());
        Assert.Equal(
            [Now.AddMinutes(-2), DateTimeOffset.Parse("2030-01-16T14:00:00Z"), DateTimeOffset.Parse("2030-01-19T14:00:00Z")],
            reminders.Select(row => row.SlaOccurrenceAtUtc!.Value).ToArray());
        Assert.All(
            reminders,
            row => Assert.Equal(RecipientSnapshot, row.NotificationRecipientsJson));
        Assert.Equal(
            DateTimeOffset.Parse("2030-01-20T14:00:00Z"),
            dbContext.WorkflowTaskSlaSchedules
                .Single(row => row.WorkflowTaskId == task.Id)
                .NextReminderAtUtc);
    }

    [Fact]
    public async Task RunSlaWarnsAndSkipsTaskWithMissingRecipientSnapshotWithoutRoleFallback()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-LEGACY-SLA", "PT-LEGACY-SLA", "Active", versionId);
        var legacyTask = AddTask(
            dbContext,
            "WF-PT-LEGACY-SLA",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5),
            storeNotificationSnapshot: false,
            notificationRecipientsJson: null);
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Contains(
            result.Warnings,
            warning => warning == $"task {legacyTask.Id} overdue notification recipients snapshot missing");
        Assert.Empty(result.QueuedNotifications);
        Assert.DoesNotContain(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == legacyTask.Id && row.NotificationType == "SlaTaskOverdue");
    }

    [Fact]
    public async Task RunSlaWarnsAndContinuesWhenRecipientSnapshotIsMalformed()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-BAD-RECIPIENTS", "PT-BAD-RECIPIENTS", "Active", versionId);
        AddInstance(dbContext, "WF-PT-GOOD-RECIPIENTS", "PT-GOOD-RECIPIENTS", "Active", versionId);
        var malformed = AddTask(
            dbContext,
            "WF-PT-BAD-RECIPIENTS",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-10),
            notificationRecipientsJson: "{");
        var valid = AddTask(
            dbContext,
            "WF-PT-GOOD-RECIPIENTS",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith(
                $"task {malformed.Id} overdue notification recipients snapshot invalid:",
                StringComparison.Ordinal));
        Assert.Equal([valid.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
        var notification = Assert.Single(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == valid.Id && row.NotificationType == "SlaTaskOverdue");
        Assert.Equal(RecipientSnapshot, notification.NotificationRecipientsJson);
    }

    [Fact]
    public async Task RunSlaWarnsAndContinuesWhenStoredNotificationContextIsInvalid()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-BAD-CONTEXT", "PT-BAD-CONTEXT", "Active", versionId);
        AddInstance(dbContext, "WF-PT-GOOD-CONTEXT", "PT-GOOD-CONTEXT", "Active", versionId);
        var malformedContextTask = AddTask(
            dbContext,
            "WF-PT-BAD-CONTEXT",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-10),
            storeNotificationSnapshot: false);
        malformedContextTask.NotificationRouteKey = "RouteCheckerAssignedOnSubmit";
        malformedContextTask.NotificationContextJson = "{";
        var validTask = AddTask(
            dbContext,
            "WF-PT-GOOD-CONTEXT",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith(
                $"task {malformedContextTask.Id} overdue notification context invalid:",
                StringComparison.Ordinal));
        Assert.Equal([validTask.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
        Assert.DoesNotContain(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == malformedContextTask.Id && row.NotificationType == "SlaTaskOverdue");
        Assert.Single(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == validTask.Id && row.NotificationType == "SlaTaskOverdue");
    }

    [Fact]
    public async Task RunSlaUsesOriginalTaskEmailWhenCurrentContextLacksLegacySlaTemplateToken()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-MISSING-LINK", "PT-MISSING-LINK", "Active", versionId);
        var task = AddTask(
            dbContext,
            "WF-PT-MISSING-LINK",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        var context = NotificationContext();
        context.Remove("tradeLink");
        task.NotificationContextJson = JsonSerializer.Serialize(context);
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Empty(result.Warnings);
        Assert.Single(result.QueuedNotifications);
        var notification = Assert.Single(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == task.Id && row.NotificationType == "SlaTaskOverdue");
        Assert.Equal("Overdue: Original CheckerReviewTask subject", notification.Subject);
        Assert.Equal("Original CheckerReviewTask plain body", notification.Body);
        Assert.Equal("<p>Original CheckerReviewTask HTML body</p>", notification.HtmlBody);
    }

    [Fact]
    public async Task RunSlaWarnsAndDoesNotAdvanceWhenPrimaryTaskEmailIsMissing()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-MISSING-PRIMARY", "PT-MISSING-PRIMARY", "Active", versionId);
        var task = AddTask(
            dbContext,
            "WF-PT-MISSING-PRIMARY",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        var primary = Assert.Single(dbContext.NotificationOutbox.Local, row => row.WorkflowTaskId == task.Id);
        dbContext.NotificationOutbox.Remove(primary);
        dbContext.SaveChanges();
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single(row => row.WorkflowTaskId == task.Id);
        var originalNextReminderAtUtc = schedule.NextReminderAtUtc;

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Empty(result.QueuedNotifications);
        Assert.Contains(result.Warnings, warning =>
            warning == $"task {task.Id} SLA primary task email missing");
        Assert.Equal(originalNextReminderAtUtc, schedule.NextReminderAtUtc);
        Assert.DoesNotContain(dbContext.NotificationOutbox, row =>
            row.WorkflowTaskId == task.Id && row.SlaReminderKind != null);
    }

    [Fact]
    public async Task RunSlaWarnsAndDoesNotAdvanceWhenPrimaryTaskEmailIsAmbiguous()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-AMBIGUOUS-PRIMARY", "PT-AMBIGUOUS-PRIMARY", "Active", versionId);
        var task = AddTask(
            dbContext,
            "WF-PT-AMBIGUOUS-PRIMARY",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        dbContext.SaveChanges();
        var primary = dbContext.NotificationOutbox.Single(row =>
            row.WorkflowTaskId == task.Id && row.SlaReminderKind == null);
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = primary.WorkflowInstanceId,
            WorkflowTaskId = primary.WorkflowTaskId,
            BusinessObjectType = primary.BusinessObjectType,
            BusinessObjectId = primary.BusinessObjectId,
            NotificationType = "SecondArbitraryPrimaryTaskEmail",
            NotificationRecipientsJson = primary.NotificationRecipientsJson,
            Subject = "Second original subject",
            Body = "Second original plain body",
            HtmlBody = "<p>Second original HTML body</p>",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = $"second-primary-{task.Id}",
            CreatedAtUtc = primary.CreatedAtUtc
        });
        dbContext.SaveChanges();
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single(row => row.WorkflowTaskId == task.Id);
        var originalNextReminderAtUtc = schedule.NextReminderAtUtc;

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Empty(result.QueuedNotifications);
        Assert.Contains(result.Warnings, warning =>
            warning == $"task {task.Id} SLA primary task email ambiguous");
        Assert.Equal(originalNextReminderAtUtc, schedule.NextReminderAtUtc);
        Assert.DoesNotContain(dbContext.NotificationOutbox, row =>
            row.WorkflowTaskId == task.Id && row.SlaReminderKind != null);
    }

    [Fact]
    public async Task RunSlaWarnsAndDoesNotAdvanceWhenPrefixedSubjectExceedsLimit()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-LONG-PRIMARY", "PT-LONG-PRIMARY", "Active", versionId);
        var task = AddTask(
            dbContext,
            "WF-PT-LONG-PRIMARY",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        dbContext.SaveChanges();
        var primary = dbContext.NotificationOutbox.Single(row =>
            row.WorkflowTaskId == task.Id && row.SlaReminderKind == null);
        primary.Subject = new string('x', 300);
        dbContext.SaveChanges();
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single(row => row.WorkflowTaskId == task.Id);
        var originalNextReminderAtUtc = schedule.NextReminderAtUtc;

        var result = await new RunSlaService(
            dbContext,
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Empty(result.QueuedNotifications);
        Assert.Contains(result.Warnings, warning =>
            warning == $"task {task.Id} SLA notification subject exceeds 300 characters");
        Assert.Equal(originalNextReminderAtUtc, schedule.NextReminderAtUtc);
        Assert.DoesNotContain(dbContext.NotificationOutbox, row =>
            row.WorkflowTaskId == task.Id && row.SlaReminderKind != null);
    }

    private static void AddInstance(WorkflowDbContext dbContext, string workflowInstanceId, string businessObjectId, string status, long versionId)
    {
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = versionId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            CurrentStateKey = status == "Completed" ? "Completed" : "CheckerReview",
            Status = status,
            StartedAtUtc = Now.AddHours(-1),
            StartedByUserId = "maker1",
            Version = 1
        });
    }

    private static WorkflowTask AddTask(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string taskKey,
        string roleKey,
        DateTimeOffset? dueAtUtc,
        string status = "Pending",
        bool storeNotificationSnapshot = true,
        string? notificationRecipientsJson = RecipientSnapshot,
        int? recurrenceIntervalMinutes = null,
        int? recurrenceIntervalDays = null,
        TimeOnly? reminderLocalTime = null,
        string? timeZoneId = null)
    {
        var task = new WorkflowTask
        {
            WorkflowInstanceId = workflowInstanceId,
            TaskKey = taskKey,
            AssignedRoleKey = roleKey,
            Status = status,
            CreatedAtUtc = Now.AddHours(-1),
            DueAtUtc = dueAtUtc,
            NotificationRouteKey = storeNotificationSnapshot
                ? taskKey switch
                {
                    "CheckerReviewTask" => "RouteCheckerAssignedOnSubmit",
                    "TradingDeskReviewTask" => "RouteTradingDeskAssigned",
                    _ => null
                }
                : null,
            NotificationContextJson = storeNotificationSnapshot && dueAtUtc is not null
                ? JsonSerializer.Serialize(NotificationContext())
                : null,
            NotificationRecipientsJson = notificationRecipientsJson ?? string.Empty
        };
        dbContext.WorkflowTasks.Add(task);
        dbContext.SaveChanges();
        if (dueAtUtc is not null)
        {
            dbContext.WorkflowTaskSlaSchedules.Add(new WorkflowTaskSlaSchedule
            {
                WorkflowTaskId = task.Id,
                ScheduleVersion = 1,
                SlaPolicyKey = $"{taskKey}TestSla",
                TargetDate = null,
                DueReminderAtUtc = null,
                OverdueAtUtc = dueAtUtc.Value,
                NextReminderAtUtc = dueAtUtc.Value,
                CalculationMode = "DurationFromTaskCreation",
                TimeZoneId = timeZoneId,
                ReminderLocalTime = reminderLocalTime,
                OverdueStartOffsetDays = 0,
                RecurrenceIntervalMinutes = recurrenceIntervalMinutes,
                RecurrenceIntervalDays = recurrenceIntervalDays,
                DueNotificationRouteKey = null,
                OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
                Status = "Active",
                CreatedAtUtc = task.CreatedAtUtc,
                CreatedByUserId = "system",
                CorrelationId = $"corr-{workflowInstanceId}",
                IdempotencyKey = $"idem-{workflowInstanceId}"
            });
        }

        if (storeNotificationSnapshot)
        {
            dbContext.NotificationOutbox.Add(new NotificationOutbox
            {
                WorkflowInstanceId = workflowInstanceId,
                WorkflowTaskId = task.Id,
                BusinessObjectType = "PortfolioTrade",
                BusinessObjectId = workflowInstanceId.Replace("WF-", string.Empty, StringComparison.Ordinal),
                NotificationType = "ArbitraryPrimaryTaskEmail",
                NotificationRecipientsJson = task.NotificationRecipientsJson,
                Subject = $"Original {taskKey} subject",
                Body = $"Original {taskKey} plain body",
                HtmlBody = $"<p>Original {taskKey} HTML body</p>",
                PayloadJson = "{}",
                Status = "Pending",
                CorrelationId = $"primary-{task.Id}",
                CreatedAtUtc = task.CreatedAtUtc
            });
        }

        return task;
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

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }
}
