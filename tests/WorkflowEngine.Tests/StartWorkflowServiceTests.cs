using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.WorkflowStart;

namespace WorkflowEngine.Tests;

[Collection("SqlServerStartWorkflow")]
public sealed class StartWorkflowServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public void FreshStartCreatesOnlyDraftWorkflowInstance()
    {
        using var dbContext = CreateSeededContext();
        var result = CreateService(dbContext).Start(Request("PT-1001"));

        var instance = Assert.Single(dbContext.WorkflowInstances);
        Assert.True(result.Envelope.Result.Success);
        Assert.Equal("WF-PT-1001", instance.WorkflowInstanceId);
        Assert.Equal("Draft", instance.CurrentStateKey);
        Assert.Equal("Active", instance.Status);
        Assert.Equal(1, instance.Version);
        Assert.Equal("maker1", instance.StartedByUserId);
        Assert.Null(instance.CompletedAtUtc);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.IdempotencyRecords);
    }

    [Theory]
    [InlineData("Active", null)]
    [InlineData("Completed", "2030-01-15T15:45:00Z")]
    public void ExistingInstanceReturnsAlreadyExists(string status, string? completedAt)
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", status, completedAt is null ? null : DateTimeOffset.Parse(completedAt));
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Start(Request("PT-1001"));

        Assert.False(result.Envelope.Result.Success);
        Assert.Equal("AlreadyExists", result.Envelope.Result.ResultCode);
        Assert.Equal(status, result.Envelope.Result.ExistingInstanceStatus);
        Assert.Single(dbContext.WorkflowInstances);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.WorkflowAudits);
    }

    [Fact]
    public async Task ConcurrentStartsForSameBusinessObjectReturnSuccessAndAlreadyExists()
    {
        using var setupContext = CreateSeededContext();
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var firstService = CreateService(firstContext);
        var secondService = CreateService(secondContext);

        var results = await Task.WhenAll(
            Task.Run(() => firstService.Start(Request("PT-RACE"))),
            Task.Run(() => secondService.Start(Request("PT-RACE"))));

        Assert.Single(results, result => result.Envelope.Result.Success);
        var duplicate = Assert.Single(results, result => !result.Envelope.Result.Success);
        Assert.Equal("AlreadyExists", duplicate.Envelope.Result.ResultCode);
        setupContext.ChangeTracker.Clear();
        Assert.Single(setupContext.WorkflowInstances.Where(row => row.BusinessObjectId == "PT-RACE"));
    }

    [Fact]
    public void DifferentBusinessObjectCanStartWhenAnotherObjectIsCompleted()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Completed", Now.AddMinutes(15));
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Start(Request("PT-1001-R2"));

        Assert.True(result.Envelope.Result.Success);
        Assert.Equal(2, dbContext.WorkflowInstances.Count());
        Assert.Contains(dbContext.WorkflowInstances, row => row.BusinessObjectId == "PT-1001-R2");
    }

    [Fact]
    public void ResetDeletesRuntimeRowsForOneInstanceAndCreatesFreshInstance()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Active", null);
        AddInstance(dbContext, "WF-PT-1002", "PT-1002", "Active", null);
        AddRuntimeRows(dbContext, "WF-PT-1001", "PT-1001");
        AddRuntimeRows(dbContext, "WF-PT-1002", "PT-1002");
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Start(Request("PT-1001", reset: true));

        Assert.True(result.Envelope.Result.Success);
        Assert.Equal(new StartWorkflowResetCounts(1, 1, 1, 1, 1, 1), result.ResetCounts);
        Assert.Equal(2, dbContext.WorkflowInstances.Count());
        Assert.Contains(dbContext.WorkflowInstances, row => row.WorkflowInstanceId == "WF-PT-1001" && row.Version == 1 && row.CurrentStateKey == "Draft");
        Assert.Single(dbContext.WorkflowTasks.Where(row => row.WorkflowInstanceId == "WF-PT-1002"));
        Assert.Single(dbContext.WorkflowAudits.Where(row => row.WorkflowInstanceId == "WF-PT-1002"));
        Assert.Single(dbContext.NotificationOutbox.Where(row => row.WorkflowInstanceId == "WF-PT-1002"));
        Assert.Single(dbContext.IdempotencyRecords.Where(row => row.WorkflowInstanceId == "WF-PT-1002"));
        Assert.Single(dbContext.MockSectionData.Where(row => row.BusinessObjectId == "PT-1002"));
    }

    [Fact]
    public void ResetAgainstMissingInstanceReportsZeroCountsAndCreatesInstance()
    {
        using var dbContext = CreateSeededContext();

        var result = CreateService(dbContext).Start(Request("PT-NEW", reset: true));

        Assert.Equal(new StartWorkflowResetCounts(0, 0, 0, 0, 0, 0), result.ResetCounts);
        Assert.Single(dbContext.WorkflowInstances);
        Assert.Equal("WF-PT-NEW", dbContext.WorkflowInstances.Single().WorkflowInstanceId);
    }

    [Fact]
    public void ResetDoesNotDeleteSeedMetadata()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Active", null);
        dbContext.SaveChanges();
        var before = CountSeedRows(dbContext);

        CreateService(dbContext).Start(Request("PT-1001", reset: true));

        Assert.Equal(before, CountSeedRows(dbContext));
    }

    internal static WorkflowDbContext CreateSeededContext()
    {
        var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        TestDatabaseFirst.InitializeSchema(dbContext);
        new WorkflowSeedService(dbContext, new FixedTimeProvider(Now)).SeedPortfolioTrade();
        return dbContext;
    }

    internal static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_phase4_start_tests"))
            .Options;

        return new WorkflowDbContext(options);
    }

    private static StartWorkflowService CreateService(WorkflowDbContext dbContext)
    {
        dbContext.ChangeTracker.Clear();
        return new StartWorkflowService(TestSqlConnectionFactory.StoreFor(dbContext), new FixedTimeProvider(Now), new WorkflowActionResultBuilder());
    }

    private static StartWorkflowRequest Request(string businessObjectId, bool reset = false)
    {
        return new StartWorkflowRequest("PortfolioTrade", "PortfolioTrade", businessObjectId, "maker1", reset);
    }

    private static void AddInstance(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string businessObjectId,
        string status,
        DateTimeOffset? completedAtUtc)
    {
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            CurrentStateKey = status == "Completed" ? "Completed" : "CheckerReview",
            Status = status,
            StartedAtUtc = Now.AddMinutes(-30),
            StartedByUserId = "maker1",
            CompletedAtUtc = completedAtUtc,
            Version = status == "Completed" ? 8 : 3
        });
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }

    private static void AddRuntimeRows(WorkflowDbContext dbContext, string workflowInstanceId, string businessObjectId)
    {
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = workflowInstanceId,
            TaskKey = "CheckerReviewTask",
            AssignedRoleKey = "Checker",
            Status = "Pending",
            CreatedAtUtc = Now
        });
        dbContext.WorkflowAudits.Add(new WorkflowAudit
        {
            WorkflowInstanceId = workflowInstanceId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            ActionKey = "SubmitForChecker",
            FromStateKey = "Draft",
            ToStateKey = "CheckerReview",
            PerformedByUserId = "maker1",
            PerformedByRoleKey = "Maker",
            CorrelationId = $"corr-{businessObjectId}",
            CreatedAtUtc = Now
        });
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = workflowInstanceId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            NotificationType = "WorkflowTaskAssigned",
            RecipientRoleKey = "Checker",
            Subject = "subject",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = $"corr-{businessObjectId}",
            CreatedAtUtc = Now
        });
        dbContext.MockSectionData.Add(new MockSectionData
        {
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            SectionKey = "Maker",
            PayloadJson = "{}",
            SavedByUserId = "maker1",
            SavedAtUtc = Now,
            CorrelationId = $"corr-{businessObjectId}"
        });
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            IdempotencyKey = $"key#{workflowInstanceId}",
            WorkflowInstanceId = workflowInstanceId,
            ActionKey = "SubmitForChecker",
            RequestHash = "request",
            ResultHash = "result",
            ResultJson = "{}",
            CreatedAtUtc = Now
        });
    }

    private static SeedCounts CountSeedRows(WorkflowDbContext dbContext)
    {
        return new SeedCounts(
            dbContext.WorkflowDefinitions.Count(),
            dbContext.WorkflowDefinitionVersions.Count(),
            dbContext.WorkflowStateDefinitions.Count(),
            dbContext.WorkflowActionDefinitions.Count(),
            dbContext.WorkflowRoleDefinitions.Count(),
            dbContext.WorkflowTransitionDefinitions.Count(),
            dbContext.WorkflowTaskDefinitions.Count(),
            dbContext.SlaPolicies.Count(),
            dbContext.NotificationRoutes.Count());
    }

    private sealed record SeedCounts(
        int WorkflowDefinitions,
        int WorkflowDefinitionVersions,
        int WorkflowStateDefinitions,
        int WorkflowActionDefinitions,
        int WorkflowRoleDefinitions,
        int WorkflowTransitionDefinitions,
        int WorkflowTaskDefinitions,
        int SlaPolicies,
        int NotificationRoutes);
}

[CollectionDefinition("SqlServerStartWorkflow")]
public sealed class SqlServerStartWorkflowCollection
{
}
