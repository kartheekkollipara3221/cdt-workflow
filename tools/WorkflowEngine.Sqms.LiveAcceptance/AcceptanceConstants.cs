using System.Globalization;
using WorkflowEngine.Sqms.Contracts;

namespace WorkflowEngine.Sqms.LiveAcceptance;

public static class AcceptanceConstants
{
    private static readonly AcceptanceProfile Profile = AcceptanceProfile.FromEnvironment();

    public static string DatabaseName => Profile.DatabaseName;
    public static string RunMarker => Profile.RunMarker;
    public const int ExpectedOpportunityCount = 3;
    public const int ExpectedBidCount = 15;
    public const int ExpectedEmailCount = 19;

    public static DateTimeOffset OpeningUtc => Profile.OpeningUtc;

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

public sealed record AcceptanceProfile(
    string DatabaseName,
    string RunMarker,
    DateTimeOffset OpeningUtc)
{
    public static AcceptanceProfile FromEnvironment() => FromValues(
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_DATABASE"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_MARKER"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_OPENING_UTC"));

    public static AcceptanceProfile FromValues(
        string? database,
        string? marker,
        string? openingUtc) => new(
            string.IsNullOrWhiteSpace(database)
                ? "workflowengine_sqms_live_acceptance"
                : database.Trim(),
            string.IsNullOrWhiteSpace(marker)
                ? "SQMS-LIVE-20260821"
                : marker.Trim(),
            string.IsNullOrWhiteSpace(openingUtc)
                ? new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero)
                : DateTimeOffset.Parse(
                    openingUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
}
