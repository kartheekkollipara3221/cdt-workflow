using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.TryAddSingleton(TimeProvider.System); // the host's TimeProvider wins (review HO-08)
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        // Registry and configuration (module 7, module 11). The platform settings come from the configuration given
        // here (review HO-01), so the target decides the source; the container IConfiguration is not used for them.
        AtlasPlatformConfiguration platformConfiguration = new(configuration);
        services.AddSingleton(platformConfiguration);
        services.AddSingleton<IAtlasConfigStore>(provider =>
            new AtlasConfigStore(platformConfiguration.Configuration, provider.GetRequiredService<IHostEnvironment>(),
                provider.GetRequiredService<TimeProvider>(), provider.GetService<ILogger<AtlasConfigStore>>()));
        services.AddScoped<IAtlasModuleResolver, AtlasModuleResolver>();

        // Data (module 5).
        services.AddAtlasPlatformData();

        // Current user from the existing IUserInfo (module 6, 4.1).
        services.AddScoped<IAtlasCurrentUserSource, UserInfoCurrentUserSource>();

        services.Configure<MvcOptions>(options => options.Filters.Add<AtlasProblemExceptionFilter>());

        AtlasPlatformServices.Add(services);

        // Review HO-06: the start-up runs as a hosted service; StartAtlasPlatformAsync in program.cs stays valid.
        services.TryAddSingleton<AtlasPlatformStartupGate>();
        services.AddHostedService<AtlasPlatformStartupService>();
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
        if (services.Any(x => x.ServiceType == typeof(AtlasModuleRegistration)
                && x.ImplementationInstance is AtlasModuleRegistration registered && registered.Code == code))
            throw new InvalidOperationException($"The app code '{code}' is registered twice. Keep one AddAtlasModule line for each app.");

        services.AddSingleton(new AtlasModuleRegistration(code, typeof(TService)));
        services.AddKeyedScoped<IAtlasModule, TService>(code);
        return services;
    }
}
