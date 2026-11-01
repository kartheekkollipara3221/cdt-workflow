using System.Globalization;
using System.Text.Json;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.Service.Platform.Audit;

public static class AtlasAuditEvents
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
}

/// <summary>One changed field. Old and Value are wire values; "***" for a field with Audit = false.</summary>
public sealed record AtlasAuditChange(string Name, string? Label, string? Old, string? Value);

/// <summary>
/// The server audit (spec 09, 4.1-4.5). The entry is written in the transaction of the save. LogMessage is a JSON
/// list: the first item is <c>{"Name":"_Event","Value":"Create|Update|Delete"}</c>, then one item for each field.
/// </summary>
public sealed class AtlasAuditWriter(AtlasPlatformContext context, IAtlasUserContext user, TimeProvider time)
{
    public const string EventName = "_Event";

    /// <summary>Adds the entry to the platform context (the caller saves). An Update with no change adds nothing.</summary>
    public bool Add(AtlasAppManifest manifest, int refId, string auditEvent, IReadOnlyList<AtlasAuditChange> changes)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(changes);
        if (auditEvent == AtlasAuditEvents.Update && changes.Count == 0)
            return false;

        AtlasTable table = AuditTable(manifest);
        List<Dictionary<string, string?>> message = [new() { ["Name"] = EventName, ["Value"] = auditEvent }];
        message.AddRange(changes.Select(x => new Dictionary<string, string?>
        {
            ["Name"] = x.Name,
            ["Label"] = x.Label,
            ["Old"] = x.Old,
            ["Value"] = x.Value
        }));

        Dictionary<string, object?> entry = table.NewRecord();
        Set(entry, table, "RefID", refId);
        Set(entry, table, "RefType", manifest.Code);
        Set(entry, table, "CreatedBy", user.DisplayName);
        Set(entry, table, "CreatedByLanId", user.LanId);
        Set(entry, table, "CreatedDate", manifest.AuditTime == AtlasAuditTime.Utc
            ? time.GetUtcNow().UtcDateTime
            : time.GetLocalNow().DateTime);
        Set(entry, table, "LogMessage", JsonSerializer.Serialize(message));
        context.Records(table.FullName).Add(entry);
        return true;
    }

    /// <summary>The entries of one record, oldest first (as the target History Log).</summary>
    public List<CPAuditLog> Read(AtlasAppManifest manifest, int refId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        AtlasTable table = AuditTable(manifest);

        IQueryable<Dictionary<string, object?>> query = context.Records(table.FullName).AsNoTracking()
            .Where(x => EF.Property<int?>(x, "RefID") == refId);
        if (manifest.AuditReadRefTypes() is { } refTypes && table.HasColumn("RefType"))
        {
            // Review DA-03: in a shared audit table, record 42 of another app is not this record.
            List<string> values = [.. refTypes];
            query = query.Where(x => values.Contains(EF.Property<string>(x, "RefType")));
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
            // As the target GetAuditLog: the History Log shows this text.
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
    /// The LogMessage as the target sends it: JSON text that the target History Log reads with JSON.parse and shows as
    /// "Name - Value". The _Event item is left out (it is in CPAuditLog.Event). Old and Label stay in each item.
    /// A message that is not a JSON list is given as it is.
    /// </summary>
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
