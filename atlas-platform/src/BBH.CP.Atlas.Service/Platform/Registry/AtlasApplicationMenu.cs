namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// v1/Application menu data (spec 07, 4.6). Adds FormType and HasDashboard from the registry to each row. A row of
/// a code that is not a current (enum) app and not an enabled platform app is removed. Current rows do not change.
/// </summary>
public static class AtlasApplicationMenu
{
    public static List<Dictionary<string, object>> Enrich(List<Dictionary<string, object>> rows, IAtlasModuleResolver resolver,
        IAtlasConfigStore configStore)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(configStore);

        List<Dictionary<string, object>> result = [];
        foreach (Dictionary<string, object> row in rows)
        {
            string? code = row.TryGetValue("Code", out object? value) ? value as string : null;
            if (resolver.IsPlatformApp(code))
            {
                AtlasAppManifest manifest = configStore.FindManifest(code!)!;
                row["FormType"] = manifest.FormType;
                row["HasDashboard"] = !string.IsNullOrWhiteSpace(manifest.Dashboard);
                result.Add(row);
            }
            else if (AtlasAppCode.IsLegacy(code) || string.IsNullOrWhiteSpace(code))
            {
                result.Add(row); // current app (or a family row with no code): unchanged
            }
        }

        return result;
    }
}
