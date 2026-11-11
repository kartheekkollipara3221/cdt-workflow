using System.Collections.Concurrent;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>What the host does when the start-up check finds errors.</summary>
public enum AtlasStartupErrorMode
{
    /// <summary>The host does not start. Set this value explicitly, because the default is DisableModule.</summary>
    Stop,

    /// <summary>The default. The host starts. The apps with errors are not available (404, not in the menu).</summary>
    DisableModule
}

/// <summary>
/// The platform apps that the start-up check switched off (AppSettings:Atlas:OnStartupError = DisableModule).
/// </summary>
/// <remarks>
/// The resolver treats these apps as not available. A restart after the correction makes them available again.
/// </remarks>
public sealed class AtlasDisabledApps
{
    private readonly ConcurrentDictionary<string, string> _apps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The disabled apps, with the reason for each app.</summary>
    public IReadOnlyDictionary<string, string> All => _apps;

    /// <summary>Disables an app for the given reason.</summary>
    public void Disable(string appCode, string reason) => _apps[appCode] = reason;

    /// <summary>Returns true when the app is disabled, with the reason.</summary>
    public bool IsDisabled(string appCode, out string reason) => _apps.TryGetValue(appCode, out reason!);
}
