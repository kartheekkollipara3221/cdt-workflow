using BBH.CP.Atlas.Data.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>A service that the platform makes for an app code. The resolver sets the code.</summary>
public interface IAtlasModule
{
    string AppCode { get; set; }
}

/// <summary>One AddAtlasModule line (spec 07, 4.2).</summary>
public sealed record AtlasModuleRegistration(string Code, Type ServiceType);

/// <summary>
/// Replaces the five if chains (spec 07, 4.3). Unknown or disabled app: 404. Missing service type: 400.
/// An app whose Table or AuditTable was not loaded at start-up (for example the start-up did not run)
/// is not available: 404 and Atlas.AppNotAvailable (review HO-05). A restart loads the table and runs the checks.
/// </summary>
public interface IAtlasModuleResolver
{
    /// <summary>True if the code has a registration line and an enabled manifest.</summary>
    bool IsPlatformApp(string? appCode);

    IReadOnlyList<AtlasModuleRegistration> Registrations { get; }

    TService Get<TService>(string appCode, string feature) where TService : class;
}

public sealed class AtlasModuleResolver(
    IServiceProvider services,
    IAtlasConfigStore configStore,
    IEnumerable<AtlasModuleRegistration> registrations,
    AtlasTableRegistry? tables = null,
    ILogger<AtlasModuleResolver>? logger = null,
    AtlasDisabledApps? disabled = null) : IAtlasModuleResolver
{
    private readonly Dictionary<string, AtlasModuleRegistration> _registrations =
        registrations.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AtlasModuleRegistration> Registrations => [.. _registrations.Values];

    public bool IsPlatformApp(string? appCode)
    {
        if (!AtlasAppCode.IsValid(appCode))
            return false;

        string code = AtlasAppCode.Normalize(appCode!);
        return _registrations.ContainsKey(code) && configStore.FindManifest(code) is { Enabled: true } manifest
            && MissingTable(manifest) == null && disabled?.IsDisabled(code, out _) != true;
    }

    /// <summary>The first table of the manifest that is not in the registry, or null (no registry: null).</summary>
    private string? MissingTable(AtlasAppManifest manifest)
    {
        if (tables == null)
            return null;

        IEnumerable<string?> names = [manifest.Table, manifest.AuditMode == AtlasAuditMode.Server ? manifest.AuditTable : null];
        return names.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name) && !tables.TryGet(name, out _));
    }

    public TService Get<TService>(string appCode, string feature) where TService : class
    {
        if (!AtlasAppCode.IsValid(appCode))
            throw AtlasProblemException.BadRequest($"'{appCode}' is not a valid app code.");

        string code = AtlasAppCode.Normalize(appCode);
        if (!_registrations.ContainsKey(code) || configStore.FindManifest(code) is not { Enabled: true } manifest)
            throw AtlasProblemException.NotFound($"App '{code}' is not registered.");

        if (disabled != null && disabled.IsDisabled(code, out string reason))
        {
            AtlasLog.AppDisabledByCheck(logger ?? (ILogger)NullLogger.Instance, code);
            throw AtlasProblemException.NotFound($"App '{code}' is not available: the start-up check found errors ({reason}).");
        }

        if (MissingTable(manifest) is { } missing)
        {
            AtlasLog.AppNotLoaded(logger ?? (ILogger)NullLogger.Instance, code, missing);
            string cause = tables is { IsLoaded: false }
                ? " The platform start-up did not run (the AddAtlasPlatform hosted service or StartAtlasPlatformAsync)."
                : " Restart the application.";
            throw AtlasProblemException.NotFound($"App '{code}' is not available: the table '{missing}' was not loaded at start-up.{cause}");
        }

        IAtlasModule module = services.GetRequiredKeyedService<IAtlasModule>(code);
        module.AppCode = code;

        return module as TService
            ?? throw AtlasProblemException.BadRequest($"App '{code}' has no {feature}.");
    }
}
