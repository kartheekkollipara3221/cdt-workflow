using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Sections;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Rules;
using BBH.CP.Atlas.Service.Platform.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Service.Platform.Dashboard;

/// <summary>
/// Loads the dashboard of a platform app for the current user.
/// </summary>
/// <remarks>
/// The service does these steps in this order:
/// 1. It checks the View access.
/// 2. It applies the tab rules before any query, so a hidden tab gets no rows.
/// 3. It reads the rows of each visible <c>Source: Table</c> tab.
/// 4. It sets the menus and the add button from <c>Permissions</c> when the JSON has no rules.
/// </remarks>
public sealed class AtlasDashboardService(
    IAtlasConfigStore configStore,
    CPSharepointContext context,
    AtlasTableRegistry tables,
    IAtlasUserContext user,
    IAtlasUserDirectory directory,
    AtlasAccessService access,
    AtlasRuleEngine rules,
    ILogger<AtlasDashboardService>? logger = null)
{
    /// <summary>Returns the dashboard of an app, with the tabs, rows, menus and buttons for the current user.</summary>
    /// <exception cref="AtlasProblemException">
    /// 404 when the app is not registered, is disabled or has no dashboard. 403 when the user cannot view the app.
    /// </exception>
    public CPAtlasDashboard Load(string appCode)
    {
        AtlasAppManifest manifest = configStore.FindManifest(appCode) is { Enabled: true } found
            ? found
            : throw AtlasProblemException.NotFound($"App '{appCode}' is not registered.");
        CPAtlasDashboard dashboard = configStore.GetDashboard(manifest.Code)
            ?? throw AtlasProblemException.NotFound($"App '{manifest.Code}' has no dashboard.");
        CPAtlasApplicationConfig? form = string.IsNullOrWhiteSpace(manifest.Form) ? null : configStore.GetForm(manifest.Code);

        access.Demand(manifest, form, AtlasOperation.View, null, 0);

        AtlasRuleContext ruleContext = new(user.RolesFor(manifest.Code).Tokens, [], IsOwner: false);
        foreach (CPAtlasTab tab in dashboard.Tabs ?? [])
        {
            AtlasRuleResult tabRule = rules.Evaluate(tab.Rules, ruleContext, manifest.RuleMode, AtlasRuleDefault.Hidden);
            if (tabRule.Matched)
                tab.Visible = tab.Visible && tabRule.Visible;

            // The rows of another app need that app to be enabled and the user to pass its View check.
            (AtlasAppManifest tabManifest, CPAtlasApplicationConfig? tabForm) = (manifest, form);
            if (tab.Visible && TabApp(manifest, tab) is { } foreign)
            {
                (tabManifest, tabForm) = foreign;
                tab.Visible = CanView(tabManifest, tabForm);
            }

            if (!tab.Visible)
            {
                tab.KeyValues = []; // A hidden tab gets no rows.
                continue;
            }

            if (string.Equals(tab.Source, "Table", StringComparison.OrdinalIgnoreCase))
                tab.KeyValues = LoadRows(manifest, tabManifest, tab);

            tab.Menus = Menus(tabManifest, tabForm, tab, ruleContext);
        }

        foreach (CPButton button in dashboard.Buttons ?? [])
        {
            if (button.Rules is { Count: > 0 })
            {
                AtlasRuleResult result = rules.Evaluate(button.Rules, ruleContext, manifest.RuleMode, AtlasRuleDefault.Hidden);
                button.Visible = button.Visible && result.Visible;
                button.Disabled = button.Disabled || result.Disabled;
            }
            else if (form?.Permissions != null)
            {
                bool canCreate = access.Evaluate(manifest, form, AtlasOperation.Create, null).Allowed;
                button.Visible = button.Visible && canCreate;
                button.Disabled = button.Disabled || !canCreate;
            }
        }

        dashboard.AppCode = manifest.Code;
        return dashboard;
    }

    /// <summary>Returns the app of the rows of a tab, or null when it is the dashboard app.</summary>
    private (AtlasAppManifest Manifest, CPAtlasApplicationConfig? Form)? TabApp(AtlasAppManifest manifest, CPAtlasTab tab)
    {
        if (string.IsNullOrWhiteSpace(tab.Code) || string.Equals(tab.Code, manifest.Code, StringComparison.OrdinalIgnoreCase))
            return null;

        AtlasAppManifest tabManifest = configStore.FindManifest(tab.Code)
            ?? throw AtlasProblemException.Config($"Dashboard tab '{tab.ID}' names the app '{tab.Code}', which has no manifest.");
        return (tabManifest, tabManifest.Enabled && !string.IsNullOrWhiteSpace(tabManifest.Form) ? configStore.GetForm(tabManifest.Code) : null);
    }

    /// <summary>Returns true when the user can see the rows of another app in a tab.</summary>
    /// <remarks>
    /// A disabled app (the rollback switch) shows nothing.
    /// A refused View hides the tab in Enforce mode. In LogOnly mode, the access check only logs the refusal.
    /// </remarks>
    private bool CanView(AtlasAppManifest tabManifest, CPAtlasApplicationConfig? tabForm)
    {
        if (!tabManifest.Enabled)
            return false;
        try
        {
            access.Demand(tabManifest, tabForm, AtlasOperation.View, null, 0);
            return true;
        }
        catch (AtlasProblemException ex) when (ex.StatusCode == 403)
        {
            return false;
        }
    }

    private List<Dictionary<string, object>> LoadRows(AtlasAppManifest manifest, AtlasAppManifest tabManifest, CPAtlasTab tab)
    {
        string tabCode = tabManifest.Code;
        AtlasTable table = tables.Get(tabManifest.Table ?? throw AtlasProblemException.Config($"App '{tabCode}' has no Table."));

        AtlasTabQuery query = AtlasTabQueryBuilder.Build(table, tab, value => Token(value, manifest.Code, tabCode));
        HashSet<string> userColumns = new(
            tab.Columns!.Where(x => string.Equals(x.Type, "user", StringComparison.OrdinalIgnoreCase)).Select(x => x.Name),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> richTextColumns = new(
            tab.Columns!.Where(x => string.Equals(x.Type, AtlasRichText.ControlType, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name),
            StringComparer.OrdinalIgnoreCase);

        List<Dictionary<string, object>> rows = AtlasSql.Query(context, query.Sql, reader =>
        {
            Dictionary<string, object> row = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < query.Columns.Count; i++)
            {
                AtlasColumn column = query.Columns[i];
                object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[column.Name] = value switch
                {
                    null => null!,
                    DateTime or bool or byte[] => AtlasValueConverter.ToWire(value, column, tabManifest.SystemTimeIsUtc)!,
                    // A rich text column shows as plain text in the grid.
                    string html when richTextColumns.Contains(column.Name) => AtlasRichText.ToPlainText(html) ?? "",
                    _ => value
                };
            }

            return row;
        }, [.. query.Parameters]);

        // Type "user": the display name from the user cache (after the read: one open reader for each connection).
        if (userColumns.Count > 0)
        {
            // One query gets the names for all rows. This prevents one query for each LAN ID.
            IReadOnlyDictionary<string, string> names = directory.DisplayNames(
                rows.SelectMany(row => userColumns.Select(name => row.GetValueOrDefault(name) as string)).OfType<string>());
            foreach (Dictionary<string, object> row in rows)
                foreach (string name in userColumns)
                    if (row.TryGetValue(name, out object? value) && value is string lanId)
                        row[name] = names.GetValueOrDefault(lanId) ?? lanId;
        }

        if (tab.MaxRows is { } maxRows && rows.Count >= maxRows)
            AtlasLog.TabRowLimit(logger ?? (ILogger)NullLogger.Instance, tabCode, tab.ID ?? "", maxRows);

        return rows;
    }

    /// <summary>
    /// Returns the menus of a tab from the JSON rules. When the JSON has no Menus, the menus come from Permissions.
    /// </summary>
    private List<CPMenu>? Menus(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, CPAtlasTab tab, AtlasRuleContext ruleContext)
    {
        if (tab.Menus is { Count: > 0 } menus)
        {
            foreach (CPMenu menu in menus.Where(x => x.Rule != null))
            {
                AtlasRuleResult result = rules.Evaluate([menu.Rule!], ruleContext, AtlasRuleMode.FirstMatch, AtlasRuleDefault.Keep);
                if (result.Matched)
                    menu.Visible = result.Visible;
            }

            return menus;
        }

        if (form?.Permissions == null)
            return tab.Menus;

        // Edit: the user can edit some records (own or all); the server checks each record on save.
        return
        [
            new CPMenu { Name = "Edit", Label = "Edit", Visible = access.Evaluate(manifest, form, AtlasOperation.Edit, null).Allowed },
            new CPMenu { Name = "History", Label = "History", Visible = access.Evaluate(manifest, form, AtlasOperation.History, null).Allowed },
            new CPMenu { Name = "Delete", Label = "Delete", Visible = access.Evaluate(manifest, form, AtlasOperation.Delete, null).Allowed }
        ];
    }

    private string Token(string value, string appCode, string tabCode) => value.Trim() switch
    {
        var x when x.Equals("{LanId}", StringComparison.OrdinalIgnoreCase) || x.Equals("LANID", StringComparison.OrdinalIgnoreCase) => user.LanId,
        var x when x.Equals("{AppCode}", StringComparison.OrdinalIgnoreCase) => appCode,
        var x when x.Equals("{TabCode}", StringComparison.OrdinalIgnoreCase) => tabCode,
        _ => value
    };
}
