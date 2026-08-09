using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Persistence.Verification;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SeedDataSmokeTests
{
    [Fact]
    public void PortfolioTradeSeedVerificationPassesOnSqlServer()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();

        var seedService = new WorkflowSeedService(dbContext, TimeProvider.System);
        seedService.SeedPortfolioTrade();

        var result = new SeedVerificationService(dbContext).Verify();

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void PortfolioTradeTransitionsMatchRequiredMatrix()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();

        new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();
        var versionId = GetPortfolioTradeVersionId(dbContext);

        var transitions = dbContext.WorkflowTransitionDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => new
            {
                row.FromStateKey,
                row.ActionKey,
                row.AllowedRoleKey,
                row.ToStateKey,
                row.TaskToCloseKey,
                row.TaskToCreateKey,
                row.NotificationRouteKey
            })
            .ToList();

        Assert.Equal(11, transitions.Count);
        Assert.Contains(transitions, row => row.FromStateKey == "ReturnedToMaker" && row.ActionKey == "ResubmitForChecker" && row.TaskToCloseKey == "MakerReworkTask");
        Assert.Contains(transitions, row => row.FromStateKey == "CheckerReview" && row.ActionKey == "CheckerReturnToMaker" && row.TaskToCreateKey == "MakerReworkTask");
        Assert.Contains(transitions, row => row.FromStateKey == "TradingDeskReview" && row.ActionKey == "TradingReturnToMaker" && row.TaskToCreateKey == "MakerReworkTask");
    }

    [Fact]
    public void PortfolioTradeTradingDeskAssignmentRolesAreSeeded()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();

        new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();
        var versionId = GetPortfolioTradeVersionId(dbContext);

        var roleKeys = dbContext.WorkflowRoleDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.RoleKey)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("TradingDeskTaxable", roleKeys);
        Assert.Contains("TradingDeskTaxExempt", roleKeys);
        Assert.Contains("TradingDeskEquity", roleKeys);

        var assignmentRoles = dbContext.WorkflowTaskAssignmentRoleDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId && row.TaskKey == "TradingDeskReviewTask")
            .Select(row => row.RoleKey)
            .OrderBy(row => row)
            .ToArray();
        Assert.Equal(
            ["TradingDeskEquity", "TradingDeskTaxExempt", "TradingDeskTaxable"],
            assignmentRoles);

    }

    [Fact]
    public void PortfolioTradeBusinessObjectParticipantAndRecipientRulesAreSeeded()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();

        new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();
        var definition = Assert.Single(dbContext.WorkflowDefinitions.Where(row =>
            row.WorkflowKey == "PortfolioTrade"));
        Assert.Equal("PortfolioTrade", definition.BusinessObjectType);

        var versionId = GetPortfolioTradeVersionId(dbContext);
        var participant = Assert.Single(dbContext.WorkflowParticipantDefinitions.Where(row =>
            row.WorkflowDefinitionVersionId == versionId));
        Assert.Equal("originalMaker", participant.ParticipantKey);
        Assert.Equal("Single", participant.Cardinality);
        Assert.Equal("Start", participant.CaptureStage);
        Assert.True(participant.IsRequired);
        Assert.True(participant.IsImmutable);
        Assert.True(participant.IsActive);

        var rules = (
            from rule in dbContext.NotificationRecipientRules
            join route in dbContext.NotificationRoutes on rule.NotificationRouteId equals route.Id
            where route.WorkflowDefinitionVersionId == versionId
            select new
            {
                route.RouteKey,
                rule.RecipientKind,
                rule.SourceType,
                rule.SourceKey,
                rule.StaticAddress,
                rule.SortOrder,
                rule.IsRequired
            }).ToList();

        Assert.Equal(11, rules.Count);
        Assert.Contains(rules, row =>
            row.RouteKey == "RouteCheckerAssignedOnSubmit"
            && row.RecipientKind == "Cc"
            && row.SourceType == "RequestInput"
            && row.SourceKey == "selectedCcUsers"
            && !row.IsRequired);
        Assert.Contains(rules, row =>
            row.RouteKey == "RouteReturnedToMakerFromChecker"
            && row.RecipientKind == "To"
            && row.SourceType == "WorkflowParticipant"
            && row.SourceKey == "originalMaker"
            && row.IsRequired);
        Assert.Contains(rules, row =>
            row.RouteKey == "RouteReturnedToMakerFromChecker"
            && row.RecipientKind == "Cc"
            && row.SourceType == "StaticAddress"
            && row.StaticAddress == "workflow-operations@example.test");
        Assert.Contains(rules, row =>
            row.RouteKey == "RouteTradingDeskAssigned"
            && row.RecipientKind == "To"
            && row.SourceType == "StaticAddress"
            && row.StaticAddress == "trading-desk@example.test");
        Assert.DoesNotContain(rules, row =>
            row.RouteKey is "RouteSlaTaskDueToday" or "RouteSlaTaskOverdue");

        var transitionRouteKeys = dbContext.WorkflowTransitionDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId && row.NotificationRouteKey != null)
            .Select(row => row.NotificationRouteKey!)
            .Distinct()
            .ToList();
        Assert.All(transitionRouteKeys, routeKey =>
            Assert.Contains(rules, rule => rule.RouteKey == routeKey && rule.RecipientKind == "To"));
    }

    [Fact]
    public void NotificationRoutes_HaveSubjectBodyHtmlBodyAndOverduePrefix()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var routes = dbContext.NotificationRoutes.ToDictionary(row => row.RouteKey, StringComparer.Ordinal);

        var submit = routes["RouteCheckerAssignedOnSubmit"];
        Assert.Equal("Directed Trade Pending Checker Approval", submit.SubjectTemplate);
        Assert.Contains("{{submitterDisplayName}}", submit.BodyTemplate, StringComparison.Ordinal);
        Assert.Contains("<strong>{{submitterDisplayName}}</strong>", submit.HtmlBodyTemplate, StringComparison.Ordinal);
        Assert.Contains("""<a href="{{tradeLink}}">Open trade</a>""", submit.HtmlBodyTemplate, StringComparison.Ordinal);
        Assert.Equal("Overdue: ", submit.OverdueSubjectPrefix);

        var returned = routes["RouteReturnedToMakerFromChecker"];
        Assert.Equal("Updates Required to Directed Trade Request", returned.SubjectTemplate);
        Assert.DoesNotContain("returnReason", returned.BodyTemplate, StringComparison.Ordinal);
        Assert.Contains("<strong>{{recipientDisplayName}}</strong>", returned.HtmlBodyTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminDemoOverrideIsNotSeededAndMakerReworkHasNoSla()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();

        new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();
        var versionId = GetPortfolioTradeVersionId(dbContext);

        Assert.DoesNotContain(dbContext.WorkflowRoleDefinitions.Where(row => row.WorkflowDefinitionVersionId == versionId), row => row.RoleKey == "AdminDemoOverride");
        Assert.DoesNotContain(dbContext.WorkflowTransitionDefinitions.Where(row => row.WorkflowDefinitionVersionId == versionId), row => row.AllowedRoleKey == "AdminDemoOverride");

        var makerRework = Assert.Single(dbContext.WorkflowTaskDefinitions.Where(row =>
            row.WorkflowDefinitionVersionId == versionId && row.TaskKey == "MakerReworkTask"));
        Assert.Null(makerRework.SlaPolicyKey);
    }

    [Fact]
    public void TradingDeskReviewUsesApplicationTargetDatePolicy()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();

        new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();
        var versionId = GetPortfolioTradeVersionId(dbContext);

        var tradingPolicy = Assert.Single(dbContext.SlaPolicies.Where(row =>
            row.WorkflowDefinitionVersionId == versionId
            && row.SlaPolicyKey == "TradingDeskReviewSla"));

        Assert.Equal("ApplicationTargetDate", tradingPolicy.CalculationMode);
        Assert.Null(tradingPolicy.DurationMinutes);
        Assert.True(tradingPolicy.SendDueDateReminder);
        Assert.Equal(new TimeOnly(9, 0), tradingPolicy.ReminderLocalTime);
        Assert.Equal("America/New_York", tradingPolicy.TimeZoneId);
        Assert.Equal(1, tradingPolicy.OverdueStartOffsetDays);
        Assert.Equal(1, tradingPolicy.RecurrenceIntervalDays);
        Assert.Equal("RouteSlaTaskDueToday", tradingPolicy.DueNotificationRouteKey);
        Assert.Equal("RouteSlaTaskOverdue", tradingPolicy.OverdueNotificationRouteKey);

        var dueRoute = Assert.Single(dbContext.NotificationRoutes.Where(row =>
            row.WorkflowDefinitionVersionId == versionId
            && row.RouteKey == "RouteSlaTaskDueToday"));
        Assert.Equal("SlaTaskDueToday", dueRoute.NotificationType);
    }

    [Fact]
    public void OtherSeededSlaPoliciesRetainConfiguredDurationMode()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();

        new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();
        var versionId = GetPortfolioTradeVersionId(dbContext);

        var policies = dbContext.SlaPolicies
            .Where(row => row.WorkflowDefinitionVersionId == versionId
                && row.SlaPolicyKey != "TradingDeskReviewSla")
            .ToDictionary(row => row.SlaPolicyKey, StringComparer.Ordinal);

        Assert.Equal(3, policies.Count);
        Assert.All(policies.Values, policy =>
        {
            Assert.Equal("DurationFromTaskCreation", policy.CalculationMode);
            Assert.Equal(2, policy.DurationMinutes);
            Assert.False(policy.SendDueDateReminder);
            Assert.Equal(0, policy.OverdueStartOffsetDays);
            Assert.Null(policy.DueNotificationRouteKey);
            Assert.Equal("RouteSlaTaskOverdue", policy.OverdueNotificationRouteKey);
        });

        foreach (var policyKey in new[] { "CheckerReviewSla", "TradingDeskReworkSla" })
        {
            Assert.Null(policies[policyKey].ReminderLocalTime);
            Assert.Null(policies[policyKey].TimeZoneId);
            Assert.Null(policies[policyKey].RecurrenceIntervalDays);
        }

        var bdrm = policies["BdrmClientServiceReviewSla"];
        Assert.Equal(new TimeOnly(9, 0), bdrm.ReminderLocalTime);
        Assert.Equal("America/New_York", bdrm.TimeZoneId);
        Assert.Equal(1, bdrm.RecurrenceIntervalDays);
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var connectionString = TestSqlConnectionFactory.ConnectionString("burrito_phase2_tests");
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new WorkflowDbContext(options);
    }

    private static long GetPortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definition.Id && row.VersionNumber == 1).Id;
    }
}
