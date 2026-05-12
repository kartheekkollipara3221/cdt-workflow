using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Api.Options;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

Program.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected server error.",
                Detail = "The workflow API encountered an unexpected error."
            });
        });
    });
    app.UseHsts();
}

app.MapControllers();

app.Run();

public partial class Program
{
    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddControllers();

        services.Configure<WorkflowApiOptions>(configuration.GetSection(WorkflowApiOptions.SectionName));
        services.Configure<WorkflowDatabaseStartupOptions>(configuration.GetSection(WorkflowDatabaseStartupOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<WorkflowActionEnvelopeValidator>();
        services.AddScoped<WorkflowActionEnvelopeNormalizer>();
        services.AddScoped<CanonicalRequestHasher>();
        services.AddScoped<WorkflowActionResultBuilder>();
        services.AddScoped<CanonicalWorkflowResultSerializer>();

        services.AddWorkflowInfrastructure(configuration);

        services.AddScoped<WorkflowDatabaseReadinessService>();
        services.AddHostedService<WorkflowDatabaseStartupHostedService>();
        services.AddScoped<IWorkflowActorResolver, ClaimsWorkflowActorResolver>();
        services.AddSingleton<WorkflowHttpResultMapper>();

        return services;
    }
}
