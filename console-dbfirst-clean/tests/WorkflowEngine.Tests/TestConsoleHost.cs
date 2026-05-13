using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Demo;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Tests;

internal sealed class TestConsoleHost : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");
    private readonly ServiceProvider serviceProvider;
    private readonly IServiceScope scope;

    public TestConsoleHost(string databaseName)
    {
        ConnectionString = TestSqlConnectionFactory.ConnectionString(databaseName);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Notifications:Sender"] = "NoOp"
            })
            .Build();

        serviceProvider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<TimeProvider>(new FixedTimeProvider(Now))
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
            .AddScoped<IDemoStepRunner, WorkflowEngineDemoStepRunner>()
            .AddScoped<IDemoCommandHandler, DemoCommandHandler>()
            .AddScoped<CommandRegistry>()
            .AddWorkflowInfrastructure(configuration)
            .BuildServiceProvider();

        scope = serviceProvider.CreateScope();
    }

    public string ConnectionString { get; }

    public WorkflowDbContext DbContext => scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

    public void ResetDatabase()
    {
        DbContext.Database.EnsureDeleted();
    }

    public CommandResult Execute(ConsoleCommand command)
    {
        var args = command.ToArgv();
        args = args.Select(ResolveSampleArgument).ToArray();
        return scope.ServiceProvider.GetRequiredService<CommandRegistry>().Dispatch(args);
    }

    public CommandResult Execute(params string[] args)
    {
        return scope.ServiceProvider.GetRequiredService<CommandRegistry>().Dispatch(args.Select(ResolveSampleArgument).ToArray());
    }

    public void Dispose()
    {
        scope.Dispose();
        serviceProvider.Dispose();
    }

    private static string ResolveSampleArgument(string arg)
    {
        if (!arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return arg;
        }

        if (File.Exists(arg))
        {
            return arg;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, arg);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return arg;
    }
}
