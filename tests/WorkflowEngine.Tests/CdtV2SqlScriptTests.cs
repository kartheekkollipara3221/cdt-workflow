using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Seed;

namespace WorkflowEngine.Tests;

// Runs the CDT v2 release scripts in sql/upgrades against SQL Server.
public sealed class CdtV2SqlScriptTests
{
    private const string ScriptDatabase = "burrito_cdtv2_script_tests";
    private const string SeedDatabase = "burrito_cdtv2_script_seed_tests";
    private static readonly FixedTimeProvider Clock = new(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));

    private static string Script(string name) =>
        File.ReadAllText(Path.Combine(SqmsDatabasePackageTests.RepositoryRoot(), "sql", "upgrades", name));

    private static void Execute(string databaseName, string sql)
    {
        using var connection = new SqlConnection(TestSqlConnectionFactory.ConnectionString(databaseName));
        connection.Open();
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        command.ExecuteNonQuery();
    }

    private static string Membership(string group, string address, string action) =>
        Script("20261019_03_recipient_group_membership_dml_template.sql")
            .Replace("DECLARE @Group nvarchar(30) = N'ICG_RA';", $"DECLARE @Group nvarchar(30) = N'{group}';", StringComparison.Ordinal)
            .Replace("DECLARE @Address nvarchar(320) = N'first.last@yourbank.com';", $"DECLARE @Address nvarchar(320) = N'{address}';", StringComparison.Ordinal)
            .Replace("DECLARE @Action nvarchar(10) = N'ADD';", $"DECLARE @Action nvarchar(10) = N'{action}';", StringComparison.Ordinal);

    private static WorkflowDbContext SeededVersionOne(string databaseName)
    {
        var db = CdtV2TestHarness.CreateContext(databaseName);
        db.Database.EnsureDeleted();
        db.Database.Migrate();
        new WorkflowSeedService(db, Clock).SeedPortfolioTrade();
        db.ChangeTracker.Clear();
        return db;
    }

    private static int[] ActiveVersions(WorkflowDbContext db) =>
        db.WorkflowDefinitionVersions.AsNoTracking().Where(row => row.IsActive).Select(row => row.VersionNumber).ToArray();

    [Fact]
    public void SchemaScriptAddsTheSixColumnsToAnUpgradedDatabaseAndCanRunAgain()
    {
        using (var db = CdtV2TestHarness.CreateContext(ScriptDatabase))
        {
            db.Database.EnsureDeleted();
            db.GetService<IMigrator>().Migrate("20261014222530_AddOutboxRetryScheduling");
        }

        Execute(ScriptDatabase, Script("20261019_01_cdt_v2_schema_ddl.sql"));
        Execute(ScriptDatabase, Script("20261019_01_cdt_v2_schema_ddl.sql"));

        using var check = CdtV2TestHarness.CreateContext(ScriptDatabase);
        var count = check.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*) AS Value FROM sys.columns
            WHERE (object_id = OBJECT_ID(N'dbo.SlaPolicy') AND name IN (N'DueReminderLocalTime', N'SkipMissedDueReminder', N'OverdueStartsNextLocalDay'))
               OR (object_id = OBJECT_ID(N'dbo.WorkflowTaskSlaSchedule') AND name IN (N'DueReminderLocalTime', N'DueRecipientsJson', N'OverdueRecipientsJson'))
            """).AsEnumerable().Single();
        Assert.Equal(6, count);
    }

    [Fact]
    public void CreateInactiveScriptProducesTheSameVersionTwoRowsAsTheSeed()
    {
        using var scripted = SeededVersionOne(ScriptDatabase);
        Execute(ScriptDatabase, Script("20261019_02_portfolio_trade_v2_create_inactive_dml.sql"));
        Assert.Throws<SqlException>(() => Execute(ScriptDatabase, Script("20261019_02_portfolio_trade_v2_create_inactive_dml.sql")));

        using var seeded = SeededVersionOne(SeedDatabase);
        new WorkflowSeedService(seeded, Clock).SeedPortfolioTradeVersion2(activate: false);
        seeded.ChangeTracker.Clear();

        Assert.Equal(VersionTwoRows(seeded), VersionTwoRows(scripted));
        Assert.Equal([1], ActiveVersions(scripted));
    }

    [Fact]
    public void ReleaseScriptsBlockPlaceholdersThenActivateAndRollBackForNewTrades()
    {
        using var db = SeededVersionOne(ScriptDatabase);
        Execute(ScriptDatabase, Script("20261019_02_portfolio_trade_v2_create_inactive_dml.sql"));

        Assert.Throws<SqlException>(() => Execute(ScriptDatabase, Script("20261019_04_portfolio_trade_v2_verify.sql")));
        Assert.Throws<SqlException>(() => Execute(ScriptDatabase, Script("20261019_05_portfolio_trade_v2_activate_dml.sql")));
        Assert.Equal([1], ActiveVersions(db));

        // The only desk user cannot be removed before a replacement exists.
        Assert.Throws<SqlException>(() => Execute(ScriptDatabase, Membership("DESK_EQUITY", "equity-trader-1@example.test", "DEACTIVATE")));

        foreach (var (group, real, placeholders) in new[]
        {
            ("ICG_RA", "ann.ra@bank.test", new[] { "icg-ra-1@example.test", "icg-ra-2@example.test" }),
            ("RISK", "rob.risk@bank.test", new[] { "risk-team-1@example.test", "risk-team-2@example.test" }),
            ("DESK_TAXABLE", "tom.tax@bank.test", new[] { "taxable-trader-1@example.test" }),
            ("DESK_TAX_EXEMPT", "eve.exempt@bank.test", new[] { "tax-exempt-trader-1@example.test" }),
            ("DESK_EQUITY", "ed.equity@bank.test", new[] { "equity-trader-1@example.test" })
        })
        {
            Execute(ScriptDatabase, Membership(group, real, "ADD"));
            Execute(ScriptDatabase, Membership(group, real, "ADD"));
            foreach (var placeholder in placeholders)
            {
                Execute(ScriptDatabase, Membership(group, placeholder, "DEACTIVATE"));
                Execute(ScriptDatabase, Membership(group, placeholder, "DEACTIVATE"));
            }
        }

        // A person in both groups keeps the Risk Team row when the ICG RA row is removed.
        Execute(ScriptDatabase, Membership("ICG_RA", "rob.risk@bank.test", "ADD"));
        Execute(ScriptDatabase, Membership("ICG_RA", "rob.risk@bank.test", "DEACTIVATE"));
        var robRows = db.NotificationRecipientRules.AsNoTracking()
            .Where(row => row.StaticAddress == "rob.risk@bank.test")
            .Select(row => new { row.SortOrder, row.IsActive })
            .ToList();
        Assert.All(robRows.Where(row => row.SortOrder >= 50), row => Assert.True(row.IsActive));
        Assert.All(robRows.Where(row => row.SortOrder < 50), row => Assert.False(row.IsActive));
        Execute(ScriptDatabase, Membership("ICG_RA", "rob.risk@bank.test", "ADD"));
        Assert.Equal(robRows.Count, db.NotificationRecipientRules.AsNoTracking().Count(row => row.StaticAddress == "rob.risk@bank.test"));

        Assert.Throws<SqlException>(() => Execute(ScriptDatabase, Membership("RISK", "a@bank.test;b@bank.test", "ADD")));

        Execute(ScriptDatabase, Script("20261019_04_portfolio_trade_v2_verify.sql"));
        Execute(ScriptDatabase, Script("20261019_05_portfolio_trade_v2_activate_dml.sql"));
        Assert.Equal([2], ActiveVersions(db));

        Execute(ScriptDatabase, Script("20261019_06_portfolio_trade_v1_reactivate_dml.sql"));
        Assert.Equal([1], ActiveVersions(db));
    }

    [Fact]
    public void MembershipTemplateStopsWhenTheGroupBlockIsFull()
    {
        using var db = SeededVersionOne(ScriptDatabase);
        Execute(ScriptDatabase, Script("20261019_02_portfolio_trade_v2_create_inactive_dml.sql"));

        // ICG RA block 10-49 holds 40 rows; the seed already uses 10 and 11.
        for (var index = 0; index < 38; index++)
        {
            Execute(ScriptDatabase, Membership("ICG_RA", $"ra{index}@bank.test", "ADD"));
        }

        var error = Assert.Throws<SqlException>(() => Execute(ScriptDatabase, Membership("ICG_RA", "one.more@bank.test", "ADD")));
        Assert.Equal(52306, error.Number);
        Assert.Equal(0, db.NotificationRecipientRules.AsNoTracking().Count(row => row.StaticAddress == "one.more@bank.test"));
    }

    private static List<string> VersionTwoRows(WorkflowDbContext db)
    {
        var versionId = db.WorkflowDefinitionVersions.AsNoTracking().Single(row => row.VersionNumber == 2).Id;
        var routes = db.NotificationRoutes.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList();
        var routeKeys = routes.ToDictionary(row => row.Id, row => row.RouteKey);
        var rows = new List<string>();
        rows.AddRange(db.WorkflowStateDefinitions.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"state|{row.StateKey}|{row.DisplayName}|{row.IsTerminal}|{row.IsActive}"));
        rows.AddRange(db.WorkflowParticipantDefinitions.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"participant|{row.ParticipantKey}|{row.Cardinality}|{row.CaptureStage}|{row.IsRequired}|{row.IsImmutable}|{row.IsActive}"));
        rows.AddRange(db.WorkflowRoleDefinitions.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"role|{row.RoleKey}|{row.DisplayName}|{row.IsActive}"));
        rows.AddRange(db.WorkflowActionDefinitions.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"action|{row.ActionKey}|{row.DisplayName}|{row.SectionKey}|{row.ButtonKey}|{row.ButtonLabel}|{row.Intent}|{row.RequiresReason}|{row.SortOrder}|{row.IsActive}"));
        rows.AddRange(db.SlaPolicies.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"policy|{row.SlaPolicyKey}|{row.CalculationMode}|{row.DurationMinutes}|{row.SendDueDateReminder}|{row.ReminderLocalTime}|{row.TimeZoneId}|{row.OverdueStartOffsetDays}|{row.RecurrenceIntervalMinutes}|{row.RecurrenceIntervalDays}|{row.DueNotificationRouteKey}|{row.OverdueNotificationRouteKey}|{row.DueReminderLocalTime}|{row.SkipMissedDueReminder}|{row.OverdueStartsNextLocalDay}|{row.IsActive}"));
        rows.AddRange(db.WorkflowTaskDefinitions.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"task|{row.TaskKey}|{row.DisplayName}|{row.OwnerRoleKey}|{row.SlaPolicyKey}|{row.IsActive}"));
        rows.AddRange(db.WorkflowTaskAssignmentRoleDefinitions.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"assignment|{row.TaskKey}|{row.RoleKey}|{row.IsActive}"));
        rows.AddRange(routes.Select(row => $"route|{row.RouteKey}|{row.NotificationType}|{row.SubjectTemplate}|{row.BodyTemplate}|{row.HtmlBodyTemplate}|{row.OverdueSubjectPrefix}|{row.IsActive}"));
        rows.AddRange(db.NotificationRecipientRules.AsNoTracking().Where(row => routeKeys.Keys.Contains(row.NotificationRouteId)).ToList()
            .Select(row => $"rule|{routeKeys[row.NotificationRouteId]}|{row.SortOrder}|{row.RecipientKind}|{row.SourceType}|{row.SourceKey}|{row.StaticAddress}|{row.AssignedRoleKey}|{row.IsRequired}|{row.IsActive}"));
        rows.AddRange(db.WorkflowTransitionDefinitions.AsNoTracking().Where(row => row.WorkflowDefinitionVersionId == versionId).ToList()
            .Select(row => $"transition|{row.FromStateKey}|{row.ActionKey}|{row.AllowedRoleKey}|{row.ToStateKey}|{row.TaskToCloseKey}|{row.TaskToCreateKey}|{row.NotificationRouteKey}|{row.AuditLabel}|{row.IsActive}"));
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }
}
