using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Demo;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Sqms.Commands;
using WorkflowEngine.Sqms.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var services = new ServiceCollection()
    .AddSingleton<IConfiguration>(configuration)
    .AddSingleton(TimeProvider.System)
    .AddScoped<WorkflowActionEnvelopeValidator>()
    .AddScoped<WorkflowActionEnvelopeNormalizer>()
    .AddScoped<CanonicalRequestHasher>()
    .AddScoped<WorkflowActionResultBuilder>()
    .AddScoped<CanonicalWorkflowResultSerializer>()
    .AddScoped<IStartWorkflowCommandHandler, StartWorkflowCommandHandler>()
    .AddScoped<IExecuteJsonCommandHandler, ExecuteJsonCommandHandler>()
    .AddScoped<IShowContextCommandHandler, ShowContextCommandHandler>()
    .AddScoped<IShowCommandHandler, ShowCommandHandler>()
    .AddScoped<IShowTasksCommandHandler, ShowTasksCommandHandler>()
    .AddScoped<IShowHistoryCommandHandler, ShowHistoryCommandHandler>()
    .AddScoped<IShowNotificationsCommandHandler, ShowNotificationsCommandHandler>()
    .AddScoped<IRunSlaCommandHandler, RunSlaCommandHandler>()
    .AddScoped<IProcessNotificationOutboxCommandHandler, ProcessNotificationOutboxCommandHandler>()
    .AddScoped<IRunSqmsScheduledCommandHandler, RunSqmsScheduledCommandHandler>()
    .AddScoped<IDemoStepRunner, WorkflowEngineDemoStepRunner>()
    .AddScoped<IDemoCommandHandler, DemoCommandHandler>()
    .AddScoped<CommandRegistry>()
    .AddWorkflowInfrastructure(configuration)
    .AddSqmsWorkflowIntegration(configuration)
    .BuildServiceProvider();

using var scope = services.CreateScope();
var registry = scope.ServiceProvider.GetRequiredService<CommandRegistry>();
var result = registry.Dispatch(args);

if (result.ExitCode == 0)
{
    Console.Out.WriteLine(result.Message);
}
else
{
    Console.Error.WriteLine(result.Message);
}

return result.ExitCode;
