using Microsoft.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.WorkflowApi.Infrastructure;
using WorkflowEngine.WorkflowApi.Services;

var builder = WebApplication.CreateBuilder(args);

Program.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

Program.ConfigurePipeline(app);

app.Run();

public partial class Program
{
    internal const string CorsPolicyName = "_corsOrigins";

    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(configuration);

        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Workflow Engine API",
                Version = "v1"
            });
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins(GetCorsOrigins(configuration))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
        services.AddAuthorization();

        services.AddOptions<WorkflowActorHeaderOptions>()
            .Bind(configuration.GetSection(WorkflowActorHeaderOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserIdHeaderName), "Actor user header name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RolesHeaderName), "Actor roles header name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DisplayNameHeaderName), "Actor display name header name is required.");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<WorkflowActionEnvelopeNormalizer>();
        services.AddScoped<CanonicalRequestHasher>();
        services.AddScoped<WorkflowActionResultBuilder>();
        services.AddScoped<CanonicalWorkflowResultSerializer>();
        services.AddWorkflowInfrastructure(configuration);
        services.AddScoped<IWorkflowActorResolver, TrustedHeaderWorkflowActorResolver>();
        services.AddScoped<IWorkflowActionNotificationDispatcher, WorkflowActionNotificationDispatcher>();
        services.AddScoped<IWorkflowActionService, WorkflowActionService>();
        services.AddScoped<IWorkflowContextQueryService, WorkflowContextQueryService>();
        services.AddScoped<IWorkflowStartService, WorkflowStartService>();

        return services;
    }

    public static WebApplication ConfigurePipeline(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        else
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Workflow Engine API V1");
                options.RoutePrefix = string.Empty;
            });
        }

        app.UseCors(CorsPolicyName);
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }

    private static string[] GetCorsOrigins(IConfiguration configuration)
    {
        var configuredOrigins = configuration
            .GetSection("Cors:Origins")
            .Get<string[]>() ?? [];

        var origins = configuredOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        return origins.Length > 0 ? origins : ["http://localhost:4200"];
    }
}
