using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Platform.Tests;

/// <summary>A user context with fixed roles (no database).</summary>
internal sealed class FakeUserContext(string lanId, params AtlasRole[] roles) : IAtlasUserContext
{
    public string LanId { get; } = lanId;

    public string DisplayName => "User " + LanId;

    public CPUser User => new() { LanID = LanId, DisplayName = DisplayName };

    public AtlasAppRoles RolesFor(string appCode) =>
        new(roles.Where(x => string.Equals(x.AppCode, appCode, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>The TTDV2 roles (spec 01, 10.5; sql/12-ttdv2-app.sql).</summary>
internal static class Ttdv2Roles
{
    public static AtlasRole Read => new("TTDV2", "Read", IsRead: true, IsWrite: false, IsAdmin: false);
    public static AtlasRole Prepare => new("TTDV2", "Prepare", IsRead: true, IsWrite: true, IsAdmin: false);
    public static AtlasRole Owner => new("TTDV2", "Owner", IsRead: true, IsWrite: true, IsAdmin: true);
}

/// <summary>Collects log entries for assertions.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(EventId EventId, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((eventId, formatter(state, exception)));
}

/// <summary>A logger factory that gives one list logger for all categories.</summary>
internal sealed class ListLoggerFactory : ILoggerFactory
{
    public ListLogger<object> Logger { get; } = new();

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => Logger;

    public void Dispose()
    {
    }
}

internal static class TestManifests
{
    public static AtlasAppManifest Ttdv2(AtlasAccessMode mode = AtlasAccessMode.Enforce) => new()
    {
        Code = "TTDV2",
        Form = "TTDV2.json",
        Table = "TTDV2.General",
        AuditTable = "TTDV2.AuditLog",
        AccessMode = mode
    };
}
