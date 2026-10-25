using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BBH.CP.Atlas.Data.Platform;

/// <summary>
/// The platform context (spec 12, question 1). It holds only shared-type entities: one
/// <c>Dictionary&lt;string, object?&gt;</c> entity for each registered table, with no C# class.
/// It uses the same connection as <see cref="CPSharepointContext"/> and can join its transaction
/// (<see cref="AtlasDataExtensions.BeginSharedTransactionAsync"/>).
/// </summary>
public class AtlasPlatformContext : DbContext
{
    public AtlasPlatformContext(DbContextOptions<AtlasPlatformContext> options, AtlasTableRegistry registry)
        : base(options)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public AtlasTableRegistry Registry { get; }

    /// <summary>The records of one registered table, for example <c>Records("TTDV2.General")</c>.</summary>
    public DbSet<Dictionary<string, object?>> Records(string tableFullName) =>
        Set<Dictionary<string, object?>>(Registry.Get(tableFullName).FullName);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // One EF Core model for each set of registered tables (the default is one model for each context type).
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, AtlasModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (AtlasTable table in Registry.Tables)
        {
            AtlasColumn key = table.Key
                ?? throw new InvalidOperationException($"Table '{table.FullName}' has no {AtlasTable.KeyColumnName} column.");

            modelBuilder.SharedTypeEntity<Dictionary<string, object?>>(table.FullName, entity =>
            {
                entity.ToTable(table.Name, table.Schema);

                foreach (AtlasColumn column in table.Columns)
                {
                    Type clrType = column.ClrType
                        ?? throw new InvalidOperationException(
                            $"Column '{table.FullName}.{column.Name}' has the type '{column.SqlType}', which the platform does not support.");

                    var property = entity.IndexerProperty(clrType, column.Name).HasColumnType(column.StoreType);

                    // A property-bag property has the sentinel null. For a non-nullable value type, the "not set"
                    // value must be the type default (for example ID = 0 on a new record), else EF Core sends it.
                    if (clrType.IsValueType && Nullable.GetUnderlyingType(clrType) == null)
                        property.HasSentinel(Activator.CreateInstance(clrType));

                    if (column.IsRowVersion)
                        property.IsRowVersion();
                    else if (column.IsIdentity)
                        property.ValueGeneratedOnAdd().UseIdentityColumn();
                    else if (column.IsComputed)
                        property.ValueGeneratedOnAddOrUpdate();
                    else if (column.UsesDatabaseDefault)
                        property.ValueGeneratedOnAdd(); // review DA-05: a null on insert is left out, so SQL Server uses the default

                    if (!column.IsNullable)
                        property.IsRequired();
                }

                entity.HasKey(key.Name);
            });
        }
    }
}

internal sealed class AtlasModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is AtlasPlatformContext platform
            ? (context.GetType(), platform.Registry.ModelKey, designTime)
            : (context.GetType(), designTime);
}
