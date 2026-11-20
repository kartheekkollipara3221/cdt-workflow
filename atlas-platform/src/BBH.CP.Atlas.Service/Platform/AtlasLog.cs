using Microsoft.Extensions.Logging;

// Atlas.StartupCheck (41020) and Atlas.AppNotAvailable (41022) each have two templates. The event IDs are stable.
#pragma warning disable SYSLIB1025

namespace BBH.CP.Atlas.Service.Platform;

/// <summary>
/// The log events of the platform, with source-generated logging (CA1848).
/// </summary>
/// <remarks>
/// The event IDs, names and message templates are stable. Operators use them to find events.
/// The public EventId fields (for example ConfigSectionService.FieldIgnored) have the same values.
/// </remarks>
public static partial class AtlasLog
{
    /// <summary>Logs that the access check refused an operation for a user.</summary>
    [LoggerMessage(EventId = 41001, EventName = "Atlas.AccessDenied", Level = LogLevel.Warning,
        Message = "Atlas.AccessDenied LanId={LanId} App={App} Record={Record} Operation={Operation} Mode={Mode} Reason={Reason}")]
    public static partial void AccessDenied(ILogger logger, string lanId, string app, int record, string operation, string mode, string reason);

    /// <summary>Logs that the platform ignored a field of the config.</summary>
    [LoggerMessage(EventId = 41010, EventName = "Atlas.FieldIgnored", Level = LogLevel.Information,
        Message = "Atlas.FieldIgnored App={App} Field={Field} Reason={Reason}")]
    public static partial void FieldIgnored(ILogger logger, string app, string field, string reason);

    /// <summary>Logs that a save failed.</summary>
    [LoggerMessage(EventId = 41011, EventName = "Atlas.SaveFailed", Level = LogLevel.Error,
        Message = "Atlas.SaveFailed App={App} LanId={LanId}")]
    public static partial void SaveFailed(ILogger logger, Exception exception, string app, string lanId);

    /// <summary>Logs that an after-save step failed. The save itself is complete.</summary>
    [LoggerMessage(EventId = 41012, EventName = "Atlas.AfterSaveFailed", Level = LogLevel.Error,
        Message = "Atlas.AfterSaveFailed App={App} Record={Record} LanId={LanId}")]
    public static partial void AfterSaveFailed(ILogger logger, Exception exception, string app, object? record, string lanId);

    /// <summary>Logs that the server refused the value of a field.</summary>
    [LoggerMessage(EventId = 41013, EventName = "Atlas.FieldRefused", Level = LogLevel.Warning,
        Message = "Atlas.FieldRefused LanId={LanId} App={App} Record={Record} Field={Field} Code={Code}")]
    public static partial void FieldRefused(ILogger logger, string lanId, string app, int record, string field, string code);

    /// <summary>Logs that the server ignored the audit data that the client sent.</summary>
    [LoggerMessage(EventId = 41014, EventName = "Atlas.ClientAuditIgnored", Level = LogLevel.Debug,
        Message = "Atlas.ClientAuditIgnored App={App} Mode={Mode}")]
    public static partial void ClientAuditIgnored(ILogger logger, string app, string mode);

    /// <summary>Logs that a dashboard tab reached its maximum number of rows.</summary>
    [LoggerMessage(EventId = 41015, EventName = "Atlas.TabRowLimit", Level = LogLevel.Warning,
        Message = "Atlas.TabRowLimit App={App} Tab={Tab} MaxRows={MaxRows}")]
    public static partial void TabRowLimit(ILogger logger, string app, string tab, int maxRows);

    /// <summary>Logs one item of the start-up check at the given level.</summary>
    [LoggerMessage(EventId = 41020, EventName = "Atlas.StartupCheck", Message = "Atlas.StartupCheck {Item}")]
    public static partial void StartupCheckItem(ILogger logger, LogLevel level, string item);

    /// <summary>Logs the result of the start-up check.</summary>
    [LoggerMessage(EventId = 41020, EventName = "Atlas.StartupCheck", Level = LogLevel.Information,
        Message = "Atlas.Started Environment={Environment} Modules={Modules} Tables={Tables} Errors={Errors} Warnings={Warnings}")]
    public static partial void Started(ILogger logger, string environment, string modules, int tables, int errors, int warnings);

