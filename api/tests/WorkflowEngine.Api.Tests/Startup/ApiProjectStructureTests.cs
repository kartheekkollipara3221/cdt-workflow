using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Api.Controllers;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Application.WorkflowStart;

namespace WorkflowEngine.Api.Tests.Startup;

public sealed class ApiProjectStructureTests
{
    [Fact]
    public void ApiProjectDoesNotReferenceWorkflowEngineConsole()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "api", "src", "WorkflowEngine.Api", "WorkflowEngine.Api.csproj"));

        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        Assert.Contains(references, reference => reference.Contains("WorkflowEngine.Application.csproj", StringComparison.Ordinal));
        Assert.Contains(references, reference => reference.Contains("WorkflowEngine.Infrastructure.csproj", StringComparison.Ordinal));
        Assert.Contains(references, reference => reference.Contains("WorkflowEngine.Domain.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference => reference.Contains("WorkflowEngine.Console", StringComparison.Ordinal));
    }

    [Fact]
    public void ApiDoesNotExposeForbiddenPublicWorkflowRoutes()
    {
        var controllerTypes = typeof(WorkflowActionsController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();
        var routeTemplates = controllerTypes.SelectMany(GetRouteTemplates).Select(template => template.ToLowerInvariant()).ToList();
        var bannedTerms = new[]
        {
            "init-db",
            "verify-seed",
            "reset",
            "admin",
            "override",
            "run-sla",
            "process-notification-outbox",
            "demo",
            "execute-json",
            "show",
            "show-tasks",
            "show-history",
            "show-notifications"
        };

        Assert.Equal(3, controllerTypes.Count);
        foreach (var bannedTerm in bannedTerms)
        {
            Assert.DoesNotContain(routeTemplates, template => template.Contains(bannedTerm, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ApiProgramDoesNotUseMinimalApiWorkflowRouteHandlersOrTemplateEndpoints()
    {
        var apiSourceDirectory = Path.Combine(RepositoryRoot(), "api", "src", "WorkflowEngine.Api");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(apiSourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("MapGet(", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost(", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("WeatherForecast", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandRegistry", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandArgumentParser", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiDependencyInjectionRegistersWorkflowInfrastructureServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=workflow_api_tests;Integrated Security=true;TrustServerCertificate=True;Encrypt=False",
                ["Notifications:Sender"] = "NoOp"
            })
            .Build();
        var services = new ServiceCollection();

        Program.ConfigureServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStartWorkflowService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowContextReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowExecutionService>());
    }

    private static IEnumerable<string> GetRouteTemplates(Type controllerType)
    {
        foreach (var route in controllerType.GetCustomAttributes<RouteAttribute>())
        {
            yield return route.Template;
        }

        foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
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
