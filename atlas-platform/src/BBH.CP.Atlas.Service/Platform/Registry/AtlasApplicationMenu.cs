using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// v1/Application menu data (spec 07, 4.6). Adds FormType and HasDashboard from the registry to each row. A row of
/// a code that is not a current (enum) app and not an enabled platform app is removed. Current rows do not change.
/// </summary>
public static class AtlasApplicationMenu
{
    /// <remarks>
    /// Review AR-02: the menu is shared with the current apps, so an error in this step does not fail it. On an
    /// exception the rows of the procedure come back unchanged (as before the platform) and Atlas.ConfigError is
    /// logged when a logger is given.
    /// </remarks>
    public static List<Dictionary<string, object>> Enrich(List<Dictionary<string, object>> rows, IAtlasModuleResolver resolver,
        IAtlasConfigStore configStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(configStore);

        try
        {
            return EnrichRows(rows, resolver, configStore);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger != null)
                AtlasLog.ConfigError(logger, ex, "v1/Application", "The menu step failed; the menu rows are returned unchanged.");
            return rows;
        }
    }

    private static List<Dictionary<string, object>> EnrichRows(List<Dictionary<string, object>> rows, IAtlasModuleResolver resolver,
        IAtlasConfigStore configStore)
    {
        List<Dictionary<string, object>> result = [];
        foreach (Dictionary<string, object> row in rows)
        {
            string? code = row.TryGetValue("Code", out object? value) ? value as string : null;
            if (resolver.IsPlatformApp(code))
            {
                AtlasAppManifest manifest = configStore.FindManifest(code!)!;
                // A new row object, so a failure later in the loop returns the procedure rows with no change.
                result.Add(new Dictionary<string, object>(row, row.Comparer)
                {
                    ["FormType"] = manifest.FormType,
                    ["HasDashboard"] = !string.IsNullOrWhiteSpace(manifest.Dashboard)
                });
            }
            else if (AtlasAppCode.IsLegacy(code) || string.IsNullOrWhiteSpace(code))
            {
                result.Add(row); // current app (or a family row with no code): unchanged
            }
        }

        return result;
    }
}
