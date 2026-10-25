using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Rules;
using BBH.CP.Atlas.Service.Platform.Security;

namespace BBH.CP.Atlas.Service.Platform.Dashboard;

/// <summary>
/// The dashboard of a platform app (spec 03, 4.1-4.7). Order: the View access check; the tab rules before any query
/// (a hidden tab gets no rows); the rows of each visible <c>Source: Table</c> tab; menus and the add button from
/// <c>Permissions</c> when the JSON has no rules.
/// </summary>
public interface IAtlasDashboardService
{
    CPAtlasDashboard Load(string appCode);
}

public sealed class AtlasDashboardService(
    IAtlasConfigStore configStore,
    CPSharepointContext context,
    AtlasTableRegistry tables,
    IAtlasUserContext user,
    IAtlasUserDirectory directory,
    IAtlasAccessService access,
    IAtlasRuleEngine rules) : IAtlasDashboardService
{
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

            if (!tab.Visible)
            {
                tab.KeyValues = []; // spec 03, 4.2: no rows for a hidden tab
                continue;
            }

            if (string.Equals(tab.Source, "Table", StringComparison.OrdinalIgnoreCase))
                tab.KeyValues = LoadRows(manifest, dashboard, tab);

            tab.Menus = Menus(manifest, form, tab, ruleContext);
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

    private List<Dictionary<string, object>> LoadRows(AtlasAppManifest manifest, CPAtlasDashboard dashboard, CPAtlasTab tab)
    {
        string tabCode = string.IsNullOrWhiteSpace(tab.Code) ? manifest.Code : tab.Code;
        AtlasAppManifest tabManifest = string.Equals(tabCode, manifest.Code, StringComparison.OrdinalIgnoreCase)
            ? manifest
            : configStore.FindManifest(tabCode) ?? throw new InvalidOperationException($"Dashboard tab '{tab.ID}' names the app '{tabCode}', which has no manifest.");
        AtlasTable table = tables.Get(tabManifest.Table ?? throw new InvalidOperationException($"App '{tabCode}' has no Table."));

        AtlasTabQuery query = AtlasTabQueryBuilder.Build(table, tab, value => Token(value, manifest.Code, tabCode));
        HashSet<string> userColumns = new(
            tab.Columns!.Where(x => string.Equals(x.Type, "user", StringComparison.OrdinalIgnoreCase)).Select(x => x.Name),
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
                    DateTime or bool or byte[] => AtlasValueConverter.ToWire(value, column)!,
                    _ => value
                };
            }

            return row;
        }, [.. query.Parameters]);

        // Type "user": the display name from the user cache (after the read: one open reader for each connection).
        foreach (Dictionary<string, object> row in rows)
            foreach (string name in userColumns)
                if (row.TryGetValue(name, out object? value) && value is string lanId)
                    row[name] = directory.DisplayName(lanId);

        return rows;
    }

    /// <summary>Menus from the JSON rules, or (no Menus in the JSON) from Permissions (spec 03, 4.4).</summary>
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
