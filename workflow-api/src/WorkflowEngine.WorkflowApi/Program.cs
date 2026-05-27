using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Application.Results;
using WorkflowEngine.WorkflowApi.Services;

var builder = WebApplication.CreateBuilder(args);

Program.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

Program.ConfigurePipeline(app);

app.Run();

public partial class Program
{
    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddProblemDetails();
        services.AddControllers();

        services.AddOptions<WorkflowActorHeaderOptions>()
            .Bind(configuration.GetSection(WorkflowActorHeaderOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserIdHeaderName), "Actor user header name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RolesHeaderName), "Actor roles header name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DisplayNameHeaderName), "Actor display name header name is required.");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<WorkflowActionResultBuilder>();
        services.AddScoped<CanonicalWorkflowResultSerializer>();
        services.AddWorkflowInfrastructure(configuration);
        services.AddScoped<IWorkflowActorResolver, TrustedHeaderWorkflowActorResolver>();

        return services;
    }

    public static WebApplication ConfigurePipeline(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.MapControllers();

        return app;
    }
}
