using WorkflowEngine.Sqms.Contracts;

namespace WorkflowEngine.Sqms.LiveAcceptance;

public static class AcceptanceConstants
{
    public const string DatabaseName = "workflowengine_sqms_live_acceptance";
    public const string RunMarker = "SQMS-LIVE-20260821";
    public const int ExpectedOpportunityCount = 3;
    public const int ExpectedBidCount = 15;
    public const int ExpectedEmailCount = 19;

    public static readonly DateTimeOffset OpeningUtc =
        new(2030, 1, 1, 15, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset OpenRunUtc => OpeningUtc.AddDays(1);
    public static DateTimeOffset ReminderRunUtc => OpeningUtc.AddDays(14);
    public static DateTimeOffset CloseRunUtc => OpeningUtc.AddDays(16);

    public static readonly SqmsActor Mfo =
        new("sqms-live-mfo", "SQMS Live MFO", [SqmsKeys.Roles.Mfo]);

    public static readonly SqmsActor Ipg =
        new("sqms-live-ipg", "SQMS Live IPG", [SqmsKeys.Roles.Ipg]);

    public static readonly SqmsActor Seller =
        new("sqms-live-seller", "SQMS Live Seller", [SqmsKeys.Roles.Seller]);

    public static readonly SqmsActor System =
        new("sqms-live-system", "SQMS Live Scheduler", [SqmsKeys.Roles.System]);
}
