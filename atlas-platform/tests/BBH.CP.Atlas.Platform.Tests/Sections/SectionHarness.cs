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
using BBH.CP.Atlas.Service.Platform.Workflow;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>
/// Makes the TTDV2 module (ConfigSectionService) on the test database, with the real TTDV2 files of the App project. Each call to
/// <see cref="Service"/> is one request: new contexts on one new connection.
/// </summary>
internal sealed class SectionHarness : IDisposable
{
    public const string General = "TTDV2.General";
    public const string AuditLog = "TTDV2.AuditLog";

    private readonly List<IDisposable> _resources = [];
    private readonly SqlServerDatabaseFixture _database;
    private readonly IConfiguration _configuration;
    private readonly AtlasConfigStore _configStore;

    public SectionHarness(SqlServerDatabaseFixture database, AtlasAccessMode accessMode = AtlasAccessMode.Enforce, string? configFolder = null)
    {
        _database = database;
        configFolder ??= RealConfigFolder;
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppSettings:ConfigFolder"] = configFolder,
            ["AppSettings:Modules:TTDV2:AccessMode"] = accessMode.ToString(),
            ["AppSettings:HostedClientURL"] = "https://atlas.test",
            ["AppSettings:Modules:CFPINFLOW:Workflow:Lists:Icg"] = "icg@test.local",
            ["AppSettings:Modules:CFPINFLOW:Workflow:Lists:CsTeam"] = "cs@test.local",
            ["AppSettings:Modules:CFPINFLOW:Workflow:Lists:Imrg"] = "imrg@test.local",
            ["AppSettings:Modules:CFPINFLOW:Workflow:Lists:StarFasIfiCash"] = "star@test.local",
            ["AppSettings:Modules:CFPINFLOW:Workflow:Lists:FixedIncome"] = "fi@test.local",
            ["AppSettings:Modules:CFPINFLOW:Workflow:Lists:Equity"] = "eq@test.local;eq2@test.local"
        }).Build();
        _configStore = new AtlasConfigStore(_configuration, new TestHost(configFolder));
    }

    public static string RealConfigFolder => Path.Combine(TestPaths.AtlasPlatformRoot(), "src", "BBH.CP.Atlas.App", "Config");

    /// <summary>Wednesday 2026-10-21 10:00 UTC.</summary>
    public FixedTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

    public ListLogger<AtlasAccessService> AccessLog { get; } = new();

    /// <summary>The workflow adapter of the services: a fake engine by default (no engine tables). Null = no adapter.</summary>
    public IAtlasWorkflowAdapter? Workflow { get; set; } = new FakeWorkflowAdapter();

    public ConfigSectionService Service(AtlasTableRegistry registry, string lanId, params AtlasRole[] roles) =>
        new(ModuleServices(registry, lanId, roles, NullLoggerFactory.Instance)) { AppCode = "TTDV2" };

    /// <summary>The module service of another app of the real Config folder (for example CFPINFLOW).</summary>
    public ConfigSectionService ServiceFor(string appCode, AtlasTableRegistry registry, string lanId, params AtlasRole[] roles) =>
        new(ModuleServices(registry, lanId, roles, NullLoggerFactory.Instance)) { AppCode = appCode };

    /// <summary>The services of a module class, for a test subclass of a module (for example a hook that throws).</summary>
    public AtlasModuleServices ModuleServices(AtlasTableRegistry registry, string lanId, AtlasRole[] roles, ILoggerFactory loggerFactory)
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
            [new PBAccountsOptionProvider(_configuration, user), new UsersInRolesOptionProvider(directory),
                new YearsOptionProvider(Time), new WeekEndingsOptionProvider(Time),
                new AtlasTableOptionProvider(context, registry, _configStore, new MemoryCache(new MemoryCacheOptions()))],
            Time, _configuration, loggerFactory, workflow: Workflow);

        return services;
    }

    public void Dispose()
    {
        foreach (IDisposable resource in _resources)
            resource.Dispose();
    }
}

/// <summary>A copy of the real Config folder in a new temp folder; deleted on Dispose.</summary>
internal sealed class ConfigCopy : IDisposable
{
    public ConfigCopy()
    {
        Directory.CreateDirectory(Folder);
        foreach (string file in Directory.GetFiles(SectionHarness.RealConfigFolder, "*.json"))
            File.Copy(file, Path.Combine(Folder, Path.GetFileName(file)));
    }

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "atlas-section-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Replaces <paramref name="oldText"/> in one file (LF or CRLF line ends). Throws when the text is not there.</summary>
    public ConfigCopy Replace(string file, string oldText, string newText)
    {
        string path = Path.Combine(Folder, file);
        string text = File.ReadAllText(path);
        if (!text.Contains(oldText, StringComparison.Ordinal))
        {
            oldText = oldText.Replace("\n", "\r\n", StringComparison.Ordinal);
            newText = newText.Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        if (!text.Contains(oldText, StringComparison.Ordinal))
            throw new InvalidOperationException($"{file} has no text {oldText}.");
        File.WriteAllText(path, text.Replace(oldText, newText, StringComparison.Ordinal));
        return this;
    }

    public ConfigCopy Write(string file, string text)
    {
        File.WriteAllText(Path.Combine(Folder, file), text);
        return this;
    }

    public void Dispose() => Directory.Delete(Folder, recursive: true);
}

/// <summary>
/// A workflow adapter with no engine: each action succeeds and gives the state of its action key (the CFPINFLOW keys by
/// default). It keeps the calls. It has no instance, so the state check on load does nothing.
/// </summary>
internal sealed class FakeWorkflowAdapter : IAtlasWorkflowAdapter
{
    public Dictionary<string, string> NewStates { get; } = new(StringComparer.Ordinal)
    {
        ["Submit"] = "Submitted", ["NotifyInformational"] = "Submitted", ["ConfirmFunds"] = "FundsReceived"
    };

    public List<AtlasWorkflowCall> Calls { get; } = [];

    public AtlasWorkflowResult Run(AtlasWorkflowCall call)
    {
        Calls.Add(call);
        return new AtlasWorkflowResult(true, "Success", null, NewStates.GetValueOrDefault(call.ActionKey), call.InstanceId, call.IdempotencyKey, false);
    }

    public string? ReadState(string instanceId, string userId, IReadOnlyList<string> roles) => null;

    public string? DispatchNotifications(AtlasWorkflowResult result) => null;
}
