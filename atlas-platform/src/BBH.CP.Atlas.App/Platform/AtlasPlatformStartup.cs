using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Start-up of the platform. program.cs calls it after Build and before Run:
/// <code>await app.Services.StartAtlasPlatformAsync();</code>
/// It reads the table metadata of each registered app into the table registry before the first request
/// (spec 05, 6.2; a new table needs a restart) and runs the start-up check (spec 07, 4.8).
/// </summary>
public static class AtlasPlatformStartup
{
    public static readonly EventId StartupCheck = new(41020, "Atlas.StartupCheck");

    /// <summary>Loads the table metadata and runs the start-up check. An error stops the start-up (spec 07, 4.8).</summary>
    public static async Task StartAtlasPlatformAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AtlasCheckItem> items = await LoadAndCheckAsync(services, cancellationToken);
        AtlasCheckItem[] errors = [.. items.Where(x => x.Severity == AtlasCheckSeverity.Error)];
        if (errors.Length > 0)
            throw new InvalidOperationException("The Atlas start-up check found errors:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(x => "  " + x)));
    }

    /// <summary>
    /// The --atlas-check command (spec 11, 4.3): the same checks, a report on <paramref name="output"/>, no web server.
    /// Returns the exit code: 0 = no errors, 1 = errors.
    /// </summary>
    public static async Task<int> RunAtlasCheckAsync(this IServiceProvider services, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        IReadOnlyList<AtlasCheckItem> items;
        try
        {
            items = await LoadAndCheckAsync(services, cancellationToken);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            await output.WriteLineAsync("ERROR *: " + ex.Message);
            return 1;
        }

        string environment = services.GetRequiredService<IHostEnvironment>().EnvironmentName;
        await output.WriteLineAsync($"Atlas check ({environment}): {items.Count(x => x.Severity == AtlasCheckSeverity.Error)} error(s), "
            + $"{items.Count(x => x.Severity == AtlasCheckSeverity.Warning)} warning(s).");
        if (services.GetService<AtlasPlatformConfiguration>() is { } configuration)
            await output.WriteLineAsync($"  Settings from: {configuration.Sources}");
        foreach (AtlasCheckItem item in items)
            await output.WriteLineAsync("  " + item);
        return items.Any(x => x.Severity == AtlasCheckSeverity.Error) ? 1 : 0;
    }

    private static async Task<IReadOnlyList<AtlasCheckItem>> LoadAndCheckAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        using IServiceScope scope = services.CreateScope();
        IServiceProvider provider = scope.ServiceProvider;
        ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("BBH.CP.Atlas.Platform.Startup");

        IAtlasConfigStore configStore = provider.GetRequiredService<IAtlasConfigStore>();
        HashSet<string> registered = new(provider.GetServices<AtlasModuleRegistration>().Select(x => x.Code), StringComparer.OrdinalIgnoreCase);
        string[] tables = [.. configStore.GetManifests()
            .Where(x => x.Enabled && registered.Contains(x.Code))
            .SelectMany(x => new[] { x.Table, x.AuditTable, x.AttachmentTable })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)];

        CPSharepointContext context = provider.GetRequiredService<CPSharepointContext>();
        await using (SqlConnection connection = new(context.Database.GetConnectionString()))
        {
            await provider.GetRequiredService<AtlasColumnMetadataReader>()
                .LoadAsync(connection, provider.GetRequiredService<AtlasTableRegistry>(), tables, cancellationToken);
        }

        IReadOnlyList<AtlasCheckItem> items = AtlasStartupCheck.Run(provider);
        foreach (AtlasCheckItem item in items)
        {
            if (item.Severity == AtlasCheckSeverity.Error)
                logger.LogError(StartupCheck, "Atlas.StartupCheck {Item}", item.ToString());
            else
                logger.LogWarning(StartupCheck, "Atlas.StartupCheck {Item}", item.ToString());
        }

        logger.LogInformation(StartupCheck, "Atlas.Started Environment={Environment} Modules={Modules} Tables={Tables} Errors={Errors} Warnings={Warnings}",
            provider.GetRequiredService<IHostEnvironment>().EnvironmentName, string.Join(",", registered), tables.Length,
            items.Count(x => x.Severity == AtlasCheckSeverity.Error), items.Count(x => x.Severity == AtlasCheckSeverity.Warning));
        return items;
    }
}
