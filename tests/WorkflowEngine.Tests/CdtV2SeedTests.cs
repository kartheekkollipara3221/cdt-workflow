using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Persistence;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Persistence.Verification;

namespace WorkflowEngine.Tests;

// Version 2 seed: v1 stays the default, one active version, removed rules are
// deactivated (never deleted), and v2 keeps the v1 request-input keys.
public sealed class CdtV2SeedTests
{
    private const string DatabaseName = "burrito_cdtv2_seed_tests";
    private static readonly FixedTimeProvider Clock = new(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));

    private static WorkflowDbContext FreshContext()
    {
        var db = CdtV2TestHarness.CreateContext(DatabaseName);
        db.Database.EnsureDeleted();
        db.Database.Migrate();
        return db;
    }

    private static int[] ActiveVersions(WorkflowDbContext db) =>
        db.WorkflowDefinitionVersions.AsNoTracking().Where(row => row.IsActive).Select(row => row.VersionNumber).OrderBy(row => row).ToArray();

    [Fact]
    public void VersionOneStaysTheDefaultAndVersionTwoStartsInactive()
    {
        using var db = FreshContext();
        var seed = new WorkflowSeedService(db, Clock);
        seed.SeedPortfolioTrade();
        Assert.Equal([1], ActiveVersions(db));
        Assert.Empty(new SeedVerificationService(db).Verify().Errors);

        seed.SeedPortfolioTradeVersion2(activate: false);
        db.ChangeTracker.Clear();
        Assert.Equal([1], ActiveVersions(db));
        Assert.Empty(new SeedVerificationService(db).Verify().Errors);
    }

    [Fact]
    public void ActivatingVersionTwoLeavesOneActiveVersionAndReseedingVersionOneDoesNotReactivateIt()
    {
        using var db = FreshContext();
        var seed = new WorkflowSeedService(db, Clock);
        seed.SeedPortfolioTrade();
        seed.SeedPortfolioTradeVersion2(activate: true);
        db.ChangeTracker.Clear();
        Assert.Equal([2], ActiveVersions(db));

        seed.SeedPortfolioTrade();
        db.ChangeTracker.Clear();
        Assert.Equal([2], ActiveVersions(db));
        Assert.Empty(new SeedVerificationService(db).Verify().Errors);

        seed.ActivatePortfolioTradeVersion(1);
        db.ChangeTracker.Clear();
        Assert.Equal([1], ActiveVersions(db));
    }

    [Fact]
    public void VerificationReportsTwoActiveVersions()
    {
        using var db = FreshContext();
        var seed = new WorkflowSeedService(db, Clock);
        seed.SeedPortfolioTrade();
        seed.SeedPortfolioTradeVersion2(activate: false);
        foreach (var version in db.WorkflowDefinitionVersions)
        {
            version.IsActive = true;
        }

        db.SaveChanges();
        Assert.Contains(
            "PortfolioTrade must have exactly one active workflow definition version; found 2.",
            new SeedVerificationService(db).Verify().Errors);
    }

    [Fact]
    public void ReseedingDeactivatesARuleThatTheSeedNoLongerListsAndKeepsTheRow()
    {
        using var db = FreshContext();
        var seed = new WorkflowSeedService(db, Clock);
        seed.SeedPortfolioTrade();
        seed.SeedPortfolioTradeVersion2(activate: true);
        var versionId = db.WorkflowDefinitionVersions.Single(row => row.VersionNumber == 2).Id;
        var route = db.NotificationRoutes.Single(row => row.WorkflowDefinitionVersionId == versionId && row.RouteKey == "RouteCheckerAssignedOnSubmit");
        db.NotificationRecipientRules.Add(new NotificationRecipientRule
        {
            NotificationRouteId = route.Id,
            RecipientKind = "To",
            SourceType = "StaticAddress",
            StaticAddress = "former.member@bank.test",
            SortOrder = 12,
            IsRequired = true,
            IsActive = true
        });
        db.SaveChanges();

        seed.SeedPortfolioTradeVersion2(activate: false);
        db.ChangeTracker.Clear();
        var row = db.NotificationRecipientRules.AsNoTracking().Single(item => item.NotificationRouteId == route.Id && item.SortOrder == 12);
        Assert.False(row.IsActive);
        Assert.Empty(new SeedVerificationService(db).Verify().Errors);
    }

    [Fact]
    public void VersionTwoDeclaresTheVersionOneRequestInputsExceptTheAgreedRemovals()
    {
        static Dictionary<string, string[]> KeysByAction(PortfolioTradeSeedDefinition definition) =>
            definition.Transitions
                .Where(row => row.NotificationRouteKey is not null)
                .GroupBy(row => row.ActionKey)
                .ToDictionary(
                    group => group.Key,
                    group => definition.NotificationRecipientRules
                        .Where(rule => group.Any(row => row.NotificationRouteKey == rule.RouteKey) && rule.SourceType == "RequestInput")
                        .Select(rule => rule.SourceKey!)
                        .Distinct()
                        .Order()
                        .ToArray());

        var v1 = KeysByAction(PortfolioTradeSeedData.Definition);
        var v2 = KeysByAction(PortfolioTradeSeedDataV2.Definition);
        string[] removedSelectedCcUsers = ["CheckerReturnToMaker", "TradingReturnToMaker", "BdrmTradeFulfilled"];

        Assert.Equal(v1.Keys.Order(), v2.Keys.Order());
        foreach (var (action, keys) in v1)
        {
            var expected = removedSelectedCcUsers.Contains(action) ? keys.Where(key => key != "selectedCcUsers").ToArray() : keys;
            Assert.True(expected.SequenceEqual(v2[action]), $"{action}: expected [{string.Join(",", expected)}], v2 [{string.Join(",", v2[action])}]");
        }
    }

    [Fact]
    public void InitDbAcceptsAnOptionalPortfolioTradeVersion()
    {
        var initializer = new RecordingInitializer();
        var registry = new CommandRegistry(databaseInitializer: initializer);

        Assert.True(registry.Dispatch(["init-db"]).ExitCode == 0);
        Assert.Equal("default", initializer.Calls.Single());
        Assert.True(registry.Dispatch(["init-db", "--portfolio-trade-version", "2"]).ExitCode == 0);
        Assert.Equal("2", initializer.Calls.Last());
        Assert.False(registry.Dispatch(["init-db", "--portfolio-trade-version", "3"]).ExitCode == 0);
        Assert.False(registry.Dispatch(["init-db", "--portfolio-trade-version"]).ExitCode == 0);
    }

    private sealed class RecordingInitializer : IWorkflowDatabaseInitializer, IPortfolioTradeVersionInitializer
    {
        public List<string> Calls { get; } = [];

        public string Initialize()
        {
            Calls.Add("default");
            return "ok";
        }

        public string Initialize(int activePortfolioTradeVersion)
        {
            Calls.Add(activePortfolioTradeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return "ok";
        }
    }
}
