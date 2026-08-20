using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Sqms.Configuration;

namespace WorkflowEngine.Sqms.DependencyInjection;

public static class SqmsServiceCollectionExtensions
{
    public static IServiceCollection AddSqmsWorkflowIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SqmsOptions.SectionName);
        services
            .AddOptions<SqmsOptions>()
            .Configure(options => Bind(section, options))
            .Validate(Validate, "Enabled SQMS requires positive scheduling day counts and a resolvable time zone.")
            .ValidateOnStart();

        return services;
    }

    private static void Bind(IConfiguration section, SqmsOptions options)
    {
        options.Enabled = ReadBoolean(section["Enabled"], options.Enabled, "Sqms:Enabled");
        options.Links.OpenBiddingDashboardUrl =
            section["Links:OpenBiddingDashboardUrl"] ?? options.Links.OpenBiddingDashboardUrl;
        options.Links.SellerApprovalBidsDashboardUrl =
            section["Links:SellerApprovalBidsDashboardUrl"] ?? options.Links.SellerApprovalBidsDashboardUrl;
        options.Links.BidFormUrl = section["Links:BidFormUrl"] ?? options.Links.BidFormUrl;
        options.Scheduling.TimeZoneId =
            section["Scheduling:TimeZoneId"] ?? options.Scheduling.TimeZoneId;
        options.Scheduling.BiddingDays = ReadInt32(
            section["Scheduling:BiddingDays"],
            options.Scheduling.BiddingDays,
            "Sqms:Scheduling:BiddingDays");
        options.Scheduling.SellerResponseDays = ReadInt32(
            section["Scheduling:SellerResponseDays"],
            options.Scheduling.SellerResponseDays,
            "Sqms:Scheduling:SellerResponseDays");
    }

    private static bool Validate(SqmsOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        if (options.Scheduling.BiddingDays <= 0 || options.Scheduling.SellerResponseDays <= 0)
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(options.Scheduling.TimeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool ReadBoolean(string? value, bool defaultValue, string key)
    {
        if (value is null)
        {
            return defaultValue;
        }

        return bool.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"Configuration value '{key}' must be a Boolean.");
    }

    private static int ReadInt32(string? value, int defaultValue, string key)
    {
        if (value is null)
        {
            return defaultValue;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Configuration value '{key}' must be an Int32.");
    }
}
