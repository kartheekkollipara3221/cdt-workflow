using System.Collections.Concurrent;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>What the host does when the start-up check finds errors (review HO-04).</summary>
public enum AtlasStartupErrorMode
{
    /// <summary>The host does not start (spec 07, 4.8). Set it explicitly; the default is DisableModule (review AR-01).</summary>
    Stop,

    /// <summary>The default (review AR-01). The host starts; only the apps with errors are not available (404, not in the menu).</summary>
    DisableModule
}

/// <summary>
/// The platform apps that the start-up switched off (AppSettings:Atlas:OnStartupError = DisableModule, review HO-04).
/// The resolver treats them as not available. A restart after the fix makes them available again.
/// </summary>
public sealed class AtlasDisabledApps
{
    private readonly ConcurrentDictionary<string, string> _apps = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> All => _apps;

    public void Disable(string appCode, string reason) => _apps[appCode] = reason;

    public bool IsDisabled(string appCode, out string reason) => _apps.TryGetValue(appCode, out reason!);
}
