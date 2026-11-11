namespace BBH.CP.Atlas.Data.Platform;

/// <summary>One column of a registered table, read from sys.columns.</summary>
/// <param name="MaxLength">Length in characters for text and binary columns; -1 = max; null for other types.</param>
/// <param name="HasDefault">True if the column has a database default constraint.</param>
public sealed record AtlasColumn(
    string Name,
    string SqlType,
    bool IsNullable,
    int? MaxLength,
    byte Precision,
    byte Scale,
    bool IsIdentity,
    bool IsComputed,
    bool HasDefault = false)
{
    /// <summary>True if the column is a rowversion (timestamp) column.</summary>
    public bool IsRowVersion => AtlasSqlTypes.IsRowVersion(SqlType);

    /// <summary>The CLR type of the value, or null if the platform does not support the SQL type.</summary>
    public Type? ClrType => AtlasSqlTypes.ClrType(this);

    /// <summary>The store type for EF Core, for example varchar(1000) or decimal(19,2).</summary>
    public string StoreType => AtlasSqlTypes.StoreType(this);

    /// <summary>True if the database writes the value, not the user.</summary>
    public bool IsDatabaseGenerated => IsIdentity || IsRowVersion || IsComputed;

    /// <summary>
    /// True if an insert with no value (null) must use the database default.
    /// </summary>
    /// <remarks>
    /// This applies to a column with a default whose type can hold null. A non-nullable value type cannot use it,
    /// because false or 0 from the user would look like "not set".
    /// </remarks>
    public bool UsesDatabaseDefault => HasDefault && !IsDatabaseGenerated && ClrType is { } type && (!type.IsValueType || Nullable.GetUnderlyingType(type) != null);
}

/// <summary>A registered table: "Schema.Name" and its columns.</summary>
public sealed class AtlasTable
{
    /// <summary>The name of the key column of each registered table.</summary>
    public const string KeyColumnName = "ID";

    /// <summary>The name of the bit column that marks a record as soft-deleted.</summary>
    public const string SoftDeleteColumnName = "IsDeleted";

    private readonly Dictionary<string, AtlasColumn> _columns;

    /// <summary>Makes a table with the given schema, name and columns.</summary>
    /// <exception cref="ArgumentException">The schema or the name is null, empty or white space.</exception>
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

    /// <summary>The schema of the table.</summary>
    public string Schema { get; }

    /// <summary>The name of the table, with no schema.</summary>
    public string Name { get; }

    /// <summary>"Schema.Name". It is also the name of the shared-type entity.</summary>
    public string FullName => $"{Schema}.{Name}";

    /// <summary>The columns in the order of sys.columns.</summary>
    public IReadOnlyList<AtlasColumn> Columns { get; }

    /// <summary>The ID column, or null if the table has no ID column.</summary>
    public AtlasColumn? Key => Find(KeyColumnName);

    /// <summary>True if the table has an IsDeleted column of type bit.</summary>
    public bool HasSoftDelete => Find(SoftDeleteColumnName) is { } column && AtlasSqlTypes.Normalize(column.SqlType) == "bit";

    /// <summary>Returns the column with the name (case-insensitive), or null if there is no such column.</summary>
    public AtlasColumn? Find(string columnName) =>
        _columns.TryGetValue(columnName, out AtlasColumn? column) ? column : null;

    /// <summary>Returns true if the table has a column with the name (case-insensitive).</summary>
    public bool HasColumn(string columnName) => _columns.ContainsKey(columnName);

    /// <summary>Returns true if the text has the form "Schema.Name".</summary>
    /// <remarks>Use it before <see cref="ParseFullName"/>, so that a bad name gives a check error, not an exception.</remarks>
    public static bool IsValidFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return false;
        string[] parts = fullName.Split('.');
        return parts.Length == 2 && !parts.Any(string.IsNullOrWhiteSpace);
    }

    /// <summary>Splits "Schema.Name" and removes the square brackets. The schema and the name must not contain a dot.</summary>
    /// <exception cref="ArgumentException">The text does not have the form Schema.Name.</exception>
    public static (string Schema, string Name) ParseFullName(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        string[] parts = fullName.Split('.');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"Table name '{fullName}' must have the form Schema.Name.", nameof(fullName));

        return (parts[0].Trim('[', ']'), parts[1].Trim('[', ']'));
    }
}
