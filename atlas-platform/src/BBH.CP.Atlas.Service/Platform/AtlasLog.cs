using Microsoft.Extensions.Logging;

// Atlas.StartupCheck (41020) and Atlas.AppNotAvailable (41022) each have two templates, as before: the IDs stay the same.
#pragma warning disable SYSLIB1025

namespace BBH.CP.Atlas.Service.Platform;

/// <summary>
/// All platform log events in one place (review SV-12), with source-generated logging (CA1848). The event IDs, names
/// and message templates are the ones that INTEGRATION.md section 4 lists for operators; the public EventId fields
/// (for example ConfigSectionService.FieldIgnored) keep the same values.
/// </summary>
public static partial class AtlasLog
{
    [LoggerMessage(EventId = 41001, EventName = "Atlas.AccessDenied", Level = LogLevel.Warning,
        Message = "Atlas.AccessDenied LanId={LanId} App={App} Record={Record} Operation={Operation} Mode={Mode} Reason={Reason}")]
    public static partial void AccessDenied(ILogger logger, string lanId, string app, int record, string operation, string mode, string reason);

    [LoggerMessage(EventId = 41010, EventName = "Atlas.FieldIgnored", Level = LogLevel.Information,
        Message = "Atlas.FieldIgnored App={App} Field={Field} Reason={Reason}")]
    public static partial void FieldIgnored(ILogger logger, string app, string field, string reason);

    [LoggerMessage(EventId = 41011, EventName = "Atlas.SaveFailed", Level = LogLevel.Error,
        Message = "Atlas.SaveFailed App={App} LanId={LanId}")]
    public static partial void SaveFailed(ILogger logger, Exception exception, string app, string lanId);

    [LoggerMessage(EventId = 41012, EventName = "Atlas.AfterSaveFailed", Level = LogLevel.Error,
        Message = "Atlas.AfterSaveFailed App={App} Record={Record} LanId={LanId}")]
    public static partial void AfterSaveFailed(ILogger logger, Exception exception, string app, object? record, string lanId);

    [LoggerMessage(EventId = 41013, EventName = "Atlas.FieldRefused", Level = LogLevel.Warning,
        Message = "Atlas.FieldRefused LanId={LanId} App={App} Record={Record} Field={Field} Code={Code}")]
    public static partial void FieldRefused(ILogger logger, string lanId, string app, int record, string field, string code);

    [LoggerMessage(EventId = 41014, EventName = "Atlas.ClientAuditIgnored", Level = LogLevel.Debug,
        Message = "Atlas.ClientAuditIgnored App={App} Mode={Mode}")]
    public static partial void ClientAuditIgnored(ILogger logger, string app, string mode);

    [LoggerMessage(EventId = 41020, EventName = "Atlas.StartupCheck", Message = "Atlas.StartupCheck {Item}")]
    public static partial void StartupCheckItem(ILogger logger, LogLevel level, string item);

    [LoggerMessage(EventId = 41020, EventName = "Atlas.StartupCheck", Level = LogLevel.Information,
        Message = "Atlas.Started Environment={Environment} Modules={Modules} Tables={Tables} Errors={Errors} Warnings={Warnings}")]
    public static partial void Started(ILogger logger, string environment, string modules, int tables, int errors, int warnings);

    [LoggerMessage(EventId = 41021, EventName = "Atlas.ConfigInvalid", Level = LogLevel.Error,
        Message = "Atlas.ConfigInvalid File={File} Error={Error}")]
    public static partial void ConfigInvalid(ILogger logger, string file, string error);

    [LoggerMessage(EventId = 41022, EventName = "Atlas.AppNotAvailable", Level = LogLevel.Warning,
        Message = "Atlas.AppNotAvailable App={App} Table={Table} Reason=NotLoadedAtStartup")]
    public static partial void AppNotLoaded(ILogger logger, string app, string table);

    [LoggerMessage(EventId = 41022, EventName = "Atlas.AppNotAvailable", Level = LogLevel.Warning,
        Message = "Atlas.AppNotAvailable App={App} Reason=StartupCheck")]
    public static partial void AppDisabledByCheck(ILogger logger, string app);

    [LoggerMessage(EventId = 41023, EventName = "Atlas.AppDisabled", Level = LogLevel.Critical,
        Message = "Atlas.AppDisabled App={App} Reason={Reason}")]
    public static partial void AppDisabled(ILogger logger, string app, string reason);

    [LoggerMessage(EventId = 41024, EventName = "Atlas.ConfigError", Level = LogLevel.Error,
        Message = "Atlas.ConfigError Path={Path} Detail={Detail}")]
    public static partial void ConfigError(ILogger logger, Exception exception, string path, string detail);
}
