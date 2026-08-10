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
    public void PortfolioTradeSeedVerificationReportsMissingAssignedRoleToRule()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
        new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();

        var routeId = dbContext.NotificationRoutes.Single(row =>
            row.RouteKey == "RouteTradingDeskAssigned").Id;
        var taxableRules = dbContext.NotificationRecipientRules.Where(row =>
            row.NotificationRouteId == routeId
            && row.RecipientKind == "To"
            && row.AssignedRoleKey == "TradingDeskTaxable");
        foreach (var rule in taxableRules)
        {
            rule.IsActive = false;
        }

        dbContext.SaveChanges();

        var result = new SeedVerificationService(dbContext).Verify();

        Assert.Contains(result.Errors, error => error ==
            "Notification route 'RouteTradingDeskAssigned' must have an active To recipient rule for assigned role 'TradingDeskTaxable'.");
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
                rule.AssignedRoleKey,
                rule.SortOrder,
                rule.IsRequired
            }).ToList();

        var expectedRules = new RecipientRuleExpectation[]
        {
            new("RouteCheckerAssignedOnSubmit", "To", "StaticAddress", null, "checker@example.test", null, 1, true),
            new("RouteCheckerAssignedOnSubmit", "Cc", "WorkflowParticipant", "originalMaker", null, null, 2, true),
            new("RouteCheckerAssignedOnSubmit", "Cc", "RequestInput", "selectedCcUsers", null, null, 3, false),
            new("RouteCheckerAssignedOnResubmit", "To", "StaticAddress", null, "checker@example.test", null, 1, true),
            new("RouteCheckerAssignedOnResubmit", "Cc", "WorkflowParticipant", "originalMaker", null, null, 2, true),
            new("RouteCheckerAssignedOnResubmit", "Cc", "RequestInput", "selectedCcUsers", null, null, 3, false),
            new("RouteReturnedToMakerFromChecker", "To", "WorkflowParticipant", "originalMaker", null, null, 1, true),
            new("RouteReturnedToMakerFromChecker", "Cc", "RequestInput", "selectedCcUsers", null, null, 2, false),
            new("RouteReturnedToMakerFromChecker", "Cc", "StaticAddress", null, "workflow-operations@example.test", null, 3, false),
            new("RouteTradingDeskAssigned", "To", "StaticAddress", null, "taxable-trading-desk@example.test", "TradingDeskTaxable", 1, true),
            new("RouteTradingDeskAssigned", "To", "StaticAddress", null, "taxable-trading-operations@example.test", "TradingDeskTaxable", 2, true),
            new("RouteTradingDeskAssigned", "To", "StaticAddress", null, "tax-exempt-trading-desk@example.test", "TradingDeskTaxExempt", 3, true),
            new("RouteTradingDeskAssigned", "To", "StaticAddress", null, "tax-exempt-trading-operations@example.test", "TradingDeskTaxExempt", 4, true),
            new("RouteTradingDeskAssigned", "To", "StaticAddress", null, "equity-trading-desk@example.test", "TradingDeskEquity", 5, true),
            new("RouteTradingDeskAssigned", "To", "StaticAddress", null, "equity-trading-operations@example.test", "TradingDeskEquity", 6, true),
            new("RouteTradingDeskAssigned", "Cc", "WorkflowParticipant", "originalMaker", null, null, 7, true),
            new("RouteTradingDeskAssigned", "Cc", "RequestInput", "selectedCcUsers", null, null, 8, false),
            new("RouteReturnedToMakerFromTradingDesk", "To", "WorkflowParticipant", "originalMaker", null, null, 1, true),
            new("RouteReturnedToMakerFromTradingDesk", "Cc", "RequestInput", "selectedCcUsers", null, null, 2, false),
            new("RouteBdrmAssigned", "To", "StaticAddress", null, "bdrm-client-service@example.test", null, 1, true),
            new("RouteBdrmAssigned", "Cc", "WorkflowParticipant", "originalMaker", null, null, 2, true),
            new("RouteBdrmAssigned", "Cc", "RequestInput", "selectedCcUsers", null, null, 3, false),
            new("RouteTradingDeskReworkAssigned", "To", "StaticAddress", null, "trading-desk@example.test", null, 1, true),
            new("RouteTradingDeskReworkAssigned", "Cc", "WorkflowParticipant", "originalMaker", null, null, 2, true),
            new("RouteTradingDeskReworkAssigned", "Cc", "RequestInput", "selectedCcUsers", null, null, 3, false),
            new("RouteWorkflowCompleted", "To", "WorkflowParticipant", "originalMaker", null, null, 1, true),
            new("RouteWorkflowCompleted", "Cc", "RequestInput", "selectedCcUsers", null, null, 2, false)
        };
        var routeOrder = expectedRules
            .Select(row => row.RouteKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var actualRules = rules
            .Select(row => new RecipientRuleExpectation(
                row.RouteKey,
                row.RecipientKind,
                row.SourceType,
                row.SourceKey,
                row.StaticAddress,
                row.AssignedRoleKey,
                row.SortOrder,
                row.IsRequired))
            .OrderBy(row => Array.IndexOf(routeOrder, row.RouteKey))
            .ThenBy(row => row.SortOrder)
            .ToArray();

        Assert.Equal(27, rules.Count);
        Assert.Equal(expectedRules, actualRules);
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

        var overdue = routes["RouteSlaTaskOverdue"];
        var dueToday = routes["RouteSlaTaskDueToday"];
        Assert.Equal("Due today: ", dueToday.OverdueSubjectPrefix);
        Assert.Equal("Overdue: ", overdue.OverdueSubjectPrefix);
        Assert.Contains("{{dueDateDisplay}}", overdue.BodyTemplate, StringComparison.Ordinal);
        Assert.Contains("{{overdueDateDisplay}}", overdue.BodyTemplate, StringComparison.Ordinal);
        Assert.Contains("{{tradeLink}}", overdue.BodyTemplate, StringComparison.Ordinal);
        Assert.Contains("{{dueDateDisplay}}", overdue.HtmlBodyTemplate, StringComparison.Ordinal);
        Assert.Contains("{{overdueDateDisplay}}", overdue.HtmlBodyTemplate, StringComparison.Ordinal);
        Assert.Contains("""<a href="{{tradeLink}}">Open request</a>""", overdue.HtmlBodyTemplate, StringComparison.Ordinal);
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
        Assert.Null(tradingPolicy.RecurrenceIntervalMinutes);
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

        var checker = policies["CheckerReviewSla"];
        Assert.Null(checker.ReminderLocalTime);
        Assert.Null(checker.TimeZoneId);
        Assert.Equal(2, checker.RecurrenceIntervalMinutes);
        Assert.Null(checker.RecurrenceIntervalDays);

        var tradingRework = policies["TradingDeskReworkSla"];
        Assert.Null(tradingRework.ReminderLocalTime);
        Assert.Null(tradingRework.TimeZoneId);
        Assert.Null(tradingRework.RecurrenceIntervalMinutes);
        Assert.Null(tradingRework.RecurrenceIntervalDays);

        var bdrm = policies["BdrmClientServiceReviewSla"];
        Assert.Equal(new TimeOnly(9, 0), bdrm.ReminderLocalTime);
        Assert.Equal("America/New_York", bdrm.TimeZoneId);
        Assert.Null(bdrm.RecurrenceIntervalMinutes);
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

    private sealed record RecipientRuleExpectation(
        string RouteKey,
        string RecipientKind,
        string SourceType,
        string? SourceKey,
        string? StaticAddress,
        string? AssignedRoleKey,
        int SortOrder,
        bool IsRequired);
}
