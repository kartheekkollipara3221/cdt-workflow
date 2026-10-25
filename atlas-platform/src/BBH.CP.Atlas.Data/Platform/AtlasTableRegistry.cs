using System.Text;

namespace BBH.CP.Atlas.Data.Platform;

/// <summary>
/// The tables that the platform maps as shared-type entities. Filled one time at start-up from the database
/// metadata (the database is the source of truth, spec 05, 6.2). A new table needs a restart of the API.
/// </summary>
public sealed class AtlasTableRegistry
{
    private readonly Dictionary<string, AtlasTable> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private string? _modelKey;

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

    public void Add(AtlasTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        lock (_lock)
        {
            _tables[table.FullName] = table;
            _modelKey = null;
        }
    }

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
