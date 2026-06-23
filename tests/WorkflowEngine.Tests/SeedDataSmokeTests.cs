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

        var route = Assert.Single(dbContext.NotificationRoutes.Where(row =>
            row.WorkflowDefinitionVersionId == versionId && row.RouteKey == "RouteTradingDeskAssigned"));
        Assert.Null(route.RecipientRoleKey);
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
