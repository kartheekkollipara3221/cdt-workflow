using System.Globalization;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service.Platform.Options;

/// <summary>The request to an option provider: the app, the control and its OptionsSource block.</summary>
public sealed record AtlasOptionRequest(string AppCode, CPApplicationControl Control, CPOptionsSource Source);

/// <summary>
/// A named source of options for a control.
/// </summary>
/// <remarks>
/// The JSON names the provider in <c>OptionsSource.Provider</c>.
/// The platform supplies PBAccounts, UsersInRoles, Years and WeekEndings.
/// An app adds its own providers in its module class (<c>ConfigSectionService.AppOptionProviders</c>).
/// Thus program.cs keeps one line for each app.
/// </remarks>
public interface IAtlasOptionProvider
{
    /// <summary>The name that <c>OptionsSource.Provider</c> uses.</summary>
    string Name { get; }

    /// <summary>Returns the options for a control.</summary>
    IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request);

    /// <summary>
    /// Returns the option for a saved value that is not in the list (KeepSavedValue). By default, the label is the value.
    /// </summary>
    CPKeyValue DescribeValue(AtlasOptionRequest request, string value) => new() { Label = value, Value = value };
}

/// <summary>
/// Supplies the PB accounts as options.
/// </summary>
/// <remarks>
/// Label and Value are "number - name", as in TTD.
/// Data holds AccountNumber, AccountName and RelationshipManager for the SetFromOption effect.
/// </remarks>
public sealed class PBAccountsOptionProvider(IConfiguration configuration, IAtlasUserContext user, CPPBContext? pbContext = null)
    : IAtlasOptionProvider
{
    /// <inheritdoc/>
    public string Name => "PBAccounts";

    /// <inheritdoc/>
    public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request) =>
        [.. new CPAtlasExternalService(configuration, pbContext, user.User).GetPBAccount().Select(account => new CPKeyValue
        {
            Label = account.AccountNumber + " - " + account.AccountShortName,
            Value = account.AccountNumber + " - " + account.AccountShortName,
            Data = new Dictionary<string, string>
            {
                ["AccountNumber"] = account.AccountNumber ?? "",
                ["AccountName"] = account.AccountShortName ?? "",
                ["RelationshipManager"] = account.RelationshipManager ?? ""
            }
        })];
}

/// <summary>
/// Supplies the users that have one of the given roles of the app.
/// </summary>
/// <remarks>
/// Value is the LAN ID. Label is the display name. <c>OptionsSource.Roles</c> holds the roles.
/// </remarks>
public sealed class UsersInRolesOptionProvider(IAtlasUserDirectory directory) : IAtlasOptionProvider
{
    /// <inheritdoc/>
    public string Name => "UsersInRoles";

    /// <inheritdoc/>
    public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request) =>
        [.. directory.UsersInRoles(request.AppCode, request.Source.Roles ?? [])
            .Select(x => new CPKeyValue { Label = x.DisplayName, Value = x.LanId })];

    /// <summary>Returns the option for a saved LAN ID, with the display name of the user as the label.</summary>
    public CPKeyValue DescribeValue(AtlasOptionRequest request, string value) =>
        new() { Label = directory.DisplayName(value), Value = value };
}

/// <summary>
/// Supplies the years around this year, for example tax years.
/// </summary>
/// <remarks>
/// Parameters: <c>Before</c> (default 3) and <c>After</c> (default 1).
/// The list starts with the latest year. It goes from this year + After down to this year - Before.
/// </remarks>
public sealed class YearsOptionProvider(TimeProvider time) : IAtlasOptionProvider
{
    /// <inheritdoc/>
    public string Name => "Years";

    /// <inheritdoc/>
    public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request)
    {
        int before = AtlasProviderParameters.Int(request, "Before", 3);
        int after = AtlasProviderParameters.Int(request, "After", 1);
        int year = time.GetLocalNow().Year;
        return [.. Enumerable.Range(0, Math.Max(0, before + after + 1))
            .Select(n => (year + after - n).ToString(CultureInfo.InvariantCulture))
            .Select(x => new CPKeyValue { Label = x, Value = x })];
    }
}

/// <summary>
/// Supplies the week-end days, for example the TTD week endings, as yyyy-MM-dd.
/// </summary>
/// <remarks>
/// The list starts with the last such day. When today is that day, the list starts with today.
/// The next days follow. Parameters: <c>Count</c> (default 4) and <c>DayOfWeek</c> (default Sunday).
/// </remarks>
public sealed class WeekEndingsOptionProvider(TimeProvider time) : IAtlasOptionProvider
{
    /// <inheritdoc/>
    public string Name => "WeekEndings";

    /// <inheritdoc/>
    public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request)
    {
        int count = AtlasProviderParameters.Int(request, "Count", 4);
        DayOfWeek day = AtlasProviderParameters.Text(request, "DayOfWeek") is { } text
            && Enum.TryParse(text, ignoreCase: true, out DayOfWeek parsed) && Enum.IsDefined(parsed)
            ? parsed
            : DayOfWeek.Sunday;
        DateTime today = time.GetLocalNow().Date;
        DateTime last = today.AddDays(-(((int)today.DayOfWeek - (int)day + 7) % 7));
        return [.. Enumerable.Range(0, Math.Max(0, count))
            .Select(n => last.AddDays(7 * n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Select(x => new CPKeyValue { Label = x, Value = x })];
    }
}

/// <summary>Reads <c>OptionsSource.Parameters</c>. A missing or invalid value gives the default.</summary>
internal static class AtlasProviderParameters
{
    public static string? Text(AtlasOptionRequest request, string name) =>
        request.Source.Parameters is { } parameters
            ? parameters.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value
            : null;

    public static int Int(AtlasOptionRequest request, string name, int fallback) =>
        int.TryParse(Text(request, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 0
            ? value
            : fallback;
}
