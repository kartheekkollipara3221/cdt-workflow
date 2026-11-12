using System.Globalization;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// Resolves the default tokens of a form: {User.LanID}, {User.DisplayName}, {Today}, {Today.Next(Day)} and {Options[n]}.
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
    /// <summary>
    /// The local date of the coming day with that name, as yyyy-MM-dd. When today has that name, it is today.
    /// An example is {Today.Next(Sunday)}.
    /// </summary>
    public const string TodayNext = "{Today.Next(Day)}";

    /// <summary>The fixed tokens, for messages. {Today.Next(Day)} and {Options[n]} are also known tokens.</summary>
    public static IReadOnlyList<string> Fixed { get; } = [UserLanId, UserDisplayName, Today];

    /// <summary>
    /// Returns true for a known token. Text that does not start with '{' is a literal value, so it is also valid.
    /// </summary>
    public static bool IsValid(string? token) =>
        token == null || !token.StartsWith('{') || Fixed.Contains(token, StringComparer.OrdinalIgnoreCase)
        || OptionsToken().IsMatch(token) || NextDay(token) != null;

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
        if (NextDay(token) is { } day)
            return localNow.Date.AddDays(((int)day - (int)localNow.DayOfWeek + 7) % 7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Match match = OptionsToken().Match(token);
        if (match.Success)
        {
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return options != null && index < options.Count ? options[index].Value : null;
        }

        return token;
    }

    /// <summary>Returns the day of a {Today.Next(Day)} token, or null when the token is not one or the day is not known.</summary>
    private static DayOfWeek? NextDay(string token)
    {
        Match match = NextToken().Match(token);
        return match.Success && Enum.TryParse(match.Groups[1].Value, ignoreCase: true, out DayOfWeek day) && Enum.IsDefined(day) ? day : null;
    }

    [GeneratedRegex(@"^\{Options\[(\d+)\]\}$", RegexOptions.IgnoreCase)]
    private static partial Regex OptionsToken();

    [GeneratedRegex(@"^\{Today\.Next\(([A-Za-z]+)\)\}$", RegexOptions.IgnoreCase)]
    private static partial Regex NextToken();
}
