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

    /// <summary>A new record with a key for each column: identity key = 0 (EF Core makes the value), others null.</summary>
    public static Dictionary<string, object?> NewRecord(this AtlasTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Dictionary<string, object?> record = new(StringComparer.OrdinalIgnoreCase);
        foreach (AtlasColumn column in table.Columns)
        {
            Type? clrType = column.ClrType;
            record[column.Name] = column.IsIdentity && clrType is { IsValueType: true }
                ? Activator.CreateInstance(Nullable.GetUnderlyingType(clrType) ?? clrType)
                : null;
        }

        return record;
    }

    /// <summary>
    /// Loads one record by its key in the database (no full table read, spec 05, 6.5). A soft-deleted record is not
    /// found. Returns null if there is no such record.
    /// </summary>
    public static async Task<Dictionary<string, object?>?> FindActiveAsync(this AtlasPlatformContext context,
        string tableFullName, int id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        AtlasTable table = context.Registry.Get(tableFullName);

        IQueryable<Dictionary<string, object?>> query = context.Records(table.FullName)
            .Where(x => EF.Property<int>(x, AtlasTable.KeyColumnName) == id);

        if (table.HasSoftDelete)
        {
            query = table.Find(AtlasTable.SoftDeleteColumnName)!.IsNullable
                ? query.Where(x => EF.Property<bool?>(x, AtlasTable.SoftDeleteColumnName) != true)
                : query.Where(x => !EF.Property<bool>(x, AtlasTable.SoftDeleteColumnName));
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }
}
