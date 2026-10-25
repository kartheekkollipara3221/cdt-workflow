using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service.Platform.Options;

/// <summary>The request to an option provider: the app, the control and its OptionsSource block.</summary>
public sealed record AtlasOptionRequest(string AppCode, CPApplicationControl Control, CPOptionsSource Source);

/// <summary>
/// A named source of options (spec 01, 10.2). The JSON names the provider in <c>OptionsSource.Provider</c>.
/// The platform supplies PBAccounts and UsersInRoles. An app adds its own providers in its module class
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
