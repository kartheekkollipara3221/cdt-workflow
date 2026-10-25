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
public interface IAtlasAuditWriter
{
    /// <summary>Adds the entry to the platform context (the caller saves). An Update with no change adds nothing.</summary>
    bool Add(AtlasAppManifest manifest, int refId, string auditEvent, IReadOnlyList<AtlasAuditChange> changes);

    /// <summary>The entries of one record, newest first.</summary>
    List<CPAuditLog> Read(AtlasAppManifest manifest, int refId);
}

public sealed class AtlasAuditWriter(AtlasPlatformContext context, IAtlasUserContext user, TimeProvider time) : IAtlasAuditWriter
{
    public const string EventName = "_Event";

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

    public List<CPAuditLog> Read(AtlasAppManifest manifest, int refId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        AtlasTable table = AuditTable(manifest);

        List<Dictionary<string, object?>> rows = [.. context.Records(table.FullName).AsNoTracking()
            .Where(x => EF.Property<int?>(x, "RefID") == refId)
            .OrderByDescending(x => EF.Property<DateTime?>(x, "CreatedDate"))
            .ThenByDescending(x => EF.Property<int>(x, AtlasTable.KeyColumnName))];

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
            CreatedDate = created?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
            CreatedDateUtc = created == null || manifest.AuditTime != AtlasAuditTime.Utc
                ? null
                : created.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            LogMessage = message is null ? text : message.Value,
            Event = auditEvent,
            Source = "App"
        };
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
