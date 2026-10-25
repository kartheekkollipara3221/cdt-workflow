using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform.Dashboard;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using BBH.CP.Atlas.Service.Platform.Sections;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.App.Platform;

public enum AtlasCheckSeverity { Warning, Error }

public sealed record AtlasCheckItem(AtlasCheckSeverity Severity, string App, string Message)
{
    public override string ToString() => $"{Severity.ToString().ToUpperInvariant()} {App}: {Message}";
}

/// <summary>
/// The start-up check of all modules (spec 07, 4.8; spec 11, 4.3): manifests and registration lines, the app row,
/// tables, form fields against the columns, option providers, the dashboard tabs, and the rule roles (warnings).
/// The table registry must be loaded first.
/// </summary>
public static class AtlasStartupCheck
{
    private static readonly string[] Flags = [AtlasAppRoles.ReadFlag, AtlasAppRoles.WriteFlag, AtlasAppRoles.AdminFlag];

    public static IReadOnlyList<AtlasCheckItem> Run(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        List<AtlasCheckItem> items = [];
        IAtlasConfigStore configStore = services.GetRequiredService<IAtlasConfigStore>();
        AtlasTableRegistry tables = services.GetRequiredService<AtlasTableRegistry>();
        CPSharepointContext context = services.GetRequiredService<CPSharepointContext>();
        Dictionary<string, AtlasModuleRegistration> registrations = services.GetServices<AtlasModuleRegistration>()
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<AtlasAppManifest> manifests;
        try
        {
            manifests = configStore.GetManifests();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return [new(AtlasCheckSeverity.Error, "*", ex.Message)];
        }

        foreach (string code in registrations.Keys.Where(code => !manifests.Any(x => x.Code == code)))
            items.Add(new(AtlasCheckSeverity.Error, code, $"has a registration line but no {code}.app.json in {configStore.ConfigFolder}."));

        Dictionary<string, (int Id, int? ParentId)> appRows = AppRows(context);
        foreach (AtlasAppManifest manifest in manifests)
        {
            void Error(string message) => items.Add(new(AtlasCheckSeverity.Error, manifest.Code, message));
            void Warning(string message) => items.Add(new(AtlasCheckSeverity.Warning, manifest.Code, message));

            if (!AtlasAppCode.IsValid(manifest.Code))
            {
                Error($"the code in {Path.GetFileName(manifest.SourceFile)} does not match ^[A-Z][A-Z0-9]{{1,19}}$.");
                continue;
            }

            if (!manifest.Enabled)
            {
                Warning("is disabled (Enabled = false); not checked.");
                continue;
            }

            if (!registrations.TryGetValue(manifest.Code, out AtlasModuleRegistration? registration))
            {
                Error($"has a manifest but no registration line: add services.AddAtlasModule<...>(\"{manifest.Code}\").");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(manifest.Service) && !registration.ServiceType.Name.Contains(manifest.Service, StringComparison.OrdinalIgnoreCase))
                Warning($"the manifest Service '{manifest.Service}' does not match the registered class {registration.ServiceType.Name}.");

            // The app row (spec 07, 4.5).
            if (!appRows.TryGetValue(manifest.Code, out var row))
                Error("has no active row in dbo.CP_Atlas_Application.");
            else
            {
                string? parentCode = row.ParentId == null ? null : appRows.FirstOrDefault(x => x.Value.Id == row.ParentId).Key;
                if (!string.Equals(parentCode, manifest.Parent, StringComparison.OrdinalIgnoreCase))
                    Error($"the manifest Parent '{manifest.Parent ?? "null"}' does not agree with ParentID ('{parentCode ?? "null"}').");
            }

            // Tables.
            AtlasTable? table = Table(manifest.Table, "Table", required: true, Error, tables);
            Table(manifest.AuditTable, "AuditTable", required: manifest.AuditMode == AtlasAuditMode.Server, Error, tables);

            CPAtlasApplicationConfig? form = null;
            try
            {
                form = string.IsNullOrWhiteSpace(manifest.Form) ? null : configStore.GetForm(manifest.Code);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                Error(ex.Message);
            }

            if (form?.Section == null)
                Error($"the form '{manifest.Form}' has no Section.");
            else if (table != null)
                CheckForm(services, manifest, form, table, Error);

            CheckDashboard(configStore, tables, manifest, Error);
            if (form != null)
                CheckRoles(context, manifest, form, Warning);
        }

        return items;
    }

