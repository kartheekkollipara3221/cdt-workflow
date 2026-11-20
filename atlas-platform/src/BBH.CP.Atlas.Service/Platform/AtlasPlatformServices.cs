using BBH.CP.Atlas.Service.Platform.Attachments;
using BBH.CP.Atlas.Service.Platform.Workflow;
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
    /// <summary>Adds the platform services of this project to the service collection.</summary>
    public static void Add(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Security.
        services.AddScoped<IAtlasUserContext, AtlasUserContext>();
        services.AddScoped<IAtlasUserDirectory, AtlasUserDirectory>();
        services.AddScoped<AtlasAccessService>();

        // Rules.
        services.AddSingleton<AtlasRuleEngine>();

        // Audit, option providers and the module service bundle.
        services.AddScoped<AtlasAuditWriter>();
        services.AddScoped<IAtlasOptionProvider, PBAccountsOptionProvider>();
        services.AddScoped<IAtlasOptionProvider, UsersInRolesOptionProvider>();
        services.AddScoped<IAtlasOptionProvider, YearsOptionProvider>();
        services.AddScoped<IAtlasOptionProvider, WeekEndingsOptionProvider>();
        services.AddScoped<AtlasTableOptionProvider>();
        services.AddScoped<IAtlasOptionProvider>(provider => provider.GetRequiredService<AtlasTableOptionProvider>());
        services.AddScoped<AtlasDashboardService>();
        services.AddScoped<IAtlasAttachmentService, AtlasAttachmentService>();
        services.AddScoped<IAtlasWorkflowAdapter, AtlasWorkflowAdapter>();
        services.AddScoped<AtlasModuleServices>();
    }
}
