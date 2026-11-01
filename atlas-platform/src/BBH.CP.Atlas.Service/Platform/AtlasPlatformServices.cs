using BBH.CP.Atlas.Service.Platform.Audit;
using BBH.CP.Atlas.Service.Platform.Dashboard;
using BBH.CP.Atlas.Service.Platform.Options;
using BBH.CP.Atlas.Service.Platform.Rules;
using BBH.CP.Atlas.Service.Platform.Sections;
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
        services.AddScoped<AtlasAccessService>();

        // Rules (module 4).
        services.AddSingleton<AtlasRuleEngine>();

        // Audit (module 9), option providers (module 1, 10.2) and the module service bundle.
        services.AddScoped<AtlasAuditWriter>();
        services.AddScoped<IAtlasOptionProvider, PBAccountsOptionProvider>();
        services.AddScoped<IAtlasOptionProvider, UsersInRolesOptionProvider>();
        services.AddScoped<IAtlasOptionProvider, YearsOptionProvider>();
        services.AddScoped<IAtlasOptionProvider, WeekEndingsOptionProvider>();
        services.AddScoped<AtlasDashboardService>();
        services.AddScoped<AtlasModuleServices>();
    }
}
