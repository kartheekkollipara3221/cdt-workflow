using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform.Audit;
using BBH.CP.Atlas.Service.Platform.Dashboard;
using BBH.CP.Atlas.Service.Platform.Options;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Rules;
using BBH.CP.Atlas.Service.Platform.Sections;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>A fixed clock. The local time zone is UTC, so local and UTC dates are the same.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>
/// Makes a CPTTDV2Service on the test database, with the real TTDV2 files of the App project. Each call to
/// <see cref="Service"/> is one request: new contexts on one new connection.
/// </summary>
internal sealed class SectionHarness : IDisposable
{
    public const string General = "TTDV2.General";
    public const string AuditLog = "TTDV2.AuditLog";

    private static readonly SemaphoreSlim RegistryLock = new(1, 1);
    private static AtlasTableRegistry? _registry;
    private readonly List<IDisposable> _resources = [];
    private readonly SqlServerDatabaseFixture _database;
    private readonly IConfiguration _configuration;
    private readonly AtlasConfigStore _configStore;

    public SectionHarness(SqlServerDatabaseFixture database, AtlasAccessMode accessMode = AtlasAccessMode.Enforce)
    {
        _database = database;
        string configFolder = Path.Combine(TestPaths.AtlasPlatformRoot(), "src", "BBH.CP.Atlas.App", "Config");
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppSettings:ConfigFolder"] = configFolder,
            ["AppSettings:Modules:TTDV2:AccessMode"] = accessMode.ToString()
        }).Build();
        _configStore = new AtlasConfigStore(_configuration, new Host(configFolder));
    }

    /// <summary>Wednesday 2026-10-21 10:00 UTC.</summary>
    public FixedTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

    public ListLogger<AtlasAccessService> AccessLog { get; } = new();

    public static async Task<AtlasTableRegistry> RegistryAsync(SqlServerDatabaseFixture database)
    {
        await RegistryLock.WaitAsync();
        try
        {
            if (_registry != null)
                return _registry;
            AtlasTableRegistry registry = new();
            await using SqlConnection connection = new(database.ConnectionString);
            await new AtlasColumnMetadataReader().LoadAsync(connection, registry, [General, AuditLog]);
            return _registry = registry;
        }
        finally
        {
            RegistryLock.Release();
        }
    }

    public CPTTDV2Service Service(AtlasTableRegistry registry, string lanId, params AtlasRole[] roles)
    {
        SqlConnection connection = new(_database.ConnectionString);
        CPSharepointContext context = new(new DbContextOptionsBuilder<CPSharepointContext>().UseSqlServer(connection).Options, _configuration);
        AtlasPlatformContext platform = new(new DbContextOptionsBuilder<AtlasPlatformContext>().UseSqlServer(connection).Options, registry);
        _resources.AddRange([platform, context, connection]);

        FakeUserContext user = new(lanId, roles);
        AtlasUserDirectory directory = new(context, new MemoryCache(new MemoryCacheOptions()));
        AtlasAccessService access = new(user, AccessLog);
        AtlasModuleServices services = new(
            _configStore, context, platform, user, directory, access, new AtlasRuleEngine(),
            new AtlasAuditWriter(platform, user, Time),
            new AtlasDashboardService(_configStore, context, registry, user, directory, access, new AtlasRuleEngine()),
            [new PBAccountsOptionProvider(_configuration, user), new UsersInRolesOptionProvider(directory)],
            Time, _configuration, NullLoggerFactory.Instance);

        return new CPTTDV2Service(services) { AppCode = "TTDV2" };
    }

    public void Dispose()
    {
        foreach (IDisposable resource in _resources)
            resource.Dispose();
    }

    private sealed class Host(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
