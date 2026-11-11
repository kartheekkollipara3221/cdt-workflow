using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// The configuration that the host gives to AddAtlasPlatform.
/// </summary>
/// <remarks>
/// The platform settings (AppSettings:ConfigFolder, AppSettings:Modules:*) come from this configuration.
/// They do not come from an IConfiguration that is in the container by chance.
/// </remarks>
public sealed class AtlasPlatformConfiguration(IConfiguration configuration)
{
    /// <summary>The configuration of the platform.</summary>
    public IConfiguration Configuration { get; } = configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <summary>The platform settings read from <see cref="Configuration"/>.</summary>
    public AtlasPlatformOptions Options { get; } = AtlasPlatformOptions.From(configuration);

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