    private static void CheckForm(IServiceProvider services, AtlasAppManifest manifest, CPAtlasApplicationConfig form, AtlasTable table, Action<string> error)
    {
        AtlasFormFields fields = AtlasFormFields.From(form.Section!);
        foreach (AtlasFormField field in fields.All.Where(x => !table.HasColumn(x.Field)))
            error($"form field '{field.Name}' names the column '{field.Field}', which is not in {table.FullName}.");

        foreach (string owner in form.OwnerFields ?? [])
            if (!table.HasColumn(owner))
                error($"OwnerFields names '{owner}', which is not a column of {table.FullName}.");

        if (form.SoftDelete != null && !table.HasColumn(form.SoftDelete.Field))
            error($"SoftDelete names '{form.SoftDelete.Field}', which is not a column of {table.FullName}.");

        foreach ((string target, string expression) in form.Computed ?? new Dictionary<string, string>())
        {
            if (!table.HasColumn(target))
                error($"Computed field '{target}' is not a column of {table.FullName}.");
            int open = expression.IndexOf('('), close = expression.LastIndexOf(')');
            string function = open > 0 ? expression[..open].Trim() : "";
            if (open < 0 || close < open || function.ToLowerInvariant() is not ("sum" or "count" or "min" or "max"))
            {
                error($"Computed field '{target}' has the expression '{expression}' (use Sum, Count, Min or Max).");
                continue;
            }

            foreach (string source in expression[(open + 1)..close].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!table.HasColumn(source))
                    error($"Computed field '{target}' uses '{source}', which is not a column of {table.FullName}.");
        }

        foreach (string name in (IEnumerable<string>?)form.Defaults?.Keys ?? [])
            if (fields.Find(name) == null)
                error($"Defaults names '{name}', which is not a field of the form.");

        // Option providers: the platform providers + the providers of the module class.
        HashSet<string> providers;
        using (IServiceScope scope = services.CreateScope())
        {
            IAtlasModule module = scope.ServiceProvider.GetRequiredKeyedService<IAtlasModule>(manifest.Code);
            providers = new(module is ConfigSectionService section
                ? section.OptionProviderNames()
                : scope.ServiceProvider.GetServices<BBH.CP.Atlas.Service.Platform.Options.IAtlasOptionProvider>().Select(x => x.Name),
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (AtlasFormField field in fields.All.Where(x => x.Control.OptionsSource != null && !x.IsGridField))
            if (!providers.Contains(field.Control.OptionsSource!.Provider))
                error($"control '{field.Name}' uses the option provider '{field.Control.OptionsSource.Provider}', which is not registered.");
    }

    private static void CheckDashboard(IAtlasConfigStore configStore, AtlasTableRegistry tables, AtlasAppManifest manifest, Action<string> error)
    {
        CPAtlasDashboard? dashboard;
        try
        {
            dashboard = configStore.GetDashboard(manifest.Code);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            error(ex.Message);
            return;
        }

        foreach (CPAtlasTab tab in dashboard?.Tabs ?? [])
        {
            bool table = string.Equals(tab.Source, "Table", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(tab.ID))
                error("a dashboard tab has no ID.");
            if (table == !string.IsNullOrWhiteSpace(tab.Procedure))
                error($"dashboard tab '{tab.ID}' must have Procedure or Source: Table, not both and not none.");
            if (!table)
                continue;

            string code = string.IsNullOrWhiteSpace(tab.Code) ? manifest.Code : tab.Code;
            AtlasAppManifest? tabManifest = configStore.FindManifest(code);
            if (tabManifest?.Table == null || !tables.TryGet(tabManifest.Table, out AtlasTable tabTable))
            {
                error($"dashboard tab '{tab.ID}' names the app '{code}', which has no registered table.");
                continue;
            }

            try
            {
                AtlasTabQueryBuilder.Build(tabTable, tab, value => value.StartsWith('{') || value == "LANID" ? "x" : value);
            }
            catch (InvalidOperationException ex)
            {
                error(ex.Message);
            }
        }
    }

    /// <summary>Spec 04, 4.7: an unknown role in a rule or in Permissions is a warning.</summary>
    private static void CheckRoles(CPSharepointContext context, AtlasAppManifest manifest, CPAtlasApplicationConfig form, Action<string> warning)
    {
        HashSet<string> known = new(Flags, StringComparer.OrdinalIgnoreCase);
        known.UnionWith(context.Database.SqlQuery<string>($"""
            SELECT e.Name AS Value FROM dbo.CP_Atlas_Entitlement AS e
            INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
            WHERE (a.Code = {manifest.Code} OR a.Code = {manifest.Parent ?? ""}) AND ISNULL(e.IsDeleted, 0) = 0 AND e.Name IS NOT NULL
            """).ToList());

        IEnumerable<string> roles = (form.Rules ?? [])
            .Concat((form.Section?.Controls ?? []).SelectMany(x => (x.Rules ?? []).Concat((x.ChildControls ?? []).SelectMany(c => c.Rules ?? []))))
            .Concat((form.Section?.Buttons ?? []).SelectMany(x => x.Rules ?? []))
            .SelectMany(x => x.Roles ?? [])
            .Concat(form.Permissions?.View ?? []).Concat(form.Permissions?.Create ?? []).Concat(form.Permissions?.Delete ?? [])
            .Concat(form.Permissions?.History ?? []).Concat(form.Permissions?.Edit?.Own ?? []).Concat(form.Permissions?.Edit?.All ?? []);

        foreach (string role in roles.Distinct(StringComparer.OrdinalIgnoreCase).Where(x => !known.Contains(x) && !x.StartsWith('~')))
            warning($"the role '{role}' in the rules or Permissions is not a role of the app.");
    }

    private static AtlasTable? Table(string? name, string setting, bool required, Action<string> error, AtlasTableRegistry tables)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (required)
                error($"the manifest has no {setting}.");
            return null;
        }

        if (tables.TryGet(name, out AtlasTable table))
            return table;

        error($"{setting} '{name}' does not exist, or the API login cannot see it.");
        return null;
    }

    private static Dictionary<string, (int Id, int? ParentId)> AppRows(CPSharepointContext context) =>
        context.CPAtlasApplications.AsNoTracking()
            .Where(x => x.Code != null && (x.IsDeleted == null || x.IsDeleted == false))
            .Select(x => new { x.Code, x.Id, x.ParentId })
            .ToList()
            .GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => (x.First().Id, x.First().ParentId), StringComparer.OrdinalIgnoreCase);
}
