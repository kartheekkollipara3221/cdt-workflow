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
/// (spec 05, 6.2). A new table needs a restart.
/// </summary>
public static class AtlasPlatformStartup
{
    public static async Task StartAtlasPlatformAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
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
        await using SqlConnection connection = new(context.Database.GetConnectionString());
        IReadOnlyList<string> missing = await provider.GetRequiredService<AtlasColumnMetadataReader>()
            .LoadAsync(connection, provider.GetRequiredService<AtlasTableRegistry>(), tables, cancellationToken);

        foreach (string table in missing)
            logger.LogError("Atlas.TableMissing Table={Table}: the table does not exist or the API login cannot see it.", table);
        logger.LogInformation("Atlas.Started Tables={Count} Modules={Modules}", tables.Length - missing.Count, string.Join(",", registered));
    }
}
