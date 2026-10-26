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
/// The platform services that a module class uses, in one constructor parameter. A module class has one
/// constructor <c>(AtlasModuleServices services)</c>; a new platform service does not change the module classes.
/// Rule for modules (review SV-13): read through <see cref="Context"/> and <see cref="PlatformContext"/> if needed, but
/// do not write the app table or the audit table through them. A write there skips the allow list, the rules, the
/// version check and the audit. Change the record in OnBeforeSave (context.Record), which the platform saves.
/// </summary>
public sealed class AtlasModuleServices(
    IAtlasConfigStore configStore,
    CPSharepointContext context,
    AtlasPlatformContext platformContext,
    IAtlasUserContext user,
    IAtlasUserDirectory directory,
    IAtlasAccessService access,
    IAtlasRuleEngine rules,
    IAtlasAuditWriter audit,
    IAtlasDashboardService dashboards,
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
    public IAtlasAccessService Access { get; } = access;
    public IAtlasRuleEngine Rules { get; } = rules;
    public IAtlasAuditWriter Audit { get; } = audit;
    public IAtlasDashboardService Dashboards { get; } = dashboards;
    public IReadOnlyList<IAtlasOptionProvider> OptionProviders { get; } = [.. optionProviders];
    public TimeProvider Time { get; } = time;
    public IConfiguration Configuration { get; } = configuration;
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}
