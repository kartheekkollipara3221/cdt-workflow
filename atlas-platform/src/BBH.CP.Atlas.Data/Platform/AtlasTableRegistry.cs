using System.Text;

namespace BBH.CP.Atlas.Data.Platform;

/// <summary>
/// The tables that the platform maps as shared-type entities.
/// </summary>
/// <remarks>
/// The platform fills it one time at start-up from the database metadata. The database is the source of truth.
/// A new table needs a restart of the API.
/// </remarks>
public sealed class AtlasTableRegistry
{
    private readonly Dictionary<string, AtlasTable> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private string? _modelKey;

    /// <summary>True after the platform start-up loaded the tables.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Records that the platform start-up loaded the tables. See <see cref="IsLoaded"/>.</summary>
    public void MarkLoaded() => IsLoaded = true;

    /// <summary>A copy of the registered tables.</summary>
    public IReadOnlyCollection<AtlasTable> Tables
    {
        get
        {
            lock (_lock)
                return [.. _tables.Values];
        }
    }

    /// <summary>Changes when the registered tables or columns change. EF Core keeps one model for each key.</summary>
    public string ModelKey
    {
        get
        {
            lock (_lock)
                return _modelKey ??= BuildModelKey();
        }
    }

    /// <summary>Adds the table, or replaces a table with the same full name.</summary>
    public void Add(AtlasTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        lock (_lock)
        {
            _tables[table.FullName] = table;
            _modelKey = null;
        }
    }

    /// <summary>Finds a table by its full name (case-insensitive). Returns false if the table is not registered.</summary>
    public bool TryGet(string fullName, out AtlasTable table)
    {
        lock (_lock)
        {
            if (_tables.TryGetValue(fullName, out AtlasTable? found))
            {
                table = found;
                return true;
            }
        }

        table = null!;
        return false;
    }

    /// <summary>Returns the table with the full name (case-insensitive).</summary>
    /// <exception cref="InvalidOperationException">The table is not registered.</exception>
    public AtlasTable Get(string fullName) =>
        TryGet(fullName, out AtlasTable table)
            ? table
            : throw new InvalidOperationException($"Table '{fullName}' is not registered in the platform.");

    private string BuildModelKey()
    {
        StringBuilder key = new();
        foreach (AtlasTable table in _tables.Values.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
        {
            key.Append(table.FullName).Append('(');
            foreach (AtlasColumn column in table.Columns)
                key.Append(column.Name).Append(':').Append(column.StoreType).Append(column.IsNullable ? "?" : "").Append(column.HasDefault ? "=" : "").Append(',');
            key.Append(')');
        }

        return key.ToString();
    }
}
