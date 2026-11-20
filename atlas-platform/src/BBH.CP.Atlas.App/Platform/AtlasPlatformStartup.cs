using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Starts the platform: loads the table metadata of each registered app and runs the start-up check.
/// </summary>
/// <remarks>
/// A hosted service of AddAtlasPlatform runs the start-up before the server takes requests.
/// program.cs can also call it after Build and before Run. The second call does nothing:
/// <code>await app.Services.StartAtlasPlatformAsync();</code>
/// The platform reads the table metadata one time only. A new table needs a restart.
/// </remarks>
public static class AtlasPlatformStartup
{
    /// <summary>
    /// The name of the setting that tells what a start-up error does: DisableModule (default) or Stop.
    /// </summary>
    /// <remarks>
    /// The platform runs in the process of the current apps. By default, a platform error must not stop them.
    /// </remarks>
    public const string OnStartupErrorKey = AtlasPlatformOptions.OnStartupErrorKey;

    /// <summary>
    /// Loads the table metadata and runs the start-up check.
    /// </summary>
    /// <remarks>
    /// With DisableModule (default), an error disables only the platform apps that have errors.
    /// With Stop, an error stops the start-up.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The mode is Stop and the check found errors.</exception>
    public static async Task StartAtlasPlatformAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        AtlasTableRegistry registry = services.GetRequiredService<AtlasTableRegistry>();
        AtlasPlatformStartupGate? gate = services.GetService<AtlasPlatformStartupGate>();
        if (registry.IsLoaded)
            return;

        if (gate != null)
            await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            if (registry.IsLoaded)
                return;

            (AtlasStartupErrorMode mode, string? settingError) = OnStartupError(services);
            IReadOnlyList<AtlasCheckItem> items;
            try
            {
                items = await LoadAndCheckAsync(services, cancellationToken);
            }
            catch (Exception ex) when (mode == AtlasStartupErrorMode.DisableModule && ex is not OperationCanceledException)
            {
                // Any start-up failure (for example, SQL is not available) disables all platform apps. The host runs.
                items = [new(AtlasCheckSeverity.Error, "*", ex.Message)];
            }

            if (settingError != null)
                items = [.. items, new(AtlasCheckSeverity.Error, "*", settingError)];

