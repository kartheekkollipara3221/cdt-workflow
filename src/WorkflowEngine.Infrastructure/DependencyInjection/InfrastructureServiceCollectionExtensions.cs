using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Domain.Workflow;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(WorkflowKeys.DefaultConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{WorkflowKeys.DefaultConnectionName}' is not configured.");
        }

        services.AddDbContext<WorkflowDbContext>(options => options.UseSqlServer(connectionString));

        return services;
    }
}
