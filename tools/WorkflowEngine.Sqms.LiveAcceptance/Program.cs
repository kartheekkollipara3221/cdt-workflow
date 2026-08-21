using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Sqms.DependencyInjection;
using WorkflowEngine.Sqms.LiveAcceptance;

try
{
    var runMode = AcceptanceRunModeParser.Parse(args);
    var configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
    var builder = new SqlConnectionStringBuilder(connectionString);
    if (!string.Equals(
            builder.InitialCatalog,
            AcceptanceConstants.DatabaseName,
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Refusing database '{builder.InitialCatalog}'. Expected '{AcceptanceConstants.DatabaseName}'.");
    }

    var clock = new AcceptanceTimeProvider(AcceptanceConstants.OpeningUtc);
    await using var services = new ServiceCollection()
        .AddSingleton<IConfiguration>(configuration)
        .AddSingleton(clock)
        .AddSingleton<TimeProvider>(clock)
        .AddSingleton(new AcceptanceReporter(Console.Out))
        .AddScoped<WorkflowActionEnvelopeNormalizer>()
        .AddScoped<CanonicalRequestHasher>()
        .AddScoped<WorkflowActionResultBuilder>()
        .AddScoped<CanonicalWorkflowResultSerializer>()
        .AddScoped<AcceptanceFixtureWriter>()
        .AddScoped<AcceptanceEvidenceVerifier>()
        .AddScoped<AcceptanceScenarioRunner>()
        .AddWorkflowInfrastructure(configuration)
        .AddSqmsWorkflowIntegration(configuration)
        .BuildServiceProvider();

    await using var scope = services.CreateAsyncScope();
    if (runMode == AcceptanceRunMode.VerifyOnly)
    {
        await scope.ServiceProvider
            .GetRequiredService<AcceptanceEvidenceVerifier>()
            .VerifyAsync(CancellationToken.None);
        Console.Out.WriteLine("SQMS live acceptance verification-only PASS");
        return 0;
    }

    return await scope.ServiceProvider.GetRequiredService<AcceptanceScenarioRunner>()
        .RunAsync(CancellationToken.None);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
