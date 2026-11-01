using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// The platform settings under AppSettings (INTEGRATION.md, section 4), read in one place (review FABLE F05). A value
/// that is missing or not valid gives the default; OnStartupError keeps its text, so the start-up check can report
/// a value that is not valid. The module overrides (AppSettings:Modules:*) are read by <see cref="AtlasConfigStore"/>.
/// AppSettings:ConfigRefreshSeconds is no longer read (review FABLE F04: the config is read one time); a value is ignored.
/// </summary>
public sealed class AtlasPlatformOptions
{
    public const string ConfigFolderKey = "AppSettings:ConfigFolder";
    public const string OnStartupErrorKey = "AppSettings:Atlas:OnStartupError";

    /// <summary>The folder of the manifests and JSON files, relative to the content root. Default "Config".</summary>
    public string ConfigFolder { get; init; } = "Config";

    /// <summary>The text of AppSettings:Atlas:OnStartupError, or null. See <see cref="StartupErrorMode"/>.</summary>
    public string? OnStartupError { get; init; }

    public static AtlasPlatformOptions From(IConfiguration? configuration) => new()
    {
        ConfigFolder = configuration?[ConfigFolderKey] is { Length: > 0 } folder ? folder : "Config",
        OnStartupError = configuration?[OnStartupErrorKey]
    };

    /// <summary>
    /// OnStartupError as a mode: DisableModule when the setting is empty; null when the text is not a mode name.
    /// </summary>
    public AtlasStartupErrorMode? StartupErrorMode =>
        string.IsNullOrWhiteSpace(OnStartupError) ? AtlasStartupErrorMode.DisableModule
        : AtlasEnumText.TryParse(OnStartupError, out AtlasStartupErrorMode mode) ? mode
        : null;
}

/// <summary>Setting text to an enum value: only the names (any case, spaces trimmed), not numbers (review HO-03).</summary>
public static class AtlasEnumText
{
    public static bool TryParse<T>(string? text, out T value) where T : struct, Enum
    {
        string? name = Enum.GetNames<T>().FirstOrDefault(x => string.Equals(x, text?.Trim(), StringComparison.OrdinalIgnoreCase));
        value = name == null ? default : Enum.Parse<T>(name);
        return name != null;
    }
}
