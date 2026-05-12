using Microsoft.Extensions.Options;
using WorkflowEngine.Api.Options;

namespace WorkflowEngine.Api.Services;

public sealed class WorkflowDatabaseStartupHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<WorkflowDatabaseStartupOptions> options,
    ILogger<WorkflowDatabaseStartupHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var startupOptions = options.Value;
        if (!startupOptions.ApplyMigrationsAndSeedOnStartup && !startupOptions.VerifySeedOnStartup)
        {
            logger.LogInformation("Workflow database startup work is disabled by configuration.");
            return Task.CompletedTask;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var readinessService = scope.ServiceProvider.GetRequiredService<WorkflowDatabaseReadinessService>();
        var result = readinessService.Run(startupOptions);
        logger.LogInformation("Workflow database startup result: {Message}", result.Message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
