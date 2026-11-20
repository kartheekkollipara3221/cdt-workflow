using System.Text;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Dashboard;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.Caching.Memory;

namespace BBH.CP.Atlas.Service.Platform.Options;

/// <summary>
/// The parameters of a Table options source, after the check against the table metadata.
/// </summary>
/// <param name="Table">The lookup table.</param>
/// <param name="Value">The column that gives the option value.</param>
/// <param name="Label">The label text with {Column} tokens.</param>
/// <param name="LabelColumns">The columns that the label tokens name.</param>
/// <param name="Data">The columns that go into the Data of each option.</param>
/// <param name="OrderBy">The sort column, or null for the order of the value.</param>
/// <param name="Descending">True when the sort is descending.</param>
/// <param name="UserNameColumns">The columns that hold display names (for the name report).</param>
public sealed record AtlasTableOptionSpec(
    AtlasTable Table, AtlasColumn Value, string Label, IReadOnlyList<AtlasColumn> LabelColumns, IReadOnlyList<AtlasColumn> Data,
    AtlasColumn? OrderBy, bool Descending, IReadOnlyList<AtlasColumn> UserNameColumns);

/// <summary>
/// Supplies the options of a control from a lookup table that the form JSON names.
/// </summary>
/// <remarks>
/// <para>Parameters: Table, Value, Label ("{Column} - {Column}"), Data (columns, separated by commas), OrderBy
/// ("Column" or "Column DESC") and UserNameColumns (columns with display names, for the name report of --atlas-check).</para>
/// <para>The table must be in the LookupTables of the app manifest. The names go into the SQL text only after the
/// check against the table metadata, in brackets. The platform never writes to a lookup table.</para>
/// <para>A table with an IsDeleted column gives only the rows that are not deleted. The list of one source is
/// cached for 60 seconds.</para>
/// </remarks>
public sealed partial class AtlasTableOptionProvider(
    CPSharepointContext context, AtlasTableRegistry tables, IAtlasConfigStore configStore, IMemoryCache cache) : IAtlasOptionProvider
{
    /// <summary>The provider name in OptionsSource.Provider.</summary>
    public const string ProviderName = "Table";

    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(60);

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <inheritdoc/>
    /// <exception cref="AtlasProblemException">The parameters do not fit the table metadata (a configuration problem).</exception>
    public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        (AtlasTableOptionSpec? spec, List<string> errors) = Parse(request);
        if (spec == null)
            throw AtlasProblemException.Config($"The options of '{request.Control.Name}' (app {request.AppCode}): {string.Join(" ", errors)}");

        string key = "atlas:options:table:" + string.Join("|", spec.Table.FullName, spec.Value.Name, spec.Label,
            string.Join(",", spec.Data.Select(x => x.Name)), spec.OrderBy?.Name, spec.Descending);
        if (cache.TryGetValue(key, out IReadOnlyList<CPKeyValue>? cached) && cached != null)
            return cached;

        IReadOnlyList<CPKeyValue> options = Read(spec);
        return cache.Set(key, options, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTime, Size = 1 });
    }

    /// <inheritdoc/>
    public IEnumerable<string> Check(AtlasOptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Parse(request).Errors;
    }

    /// <summary>
    /// Returns the rows of the lookup table with the text of each UserNameColumns column, for the name report.
    /// </summary>
    /// <returns>The value of each row and the text of each name column; an empty list when the source has no name columns.</returns>
    /// <exception cref="AtlasProblemException">The parameters do not fit the table metadata.</exception>
    public IReadOnlyList<(string Value, IReadOnlyDictionary<string, string?> Names)> UserNameRows(AtlasOptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        (AtlasTableOptionSpec? spec, List<string> errors) = Parse(request);
        if (spec == null)
            throw AtlasProblemException.Config($"The options of '{request.Control.Name}' (app {request.AppCode}): {string.Join(" ", errors)}");
        if (spec.UserNameColumns.Count == 0)
            return [];

        string sql = Select(spec.Table, [spec.Value, .. spec.UserNameColumns], spec.OrderBy ?? spec.Value, spec.Descending);
        return AtlasSql.Query(context, sql, reader =>
        {
            Dictionary<string, string?> names = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < spec.UserNameColumns.Count; i++)
                names[spec.UserNameColumns[i].Name] = AtlasValueConverter.ToWire(reader.GetValue(i + 1), spec.UserNameColumns[i]);
            return (Value: AtlasValueConverter.ToWire(reader.GetValue(0), spec.Value) ?? "", Names: (IReadOnlyDictionary<string, string?>)names);
        });
    }

    /// <summary>Reads and checks the parameters. Returns the spec, or null and the errors.</summary>
    private (AtlasTableOptionSpec? Spec, List<string> Errors) Parse(AtlasOptionRequest request)
    {
        List<string> errors = [];
        string? tableName = AtlasProviderParameters.Text(request, "Table")?.Trim();
        if (string.IsNullOrEmpty(tableName))
            return (null, ["the Table provider needs the parameter Table."]);

        AtlasAppManifest? manifest = configStore.FindManifest(request.AppCode);
        if (manifest?.LookupTables?.Contains(tableName, StringComparer.OrdinalIgnoreCase) != true)
            return (null, [$"the table '{tableName}' is not in the LookupTables of the manifest."]);
        if (!tables.TryGet(tableName, out AtlasTable table))
            return (null, [$"the lookup table '{tableName}' does not exist, or the API login cannot see it."]);

        AtlasColumn? Column(string parameter, string? name, bool required)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (required)
                    errors.Add($"the Table provider needs the parameter {parameter}.");
                return null;
            }

            AtlasColumn? column = table.Find(name.Trim());
            if (column == null)
                errors.Add($"{parameter} names '{name.Trim()}', which is not a column of {table.FullName}.");
            return column;
        }

        List<AtlasColumn> Columns(string parameter) =>
            [.. (AtlasProviderParameters.Text(request, parameter) ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Column(parameter, x, required: true))
                .OfType<AtlasColumn>()];

        AtlasColumn? value = Column("Value", AtlasProviderParameters.Text(request, "Value"), required: true);
        string label = AtlasProviderParameters.Text(request, "Label") is { Length: > 0 } text ? text : "{" + value?.Name + "}";
        List<AtlasColumn> labelColumns = [.. Token().Matches(label).Select(x => Column("Label", x.Groups[1].Value, required: true)).OfType<AtlasColumn>()];
        List<AtlasColumn> data = Columns("Data");
        List<AtlasColumn> userNames = Columns("UserNameColumns");

        string[] order = (AtlasProviderParameters.Text(request, "OrderBy") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool descending = order.Length == 2 && string.Equals(order[1], "DESC", StringComparison.OrdinalIgnoreCase);
        if (order.Length > 2 || order.Length == 2 && !descending && !string.Equals(order[1], "ASC", StringComparison.OrdinalIgnoreCase))
            errors.Add("OrderBy must be 'Column', 'Column ASC' or 'Column DESC'.");
        AtlasColumn? orderBy = order.Length > 0 ? Column("OrderBy", order[0], required: false) : null;

        if (errors.Count > 0 || value == null)
            return (null, errors);
        return (new AtlasTableOptionSpec(table, value, label, labelColumns, data, orderBy, descending, userNames), errors);
    }

    private List<CPKeyValue> Read(AtlasTableOptionSpec spec)
    {
        List<AtlasColumn> columns = [spec.Value, .. spec.LabelColumns, .. spec.Data];
        List<AtlasColumn> distinct = [.. columns.DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
        string sql = Select(spec.Table, distinct, spec.OrderBy ?? spec.Value, spec.Descending);

        return [.. AtlasSql.Query(context, sql, reader =>
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < distinct.Count; i++)
                values[distinct[i].Name] = AtlasValueConverter.ToWire(reader.GetValue(i), distinct[i]) ?? "";

            string label = Token().Replace(spec.Label, m => values.GetValueOrDefault(m.Groups[1].Value.Trim()) ?? "").Trim();
            return new CPKeyValue
            {
                Value = values[spec.Value.Name],
                Label = label,
                Data = spec.Data.ToDictionary(x => x.Name, x => values[x.Name], StringComparer.OrdinalIgnoreCase)
            };
        }).Where(x => !string.IsNullOrEmpty(x.Value))];
    }

    private static string Select(AtlasTable table, IEnumerable<AtlasColumn> columns, AtlasColumn orderBy, bool descending)
    {
        StringBuilder sql = new("SELECT ");
        sql.Append(string.Join(", ", columns.Select(x => AtlasTabQueryBuilder.Quote(x.Name))));
        sql.Append(" FROM ").Append(AtlasTabQueryBuilder.Quote(table.Schema)).Append('.').Append(AtlasTabQueryBuilder.Quote(table.Name));
        if (table.HasSoftDelete)
            sql.Append(" WHERE ISNULL(").Append(AtlasTabQueryBuilder.Quote(AtlasTable.SoftDeleteColumnName)).Append(", 0) = 0");
        sql.Append(" ORDER BY ").Append(AtlasTabQueryBuilder.Quote(orderBy.Name)).Append(descending ? " DESC" : " ASC").Append(';');
        return sql.ToString();
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex Token();
}
