using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Start-up of the platform. A hosted service of AddAtlasPlatform runs it before the server takes requests (review
/// HO-06). program.cs can still call it after Build and before Run; the second call does nothing:
/// <code>await app.Services.StartAtlasPlatformAsync();</code>
/// It reads the table metadata of each registered app into the table registry before the first request
/// (spec 05, 6.2; a new table needs a restart) and runs the start-up check (spec 07, 4.8).
/// </summary>
public static class AtlasPlatformStartup
{
    /// <summary>
    /// The setting of review HO-04: DisableModule (default since review 2, AR-01) or Stop. The platform runs in the
    /// process of the current apps, so by default a platform error must not stop them.
    /// </summary>
    public const string OnStartupErrorKey = "AppSettings:Atlas:OnStartupError";

    /// <summary>
    /// Loads the table metadata and runs the start-up check. With DisableModule (default) an error removes only the
    /// platform apps with errors; with Stop an error stops the start-up (spec 07, 4.8).
    /// </summary>
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
                // Review HO-04, AR-01: any start-up failure (for example SQL is not available): no platform app, the host runs.
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
    /// The mode, and an error text when the value is not valid. Review AR-01: no value = DisableModule; a value that
    /// is not valid also gives DisableModule (the host runs) and an error for all platform apps, so a typo does not
    /// stop the current apps and is seen in the log and in --atlas-check.
    /// </summary>
    private static (AtlasStartupErrorMode Mode, string? Error) OnStartupError(IServiceProvider services)
    {
        IConfiguration? configuration = services.GetService<AtlasPlatformConfiguration>()?.Configuration ?? services.GetService<IConfiguration>();
        string? text = configuration?[OnStartupErrorKey];
        if (string.IsNullOrWhiteSpace(text))
            return (AtlasStartupErrorMode.DisableModule, null);
        return Enum.TryParse(text.Trim(), ignoreCase: true, out AtlasStartupErrorMode mode) && Enum.IsDefined(mode) && !int.TryParse(text, out _)
            ? (mode, null)
            : (AtlasStartupErrorMode.DisableModule, $"The setting {OnStartupErrorKey} = '{text}' is not valid (allowed: DisableModule, Stop).");
    }

    /// <summary>DisableModule: the apps with errors are not available; an error of all apps ("*") disables all of them.</summary>
    private static void DisableApps(IServiceProvider services, AtlasCheckItem[] errors)
    {
        AtlasDisabledApps disabled = services.GetRequiredService<AtlasDisabledApps>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("BBH.CP.Atlas.Platform.Startup");
        string[] all = [.. services.GetServices<AtlasModuleRegistration>().Select(x => x.Code)];
        // A config file that cannot be read already removes only its own app (review HO-02), so it does not disable all.
        // Review 3 RV-07: this runs after a failed start-up too; if the config store fails again, disable all apps here
        // and do not let the exception stop the host.
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Review 3 RV-06: a report line and exit code 1, not a stack trace, for any failure.
            await output.WriteLineAsync("ERROR *: " + ex.Message);
            return 1;
        }

        if (OnStartupError(services).Error is { } settingError)
            items = [.. items, new(AtlasCheckSeverity.Error, "*", settingError)];

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
        // All registered manifests, also disabled ones, so an app that is switched on later finds its tables (review HO-05).
        string[] tables = [.. configStore.GetManifests()
            .Where(x => registered.Contains(x.Code))
            .SelectMany(x => new[] { x.Table, x.AuditTable, x.AttachmentTable })
            .Where(x => AtlasTable.IsValidFullName(x)) // review 3 RV-06: a bad name is a check error of its app only
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
            AtlasLog.StartupCheckItem(logger, item.Severity == AtlasCheckSeverity.Error ? LogLevel.Error : LogLevel.Warning, item.ToString());
        }

        AtlasLog.Started(logger, provider.GetRequiredService<IHostEnvironment>().EnvironmentName, string.Join(",", registered), tables.Length,
            items.Count(x => x.Severity == AtlasCheckSeverity.Error), items.Count(x => x.Severity == AtlasCheckSeverity.Warning));
        return items;
    }
}

/// <summary>One start-up at a time (the hosted service and a program.cs call).</summary>
public sealed class AtlasPlatformStartupGate
{
    internal SemaphoreSlim Lock { get; } = new(1, 1);
}

/// <summary>
/// Runs the platform start-up before the server takes requests (review HO-06), so a host that does not call
/// StartAtlasPlatformAsync still loads the tables. The start-up token stops it when the host stops.
/// </summary>
public sealed class AtlasPlatformStartupService(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => services.StartAtlasPlatformAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
