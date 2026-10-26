using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.Data.Platform;

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
    public static async Task<IDbContextTransaction> BeginSharedTransactionAsync(this AtlasPlatformContext platform,
        CPSharepointContext main, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(main);

        if (!ReferenceEquals(platform.Database.GetDbConnection(), main.Database.GetDbConnection()))
            throw new InvalidOperationException("The platform context and the main context must use the same connection.");

        IDbContextTransaction transaction = await main.Database.BeginTransactionAsync(cancellationToken);
        await platform.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
        return transaction;
    }

    /// <summary>The same as <see cref="BeginSharedTransactionAsync"/>, for the synchronous service methods.</summary>
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
    /// A new record with a key for each column: identity key = 0 (EF Core makes the value), a NOT NULL value column =
    /// the type default (for example IsDeleted = 0; review DA-08: EF Core cannot read null for a bool property), others null.
    /// </summary>
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

    /// <summary>The same as <see cref="FindActiveAsync"/>, for the synchronous service methods.</summary>
    public static Dictionary<string, object?>? FindActive(this AtlasPlatformContext context, string tableFullName, int id)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActiveByKey(context, tableFullName, id).SingleOrDefault();
    }

    /// <summary>
    /// The same as <see cref="FindActive(AtlasPlatformContext, string, int)"/>; <paramref name="tracking"/> = false for a
    /// read-only path (load, history), so the record is not kept in the change tracker (review DA-10).
    /// </summary>
    public static Dictionary<string, object?>? FindActive(this AtlasPlatformContext context, string tableFullName, int id, bool tracking)
    {
        ArgumentNullException.ThrowIfNull(context);
        IQueryable<Dictionary<string, object?>> query = ActiveByKey(context, tableFullName, id);
        return (tracking ? query : query.AsNoTracking()).SingleOrDefault();
    }

    /// <summary>
    /// Loads one record by its key in the database (no full table read, spec 05, 6.5). A soft-deleted record is not
    /// found. Returns null if there is no such record.
    /// </summary>
    public static async Task<Dictionary<string, object?>?> FindActiveAsync(this AtlasPlatformContext context,
        string tableFullName, int id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await ActiveByKey(context, tableFullName, id).SingleOrDefaultAsync(cancellationToken);
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
