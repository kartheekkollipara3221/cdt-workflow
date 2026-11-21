using System.Globalization;
using System.Text.Json;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.Service.Platform.Audit;

/// <summary>The names of the audit events.</summary>
public static class AtlasAuditEvents
{
    /// <summary>A new record.</summary>
    public const string Create = "Create";
    /// <summary>A change to a record.</summary>
    public const string Update = "Update";
    /// <summary>A removed record.</summary>
    public const string Delete = "Delete";
    /// <summary>A new file of a record. The change names the file picker and gives the file name as Value.</summary>
    public const string AttachmentAdd = "AttachmentAdd";
    /// <summary>A removed file of a record. The change names the file picker and gives the file name as Old.</summary>
    public const string AttachmentRemove = "AttachmentRemove";
    /// <summary>The state check on load wrote the engine state to the record.</summary>
    public const string WorkflowSync = "WorkflowSync";
}

/// <summary>One changed field. Old and Value are wire values; "***" for a field with Audit = false.</summary>
public sealed record AtlasAuditChange(string Name, string? Label, string? Old, string? Value);

/// <summary>
/// Writes and reads the server audit entries of a record.
/// </summary>
/// <remarks>
/// The writer adds the entry in the transaction of the save. LogMessage is a JSON list.
/// The first item is <c>{"Name":"_Event","Value":"Create|Update|Delete"}</c>. Then there is one item for each field.
/// </remarks>
public sealed class AtlasAuditWriter(AtlasPlatformContext context, IAtlasUserContext user, TimeProvider time)
{
    /// <summary>The name of the LogMessage item that holds the audit event.</summary>
    public const string EventName = "_Event";

    /// <summary>The audit column that holds the ID of the record.</summary>
    public const string ReferenceIdColumn = "ReferenceID";

    /// <summary>The audit column that holds the app code of the record.</summary>
    public const string ReferenceTypeColumn = "ReferenceType";

    /// <summary>Adds the entry to the platform context. The caller saves the context.</summary>
    /// <returns>False when the event is an Update with no change. Then the method adds nothing.</returns>
    public bool Add(AtlasAppManifest manifest, int refId, string auditEvent, IReadOnlyList<AtlasAuditChange> changes) =>
        AddEntry(manifest, refId, auditEvent, changes) != null;

    /// <summary>Adds the entry to the platform context and returns it. The caller saves the context.</summary>
    /// <param name="always">True adds an Update entry also when it has no change, because the caller appends changes later.</param>
    /// <returns>The tracked entry, or null when the event is an Update with no change and <paramref name="always"/> is false.</returns>
    public Dictionary<string, object?>? AddEntry(AtlasAppManifest manifest, int refId, string auditEvent,
        IReadOnlyList<AtlasAuditChange> changes, bool always = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(changes);
        if (auditEvent == AtlasAuditEvents.Update && changes.Count == 0 && !always)
            return null;

        AtlasTable table = AuditTable(manifest);
        List<Dictionary<string, string?>> message = [new() { ["Name"] = EventName, ["Value"] = auditEvent }];
        message.AddRange(changes.Select(Item));

        Dictionary<string, object?> entry = table.NewRecord();
        Set(entry, table, ReferenceIdColumn, refId);
        Set(entry, table, ReferenceTypeColumn, manifest.Code);
        Set(entry, table, "CreatedBy", user.DisplayName);
        Set(entry, table, "CreatedByLanId", user.LanId);
        Set(entry, table, "CreatedDate", manifest.AuditTime == AtlasAuditTime.Utc
            ? time.GetUtcNow().UtcDateTime
            : time.GetLocalNow().DateTime);
        Set(entry, table, "LogMessage", JsonSerializer.Serialize(message));
        context.Records(table.FullName).Add(entry);
        return entry;
    }

