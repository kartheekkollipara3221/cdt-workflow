using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BBH.CP.Atlas.Data.Platform;

/// <summary>
/// The EF Core context for the tables of the platform apps.
/// </summary>
/// <remarks>
/// It holds only shared-type entities: one <c>Dictionary&lt;string, object?&gt;</c> entity for each
/// registered table, with no C# class. It uses the same connection as <see cref="CPSharepointContext"/>
/// and can join its transaction (<see cref="AtlasDataExtensions.BeginSharedTransaction"/>).
/// </remarks>
public class AtlasPlatformContext : DbContext
{
    /// <summary>Makes the context for the tables in <paramref name="registry"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public AtlasPlatformContext(DbContextOptions<AtlasPlatformContext> options, AtlasTableRegistry registry)
        : base(options)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>The registered tables that the model maps.</summary>
    public AtlasTableRegistry Registry { get; }

    /// <summary>The records of one registered table, for example <c>Records("TTDV2.General")</c>.</summary>
    public DbSet<Dictionary<string, object?>> Records(string tableFullName) =>
        Set<Dictionary<string, object?>>(Registry.Get(tableFullName).FullName);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // One EF Core model for each set of registered tables (the default is one model for each context type).
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, AtlasModelCacheKeyFactory>();
    }

    /// <summary>Returns true if the table has an ID column and only column types that the platform supports.</summary>
    public static bool IsMappable(AtlasTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Key != null && table.Columns.All(x => x.ClrType != null);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A table that cannot be mapped (no ID column, or a column type that the platform does not support)
        // is left out of the one shared model. The start-up check names it and disables only its app. The model of
        // the other apps still builds. A request for that table fails for that app only.
        foreach (AtlasTable table in Registry.Tables.Where(IsMappable))
        {
            AtlasColumn key = table.Key!;

            modelBuilder.SharedTypeEntity<Dictionary<string, object?>>(table.FullName, entity =>
            {
                entity.ToTable(table.Name, table.Schema);

                foreach (AtlasColumn column in table.Columns)
                {
                    Type clrType = column.ClrType!;

                    var property = entity.IndexerProperty(clrType, column.Name).HasColumnType(column.StoreType);

                    // A property-bag property has the sentinel null. For a non-nullable value type, the "not set"
                    // value must be the type default (for example ID = 0 on a new record), else EF Core sends it.
                    if (clrType.IsValueType && Nullable.GetUnderlyingType(clrType) == null)
                        property.HasSentinel(Activator.CreateInstance(clrType));

                    if (column.IsRowVersion)
                        property.IsRowVersion();
                    else if (column.IsIdentity)
                        // The host can also reference the Oracle provider, which has a method with the same name.
                        SqlServerPropertyBuilderExtensions.UseIdentityColumn(property.ValueGeneratedOnAdd());
                    else if (column.IsComputed)
                        property.ValueGeneratedOnAddOrUpdate();
                    else if (column.UsesDatabaseDefault)
                        property.ValueGeneratedOnAdd(); // a null on insert is left out, so SQL Server uses the default

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
