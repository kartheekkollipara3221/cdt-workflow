using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.WorkflowApi.Controllers;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Startup;

public sealed class WorkflowApiProjectStructureTests
{
    [Fact]
    public void StandaloneWorkflowApiDoesNotReferenceConsoleOrExistingApiProject()
    {
        var project = XDocument.Load(Path.Combine(
            RepositoryRoot(),
            "workflow-api",
            "src",
            "WorkflowEngine.WorkflowApi",
            "WorkflowEngine.WorkflowApi.csproj"));

        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        Assert.Contains(references, reference => reference.Contains("WorkflowEngine.Application.csproj", StringComparison.Ordinal));
        Assert.Contains(references, reference => reference.Contains("WorkflowEngine.Infrastructure.csproj", StringComparison.Ordinal));
        Assert.Contains(references, reference => reference.Contains("WorkflowEngine.Domain.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference => reference.Contains("WorkflowEngine.Console", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference => reference.StartsWith("..\\..\\..\\api", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkflowApiUsesControllersAndDoesNotExposeConsoleCommands()
    {
        var controllerTypes = typeof(WorkflowContextController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();
        var routeTemplates = controllerTypes.SelectMany(GetRouteTemplates).Select(template => template.ToLowerInvariant()).ToList();
        var sourceDirectory = Path.Combine(RepositoryRoot(), "workflow-api", "src", "WorkflowEngine.WorkflowApi");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.Equal(3, controllerTypes.Count);
        Assert.Contains(typeof(WorkflowActionController), controllerTypes);
        Assert.Contains(typeof(WorkflowContextController), controllerTypes);
        Assert.Contains(typeof(WorkflowStartController), controllerTypes);
        Assert.Contains("v1/workflowaction", routeTemplates);
        Assert.Contains("v1/workflowcontext", routeTemplates);
        Assert.Contains("v1/workflowstart", routeTemplates);
        Assert.DoesNotContain("api/workflow-instances/{workflowinstanceid}/context", routeTemplates);
        Assert.Contains("MapControllers(", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGet(", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost(", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("WeatherForecast", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandRegistry", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandArgumentParser", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("execute-json", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("init-db", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verify-seed", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admin-override", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MockSectionData", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("[FromBody] WorkflowActionEnvelope", sourceText, StringComparison.Ordinal);
        Assert.Contains("[FromBody] WorkflowActionRequest", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureServicesRegistersWorkflowControllerDependencies()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=workflow_api_phase1_tests;Integrated Security=true;TrustServerCertificate=True;Encrypt=False",
                ["Notifications:Sender"] = "NoOp"
            })
            .Build();
        var services = new ServiceCollection();

        Program.ConfigureServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowContextReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowExecutionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowActorResolver>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WorkflowActionEnvelopeNormalizer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CanonicalRequestHasher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowActionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowContextQueryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowStartService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WorkflowEngine.Application.WorkflowStart.IStartWorkflowService>());

        var jsonOptions = provider.GetRequiredService<IOptions<JsonOptions>>();
        Assert.Null(jsonOptions.Value.JsonSerializerOptions.PropertyNamingPolicy);
    }

    private static IEnumerable<string> GetRouteTemplates(Type controllerType)
    {
        foreach (var route in controllerType.GetCustomAttributes<RouteAttribute>())
        {
            yield return route.Template;
        }

        foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var route in method.GetCustomAttributes<RouteAttribute>())
            {
                yield return route.Template;
            }

            foreach (var httpMethod in method.GetCustomAttributes<HttpMethodAttribute>())
            {
                if (!string.IsNullOrWhiteSpace(httpMethod.Template))
                {
                    yield return httpMethod.Template;
                }
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PROJECT.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
