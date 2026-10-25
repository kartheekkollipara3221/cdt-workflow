using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Mvc;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Composition root lines for the platform (spec 07, 4.2). At the target, program.cs gets:
/// <code>
/// builder.Services.AddAtlasPlatform(builder.Configuration);
/// builder.Services.AddAtlasModule&lt;CPTTDV2Service&gt;("TTDV2");
/// </code>
/// </summary>
public static class AtlasPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        // Registry and configuration (module 7, module 11). The platform settings come from the configuration given
        // here (review HO-01), so the target decides the source; the container IConfiguration is not used for them.
        AtlasPlatformConfiguration platformConfiguration = new(configuration);
        services.AddSingleton(platformConfiguration);
        services.AddSingleton<IAtlasConfigStore>(provider =>
            new AtlasConfigStore(platformConfiguration.Configuration, provider.GetRequiredService<IHostEnvironment>()));
        services.AddScoped<IAtlasModuleResolver, AtlasModuleResolver>();

        // Data (module 5).
        services.AddAtlasPlatformData();

        // Current user from the existing IUserInfo (module 6, 4.1).
        services.AddScoped<IAtlasCurrentUserSource, UserInfoCurrentUserSource>();

        services.Configure<MvcOptions>(options => options.Filters.Add<AtlasProblemExceptionFilter>());

        AtlasPlatformServices.Add(services);
        return services;
    }

    /// <summary>One line for each app (spec 07, 4.2, question 1). The class must match the manifest Service.</summary>
    public static IServiceCollection AddAtlasModule<TService>(this IServiceCollection services, string appCode)
        where TService : class, IAtlasModule
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!AtlasAppCode.IsValid(appCode))
            throw new ArgumentException($"'{appCode}' is not a valid app code (pattern ^[A-Z][A-Z0-9]{{1,19}}$).", nameof(appCode));

        string code = AtlasAppCode.Normalize(appCode);
        services.AddSingleton(new AtlasModuleRegistration(code, typeof(TService)));
        services.AddKeyedScoped<IAtlasModule, TService>(code);
        return services;
    }
}
