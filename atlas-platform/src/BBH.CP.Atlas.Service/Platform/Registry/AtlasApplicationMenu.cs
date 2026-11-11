using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// Adds the platform data to the v1/Application menu rows.
/// </summary>
/// <remarks>
/// Each platform row gets FormType and HasDashboard from the registry.
/// The step removes a row whose code is not a current (enum) app and not an enabled platform app.
/// The rows of the current apps do not change.
/// </remarks>
public static class AtlasApplicationMenu
{
    /// <summary>Returns the menu rows with the platform data added.</summary>
    /// <remarks>
    /// The current apps share the menu, so an error in this step must not fail the menu.
    /// On an exception, the method returns the rows of the procedure with no change.
    /// It then logs Atlas.ConfigError when a logger is given.
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
