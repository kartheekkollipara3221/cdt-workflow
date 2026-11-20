using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Options;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Lists the display names in lookup tables that do not give exactly one user with an email (--atlas-check only).
/// </summary>
/// <remarks>
/// The report reads each Table options source with the parameter UserNameColumns. A name is a text between ';'
/// separators. Each line is an Info item: it is not counted and does not change the exit code.
/// The report shows 200 lines at most for each lookup table, then the count of the other lines.
/// </remarks>
public static class AtlasNameReport
{
    /// <summary>The largest number of lines for one lookup table.</summary>
    public const int MaxLinesPerTable = 200;

    /// <summary>Returns the Info items of the report for all enabled apps that have a registration line.</summary>
    public static IReadOnlyList<AtlasCheckItem> Run(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        using IServiceScope scope = services.CreateScope();
        IServiceProvider provider = scope.ServiceProvider;
        IAtlasConfigStore configStore = provider.GetRequiredService<IAtlasConfigStore>();
        AtlasTableOptionProvider tables = provider.GetRequiredService<AtlasTableOptionProvider>();
        IAtlasUserDirectory directory = provider.GetRequiredService<IAtlasUserDirectory>();
        HashSet<string> registered = new(provider.GetServices<AtlasModuleRegistration>().Select(x => x.Code), StringComparer.OrdinalIgnoreCase);

        List<AtlasCheckItem> items = [];
        HashSet<string> done = new(StringComparer.OrdinalIgnoreCase);
        foreach (AtlasAppManifest manifest in configStore.GetManifests().Where(x => x.Enabled && registered.Contains(x.Code)))
        {
            CPAtlasApplicationConfig form;
            try
            {
                form = configStore.GetForm(manifest.Code);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                continue; // the start-up check reports a form that cannot be read
            }

            foreach (CPApplicationControl control in Controls(form).Where(IsNameSource))
            {
                AtlasOptionRequest request = new(manifest.Code, control, control.OptionsSource!);
                string table = Parameter(control, "Table") ?? "";
                string valueColumn = Parameter(control, "Value") ?? "";
                // Two controls that read the same table and columns give the same lines one time.
                if (!done.Add(string.Join("|", table, valueColumn, Parameter(control, "UserNameColumns"))))
                    continue;

                try
                {
                    items.AddRange(Lines(manifest.Code, table, valueColumn, tables.UserNameRows(request), directory));
                }
                catch (AtlasProblemException)
                {
                    // A bad options source is a check error of its app. The report leaves it out.
                }
            }
        }

        return items;
    }

    private static IEnumerable<AtlasCheckItem> Lines(string app, string table, string valueColumn,
        IReadOnlyList<(string Value, IReadOnlyDictionary<string, string?> Names)> rows, IAtlasUserDirectory directory)
    {
        List<string> lines = [];
        foreach ((string value, IReadOnlyDictionary<string, string?> names) in rows)
            foreach ((string column, string? text) in names)
                foreach (string name in (text ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    if (directory.EmailOfDisplayName(name) is { Email: null, Problem: { } problem })
                        lines.Add($"{table} {valueColumn} {value}: {column} \"{name}\": {problem}");

        foreach (string line in lines.Take(MaxLinesPerTable))
            yield return new(AtlasCheckSeverity.Info, app, line);
        if (lines.Count > MaxLinesPerTable)
            yield return new(AtlasCheckSeverity.Info, app, $"{table}: {lines.Count - MaxLinesPerTable} more name(s) with no single user.");
    }

    private static bool IsNameSource(CPApplicationControl control) =>
        string.Equals(control.OptionsSource?.Provider, AtlasTableOptionProvider.ProviderName, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Parameter(control, "UserNameColumns"));

    private static string? Parameter(CPApplicationControl control, string name) =>
        control.OptionsSource?.Parameters?.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static IEnumerable<CPApplicationControl> Controls(CPAtlasApplicationConfig form) =>
        (form.Section?.Controls ?? []).SelectMany(x => new[] { x }.Concat(x.ChildControls ?? []));
}
