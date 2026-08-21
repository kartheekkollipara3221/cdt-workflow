using System.Globalization;
using WorkflowEngine.Sqms.Configuration;

namespace WorkflowEngine.Sqms;

public sealed record SqmsLinkResult(bool Success, string? Url, string? Error)
{
    public static SqmsLinkResult Succeeded(string url) => new(true, url, null);

    public static SqmsLinkResult Failed(string error) => new(false, null, error);
}

public sealed class SqmsLinkBuilder(SqmsLinkOptions options)
{
    private readonly SqmsLinkOptions options = options ?? throw new ArgumentNullException(nameof(options));

    public SqmsLinkResult BuildOpenBiddingDashboardLink() =>
        ValidateUnchanged(options.OpenBiddingDashboardUrl, nameof(options.OpenBiddingDashboardUrl));

    public SqmsLinkResult BuildSellerApprovalBidsDashboardLink(int opportunityId) =>
        AppendId(
            options.SellerApprovalBidsDashboardUrl,
            nameof(options.SellerApprovalBidsDashboardUrl),
            "opportunityId",
            opportunityId);

    public SqmsLinkResult BuildBidFormLink(int bidId) =>
        AppendId(options.BidFormUrl, nameof(options.BidFormUrl), "bidId", bidId);

    private static SqmsLinkResult ValidateUnchanged(string? configuredUrl, string optionName)
    {
        return TryCreateHttpUri(configuredUrl, out _)
            ? SqmsLinkResult.Succeeded(configuredUrl!)
            : SqmsLinkResult.Failed($"{optionName} must be an absolute HTTP or HTTPS URL.");
    }

    private static SqmsLinkResult AppendId(
        string? configuredUrl,
        string optionName,
        string parameterName,
        int id)
    {
        if (id <= 0)
        {
            return SqmsLinkResult.Failed($"{parameterName} must be positive.");
        }

        if (!TryCreateHttpUri(configuredUrl, out var uri))
        {
            return SqmsLinkResult.Failed($"{optionName} must be an absolute HTTP or HTTPS URL.");
        }

        var builder = new UriBuilder(uri!);
        var queryPairs = ParseQueryPairs(builder.Query);
        queryPairs.Add(new KeyValuePair<string, string?>(
            parameterName,
            id.ToString(CultureInfo.InvariantCulture)));
        builder.Query = string.Join("&", queryPairs.Select(FormatQueryPair));
        return SqmsLinkResult.Succeeded(builder.Uri.AbsoluteUri);
    }

    private static bool TryCreateHttpUri(string? configuredUrl, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(configuredUrl) ||
            !Uri.TryCreate(configuredUrl, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static List<KeyValuePair<string, string?>> ParseQueryPairs(string query)
    {
        var result = new List<KeyValuePair<string, string?>>();
        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                result.Add(new KeyValuePair<string, string?>(Uri.UnescapeDataString(segment), null));
                continue;
            }

            result.Add(new KeyValuePair<string, string?>(
                Uri.UnescapeDataString(segment[..separatorIndex]),
                Uri.UnescapeDataString(segment[(separatorIndex + 1)..])));
        }

        return result;
    }

    private static string FormatQueryPair(KeyValuePair<string, string?> pair)
    {
        var encodedKey = Uri.EscapeDataString(pair.Key);
        return pair.Value is null
            ? encodedKey
            : $"{encodedKey}={Uri.EscapeDataString(pair.Value)}";
    }
}
