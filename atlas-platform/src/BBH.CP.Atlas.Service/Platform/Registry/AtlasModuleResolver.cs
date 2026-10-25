using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>A service that the platform makes for an app code. The resolver sets the code.</summary>
public interface IAtlasModule
{
    string AppCode { get; set; }
}

/// <summary>One AddAtlasModule line (spec 07, 4.2).</summary>
public sealed record AtlasModuleRegistration(string Code, Type ServiceType);

/// <summary>Replaces the five if chains (spec 07, 4.3). Unknown or disabled app: 404. Missing service type: 400.</summary>
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
    IEnumerable<AtlasModuleRegistration> registrations) : IAtlasModuleResolver
{
    private readonly Dictionary<string, AtlasModuleRegistration> _registrations =
        registrations.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AtlasModuleRegistration> Registrations => [.. _registrations.Values];

    public bool IsPlatformApp(string? appCode)
    {
        if (!AtlasAppCode.IsValid(appCode))
            return false;

        string code = AtlasAppCode.Normalize(appCode!);
        return _registrations.ContainsKey(code) && configStore.FindManifest(code) is { Enabled: true };
    }

    public TService Get<TService>(string appCode, string feature) where TService : class
    {
        if (!AtlasAppCode.IsValid(appCode))
            throw AtlasProblemException.BadRequest($"'{appCode}' is not a valid app code.");

        string code = AtlasAppCode.Normalize(appCode);
        if (!_registrations.ContainsKey(code) || configStore.FindManifest(code) is not { Enabled: true })
            throw AtlasProblemException.NotFound($"App '{code}' is not registered.");

        IAtlasModule module = services.GetRequiredKeyedService<IAtlasModule>(code);
        module.AppCode = code;

        return module as TService
            ?? throw AtlasProblemException.BadRequest($"App '{code}' has no {feature}.");
    }
}
