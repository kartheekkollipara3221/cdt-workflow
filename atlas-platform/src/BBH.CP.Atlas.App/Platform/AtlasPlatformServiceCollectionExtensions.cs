using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Registers the platform services and the platform apps in the composition root.
/// </summary>
/// <remarks>
/// At the target, program.cs gets these lines:
/// <code>
/// builder.Services.AddAtlasPlatform(configuration);   // the configuration root that program.cs builds
/// builder.Services.AddAtlasModule&lt;ConfigSectionService&gt;("TTDV2");
/// </code>
/// </remarks>
public static class AtlasPlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform services, the start-up service and the error filter.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="configuration">The configuration that holds the platform settings. The target decides its source.</param>
    public static IServiceCollection AddAtlasPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System); // a TimeProvider that the host registers has priority
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        // Registry and configuration. The platform settings come from the configuration parameter, so the target
        // decides the source. The platform does not read its settings from the container IConfiguration.
        AtlasPlatformConfiguration platformConfiguration = new(configuration);
        services.AddSingleton(platformConfiguration);
        services.AddSingleton<IAtlasConfigStore>(provider =>
            new AtlasConfigStore(platformConfiguration.Configuration, provider.GetRequiredService<IHostEnvironment>(),
                provider.GetService<ILogger<AtlasConfigStore>>()));
        services.AddScoped<IAtlasModuleResolver, AtlasModuleResolver>();

        services.AddAtlasPlatformData();

        // The current user comes from the existing IUserInfo.
        services.AddScoped<IAtlasCurrentUserSource, UserInfoCurrentUserSource>();

        services.Configure<MvcOptions>(options => options.Filters.Add<AtlasProblemExceptionFilter>());

        AtlasPlatformServices.Add(services);

        // The start-up runs as a hosted service. A call to StartAtlasPlatformAsync in program.cs is also valid.
        services.TryAddSingleton<AtlasPlatformStartupGate>();
        services.TryAddSingleton<AtlasDisabledApps>();
        services.AddHostedService<AtlasPlatformStartupService>();
        return services;
    }

    /// <summary>
    /// Registers one platform app. Use one line for each app.
    /// </summary>
    /// <typeparam name="TService">The module class of the app. It must match the Service of the app manifest.</typeparam>
    /// <exception cref="ArgumentException">The app code is not valid.</exception>
    /// <exception cref="InvalidOperationException">The app code is already registered.</exception>
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
