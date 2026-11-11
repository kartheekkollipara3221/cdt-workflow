using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// The platform settings under AppSettings, read in one place.
/// </summary>
/// <remarks>
/// A value that is missing or not valid gives the default.
/// OnStartupError keeps its text, so the start-up check can report a value that is not valid.
/// <see cref="AtlasConfigStore"/> reads the module overrides (AppSettings:Modules:*).
/// The platform ignores AppSettings:ConfigRefreshSeconds, because it reads the config one time.
/// </remarks>
public sealed class AtlasPlatformOptions
{
    /// <summary>The key of the config folder setting.</summary>
    public const string ConfigFolderKey = "AppSettings:ConfigFolder";
    /// <summary>The key of the start-up error mode setting.</summary>
    public const string OnStartupErrorKey = "AppSettings:Atlas:OnStartupError";

    /// <summary>The folder of the manifests and JSON files, relative to the content root. Default "Config".</summary>
    public string ConfigFolder { get; init; } = "Config";

    /// <summary>The text of AppSettings:Atlas:OnStartupError, or null. See <see cref="StartupErrorMode"/>.</summary>
    public string? OnStartupError { get; init; }

    /// <summary>Reads the platform settings from a configuration. A null configuration gives the defaults.</summary>
    public static AtlasPlatformOptions From(IConfiguration? configuration) => new()
    {
        ConfigFolder = configuration?[ConfigFolderKey] is { Length: > 0 } folder ? folder : "Config",
        OnStartupError = configuration?[OnStartupErrorKey]
    };

    /// <summary>
    /// OnStartupError as a mode. DisableModule when the setting is empty. Null when the text is not a mode name.
    /// </summary>
    public AtlasStartupErrorMode? StartupErrorMode =>
        string.IsNullOrWhiteSpace(OnStartupError) ? AtlasStartupErrorMode.DisableModule
        : AtlasEnumText.TryParse(OnStartupError, out AtlasStartupErrorMode mode) ? mode
        : null;
}

/// <summary>Changes setting text to an enum value.</summary>
/// <remarks>
/// Only the names are accepted, in any case, with spaces at the ends removed. Numbers are not accepted.
/// </remarks>
public static class AtlasEnumText
{
    /// <summary>Returns true when the text is the name of a value of <typeparamref name="T"/>.</summary>
    public static bool TryParse<T>(string? text, out T value) where T : struct, Enum
    {
        string? name = Enum.GetNames<T>().FirstOrDefault(x => string.Equals(x, text?.Trim(), StringComparison.OrdinalIgnoreCase));
        value = name == null ? default : Enum.Parse<T>(name);
        return name != null;
    }
}
