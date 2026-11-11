using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform.Dashboard;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using BBH.CP.Atlas.Service.Platform.Sections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>The severity of a start-up check item. An error disables the app; a warning does not.</summary>
public enum AtlasCheckSeverity { Warning, Error }

/// <summary>
/// One result of the start-up check. App is the app code, or "*" for all apps.
/// </summary>
public sealed record AtlasCheckItem(AtlasCheckSeverity Severity, string App, string Message)
{
    /// <summary>Returns the item as a report line: "SEVERITY App: Message".</summary>
    public override string ToString() => $"{Severity.ToString().ToUpperInvariant()} {App}: {Message}";
}

/// <summary>
/// Checks the configuration of all platform apps at start-up and for --atlas-check.
/// </summary>
/// <remarks>
/// The check covers the manifests and registration lines, the app row, the tables, the form fields against the
/// columns, the option providers, the dashboard tabs and the rule roles (warnings).
/// The table registry must be loaded first.
/// </remarks>
public static class AtlasStartupCheck
{
    private static readonly string[] Flags = [AtlasAppRoles.ReadFlag, AtlasAppRoles.WriteFlag, AtlasAppRoles.AdminFlag];

    /// <summary>
    /// Runs all checks and returns the errors and warnings.
    /// </summary>
    /// <remarks>
    /// An exception in the check of one app is an error of that app only. The other apps are still checked.
    /// </remarks>
    public static IReadOnlyList<AtlasCheckItem> Run(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        List<AtlasCheckItem> items = [];
        IAtlasConfigStore configStore = services.GetRequiredService<IAtlasConfigStore>();
        IReadOnlyList<AtlasAppManifest> manifests;
        try
        {
            manifests = configStore.GetManifests();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return [new(AtlasCheckSeverity.Error, "*", ex.Message)];
        }

        CheckContext check = new(services, configStore, services.GetRequiredService<AtlasTableRegistry>(),
            services.GetRequiredService<CPSharepointContext>(), services.GetService<IHostEnvironment>(),
            services.GetServices<AtlasModuleRegistration>().ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase));

        foreach (string configError in configStore.ConfigErrors)
            items.Add(new(AtlasCheckSeverity.Error, "*", configError));

        foreach (string code in check.Registrations.Keys.Where(code => !manifests.Any(x => x.Code == code)))
            items.Add(new(AtlasCheckSeverity.Error, code, $"has a registration line but no {code}.app.json in {configStore.ConfigFolder}."));

        Dictionary<string, (int Id, int? ParentId)> appRows = AppRows(check.Context);
        CheckSharedAuditTables(manifests, items);
        foreach (AtlasAppManifest manifest in manifests)
        {
            void Error(string message) => items.Add(new(AtlasCheckSeverity.Error, manifest.Code, message));
            void Warning(string message) => items.Add(new(AtlasCheckSeverity.Warning, manifest.Code, message));

            // An exception in the check of one app (for example, in its module constructor) is an error of that app only.
            // The other apps are still checked and stay available.
            try
            {
                CheckApp(check, appRows, manifest, Error, Warning);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Error("the check of this app stopped with an error: " + ex.Message);
            }
        }

