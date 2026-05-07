using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Domain.Workflow;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Persistence.Verification;
using WorkflowEngine.Infrastructure.WorkflowContext;
using WorkflowEngine.Infrastructure.WorkflowExecution;
using WorkflowEngine.Infrastructure.WorkflowInspection;
using WorkflowEngine.Infrastructure.WorkflowStart;

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
        services.AddScoped<WorkflowSeedService>();
        services.AddScoped<WorkflowDatabaseInitializer>();
        services.AddScoped<SeedVerificationService>();
        services.AddScoped<WorkflowContextReader>();
        services.AddScoped<WorkflowExecutionService>();
        services.AddScoped<WorkflowInspectionReader>();
        services.AddScoped<StartWorkflowService>();
        services.AddScoped<global::WorkflowEngine.Application.Persistence.IWorkflowDatabaseInitializer>(provider =>
            provider.GetRequiredService<WorkflowDatabaseInitializer>());
        services.AddScoped<global::WorkflowEngine.Application.Persistence.ISeedVerificationService>(provider =>
            provider.GetRequiredService<SeedVerificationService>());
        services.AddScoped<global::WorkflowEngine.Application.Context.IWorkflowContextReader>(provider =>
            provider.GetRequiredService<WorkflowContextReader>());
        services.AddScoped<global::WorkflowEngine.Application.WorkflowExecution.IWorkflowExecutionService>(provider =>
            provider.GetRequiredService<WorkflowExecutionService>());
        services.AddScoped<global::WorkflowEngine.Application.Inspection.IWorkflowInspectionReader>(provider =>
            provider.GetRequiredService<WorkflowInspectionReader>());
        services.AddScoped<global::WorkflowEngine.Application.WorkflowStart.IStartWorkflowService>(provider =>
            provider.GetRequiredService<StartWorkflowService>());

        return services;
    }
}