    /// <summary>Logs that a config file is not valid.</summary>
    [LoggerMessage(EventId = 41021, EventName = "Atlas.ConfigInvalid", Level = LogLevel.Error,
        Message = "Atlas.ConfigInvalid File={File} Error={Error}")]
    public static partial void ConfigInvalid(ILogger logger, string file, string error);

    /// <summary>Logs a request for an app whose table metadata did not load at start-up.</summary>
    [LoggerMessage(EventId = 41022, EventName = "Atlas.AppNotAvailable", Level = LogLevel.Warning,
        Message = "Atlas.AppNotAvailable App={App} Table={Table} Reason=NotLoadedAtStartup")]
    public static partial void AppNotLoaded(ILogger logger, string app, string table);

    /// <summary>Logs a request for an app that the start-up check disabled.</summary>
    [LoggerMessage(EventId = 41022, EventName = "Atlas.AppNotAvailable", Level = LogLevel.Warning,
        Message = "Atlas.AppNotAvailable App={App} Reason=StartupCheck")]
    public static partial void AppDisabledByCheck(ILogger logger, string app);

    /// <summary>Logs that the start-up check disabled an app.</summary>
    [LoggerMessage(EventId = 41023, EventName = "Atlas.AppDisabled", Level = LogLevel.Critical,
        Message = "Atlas.AppDisabled App={App} Reason={Reason}")]
    public static partial void AppDisabled(ILogger logger, string app, string reason);

    /// <summary>Logs a defect in the config of an app that a request found.</summary>
    [LoggerMessage(EventId = 41024, EventName = "Atlas.ConfigError", Level = LogLevel.Error,
        Message = "Atlas.ConfigError Path={Path} Detail={Detail}")]
    public static partial void ConfigError(ILogger logger, Exception exception, string path, string detail);

    /// <summary>Logs a name of a recipient field that does not match exactly one user with an email. The email goes to the others.</summary>
    [LoggerMessage(EventId = 41025, EventName = "Atlas.RecipientSkipped", Level = LogLevel.Warning,
        Message = "Atlas.RecipientSkipped App={App} RecordId={RecordId} Key={Key} Name={Name} Reason={Reason}")]
    public static partial void RecipientSkipped(ILogger logger, string app, int recordId, string key, string name, string reason);

    /// <summary>Logs an engine action that failed, or a state difference. The support team acts on it.</summary>
    [LoggerMessage(EventId = 41026, EventName = "Atlas.WorkflowStepFailed", Level = LogLevel.Error,
        Message = "Atlas.WorkflowStepFailed App={App} RecordId={RecordId} Instance={Instance} Action={Action} Reason={Reason}")]
    public static partial void WorkflowStepFailed(ILogger logger, Exception? exception, string app, int recordId, string instance, string action, string reason);

    /// <summary>Logs the state check on load: the engine state was written to the record, or the check failed.</summary>
    [LoggerMessage(EventId = 41027, EventName = "Atlas.WorkflowSync", Level = LogLevel.Warning,
        Message = "Atlas.WorkflowSync App={App} RecordId={RecordId} Instance={Instance} RecordState={RecordState} EngineState={EngineState} Outcome={Outcome}")]
    public static partial void WorkflowSync(ILogger logger, Exception? exception, string app, int recordId, string instance, string recordState, string engineState, string outcome);

    /// <summary>Logs an email that was not sent after a submit. The engine outbox sends it again.</summary>
    [LoggerMessage(EventId = 41028, EventName = "Atlas.NotificationDispatchFailed", Level = LogLevel.Warning,
        Message = "Atlas.NotificationDispatchFailed App={App} RecordId={RecordId} Instance={Instance} Reason={Reason}")]
    public static partial void NotificationDispatchFailed(ILogger logger, Exception? exception, string app, int recordId, string instance, string reason);

    /// <summary>Logs a file of a save that the server refused, for example a wrong type or a control that is disabled.</summary>
    [LoggerMessage(EventId = 41029, EventName = "Atlas.AttachmentRefused", Level = LogLevel.Warning,
        Message = "Atlas.AttachmentRefused App={App} User={User} RecordId={RecordId} Control={Control} File={File} Reason={Reason}")]
    public static partial void AttachmentRefused(ILogger logger, string app, string user, int recordId, string control, string file, string reason);
}
