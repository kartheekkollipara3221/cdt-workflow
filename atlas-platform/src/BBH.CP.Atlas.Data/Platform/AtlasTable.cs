namespace BBH.CP.Atlas.Data.Platform;

/// <summary>One column of a registered table, read from sys.columns (spec 05, 6.2).</summary>
/// <param name="MaxLength">Length in characters for text and binary columns; -1 = max; null for other types.</param>
public sealed record AtlasColumn(
    string Name,
    string SqlType,
    bool IsNullable,
    int? MaxLength,
    byte Precision,
    byte Scale,
    bool IsIdentity,
    bool IsComputed)
{
    public bool IsRowVersion => AtlasSqlTypes.IsRowVersion(SqlType);

    /// <summary>The CLR type of the value, or null if the platform does not support the SQL type.</summary>
    public Type? ClrType => AtlasSqlTypes.ClrType(this);

    /// <summary>The store type for EF Core, for example varchar(1000) or decimal(19,2).</summary>
    public string StoreType => AtlasSqlTypes.StoreType(this);

    /// <summary>True if the platform writes the value, not the user (spec 05, 6.3).</summary>
    public bool IsDatabaseGenerated => IsIdentity || IsRowVersion || IsComputed;
}

/// <summary>A registered table: "Schema.Name" and its columns.</summary>
public sealed class AtlasTable
{
    public const string KeyColumnName = "ID";
    public const string SoftDeleteColumnName = "IsDeleted";

    private readonly Dictionary<string, AtlasColumn> _columns;

    public AtlasTable(string schema, string name, IEnumerable<AtlasColumn> columns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(columns);

        Schema = schema;
        Name = name;
        Columns = [.. columns];
        _columns = Columns.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    public string Schema { get; }

    public string Name { get; }

    /// <summary>"Schema.Name". It is also the name of the shared-type entity.</summary>
    public string FullName => $"{Schema}.{Name}";

    public IReadOnlyList<AtlasColumn> Columns { get; }

    public AtlasColumn? Key => Find(KeyColumnName);

    public bool HasSoftDelete => Find(SoftDeleteColumnName) is { } column && AtlasSqlTypes.Normalize(column.SqlType) == "bit";

    public AtlasColumn? Find(string columnName) =>
        _columns.TryGetValue(columnName, out AtlasColumn? column) ? column : null;

    public bool HasColumn(string columnName) => _columns.ContainsKey(columnName);

    /// <summary>Splits "Schema.Name". The schema and the name must not contain a dot.</summary>
    public static (string Schema, string Name) ParseFullName(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        string[] parts = fullName.Split('.');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"Table name '{fullName}' must have the form Schema.Name.", nameof(fullName));

        return (parts[0].Trim('[', ']'), parts[1].Trim('[', ']'));
    }
}
