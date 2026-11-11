using BBH.CP.Atlas.Data.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>A service that the platform makes for an app code. The resolver sets the code.</summary>
public interface IAtlasModule
{
    /// <summary>The app code of the service.</summary>
    string AppCode { get; set; }
}

/// <summary>The data of one AddAtlasModule line: the app code and the service type.</summary>
public sealed record AtlasModuleRegistration(string Code, Type ServiceType);

/// <summary>
/// Finds the service of a platform app from its app code. It replaces the five if chains of the target.
/// </summary>
/// <remarks>
/// An unknown or disabled app gives 404. A missing service type gives 400.
/// An app whose Table or AuditTable did not load at start-up is not available.
/// For example, the start-up did not run. The resolver then gives 404 and logs Atlas.AppNotAvailable.
/// A restart loads the table and runs the checks.
/// </remarks>
public interface IAtlasModuleResolver
{
    /// <summary>
    /// Returns true when the code has a registration line, an enabled manifest and loaded tables.
    /// </summary>
    bool IsPlatformApp(string? appCode);

    /// <summary>The registration lines of the platform apps.</summary>
    IReadOnlyList<AtlasModuleRegistration> Registrations { get; }

    /// <summary>Returns the service of the app for a feature.</summary>
    /// <param name="appCode">The app code.</param>
    /// <param name="feature">The name of the feature, for the error message.</param>
    /// <exception cref="AtlasProblemException">
    /// 400 when the code is not valid or the service does not supply the feature. 404 when the app is not available.
    /// </exception>
    TService Get<TService>(string appCode, string feature) where TService : class;
}

/// <summary>Finds the service of a platform app from its app code. See <see cref="IAtlasModuleResolver"/>.</summary>
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

    /// <summary>Returns the first table of the manifest that is not in the registry, or null.</summary>
    /// <remarks>When there is no registry, the method returns null.</remarks>
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
