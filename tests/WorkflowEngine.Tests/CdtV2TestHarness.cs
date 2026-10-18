using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Sla;
using WorkflowEngine.Infrastructure.WorkflowContext;
using WorkflowEngine.Infrastructure.WorkflowExecution;

namespace WorkflowEngine.Tests;

// Drives PortfolioTrade v1/v2 workflows through the engine with a fixed clock,
// the same way the target does: typed recipient inputs, no explicit recipients.
internal sealed class CdtV2TestHarness : IDisposable
{
    public const string MakerAddress = "maker@bank.test";

    private CdtV2TestHarness(WorkflowDbContext db)
    {
        Db = db;
    }

    public WorkflowDbContext Db { get; }

    public static CdtV2TestHarness Create(string databaseName, int activeVersion)
    {
        var db = CreateContext(databaseName);
        db.Database.EnsureDeleted();
        db.Database.Migrate();
        var seed = new WorkflowSeedService(db, new FixedTimeProvider(DateTimeOffset.Parse("2030-01-01T00:00:00Z")));
        seed.SeedPortfolioTrade();
        seed.SeedPortfolioTradeVersion2(activate: activeVersion == 2);
        db.ChangeTracker.Clear();
        return new CdtV2TestHarness(db);
    }

    public static WorkflowDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString(databaseName))
            .Options;
        return new WorkflowDbContext(options);
    }

    public long VersionId(int versionNumber) =>
        Db.WorkflowDefinitionVersions.AsNoTracking().Single(row => row.VersionNumber == versionNumber).Id;

    // Inserts a Draft instance and its captured originalMaker participant.
    public string StartTrade(string businessObjectId, int versionNumber, DateTimeOffset now)
    {
        var instanceId = "WF-" + businessObjectId;
        Db.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = instanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = VersionId(versionNumber),
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            CurrentStateKey = "Draft",
            Status = "Active",
            StartedAtUtc = now,
            StartedByUserId = "maker1",
            Version = 1
        });
        Db.WorkflowParticipants.Add(new WorkflowParticipant
        {
            WorkflowInstanceId = instanceId,
            ParticipantKey = "originalMaker",
            UserId = "maker1",
            EmailAddress = MakerAddress,
            DisplayName = "Maker One",
            SortOrder = 1,
            CreatedAtUtc = now
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return instanceId;
    }

    public WorkflowActionResultEnvelope Act(
        string businessObjectId,
        string actionKey,
        string role,
        DateTimeOffset now,
        string? desk = null,
        DateOnly? targetDate = null,
        string[]? ccUsers = null,
        bool sendCcInput = true)
    {
        Db.ChangeTracker.Clear();
        var instanceId = "WF-" + businessObjectId;
        var instance = Db.WorkflowInstances.AsNoTracking().Single(row => row.WorkflowInstanceId == instanceId);
        var versionNumber = Db.WorkflowDefinitionVersions.AsNoTracking()
            .Single(row => row.Id == instance.WorkflowDefinitionVersionId).VersionNumber;

        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.BusinessObject!.ObjectId = businessObjectId;
        envelope.Workflow!.WorkflowInstanceId = instanceId;
        envelope.Workflow.WorkflowDefinitionVersion = versionNumber;
        envelope.Workflow.ExpectedCurrentState = instance.CurrentStateKey;
        envelope.Workflow.ExpectedInstanceVersion = instance.Version;
        envelope.Workflow.ExpectedCurrentTaskKey = null;
        envelope.Workflow.ActionKey = actionKey;
        envelope.Workflow.AssignedRoleKey = desk;
        envelope.Workflow.SlaInput = targetDate is null ? null : new WorkflowSlaInput { TargetDate = targetDate };
        envelope.Workflow.IdempotencyKey = Guid.NewGuid().ToString("N");
        envelope.Workflow.NotificationRecipients = null;
        envelope.Workflow.NotificationContext = TemplateData(desk);
        envelope.Request!.ClientActionId = Guid.NewGuid().ToString("N");
        envelope.Actor!.Roles = [role];
        var (section, button) = actionKey switch
        {
            "CheckerReturnToMaker" or "CheckerSendToTradingDesk" => ("Checker", "CheckerPrimary"),
            "TradingReturnToMaker" or "TradingTradeExecuted" => ("TradingDesk", "TradingPrimary"),
            "BdrmTradeFulfilled" or "BdrmTradeNotExecutedProperly" => ("BDRMClientService", "BdrmPrimary"),
            _ => ("Maker", "MakerPrimary")
        };
        envelope.UiAction!.SectionKey = section;
        envelope.UiAction.ButtonKey = button;
        envelope.DataSave!.SectionKey = section;

        var recipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);
        if (sendCcInput)
        {
            recipientInputs["selectedCcUsers"] = (ccUsers ?? [])
                .Select((address, index) => new WorkflowRecipientInput($"cc{index}", address, address))
                .ToArray();
        }

        var input = new WorkflowNotificationInput
        {
            RecipientInputs = recipientInputs,
            TemplateData = envelope.Workflow.NotificationContext
        };

        var normalized = new WorkflowActionEnvelopeNormalizer().Normalize(envelope, null, null);
        var time = new FixedTimeProvider(now);
        var service = new WorkflowExecutionService(
            Db,
            new WorkflowContextReader(Db, time),
            time,
            new WorkflowActionResultBuilder(),
            new CanonicalWorkflowResultSerializer(),
            new NotificationTemplateRenderer());
        var result = service.Execute(new WorkflowExecutionRequest(
            normalized,
            new CanonicalRequestHasher().ComputeHash(normalized.Envelope),
            false) { NotificationInput = input });
        Db.ChangeTracker.Clear();
        return result;
    }

    public WorkflowActionResultEnvelope ActOk(
        string businessObjectId,
        string actionKey,
        string role,
        DateTimeOffset now,
        string? desk = null,
        DateOnly? targetDate = null,
        string[]? ccUsers = null,
        bool sendCcInput = true)
    {
        var result = Act(businessObjectId, actionKey, role, now, desk, targetDate, ccUsers, sendCcInput);
        Assert.True(result.Result.Success, $"{actionKey}: {result.Result.ResultCode} {result.Result.Message}");
        return result;
    }

    public async Task<RunSlaResult> RunSla(DateTimeOffset now)
    {
        Db.ChangeTracker.Clear();
        var result = await new RunSlaService(Db, new SlaScheduleCalculator()).RunAsync(new RunSlaRequest(now), CancellationToken.None);
        Db.ChangeTracker.Clear();
        return result;
    }

    public WorkflowTask PendingTask(string businessObjectId) =>
        Db.WorkflowTasks.AsNoTracking().Single(row => row.WorkflowInstanceId == "WF-" + businessObjectId && row.Status == "Pending");

    public WorkflowTaskSlaSchedule ActiveSchedule(long taskId) =>
        Db.WorkflowTaskSlaSchedules.AsNoTracking().Single(row => row.WorkflowTaskId == taskId && row.Status == "Active");

    public NotificationOutbox TaskEmail(long taskId) =>
        Db.NotificationOutbox.AsNoTracking().Single(row => row.WorkflowTaskId == taskId && row.SlaReminderKind == null);

    public List<NotificationOutbox> Reminders(long taskId) =>
        Db.NotificationOutbox.AsNoTracking()
            .Where(row => row.WorkflowTaskId == taskId && row.SlaReminderKind != null)
            .OrderBy(row => row.SlaOccurrenceAtUtc)
            .ToList();

    public NotificationOutbox LastWorkflowEmail(string businessObjectId, string notificationType) =>
        Db.NotificationOutbox.AsNoTracking()
            .Where(row => row.WorkflowInstanceId == "WF-" + businessObjectId && row.NotificationType == notificationType)
            .OrderByDescending(row => row.Id)
            .First();

    public static (string[] To, string[] Cc) Recipients(string json)
    {
        var recipients = JsonSerializer.Deserialize<NotificationRecipients>(json, WorkflowJson.Options)!;
        return (recipients.ToEmailAddresses.ToArray(), recipients.CcEmailAddresses.ToArray());
    }

    // Adds or deactivates one static rule the same way the membership template does.
    public void SetStaticRule(int versionNumber, string routeKey, int sortOrder, string address, string? assignedRoleKey, bool active)
    {
        Db.ChangeTracker.Clear();
        var versionId = VersionId(versionNumber);
        var route = Db.NotificationRoutes.Single(row => row.WorkflowDefinitionVersionId == versionId && row.RouteKey == routeKey);
        var rule = Db.NotificationRecipientRules.SingleOrDefault(row => row.NotificationRouteId == route.Id && row.SortOrder == sortOrder);
        if (rule is null)
        {
            rule = new NotificationRecipientRule
            {
                NotificationRouteId = route.Id,
                SortOrder = sortOrder,
                RecipientKind = "To",
                SourceType = "StaticAddress",
                StaticAddress = address,
                IsRequired = true,
                AssignedRoleKey = assignedRoleKey
            };
            Db.NotificationRecipientRules.Add(rule);
        }

        rule.IsActive = active;
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public WorkflowSlaSynchronizationResult ChangeTargetDate(string businessObjectId, DateOnly targetDate, DateTimeOffset now)
    {
        Db.ChangeTracker.Clear();
        var result = new WorkflowSlaSynchronizationService(Db, new FixedTimeProvider(now), new SlaScheduleCalculator())
            .Synchronize(new WorkflowSlaSynchronizationRequest(
                "WF-" + businessObjectId, null, targetDate, "maker1", Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        Db.ChangeTracker.Clear();
        return result;
    }

    private static Dictionary<string, string?> TemplateData(string? desk) => new(StringComparer.Ordinal)
    {
        ["referenceNumber"] = "1050",
        ["submitterDisplayName"] = "Maker One",
        ["returnedByDisplayName"] = "Checker One",
        ["recipientDisplayName"] = "Maker One",
        ["traderDisplayName"] = "Trader One",
        ["confirmerDisplayName"] = "Maker One",
        ["deskName"] = desk ?? "Taxable",
        ["securityType"] = "Other",
        ["accountShortName"] = "EFG",
        ["accountDisplayName"] = "EFG INSURANCE",
        ["submittedDate"] = "1/15/2030",
        ["tradeLink"] = "https://atlas.example.test/stepmaster/CDT/1050"
    };

    public void Dispose()
    {
        Db.Dispose();
    }
}
