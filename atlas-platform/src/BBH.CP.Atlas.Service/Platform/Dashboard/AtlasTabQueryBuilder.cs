using System.Data;
using System.Text;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Service.Platform.Dashboard;

/// <summary>One list query: the SQL text and its parameters.</summary>
public sealed record AtlasTabQuery(string Sql, IReadOnlyList<SqlParameter> Parameters, IReadOnlyList<AtlasColumn> Columns);

/// <summary>
/// Makes the list query of a <c>Source: Table</c> tab.
/// </summary>
/// <remarks>
/// Table and column names come only from the table metadata. They are checked names in bracket form.
/// Each value is a parameter. The platform always adds <c>IsDeleted = 0</c>.
/// AND joins the filters. OR joins the items of an <c>Any</c> group. A group cannot contain a group.
/// </remarks>
public static class AtlasTabQueryBuilder
{
    /// <summary>The filter operators that a tab can use.</summary>
    public static readonly IReadOnlySet<string> Operators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Eq", "NotEq", "In", "IsNull", "NotNull", "Gt", "Lt"
    };

    /// <summary>Returns the list query of a tab.</summary>
    /// <param name="resolveToken">Changes a value token ({LanId}, LANID, {AppCode}, {TabCode}) to its value; other text stays.</param>
    /// <exception cref="InvalidOperationException">The tab has a column, filter, MaxRows or OrderBy that is not valid.</exception>
    public static AtlasTabQuery Build(AtlasTable table, CPAtlasTab tab, Func<string, string> resolveToken)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(resolveToken);

        if (tab.Columns is not { Count: > 0 })
            throw Invalid(tab, "has no Columns list");

        List<AtlasColumn> columns = [];
        foreach (CPTabColumn column in tab.Columns)
            columns.Add(Column(table, tab, column.Name));
        if (table.Key is { } key && !columns.Contains(key))
            columns.Insert(0, key); // the row link needs the ID

        List<SqlParameter> parameters = [];
        List<string> conditions = [];
        if (table.HasSoftDelete)
        {
            // On a NOT NULL column, "= 0" matches the filtered indexes (WHERE [IsDeleted] = 0), so SQL Server can seek.
            // A nullable column keeps ISNULL, because there NULL also means "not deleted".
            string deleted = Quote(AtlasTable.SoftDeleteColumnName);
            conditions.Add(table.Find(AtlasTable.SoftDeleteColumnName)!.IsNullable ? $"ISNULL({deleted}, 0) = 0" : $"{deleted} = 0");
        }

        foreach (CPTabFilter filter in tab.Filter ?? [])
        {
            if (filter.Any is { Count: > 0 })
            {
                if (filter.Column != null || filter.Any.Any(x => x.Any != null))
                    throw Invalid(tab, "has an Any group with a Column or a nested group (one level only)");
                conditions.Add("(" + string.Join(" OR ", filter.Any.Select(x => Condition(table, tab, x, parameters, resolveToken))) + ")");
            }
            else
            {
                conditions.Add(Condition(table, tab, filter, parameters, resolveToken));
            }
        }

        StringBuilder sql = new();
        sql.Append("SELECT ");
        if (tab.MaxRows is { } maxRows)
        {
            if (maxRows <= 0)
                throw Invalid(tab, $"has MaxRows {maxRows} (use a number greater than 0, or leave it out)");
            parameters.Add(new SqlParameter("@max", SqlDbType.Int) { Value = maxRows });
            sql.Append("TOP (@max) ");
        }

        sql.Append(string.Join(", ", columns.Select(x => Quote(x.Name))));
        sql.Append(" FROM ").Append(Quote(table.Schema)).Append('.').Append(Quote(table.Name));
        if (conditions.Count > 0)
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        sql.Append(" ORDER BY ").Append(OrderBy(table, tab));
        sql.Append(';');

        return new AtlasTabQuery(sql.ToString(), parameters, columns);
    }

    private static string Condition(AtlasTable table, CPAtlasTab tab, CPTabFilter filter, List<SqlParameter> parameters, Func<string, string> resolveToken)
    {
        AtlasColumn column = Column(table, tab, filter.Column ?? throw Invalid(tab, "has a filter with no Column"));
        string op = filter.Op ?? "Eq";
        if (!Operators.Contains(op))
            throw Invalid(tab, $"has the filter operator '{op}', which is not supported");

        string name = Quote(column.Name);
        switch (op.ToLowerInvariant())
        {
            case "isnull":
                return $"{name} IS NULL";
            case "notnull":
                return $"{name} IS NOT NULL";
            case "in":
                List<string> values = filter.Values is { Count: > 0 } list ? list : filter.Value != null ? [filter.Value] : [];
                if (values.Count == 0)
                    throw Invalid(tab, $"has an In filter on {column.Name} with no values");
                return $"{name} IN ({string.Join(", ", values.Select(x => Parameter(column, tab, resolveToken(x), parameters)))})";
            default:
                string sqlOp = op.ToLowerInvariant() switch { "eq" => "=", "noteq" => "<>", "gt" => ">", _ => "<" };
                if (filter.Value == null)
                    throw Invalid(tab, $"has a {op} filter on {column.Name} with no Value");
                return $"{name} {sqlOp} {Parameter(column, tab, resolveToken(filter.Value), parameters)}";
        }
    }

    private static string Parameter(AtlasColumn column, CPAtlasTab tab, string value, List<SqlParameter> parameters)
    {
        AtlasConversionResult converted = AtlasValueConverter.FromWire(value, column with { IsNullable = true, IsIdentity = false, IsComputed = false });
        if (!converted.Success)
            throw Invalid(tab, $"has the filter value '{value}' for {column.Name}: {converted.Message}");

        string parameterName = "@p" + parameters.Count;
        SqlParameter parameter = new(parameterName, converted.Value ?? DBNull.Value);
        if (AtlasSqlTypes.IsText(column.SqlType))
        {
            parameter.SqlDbType = AtlasSqlTypes.Normalize(column.SqlType) switch
            {
                "nvarchar" => SqlDbType.NVarChar,
                "nchar" => SqlDbType.NChar,
                "char" => SqlDbType.Char,
                _ => SqlDbType.VarChar
            };
            parameter.Size = column.MaxLength is > 0 ? column.MaxLength.Value : -1;
        }

        parameters.Add(parameter);
        return parameterName;
    }

    private static string OrderBy(AtlasTable table, CPAtlasTab tab)
    {
        string key = $"{Quote(AtlasTable.KeyColumnName)} DESC";
        if (tab.OrderBy is not { Count: > 0 })
            return key;

        List<string> items = [];
        bool hasKey = false;
        foreach (string entry in tab.OrderBy)
        {
            string[] parts = entry.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is 0 or > 2)
                throw Invalid(tab, $"has the OrderBy item '{entry}', which is not valid");
            string direction = parts.Length == 2 ? parts[1].ToUpperInvariant() : "ASC";
            if (direction is not ("ASC" or "DESC"))
                throw Invalid(tab, $"has the OrderBy direction '{parts[1]}', which is not valid");
            AtlasColumn column = Column(table, tab, parts[0]);
            hasKey |= string.Equals(column.Name, AtlasTable.KeyColumnName, StringComparison.OrdinalIgnoreCase);
            items.Add($"{Quote(column.Name)} {direction}");
        }

        // A unique last item makes the order stable. Then paging gives no duplicate or missing rows.
        if (!hasKey && table.Key != null)
            items.Add(key);
        return string.Join(", ", items);
    }

    private static AtlasColumn Column(AtlasTable table, CPAtlasTab tab, string name) =>
        table.Find(name) ?? throw Invalid(tab, $"names the column '{name}', which is not in {table.FullName}");

    /// <summary>Returns the name in bracket form. The result is the same as QUOTENAME for up to 128 characters.</summary>
    public static string Quote(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static InvalidOperationException Invalid(CPAtlasTab tab, string problem) =>
        new($"Dashboard tab '{tab.ID}' {problem}.");
}
