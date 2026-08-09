using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Domain.Workflow;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Persistence.Verification;
using WorkflowEngine.Infrastructure.Sla;
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

        ValidateNotificationSenderConfiguration(configuration);

        services.AddDbContext<WorkflowDbContext>(options => options.UseSqlServer(
            connectionString,
            sqlOptions => sqlOptions.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                WorkflowDbContext.DefaultSchema)));
        services.AddSingleton<NotificationTemplateRenderer>();
        services.AddScoped<WorkflowSeedService>();
        services.AddScoped<WorkflowDatabaseInitializer>();
        services.AddScoped<SeedVerificationService>();
        services.AddScoped<WorkflowContextReader>();
        services.AddScoped<WorkflowExecutionService>();
        services.AddScoped<WorkflowInspectionReader>();
        services.AddScoped<StartWorkflowService>();
        services.AddScoped<WorkflowParticipantCaptureService>();
        services.AddScoped<NotificationRecipientResolver>();
        services.AddScoped<RunSlaService>();
        services.AddSingleton<SlaScheduleCalculator>();
        services.AddScoped<WorkflowSlaSynchronizationService>();
        services.AddScoped<ProcessNotificationOutboxService>();
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
        services.AddScoped<global::WorkflowEngine.Application.Sla.IRunSlaService>(provider =>
            provider.GetRequiredService<RunSlaService>());
        services.AddScoped<global::WorkflowEngine.Application.Sla.IWorkflowSlaSynchronizationService>(provider =>
            provider.GetRequiredService<WorkflowSlaSynchronizationService>());
        services.AddScoped<global::WorkflowEngine.Application.Notifications.IProcessNotificationOutboxService>(provider =>
            provider.GetRequiredService<ProcessNotificationOutboxService>());
        services.AddSingleton<INotificationSender>(provider =>
            CreateNotificationSender(configuration, provider));

        return services;
    }

    private static void ValidateNotificationSenderConfiguration(IConfiguration configuration)
    {
        var senderType = configuration["Notifications:Sender"] ?? "Console";
        switch (senderType.Trim().ToLowerInvariant())
        {
            case "smtp":
                SmtpNotificationSender.ValidateConfiguration(configuration);
                break;
            case "noop":
            case "console":
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown Notifications:Sender value '{senderType}'. Allowed values: Console, NoOp, Smtp.");
        }
    }

    private static INotificationSender CreateNotificationSender(
        IConfiguration configuration,
        IServiceProvider provider)
    {
        var senderType = configuration["Notifications:Sender"] ?? "Console";
        return senderType.Trim().ToLowerInvariant() switch
        {
            "smtp" => new SmtpNotificationSender(configuration),
            "noop" => new NoOpNotificationSender(),
            "console" => new ConsoleNotificationSender(),
            _ => throw new InvalidOperationException(
                $"Unknown Notifications:Sender value '{senderType}'. Allowed values: Console, NoOp, Smtp.")
        };
    }
}