        CheckModel(services, items);
        return items;
    }

    /// <summary>The services and the shared lookups that the checks of one app read.</summary>
    private sealed record CheckContext(
        IServiceProvider Services, IAtlasConfigStore ConfigStore, AtlasTableRegistry Tables, CPSharepointContext Context,
        IHostEnvironment? Environment, Dictionary<string, AtlasModuleRegistration> Registrations);

    private static void CheckApp(CheckContext check, Dictionary<string, (int Id, int? ParentId)> appRows, AtlasAppManifest manifest,
        Action<string> error, Action<string> warning)
    {
        if (!AtlasAppCode.IsValid(manifest.Code))
        {
            error($"the code in {Path.GetFileName(manifest.SourceFile)} does not match ^[A-Z][A-Z0-9]{{1,19}}$.");
            return;
        }

        if (!CheckSettings(check, manifest, error, warning) || !CheckRegistration(check, manifest, error, warning))
            return;

        CheckAppRow(appRows, manifest, error);

        AtlasTable? table = Table(manifest.Table, "Table", required: true, error, check.Tables);
        Table(manifest.AuditTable, "AuditTable", required: manifest.AuditMode == AtlasAuditMode.Server, error, check.Tables);

        CPAtlasApplicationConfig? form = null;
        try
        {
            form = string.IsNullOrWhiteSpace(manifest.Form) ? null : check.ConfigStore.GetForm(manifest.Code);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            error(ex.Message);
        }

        if (form?.Section == null)
            error($"the form '{manifest.Form}' has no Section.");
        else if (table != null)
        {
            CheckForm(check.Services, manifest, form, table, error);
            CheckDefaults(form, table, warning);
        }

        CheckDashboard(check.ConfigStore, check.Tables, manifest, error);
        if (form != null)
            CheckRoles(check.Context, manifest, form, warning);
        if (form?.Section != null)
        {
            CheckControlTypes(form, warning);
            CheckGridCodes(manifest, form, warning);
        }

        // The record times and the audit times have their own settings.
        if (manifest.SystemTime == AtlasAuditTime.Local && manifest.AuditTime == AtlasAuditTime.Utc)
            warning("SystemTime is Local but AuditTime is Utc: the History shows times that differ from ModifiedDate by the UTC offset. Set \"AuditTime\": \"Local\" too, unless that is intended.");

        // Typing errors in property names are warnings. Typing errors in names, tokens, patterns and actions are errors.
        CheckContract(check.ConfigStore, manifest, form, error, warning);
    }

    /// <summary>Checks the settings of the manifest. Returns false when the app is disabled and is not checked further.</summary>
    private static bool CheckSettings(CheckContext check, AtlasAppManifest manifest, Action<string> error, Action<string> warning)
    {
        foreach (string settingError in manifest.SettingErrors)
            error(settingError);

        if (!manifest.Enabled)
        {
            warning("is disabled (Enabled = false); not checked.");
            return false;
        }

        // A current app that the platform takes over in place has local times in its rows.
        // New UTC times in the same rows give wrong data, and nothing reports it.
        if (AtlasAppCode.IsLegacy(manifest.Code) && manifest.SystemTime == null)
            warning("is a current app taken over by the platform but the manifest has no SystemTime: new saves write UTC while the old rows have local time. Set \"SystemTime\": \"Local\" (or \"Utc\" after a data change).");

        if (manifest.AccessMode == AtlasAccessMode.LogOnly && check.Environment?.IsDevelopment() == false)
            warning($"AccessMode is LogOnly in {check.Environment.EnvironmentName}: denied View, Create, Edit and History are only logged. Set Enforce after the log review.");
        return true;
    }

    /// <summary>Checks the AddAtlasModule line of the app. Returns false when there is no line; the app is then not checked further.</summary>
    private static bool CheckRegistration(CheckContext check, AtlasAppManifest manifest, Action<string> error, Action<string> warning)
    {
        if (!check.Registrations.TryGetValue(manifest.Code, out AtlasModuleRegistration? registration))
        {
            error($"has a manifest but no registration line: add services.AddAtlasModule<...>(\"{manifest.Code}\").");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(manifest.Service) && !registration.ServiceType.Name.Contains(manifest.Service, StringComparison.OrdinalIgnoreCase))
            warning($"the manifest Service '{manifest.Service}' does not match the registered class {registration.ServiceType.Name}.");
        return true;
    }

    /// <summary>Checks that the app has an active row in dbo.CP_Atlas_Application with the parent of the manifest.</summary>
    private static void CheckAppRow(Dictionary<string, (int Id, int? ParentId)> appRows, AtlasAppManifest manifest, Action<string> error)
    {
        if (!appRows.TryGetValue(manifest.Code, out var row))
        {
            error("has no active row in dbo.CP_Atlas_Application.");
            return;
        }

        string? parentCode = row.ParentId == null ? null : appRows.FirstOrDefault(x => x.Value.Id == row.ParentId).Key;
        if (!string.Equals(parentCode, manifest.Parent, StringComparison.OrdinalIgnoreCase))
            error($"the manifest Parent '{manifest.Parent ?? "null"}' does not agree with ParentID ('{parentCode ?? "null"}').");
    }

    private static void CheckForm(IServiceProvider services, AtlasAppManifest manifest, CPAtlasApplicationConfig form, AtlasTable table, Action<string> error)
    {
        CheckPermissions(form, error);

        AtlasFormFields fields = AtlasFormFields.From(form.Section!);
        foreach (AtlasFormField field in fields.All.Where(x => !table.HasColumn(x.Field)))
            error($"form field '{field.Name}' names the column '{field.Field}', which is not in {table.FullName}.");

        foreach (string owner in form.OwnerFields ?? [])
            if (!table.HasColumn(owner))
                error($"OwnerFields names '{owner}', which is not a column of {table.FullName}.");

        CheckSoftDelete(form, table, error);
        CheckComputed(form, table, error);

        foreach (string name in (IEnumerable<string>?)form.Defaults?.Keys ?? [])
            if (fields.Find(name) == null)
                error($"Defaults names '{name}', which is not a field of the form.");

        CheckProviders(services, manifest, fields, error);
    }

    /// <summary>
    /// Reports a missing Permissions block, and a missing Create, Edit or Delete key.
    /// </summary>
    /// <remarks>Without the block, each role of the app can do every operation.</remarks>
    private static void CheckPermissions(CPAtlasApplicationConfig form, Action<string> error)
    {
        if (form.Permissions == null)
        {
            error("the form has no Permissions block (View, Create, Edit, Delete, History); a missing block allows all operations.");
            return;
        }

        // A missing key allows that operation for each role of the app (also @Read).
        if (form.Permissions.Create == null)
            error("the Permissions block has no Create key; a missing key allows each role of the app to create records.");
        if (form.Permissions.Edit == null)
            error("the Permissions block has no Edit key; a missing key allows each role of the app to edit all records.");
        if (form.Permissions.Delete == null)
            error("the Permissions block has no Delete key; a missing key allows each role of the app to delete records.");
    }

    private static void CheckSoftDelete(CPAtlasApplicationConfig form, AtlasTable table, Action<string> error)
    {
        if (form.SoftDelete != null && !table.HasColumn(form.SoftDelete.Field))
            error($"SoftDelete names '{form.SoftDelete.Field}', which is not a column of {table.FullName}.");
        // The reads, the dashboard filter and the system columns use only IsDeleted (bit).
        else if (form.SoftDelete != null && !string.Equals(form.SoftDelete.Field, AtlasTable.SoftDeleteColumnName, StringComparison.OrdinalIgnoreCase))
            error($"SoftDelete names '{form.SoftDelete.Field}'; the platform reads only {AtlasTable.SoftDeleteColumnName} (a bit column), so a deleted record would stay visible.");
        else if (form.SoftDelete != null && !table.HasSoftDelete)
            error($"SoftDelete: the column {AtlasTable.SoftDeleteColumnName} of {table.FullName} must be a bit column (it is {table.Find(AtlasTable.SoftDeleteColumnName)!.SqlType}).");
    }

    private static void CheckComputed(CPAtlasApplicationConfig form, AtlasTable table, Action<string> error)
    {
        foreach ((string target, string expression) in form.Computed ?? new Dictionary<string, string>())
        {
            if (!table.HasColumn(target))
                error($"Computed field '{target}' is not a column of {table.FullName}.");
            // The check uses the same function table as the save.
            if (!AtlasComputedFields.TryParse(expression, out _, out IReadOnlyList<string> sources))
            {
                error($"Computed field '{target}' has the expression '{expression}' (use {string.Join(", ", AtlasComputedFields.Functions)}).");
                continue;
            }

            foreach (string source in sources)
                if (!table.HasColumn(source))
                    error($"Computed field '{target}' uses '{source}', which is not a column of {table.FullName}.");
                else if (!IsNumeric(table.Find(source)!.ClrType))
                    // The save converts each source to a number. A text or date source gives a 500 on each save.
                    error($"Computed field '{target}' uses '{source}', which is not a number column ({table.Find(source)!.SqlType}).");
            if (table.HasColumn(target) && !IsNumeric(table.Find(target)!.ClrType))
                error($"Computed field '{target}' is not a number column ({table.Find(target)!.SqlType}).");
        }
    }

    /// <summary>Checks that each option provider of the form is a platform provider or a provider of the module class.</summary>
    private static void CheckProviders(IServiceProvider services, AtlasAppManifest manifest, AtlasFormFields fields, Action<string> error)
    {
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

    private static bool IsNumeric(Type? type) =>
        (Nullable.GetUnderlyingType(type ?? typeof(object)) ?? type) is { } t
        && (t == typeof(byte) || t == typeof(short) || t == typeof(int) || t == typeof(long)
            || t == typeof(decimal) || t == typeof(double) || t == typeof(float));

    /// <summary>
    /// Warns about each app that shares an AuditTable but does not limit its History to its own RefType values.
    /// </summary>
    private static void CheckSharedAuditTables(IReadOnlyList<AtlasAppManifest> manifests, List<AtlasCheckItem> items)
    {
        foreach (IGrouping<string, AtlasAppManifest> group in manifests
            .Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.AuditTable))
            .GroupBy(x => x.AuditTable!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1))
        {
            foreach (AtlasAppManifest manifest in group.Where(x => x.AuditReadRefTypes() == null))
                items.Add(new(AtlasCheckSeverity.Warning, manifest.Code,
                    $"the AuditTable {group.Key} is shared with {string.Join(", ", group.Where(x => x != manifest).Select(x => x.Code))}, but the manifest has no AuditRefTypes and no Parent, so History shows the entries of the other apps with the same ID. Set \"AuditRefTypes\": [\"{manifest.Code}\"]."));
        }
    }

    /// <summary>
    /// Warns about each NOT NULL value-type column with a database default that no field, Defaults entry or computed value sets.
    /// </summary>
    /// <remarks>
    /// The platform does not use the database default of such a column (for example bit NOT NULL DEFAULT 1).
    /// A false or 0 from the user would look like "not set". If nothing sets the column, each create fails.
    /// </remarks>
    private static void CheckDefaults(CPAtlasApplicationConfig form, AtlasTable table, Action<string> warning)
    {
        AtlasFormFields fields = AtlasFormFields.From(form.Section!);
        HashSet<string> set = new(fields.All.Select(x => x.Field), StringComparer.OrdinalIgnoreCase);
        set.UnionWith(ConfigSectionService.SystemColumns);
        set.UnionWith((IEnumerable<string>?)form.Computed?.Keys ?? []);
        foreach (string name in (IEnumerable<string>?)form.Defaults?.Keys ?? [])
            if (fields.Find(name) is { } field)
                set.Add(field.Field);

        foreach (AtlasColumn column in table.Columns.Where(x => x.HasDefault && !x.IsDatabaseGenerated && !x.UsesDatabaseDefault && !set.Contains(x.Name)))
            warning($"the column '{column.Name}' of {table.FullName} is NOT NULL {column.SqlType} with a default; the platform does not use the default for this type, so a create with no value fails. Add a form field or a Defaults entry, or make the column nullable.");
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
            else if (!table)
                // The platform dashboard does not run tab procedures, so the tab would show no rows.
                error($"dashboard tab '{tab.ID}' has a Procedure; the platform dashboard runs only Source: Table tabs (spec 03, 4.7). Use Source: Table.");
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

    /// <summary>Warns about each role in a rule or in Permissions that is not a role of the app.</summary>
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

        if (!AtlasTable.IsValidFullName(name))
        {
            error($"{setting} '{name}' must have the form Schema.Name (for example TTDV2.General).");
            return null;
        }

        if (!tables.TryGet(name, out AtlasTable table))
        {
            error($"{setting} '{name}' does not exist, or the API login cannot see it.");
            return null;
        }

        // These errors stop the model build of all platform tables on the first request, so report them here.
        if (table.Key is not { } key)
            error($"{setting} '{table.FullName}' has no {AtlasTable.KeyColumnName} column.");
        else if (!key.IsIdentity || key.ClrType != typeof(int))
            error($"{setting} '{table.FullName}': the {AtlasTable.KeyColumnName} column must be an int IDENTITY column (it is {key.SqlType}{(key.IsIdentity ? "" : ", not IDENTITY")}).");

        foreach (AtlasColumn column in table.Columns.Where(x => x.ClrType == null))
            error($"{setting} '{table.FullName}': the column '{column.Name}' has the type '{column.SqlType}', which the platform does not support.");

        return table;
    }

    private static void CheckContract(IAtlasConfigStore configStore, AtlasAppManifest manifest, CPAtlasApplicationConfig? form, Action<string> error, Action<string> warning)
    {
        if (manifest.SourceFile != null)
            AtlasConfigLint.CheckUnknownProperties(manifest.SourceFile, typeof(AtlasAppManifest), warning);
        if (!string.IsNullOrWhiteSpace(manifest.Form))
            AtlasConfigLint.CheckUnknownProperties(Path.Combine(configStore.ConfigFolder, manifest.Form), typeof(CPAtlasApplicationConfig), warning);
        if (!string.IsNullOrWhiteSpace(manifest.Dashboard))
            AtlasConfigLint.CheckUnknownProperties(Path.Combine(configStore.ConfigFolder, manifest.Dashboard), typeof(CPAtlasDashboard), warning);
        if (form != null)
            AtlasConfigLint.CheckForm(manifest, form, error, warning);
    }

    /// <summary>The control types that the platform client renders (see atlas-renderer-registry.ts).</summary>
    private static readonly HashSet<string> ClientTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "section", "text", "currency", "percent", "select", "ngselect", "textarea", "datetimepicker", "date",
        "checkbox", "check", "option", "user", "grid"
    };

    /// <summary>
    /// Warns about each grid column whose cellRendererParams.code is not the app code.
    /// </summary>
    /// <remarks>The JSON holds this code as text, so a copied form keeps the code of the old app.</remarks>
    private static void CheckGridCodes(AtlasAppManifest manifest, CPAtlasApplicationConfig form, Action<string> warning)
    {
        IEnumerable<CPApplicationControl> controls = (form.Section!.Controls ?? [])
            .SelectMany(x => new[] { x }.Concat(x.ChildControls ?? []));
        foreach (CPApplicationControl control in controls.Where(x => x.Grid?.Columns != null))
            foreach (CPGridColumn column in control.Grid!.Columns!.Where(x => !string.IsNullOrWhiteSpace(x.cellRendererParams?.code)
                && !string.Equals(x.cellRendererParams!.code, manifest.Code, StringComparison.OrdinalIgnoreCase)))
                warning($"the grid '{control.Name}' column '{column.field}' has cellRendererParams.code '{column.cellRendererParams!.code}', not '{manifest.Code}'.");
    }

    /// <summary>
    /// Warns about each control type that the platform client does not render.
    /// </summary>
    /// <remarks>Such a control shows only a note, and the user cannot edit it.</remarks>
    private static void CheckControlTypes(CPAtlasApplicationConfig form, Action<string> warning)
    {
        IEnumerable<CPApplicationControl> controls = (form.Section!.Controls ?? [])
            .SelectMany(x => new[] { x }.Concat(x.ChildControls ?? []));
        foreach (CPApplicationControl control in controls.Where(x => !ClientTypes.Contains(x.Type ?? "")))
            warning($"the control '{control.Name}' has the type '{control.Type}', which the platform client does not render (unless the app adds a renderer with provideAtlasRenderers).");
    }

    /// <summary>Builds the EF Core model of all registered tables one time, so a model error shows at start-up.</summary>
    private static void CheckModel(IServiceProvider services, List<AtlasCheckItem> items)
    {
        // Build the model also when an app has errors. The model leaves out a table that it cannot map.
        // The app of that table already has a table error. Thus a model error here is a new error for all apps.
        try
        {
            _ = services.GetRequiredService<AtlasPlatformContext>().Model;
        }
        catch (InvalidOperationException ex)
        {
            items.Add(new(AtlasCheckSeverity.Error, "*", "the data model of the platform tables cannot be built: " + ex.Message));
        }
    }

    private static Dictionary<string, (int Id, int? ParentId)> AppRows(CPSharepointContext context) =>
        context.CPAtlasApplications.AsNoTracking()
            .Where(x => x.Code != null && (x.IsDeleted == null || x.IsDeleted == false))
            .Select(x => new { x.Code, x.Id, x.ParentId })
            .ToList()
            .GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => (x.First().Id, x.First().ParentId), StringComparer.OrdinalIgnoreCase);
}
