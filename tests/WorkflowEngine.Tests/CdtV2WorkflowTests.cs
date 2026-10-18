using WorkflowEngine.Infrastructure.Persistence.Seed;

namespace WorkflowEngine.Tests;

// PortfolioTrade v2 (CDT notification routes): recipients of every email and
// reminder, SLA timing, saved reminder audiences and v1 compatibility.
public sealed class CdtV2WorkflowTests
{
    private const string DatabaseName = "burrito_cdtv2_workflow_tests";
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly string[] CheckerGroups =
        ["icg-ra-1@example.test", "icg-ra-2@example.test", "risk-team-1@example.test", "risk-team-2@example.test"];
    private static readonly string[] RiskTeam = ["risk-team-1@example.test", "risk-team-2@example.test"];

    private static DateTimeOffset Local(int month, int day, int hour, int minute = 0)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2030, month, day, hour, minute, 0, DateTimeKind.Unspecified), NewYork);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static DateOnly Date(int month, int day) => new(2030, month, day);

    private static void AssertRecipients(string json, string[] expectedTo, string[] expectedCc)
    {
        var (to, cc) = CdtV2TestHarness.Recipients(json);
        Assert.Equal(expectedTo, to);
        Assert.Equal(expectedCc, cc);
    }

    private static string DeskAddress(string desk) => desk switch
    {
        "TradingDeskTaxable" => "taxable-trader-1@example.test",
        "TradingDeskTaxExempt" => "tax-exempt-trader-1@example.test",
        _ => "equity-trader-1@example.test"
    };

    private static void ToTradingDesk(CdtV2TestHarness harness, string id, string desk, DateOnly target, DateTimeOffset submitAt, DateTimeOffset sendAt, string[]? cc = null)
    {
        harness.ActOk(id, "SubmitForChecker", "Maker", submitAt, ccUsers: ["watcher@bank.test"]);
        harness.ActOk(id, "CheckerSendToTradingDesk", "Checker", sendAt, desk, target, cc ?? ["rm@bank.test", "watcher@bank.test"]);
    }

    [Fact]
    public async Task CheckerTaskEmailAndOverdueRemindersGoToIcgRasAndRiskTeamWithSelectedCcOnly()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-101", 2, Local(1, 15, 9));
        harness.ActOk("CDT-101", "SubmitForChecker", "Maker", Local(1, 15, 10), ccUsers: ["watcher@bank.test"]);

        var task = harness.PendingTask("CDT-101");
        Assert.Equal("CheckerReviewTask", task.TaskKey);
        var (to, cc) = CdtV2TestHarness.Recipients(task.NotificationRecipientsJson);
        Assert.Equal(CheckerGroups, to);
        Assert.Equal(["watcher@bank.test"], cc);
        Assert.Equal(task.NotificationRecipientsJson, harness.TaskEmail(task.Id).NotificationRecipientsJson);

        // RouteSlaTaskOverdue has no rule rows, so the reminder reuses the task snapshot.
        var schedule = harness.ActiveSchedule(task.Id);
        Assert.Null(schedule.DueRecipientsJson);
        Assert.Null(schedule.OverdueRecipientsJson);
        Assert.Equal(Local(1, 15, 11), schedule.OverdueAtUtc);

        await harness.RunSla(Local(1, 15, 10, 59));
        Assert.Empty(harness.Reminders(task.Id));
        await harness.RunSla(Local(1, 15, 11));
        await harness.RunSla(Local(1, 15, 12));
        var reminders = harness.Reminders(task.Id);
        Assert.Equal(2, reminders.Count);
        Assert.All(reminders, reminder =>
        {
            Assert.Equal("Overdue: Directed Trade Pending Checker Approval", reminder.Subject);
            Assert.Equal(task.NotificationRecipientsJson, reminder.NotificationRecipientsJson);
        });
    }

    [Theory]
    [InlineData("TradingDeskTaxable")]
    [InlineData("TradingDeskTaxExempt")]
    [InlineData("TradingDeskEquity")]
    public async Task TradingDeskEmailIncludesGroupsAndRmWhileRemindersGoToTheSelectedDeskOnly(string desk)
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-201", 2, Local(1, 15, 9));
        ToTradingDesk(harness, "CDT-201", desk, Date(1, 15), Local(1, 15, 9, 30), Local(1, 15, 10, 30));

        var task = harness.PendingTask("CDT-201");
        Assert.Equal(desk, task.AssignedRoleKey);
        var (to, cc) = CdtV2TestHarness.Recipients(harness.TaskEmail(task.Id).NotificationRecipientsJson);
        Assert.Equal([.. CheckerGroups, DeskAddress(desk)], to);
        Assert.Equal(["rm@bank.test", "watcher@bank.test"], cc);

        var schedule = harness.ActiveSchedule(task.Id);
        Assert.Equal(new TimeOnly(14, 0), schedule.DueReminderLocalTime);
        Assert.Equal(Local(1, 15, 14), schedule.DueReminderAtUtc);
        Assert.Equal(Local(1, 16, 8), schedule.OverdueAtUtc);
        AssertRecipients(schedule.DueRecipientsJson!, [DeskAddress(desk)], []);
        AssertRecipients(schedule.OverdueRecipientsJson!, [DeskAddress(desk)], []);

        await harness.RunSla(Local(1, 15, 13, 59));
        Assert.Empty(harness.Reminders(task.Id));
        await harness.RunSla(Local(1, 15, 14));
        await harness.RunSla(Local(1, 16, 8));
        await harness.RunSla(Local(1, 19, 8)); // Saturday: weekends included, no backfill of 17th and 18th.

        var reminders = harness.Reminders(task.Id);
        Assert.Equal(["DueToday", "Overdue", "Overdue"], reminders.Select(row => row.SlaReminderKind));
        Assert.Equal("Due Today: Directed Trade Request Pending Execution", reminders[0].Subject);
        Assert.Equal("Overdue: Directed Trade Request Pending Execution", reminders[1].Subject);
        Assert.Equal(Date(1, 19), reminders[2].SlaOccurrenceDate);
        Assert.All(reminders, reminder =>
            AssertRecipients(reminder.NotificationRecipientsJson, [DeskAddress(desk)], []));
    }

    [Fact]
    public async Task SameDayTradeAfterTwoPmSkipsDueTodayAndStartsOverdueNextDayAtEight()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-301", 2, Local(1, 15, 9));
        ToTradingDesk(harness, "CDT-301", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 14, 30), Local(1, 15, 15));

        var task = harness.PendingTask("CDT-301");
        var schedule = harness.ActiveSchedule(task.Id);
        Assert.Null(schedule.DueReminderAtUtc);
        Assert.Equal(Local(1, 16, 8), schedule.NextReminderAtUtc);

        await harness.RunSla(Local(1, 15, 15, 30));
        await harness.RunSla(Local(1, 15, 23));
        Assert.Empty(harness.Reminders(task.Id));
        await harness.RunSla(Local(1, 16, 8));
        var reminder = Assert.Single(harness.Reminders(task.Id));
        Assert.Equal("Overdue", reminder.SlaReminderKind);
        Assert.Equal("Overdue: Directed Trade Request Pending Execution", reminder.Subject);
    }

    [Fact]
    public async Task FutureTargetDateSendsDueTodayAtTwoPmOnThatDate()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-302", 2, Local(1, 14, 9));
        ToTradingDesk(harness, "CDT-302", "TradingDeskEquity", Date(1, 16), Local(1, 14, 9, 30), Local(1, 14, 10));

        var task = harness.PendingTask("CDT-302");
        await harness.RunSla(Local(1, 15, 14));
        Assert.Empty(harness.Reminders(task.Id));
        await harness.RunSla(Local(1, 16, 14));
        await harness.RunSla(Local(1, 17, 8));
        var reminders = harness.Reminders(task.Id);
        Assert.Equal(["DueToday", "Overdue"], reminders.Select(row => row.SlaReminderKind));
        Assert.Equal(Local(1, 16, 14), reminders[0].SlaOccurrenceAtUtc);
        Assert.Equal(Local(1, 17, 8), reminders[1].SlaOccurrenceAtUtc);
    }

    [Fact]
    public async Task LateScansOnTheTargetDateAndTheNextDateFollowTheCatchUpRules()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-401", 2, Local(1, 15, 9));
        ToTradingDesk(harness, "CDT-401", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 9, 30), Local(1, 15, 10));
        var task = harness.PendingTask("CDT-401");

        // A late scan on the target date sends Due Today once.
        await harness.RunSla(Local(1, 15, 14, 20));
        await harness.RunSla(Local(1, 15, 14, 25));
        Assert.Equal(["DueToday"], harness.Reminders(task.Id).Select(row => row.SlaReminderKind));

        // On the next date nothing is queued before 08:00; then one Overdue, no duplicate.
        await harness.RunSla(Local(1, 16, 7));
        Assert.Single(harness.Reminders(task.Id));
        await harness.RunSla(Local(1, 16, 8));
        await harness.RunSla(Local(1, 16, 8, 5));
        Assert.Equal(["DueToday", "Overdue"], harness.Reminders(task.Id).Select(row => row.SlaReminderKind));
    }

    [Fact]
    public async Task ScannerDownForTheWholeTargetDateNeverSendsTheMissedDueToday()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-402", 2, Local(1, 15, 9));
        ToTradingDesk(harness, "CDT-402", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 9, 30), Local(1, 15, 10));
        var task = harness.PendingTask("CDT-402");

        await harness.RunSla(Local(1, 16, 7));
        Assert.Empty(harness.Reminders(task.Id));
        await harness.RunSla(Local(1, 16, 9));
        var reminder = Assert.Single(harness.Reminders(task.Id));
        Assert.Equal("Overdue", reminder.SlaReminderKind);
        Assert.Equal(Local(1, 16, 8), reminder.SlaOccurrenceAtUtc);
    }

    [Fact]
    public async Task OverdueKeepsEightAmLocalAcrossTheDaylightSavingChange()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-403", 2, Local(3, 8, 9));
        ToTradingDesk(harness, "CDT-403", "TradingDeskTaxable", Date(3, 8), Local(3, 8, 9, 30), Local(3, 8, 10));
        var task = harness.PendingTask("CDT-403");

        await harness.RunSla(Local(3, 8, 14));
        await harness.RunSla(Local(3, 9, 8));
        await harness.RunSla(Local(3, 10, 8)); // DST starts on 2030-03-10 in New York.
        await harness.RunSla(Local(3, 11, 8));
        var occurrences = harness.Reminders(task.Id).Select(row => row.SlaOccurrenceAtUtc!.Value.UtcDateTime).ToArray();
        Assert.Equal(
            [
                new DateTime(2030, 3, 8, 19, 0, 0, DateTimeKind.Utc),
                new DateTime(2030, 3, 9, 13, 0, 0, DateTimeKind.Utc),
                new DateTime(2030, 3, 10, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2030, 3, 11, 12, 0, 0, DateTimeKind.Utc)
            ],
            occurrences);
    }

    [Fact]
    public async Task BdrmConfirmationGoesToMakerAndRiskWithRmCcAndOverdueGoesToMakerNextDayAtNine()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-501", 2, Local(1, 14, 8));
        ToTradingDesk(harness, "CDT-501", "TradingDeskTaxable", Date(1, 14), Local(1, 14, 8, 30), Local(1, 14, 9));
        harness.ActOk("CDT-501", "TradingTradeExecuted", "TradingDesk", Local(1, 14, 10, 30), ccUsers: ["rm@bank.test"]);

        var task = harness.PendingTask("CDT-501");
        Assert.Equal("BdrmClientServiceReviewTask", task.TaskKey);
        var (to, cc) = CdtV2TestHarness.Recipients(harness.TaskEmail(task.Id).NotificationRecipientsJson);
        Assert.Equal([CdtV2TestHarness.MakerAddress, .. RiskTeam], to);
        Assert.Equal(["rm@bank.test"], cc);

        var schedule = harness.ActiveSchedule(task.Id);
        Assert.Equal(Local(1, 15, 9), schedule.OverdueAtUtc);
        AssertRecipients(schedule.OverdueRecipientsJson!, [CdtV2TestHarness.MakerAddress], []);

        await harness.RunSla(Local(1, 15, 8, 59));
        Assert.Empty(harness.Reminders(task.Id));
        await harness.RunSla(Local(1, 15, 9));
        await harness.RunSla(Local(1, 16, 9));
        var reminders = harness.Reminders(task.Id);
        Assert.Equal([Local(1, 15, 9), Local(1, 16, 9)], reminders.Select(row => row.SlaOccurrenceAtUtc!.Value));
        Assert.All(reminders, reminder =>
        {
            Assert.Equal("Overdue: Trade Execution Pending Confirmation", reminder.Subject);
            AssertRecipients(reminder.NotificationRecipientsJson, [CdtV2TestHarness.MakerAddress], []);
        });
    }

    [Fact]
    public void CompletionEmailGoesToMakerAndRiskOnlyAndRejectsSelectedCcUsers()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-601", 2, Local(1, 14, 8));
        ToTradingDesk(harness, "CDT-601", "TradingDeskTaxable", Date(1, 14), Local(1, 14, 8, 30), Local(1, 14, 9));
        harness.ActOk("CDT-601", "TradingTradeExecuted", "TradingDesk", Local(1, 14, 10), ccUsers: ["rm@bank.test"]);

        var rejected = harness.Act("CDT-601", "BdrmTradeFulfilled", "BDRMClientService", Local(1, 14, 11), ccUsers: ["rm@bank.test"]);
        Assert.False(rejected.Result.Success);
        Assert.Equal("ValidationFailed", rejected.Result.ResultCode);

        harness.ActOk("CDT-601", "BdrmTradeFulfilled", "BDRMClientService", Local(1, 14, 11), sendCcInput: false);
        var email = harness.LastWorkflowEmail("CDT-601", "WorkflowCompleted");
        AssertRecipients(email.NotificationRecipientsJson, [CdtV2TestHarness.MakerAddress, .. RiskTeam], []);
    }

    [Theory]
    [InlineData("CheckerReturnToMaker")]
    [InlineData("TradingReturnToMaker")]
    public void UpdatesRequiredEmailGoesToMakerOnlyAndRejectsSelectedCcUsers(string returnAction)
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-701", 2, Local(1, 15, 8));
        harness.ActOk("CDT-701", "SubmitForChecker", "Maker", Local(1, 15, 9), ccUsers: ["watcher@bank.test"]);
        var role = "Checker";
        if (returnAction == "TradingReturnToMaker")
        {
            harness.ActOk("CDT-701", "CheckerSendToTradingDesk", "Checker", Local(1, 15, 9, 30), "TradingDeskTaxable", Date(1, 15), ["rm@bank.test"]);
            role = "TradingDesk";
        }

        var rejected = harness.Act("CDT-701", returnAction, role, Local(1, 15, 10), ccUsers: ["watcher@bank.test"]);
        Assert.Equal("ValidationFailed", rejected.Result.ResultCode);

        harness.ActOk("CDT-701", returnAction, role, Local(1, 15, 10), sendCcInput: false);
        var email = harness.LastWorkflowEmail("CDT-701", "WorkflowReturnedToMaker");
        AssertRecipients(email.NotificationRecipientsJson, [CdtV2TestHarness.MakerAddress], []);
    }

    [Fact]
    public async Task MembershipChangeAfterTaskCreationDoesNotChangeThatTasksReminders()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-801", 2, Local(1, 15, 8));
        ToTradingDesk(harness, "CDT-801", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 8, 30), Local(1, 15, 9));
        var task = harness.PendingTask("CDT-801");

        foreach (var routeKey in new[] { "RouteTradingDeskAssigned", PortfolioTradeSeedDataV2.TradingDeskDueTodayRouteKey, PortfolioTradeSeedDataV2.TradingDeskOverdueRouteKey })
        {
            harness.SetStaticRule(2, routeKey, 101, "new.trader@bank.test", "TradingDeskTaxable", active: true);
            harness.SetStaticRule(2, routeKey, 100, "taxable-trader-1@example.test", "TradingDeskTaxable", active: false);
        }

        await harness.RunSla(Local(1, 15, 14));
        var reminder = Assert.Single(harness.Reminders(task.Id));
        AssertRecipients(reminder.NotificationRecipientsJson, ["taxable-trader-1@example.test"], []);

        harness.StartTrade("CDT-802", 2, Local(1, 15, 8));
        ToTradingDesk(harness, "CDT-802", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 9), Local(1, 15, 9, 30));
        var newTask = harness.PendingTask("CDT-802");
        AssertRecipients(harness.ActiveSchedule(newTask.Id).DueRecipientsJson!, ["new.trader@bank.test"], []);
    }

    [Fact]
    public void OwnRulesRouteWithoutAnActiveRuleForTheDeskRejectsTheActionInsteadOfWideningRecipients()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.SetStaticRule(2, PortfolioTradeSeedDataV2.TradingDeskDueTodayRouteKey, 300, "equity-trader-1@example.test", "TradingDeskEquity", active: false);
        harness.StartTrade("CDT-901", 2, Local(1, 15, 8));
        harness.ActOk("CDT-901", "SubmitForChecker", "Maker", Local(1, 15, 9), ccUsers: []);

        var rejected = harness.Act("CDT-901", "CheckerSendToTradingDesk", "Checker", Local(1, 15, 9, 30), "TradingDeskEquity", Date(1, 15), []);
        Assert.Equal("ValidationFailed", rejected.Result.ResultCode);
        Assert.Contains(rejected.Result.Diagnostics.ValidationErrors ?? [], error => error.Message.Contains("RouteTradingDeskDueToday", StringComparison.Ordinal));
        Assert.Equal("CheckerReviewTask", harness.PendingTask("CDT-901").TaskKey);

        // The Taxable desk still has an active rule, so its action succeeds.
        harness.ActOk("CDT-901", "CheckerSendToTradingDesk", "Checker", Local(1, 15, 9, 30), "TradingDeskTaxable", Date(1, 15), []);
    }

    [Fact]
    public void TargetDateChangeSupersedesTheScheduleAndKeepsTheSavedAudience()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-1001", 2, Local(1, 15, 8));
        ToTradingDesk(harness, "CDT-1001", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 8, 30), Local(1, 15, 9));
        var task = harness.PendingTask("CDT-1001");
        var original = harness.ActiveSchedule(task.Id);

        harness.SetStaticRule(2, PortfolioTradeSeedDataV2.TradingDeskDueTodayRouteKey, 101, "new.trader@bank.test", "TradingDeskTaxable", active: true);
        harness.SetStaticRule(2, PortfolioTradeSeedDataV2.TradingDeskDueTodayRouteKey, 100, "taxable-trader-1@example.test", "TradingDeskTaxable", active: false);
        var result = harness.ChangeTargetDate("CDT-1001", Date(1, 16), Local(1, 15, 11));

        Assert.True(result.Changed);
        var superseding = harness.ActiveSchedule(task.Id);
        Assert.Equal(2, superseding.ScheduleVersion);
        Assert.Equal(Date(1, 16), superseding.TargetDate);
        Assert.Equal(Local(1, 16, 14), superseding.DueReminderAtUtc);
        Assert.Equal(original.DueRecipientsJson, superseding.DueRecipientsJson);
        Assert.Equal(original.OverdueRecipientsJson, superseding.OverdueRecipientsJson);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ReturnAndResubmitUseTheNewDeskAndTheNewTargetDate(int versionNumber)
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-1101", versionNumber, Local(1, 15, 8));
        ToTradingDesk(harness, "CDT-1101", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 8, 30), Local(1, 15, 9));
        harness.ActOk("CDT-1101", "TradingReturnToMaker", "TradingDesk", Local(1, 15, 10), ccUsers: [], sendCcInput: versionNumber == 1);
        harness.ActOk("CDT-1101", "ResubmitForChecker", "Maker", Local(1, 15, 11), ccUsers: ["watcher@bank.test"]);
        harness.ActOk("CDT-1101", "CheckerSendToTradingDesk", "Checker", Local(1, 15, 12), "TradingDeskEquity", Date(1, 17), ["rm@bank.test"]);

        var task = harness.PendingTask("CDT-1101");
        Assert.Equal("TradingDeskReviewTask", task.TaskKey);
        Assert.Equal("TradingDeskEquity", task.AssignedRoleKey);
        var schedule = harness.ActiveSchedule(task.Id);
        Assert.Equal(Date(1, 17), schedule.TargetDate);
        var emailJson = harness.TaskEmail(task.Id).NotificationRecipientsJson;
        Assert.Contains("equity", emailJson, StringComparison.Ordinal);
        Assert.DoesNotContain("taxable", emailJson, StringComparison.Ordinal);
        if (versionNumber == 2)
        {
            AssertRecipients(schedule.OverdueRecipientsJson!, ["equity-trader-1@example.test"], []);
        }
        else
        {
            Assert.Null(schedule.OverdueRecipientsJson);
        }
    }

    [Fact]
    public async Task VersionOneTradeKeepsVersionOneRecipientsAndTimingWhileVersionTwoIsActive()
    {
        using var harness = CdtV2TestHarness.Create(DatabaseName, activeVersion: 2);
        harness.StartTrade("CDT-1201", 1, Local(1, 15, 8));
        ToTradingDesk(harness, "CDT-1201", "TradingDeskTaxable", Date(1, 15), Local(1, 15, 8, 30), Local(1, 15, 10));

        var task = harness.PendingTask("CDT-1201");
        var schedule = harness.ActiveSchedule(task.Id);
        Assert.Null(schedule.DueRecipientsJson);
        Assert.Null(schedule.OverdueRecipientsJson);
        Assert.Null(schedule.DueReminderLocalTime);
        var (to, cc) = CdtV2TestHarness.Recipients(task.NotificationRecipientsJson);
        Assert.Equal(["taxable-trading-desk@example.test", "taxable-trading-operations@example.test"], to);
        Assert.Contains(CdtV2TestHarness.MakerAddress, cc);

        // Version 1 due time is 09:00, already passed, so the next scan sends Due Today.
        await harness.RunSla(Local(1, 15, 10, 15));
        var reminder = Assert.Single(harness.Reminders(task.Id));
        Assert.Equal("Due today: Directed Trade Request Pending Execution", reminder.Subject);
        Assert.Equal(task.NotificationRecipientsJson, reminder.NotificationRecipientsJson);
    }
}
