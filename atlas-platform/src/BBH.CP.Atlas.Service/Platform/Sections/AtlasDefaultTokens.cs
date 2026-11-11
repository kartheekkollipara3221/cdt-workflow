using System.Globalization;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// Resolves the default tokens of a form: {User.LanID}, {User.DisplayName}, {Today} and {Options[n]}.
/// </summary>
/// <remarks>
/// Other text is the value itself. The load, the save and the start-up check use the same list.
/// </remarks>
public static partial class AtlasDefaultTokens
{
    /// <summary>The LAN ID of the current user.</summary>
    public const string UserLanId = "{User.LanID}";
    /// <summary>The display name of the current user.</summary>
    public const string UserDisplayName = "{User.DisplayName}";
    /// <summary>The local date of today, as yyyy-MM-dd.</summary>
    public const string Today = "{Today}";

    /// <summary>The fixed tokens, for messages. {Options[n]} is also a known token.</summary>
    public static IReadOnlyList<string> Fixed { get; } = [UserLanId, UserDisplayName, Today];

    /// <summary>
    /// Returns true for a known token. Text that does not start with '{' is a literal value, so it is also valid.
    /// </summary>
    public static bool IsValid(string? token) =>
        token == null || !token.StartsWith('{') || Fixed.Contains(token, StringComparer.OrdinalIgnoreCase) || OptionsToken().IsMatch(token);

    /// <summary>Returns the value of a token. Returns null for {Options[n]} when there is no option n.</summary>
    public static string? Resolve(string token, string lanId, string displayName, DateTimeOffset localNow, IReadOnlyList<CPKeyValue>? options)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (string.Equals(token, UserLanId, StringComparison.OrdinalIgnoreCase))
            return lanId;
        if (string.Equals(token, UserDisplayName, StringComparison.OrdinalIgnoreCase))
            return displayName;
        if (string.Equals(token, Today, StringComparison.OrdinalIgnoreCase))
            return localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Match match = OptionsToken().Match(token);
        if (match.Success)
        {
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return options != null && index < options.Count ? options[index].Value : null;
        }

        return token;
    }

    [GeneratedRegex(@"^\{Options\[(\d+)\]\}$", RegexOptions.IgnoreCase)]
    private static partial Regex OptionsToken();
}
