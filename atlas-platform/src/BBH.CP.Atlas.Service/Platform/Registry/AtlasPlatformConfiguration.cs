using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// The configuration given to AddAtlasPlatform (review HO-01). The platform settings (AppSettings:ConfigFolder,
/// AppSettings:Modules:*) come from it, not from the IConfiguration that happens to be in the container.
/// </summary>
public sealed class AtlasPlatformConfiguration(IConfiguration configuration)
{
    public IConfiguration Configuration { get; } = configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <summary>The setting sources in order (last wins), for the --atlas-check report.</summary>
    public string Sources => Configuration is IConfigurationRoot root
        ? string.Join(", ", root.Providers.Select(Describe))
        : Configuration.GetType().Name;

    private static string Describe(IConfigurationProvider provider) => provider switch
    {
        JsonConfigurationProvider json => json.Source.Path ?? "json",
        _ => provider.GetType().Name.Replace("ConfigurationProvider", "", StringComparison.Ordinal)
    };
}
