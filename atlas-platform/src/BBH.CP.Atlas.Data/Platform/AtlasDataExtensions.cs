using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.Data.Platform;

/// <summary>
/// Registers the platform data services and gives helpers for the records of registered tables.
/// </summary>
public static class AtlasDataExtensions
{
    /// <summary>
    /// Registers the table registry (singleton) and <see cref="AtlasPlatformContext"/> (scoped). The platform context
    /// uses the connection of the scoped <see cref="CPSharepointContext"/>, so both contexts use one connection.
    /// </summary>
    public static IServiceCollection AddAtlasPlatformData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AtlasTableRegistry>();
        services.AddSingleton<AtlasColumnMetadataReader>();
        services.AddDbContext<AtlasPlatformContext>((provider, options) =>
            options.UseSqlServer(provider.GetRequiredService<CPSharepointContext>().Database.GetDbConnection()));

        return services;
    }

    /// <summary>
    /// Starts a transaction on <paramref name="main"/> and makes <paramref name="platform"/> use it. Both contexts must
    /// use the same connection. The caller commits or disposes the returned transaction.
    /// </summary>
    public static IDbContextTransaction BeginSharedTransaction(this AtlasPlatformContext platform, CPSharepointContext main)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(main);

        if (!ReferenceEquals(platform.Database.GetDbConnection(), main.Database.GetDbConnection()))
            throw new InvalidOperationException("The platform context and the main context must use the same connection.");

        IDbContextTransaction transaction = main.Database.BeginTransaction();
        platform.Database.UseTransaction(transaction.GetDbTransaction());
        return transaction;
    }

    /// <summary>
    /// Returns a new record with a key for each column of the table.
    /// </summary>
    /// <remarks>
    /// The identity key is 0, and EF Core makes the value. A NOT NULL value column gets the type default
    /// (for example IsDeleted = 0), because EF Core cannot read null for a bool property. Other columns are null.
    /// </remarks>
    public static Dictionary<string, object?> NewRecord(this AtlasTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Dictionary<string, object?> record = new(StringComparer.OrdinalIgnoreCase);
        foreach (AtlasColumn column in table.Columns)
        {
            Type? clrType = column.ClrType;
            record[column.Name] = clrType is { IsValueType: true } && Nullable.GetUnderlyingType(clrType) == null
                ? Activator.CreateInstance(clrType)
                : null;
        }

        return record;
    }

    /// <summary>
    /// Loads one record by its key in the database, with no full table read. A soft-deleted record is not
    /// found. Returns null if there is no such record.
    /// </summary>
    public static Dictionary<string, object?>? FindActive(this AtlasPlatformContext context, string tableFullName, int id)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActiveByKey(context, tableFullName, id).SingleOrDefault();
    }

    /// <summary>
    /// Loads one active record by its key, with or without change tracking.
    /// </summary>
    /// <remarks>
    /// The same as <see cref="FindActive(AtlasPlatformContext, string, int)"/>.
    /// </remarks>
    /// <param name="tracking">
    /// False for a read-only path (load, history), so the change tracker does not keep the record.
    /// </param>
    public static Dictionary<string, object?>? FindActive(this AtlasPlatformContext context, string tableFullName, int id, bool tracking)
    {
        ArgumentNullException.ThrowIfNull(context);
        IQueryable<Dictionary<string, object?>> query = ActiveByKey(context, tableFullName, id);
        return (tracking ? query : query.AsNoTracking()).SingleOrDefault();
    }

    private static IQueryable<Dictionary<string, object?>> ActiveByKey(AtlasPlatformContext context, string tableFullName, int id)
    {
        AtlasTable table = context.Registry.Get(tableFullName);

        IQueryable<Dictionary<string, object?>> query = context.Records(table.FullName)
            .Where(x => EF.Property<int>(x, AtlasTable.KeyColumnName) == id);

        if (table.HasSoftDelete)
        {
            query = table.Find(AtlasTable.SoftDeleteColumnName)!.IsNullable
                ? query.Where(x => EF.Property<bool?>(x, AtlasTable.SoftDeleteColumnName) != true)
                : query.Where(x => !EF.Property<bool>(x, AtlasTable.SoftDeleteColumnName));
        }

        return query;
    }
}