    /// <summary>Adds changes to the end of an entry that <see cref="AddEntry"/> returned. The caller saves the context.</summary>
    public void Append(AtlasAppManifest manifest, Dictionary<string, object?> entry, IReadOnlyList<AtlasAuditChange> changes)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0 || AuditTable(manifest).Find("LogMessage") is not { } column)
            return;

        List<Dictionary<string, string?>> message =
            JsonSerializer.Deserialize<List<Dictionary<string, string?>>>(entry[column.Name] as string ?? "[]") ?? [];
        message.AddRange(changes.Select(Item));
        entry[column.Name] = JsonSerializer.Serialize(message);
    }

    private static Dictionary<string, string?> Item(AtlasAuditChange change) => new()
    {
        ["Name"] = change.Name,
        ["Label"] = change.Label,
        ["Old"] = change.Old,
        ["Value"] = change.Value
    };

    /// <summary>Returns the entries of one record, oldest first (the same order as the target History Log).</summary>
    public List<CPAuditLog> Read(AtlasAppManifest manifest, int refId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        AtlasTable table = AuditTable(manifest);

        IQueryable<Dictionary<string, object?>> query = context.Records(table.FullName).AsNoTracking()
            .Where(x => EF.Property<int?>(x, ReferenceIdColumn) == refId);
        if (manifest.AuditReadRefTypes() is { } refTypes && table.HasColumn(ReferenceTypeColumn))
        {
            // In a shared audit table, record 42 of another app is not this record.
            List<string> values = [.. refTypes];
            query = query.Where(x => values.Contains(EF.Property<string>(x, ReferenceTypeColumn)));
        }

        List<Dictionary<string, object?>> rows = [.. query
            .OrderBy(x => EF.Property<DateTime?>(x, "CreatedDate"))
            .ThenBy(x => EF.Property<int>(x, AtlasTable.KeyColumnName))];

        return [.. rows.Select(row => ToDto(manifest, refId, row))];
    }

    private static CPAuditLog ToDto(AtlasAppManifest manifest, int refId, Dictionary<string, object?> row)
    {
        DateTime? created = row.GetValueOrDefault("CreatedDate") as DateTime?;
        string? text = row.GetValueOrDefault("LogMessage") as string;
        JsonElement? message = null;
        string? auditEvent = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                message = document.RootElement.Clone();
                if (message.Value.ValueKind == JsonValueKind.Array)
                {
                    auditEvent = message.Value.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.Object
                            && x.TryGetProperty("Name", out JsonElement name) && name.GetString() == EventName)
                        .Select(x => x.TryGetProperty("Value", out JsonElement value) ? value.GetString() : null)
                        .FirstOrDefault();
                }
            }
            catch (JsonException)
            {
                message = null;
            }
        }

        return new CPAuditLog
        {
            AppCode = manifest.Code,
            RefID = refId,
            CreatedBy = row.GetValueOrDefault("CreatedBy") as string ?? "",
            // The same format as the target GetAuditLog. The History Log shows this text.
            CreatedDate = created?.ToString("MM-dd-yyyy HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
            CreatedDateUtc = created == null || manifest.AuditTime != AtlasAuditTime.Utc
                ? null
                : created.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            LogMessage = message is null ? text : message.Value,
            Event = auditEvent,
            Source = "App"
        };
    }

    /// <summary>
    /// Returns the LogMessage as JSON text, in the format that the target sends.
    /// </summary>
    /// <remarks>
    /// The target History Log reads the text with JSON.parse and shows "Name - Value".
    /// The _Event item is not in the text, because CPAuditLog.Event holds it. Old and Label stay in each item.
    /// A message that is not a JSON list is returned with no change.
    /// </remarks>
    public static object? ToText(object? logMessage)
    {
        if (logMessage is not JsonElement { ValueKind: JsonValueKind.Array } items)
            return logMessage is JsonElement other ? other.GetRawText() : logMessage;
        return JsonSerializer.Serialize(items.EnumerateArray()
            .Where(x => !(x.ValueKind == JsonValueKind.Object && x.TryGetProperty("Name", out JsonElement name)
                && name.ValueKind == JsonValueKind.String && name.GetString() == EventName)));
    }

    private AtlasTable AuditTable(AtlasAppManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.AuditTable))
            throw new InvalidOperationException($"App '{manifest.Code}' has no AuditTable in its manifest.");
        return context.Registry.Get(manifest.AuditTable);
    }

    private static void Set(Dictionary<string, object?> entry, AtlasTable table, string column, object? value)
    {
        if (table.HasColumn(column))
            entry[table.Find(column)!.Name] = value;
    }
}
