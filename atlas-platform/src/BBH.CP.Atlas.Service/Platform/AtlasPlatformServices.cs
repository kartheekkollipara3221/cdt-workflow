using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.Service.Platform;

/// <summary>Registers the platform services of this project. Called by AddAtlasPlatform (App/Platform).</summary>
public static class AtlasPlatformServices
{
    public static void Add(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Security (module 6).
        services.AddScoped<IAtlasUserContext, AtlasUserContext>();
        services.AddScoped<IAtlasUserDirectory, AtlasUserDirectory>();
        services.AddScoped<IAtlasAccessService, AtlasAccessService>();
    }
}
