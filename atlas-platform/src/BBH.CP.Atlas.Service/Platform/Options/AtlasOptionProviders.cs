using System.Globalization;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service.Platform.Options;

/// <summary>The request to an option provider: the app, the control and its OptionsSource block.</summary>
public sealed record AtlasOptionRequest(string AppCode, CPApplicationControl Control, CPOptionsSource Source);

/// <summary>
/// A named source of options (spec 01, 10.2). The JSON names the provider in <c>OptionsSource.Provider</c>.
/// The platform supplies PBAccounts, UsersInRoles, Years and WeekEndings. An app adds its own providers in its module class
/// (<c>ConfigSectionService.AppOptionProviders</c>), so program.cs keeps one line for each app.
/// </summary>
public interface IAtlasOptionProvider
{
    string Name { get; }

    IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request);

    /// <summary>The option for a saved value that is not in the list (KeepSavedValue). Default: label = value.</summary>
    CPKeyValue DescribeValue(AtlasOptionRequest request, string value) => new() { Label = value, Value = value };
}

/// <summary>
/// PB accounts (spec 01, 10.1). Label and Value "number - name", as in TTD. Data has AccountNumber, AccountName
/// and RelationshipManager for SetFromOption (spec 01, 5.2).
/// </summary>
public sealed class PBAccountsOptionProvider(IConfiguration configuration, IAtlasUserContext user, CPPBContext? pbContext = null)
    : IAtlasOptionProvider
{
    public string Name => "PBAccounts";

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
/// The users with one of the roles of the app (spec 06, 4.5). Value = LAN ID, Label = display name.
/// Roles: <c>OptionsSource.Roles</c>.
/// </summary>
public sealed class UsersInRolesOptionProvider(IAtlasUserDirectory directory) : IAtlasOptionProvider
{
    public string Name => "UsersInRoles";

    public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request) =>
        [.. directory.UsersInRoles(request.AppCode, request.Source.Roles ?? [])
            .Select(x => new CPKeyValue { Label = x.DisplayName, Value = x.LanId })];

    public CPKeyValue DescribeValue(AtlasOptionRequest request, string value) =>
        new() { Label = directory.DisplayName(value), Value = value };
}

/// <summary>
/// Years around this year (review FABLE F03; TTD tax years). Parameters: <c>Before</c> (default 3) and <c>After</c>
/// (default 1). The list starts with the latest year: this year + After down to this year - Before.
/// </summary>
public sealed class YearsOptionProvider(TimeProvider time) : IAtlasOptionProvider
{
    public string Name => "Years";

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
/// Week-end days (review FABLE F03; TTD week endings): the last such day (today if today is that day) and the next
/// ones, as yyyy-MM-dd. Parameters: <c>Count</c> (default 4) and <c>DayOfWeek</c> (default Sunday).
/// </summary>
public sealed class WeekEndingsOptionProvider(TimeProvider time) : IAtlasOptionProvider
{
    public string Name => "WeekEndings";

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
