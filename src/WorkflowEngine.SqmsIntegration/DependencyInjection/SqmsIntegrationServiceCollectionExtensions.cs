using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkflowEngine.Application.Normalization;

namespace WorkflowEngine.SqmsIntegration.DependencyInjection;

public static class SqmsIntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddSqmsIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<WorkflowActionEnvelopeNormalizer>();
        services.TryAddScoped<CanonicalRequestHasher>();
        services.TryAddSingleton<SqmsNotificationInputFactory>();
        services.AddScoped<SqmsApplicationRepository>();
        services.AddScoped<SqmsWorkflowCoordinator>();
        return services;
    }
}