            AtlasCheckItem[] errors = [.. items.Where(x => x.Severity == AtlasCheckSeverity.Error)];
            if (errors.Length > 0 && mode == AtlasStartupErrorMode.Stop)
                throw new InvalidOperationException("The Atlas start-up check found errors:" + Environment.NewLine
                    + string.Join(Environment.NewLine, errors.Select(x => "  " + x)));
            if (errors.Length > 0)
                DisableApps(services, errors);
            registry.MarkLoaded();
        }
        finally
        {
            gate?.Lock.Release();
        }
    }

    /// <summary>
    /// Returns the start-up error mode, and an error text when the setting is not valid.
    /// </summary>
    /// <remarks>
    /// No value gives DisableModule. A value that is not valid also gives DisableModule, and an error for all platform apps.
    /// Thus a typing error does not stop the current apps. The log and --atlas-check show the error.
    /// </remarks>
    private static (AtlasStartupErrorMode Mode, string? Error) OnStartupError(IServiceProvider services)
    {
        AtlasPlatformOptions options = services.GetService<AtlasPlatformConfiguration>()?.Options
            ?? AtlasPlatformOptions.From(services.GetService<IConfiguration>());
        return options.StartupErrorMode is { } mode
            ? (mode, null)
            : (AtlasStartupErrorMode.DisableModule, $"The setting {OnStartupErrorKey} = '{options.OnStartupError}' is not valid (allowed: DisableModule, Stop).");
    }

    /// <summary>Disables the apps that have errors. An error for all apps ("*") disables all of them.</summary>
    private static void DisableApps(IServiceProvider services, AtlasCheckItem[] errors)
    {
        AtlasDisabledApps disabled = services.GetRequiredService<AtlasDisabledApps>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("BBH.CP.Atlas.Platform.Startup");
        string[] all = [.. services.GetServices<AtlasModuleRegistration>().Select(x => x.Code)];
        // A config file that the store cannot read already removes only its own app. Thus it does not disable all apps.
        // This method also runs after a failed start-up. If the config store fails again, disable all apps.
        // The exception must not stop the host.
        HashSet<string> fileErrors = new(StringComparer.Ordinal);
        try
        {
            fileErrors.UnionWith(services.GetRequiredService<IAtlasConfigStore>().ConfigErrors);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors = [.. errors, new(AtlasCheckSeverity.Error, "*", "the config store cannot be read: " + ex.Message)];
        }
        foreach (IGrouping<string, AtlasCheckItem> group in errors.Where(x => !(x.App == "*" && fileErrors.Contains(x.Message)))
            .GroupBy(x => x.App, StringComparer.OrdinalIgnoreCase))
        {
            string reason = string.Join(" ", group.Select(x => x.Message));
            foreach (string code in group.Key == "*" ? all : [group.Key])
            {
                disabled.Disable(code, reason);
                AtlasLog.AppDisabled(logger, code, reason);
            }
        }
    }

    /// <summary>
    /// Runs the --atlas-check command: the start-up checks without a web server, with a report on <paramref name="output"/>.
    /// </summary>
    /// <returns>The exit code: 0 when there are no errors, 1 when there are errors.</returns>
    public static async Task<int> RunAtlasCheckAsync(this IServiceProvider services, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        IReadOnlyList<AtlasCheckItem> items;
        try
        {
            items = await LoadAndCheckAsync(services, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Any failure gives a report line and exit code 1, not a stack trace.
            await output.WriteLineAsync("ERROR *: " + ex.Message);
            return 1;
        }

        if (OnStartupError(services).Error is { } settingError)
            items = [.. items, new(AtlasCheckSeverity.Error, "*", settingError)];

        // The name report reads the lookup tables, so only --atlas-check runs it, not each start-up.
        try
        {
            items = [.. items, .. AtlasNameReport.Run(services)];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            items = [.. items, new(AtlasCheckSeverity.Warning, "*", "the name report stopped with an error: " + ex.Message)];
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
        // Load the tables of all registered manifests, also disabled ones. An app that is enabled later then finds its tables.
        string[] tables = [.. configStore.GetManifests()
            .Where(x => registered.Contains(x.Code))
            .SelectMany(x => new[] { x.Table, x.AuditTable, x.AttachmentTable }.Concat(x.LookupTables ?? []))
            .Where(x => AtlasTable.IsValidFullName(x)) // a bad name is a check error of its app only
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        CPSharepointContext context = provider.GetRequiredService<CPSharepointContext>();
        await using (SqlConnection connection = new(context.Database.GetConnectionString()))
        {
            await provider.GetRequiredService<AtlasColumnMetadataReader>()
                .LoadAsync(connection, provider.GetRequiredService<AtlasTableRegistry>(), tables, cancellationToken);
        }

        IReadOnlyList<AtlasCheckItem> items = AtlasStartupCheck.Run(provider);
        foreach (AtlasCheckItem item in items)
        {
            AtlasLog.StartupCheckItem(logger, item.Severity switch
            {
                AtlasCheckSeverity.Error => LogLevel.Error,
                AtlasCheckSeverity.Info => LogLevel.Information,
                _ => LogLevel.Warning
            }, item.ToString());
        }

        AtlasLog.Started(logger, provider.GetRequiredService<IHostEnvironment>().EnvironmentName, string.Join(",", registered), tables.Length,
            items.Count(x => x.Severity == AtlasCheckSeverity.Error), items.Count(x => x.Severity == AtlasCheckSeverity.Warning));
        return items;
    }
}

/// <summary>Lets only one start-up run at a time (the hosted service or a program.cs call).</summary>
public sealed class AtlasPlatformStartupGate
{
    internal SemaphoreSlim Lock { get; } = new(1, 1);
}

/// <summary>
/// Runs the platform start-up before the server takes requests.
/// </summary>
/// <remarks>
/// A host that does not call StartAtlasPlatformAsync thus still loads the tables.
/// The start-up token stops the start-up when the host stops.
/// </remarks>
public sealed class AtlasPlatformStartupService(IServiceProvider services) : IHostedService
{
    /// <summary>Runs the platform start-up.</summary>
    public Task StartAsync(CancellationToken cancellationToken) => services.StartAtlasPlatformAsync(cancellationToken);

    /// <summary>Does nothing. The platform has no work to stop.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
