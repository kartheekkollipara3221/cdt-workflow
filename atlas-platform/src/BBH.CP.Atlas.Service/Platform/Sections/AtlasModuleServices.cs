using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Audit;
using BBH.CP.Atlas.Service.Platform.Dashboard;
using BBH.CP.Atlas.Service.Platform.Options;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Rules;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// The platform services that a module class uses, in one constructor parameter.
/// </summary>
/// <remarks>
/// A module class has one constructor <c>(AtlasModuleServices services)</c>.
/// Thus a new platform service does not change the module classes.
/// A module can read through <see cref="Context"/> and <see cref="PlatformContext"/>.
/// A module must not write the app table or the audit table through them.
/// Such a write skips the allow list, the rules, the version check and the audit.
/// Change the record in OnBeforeSave (context.Record). The platform then saves it.
/// </remarks>
public sealed class AtlasModuleServices(
    IAtlasConfigStore configStore,
    CPSharepointContext context,
    AtlasPlatformContext platformContext,
    IAtlasUserContext user,
    IAtlasUserDirectory directory,
    AtlasAccessService access,
    AtlasRuleEngine rules,
    AtlasAuditWriter audit,
    AtlasDashboardService dashboards,
    IEnumerable<IAtlasOptionProvider> optionProviders,
    TimeProvider time,
    IConfiguration configuration,
    ILoggerFactory loggerFactory)
{
    public IAtlasConfigStore ConfigStore { get; } = configStore;
    public CPSharepointContext Context { get; } = context;
    public AtlasPlatformContext PlatformContext { get; } = platformContext;
    public IAtlasUserContext User { get; } = user;
    public IAtlasUserDirectory Directory { get; } = directory;
    public AtlasAccessService Access { get; } = access;
    public AtlasRuleEngine Rules { get; } = rules;
    public AtlasAuditWriter Audit { get; } = audit;
    public AtlasDashboardService Dashboards { get; } = dashboards;
    public IReadOnlyList<IAtlasOptionProvider> OptionProviders { get; } = [.. optionProviders];
    public TimeProvider Time { get; } = time;
    public IConfiguration Configuration { get; } = configuration;
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}
