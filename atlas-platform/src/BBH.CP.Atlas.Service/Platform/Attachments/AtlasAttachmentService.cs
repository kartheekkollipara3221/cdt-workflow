using System.Data;
using System.Data.Common;
using System.IO.Compression;
using System.Text;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Dashboard;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Service.Platform.Attachments;

/// <summary>One stored file of a record, with no content.</summary>
public sealed record AtlasAttachmentInfo(
    int Id, int RecordId, string ControlName, string FileName, long? SizeBytes, string? CreatedBy, string? CreatedByLanId, DateTime? CreatedDate);

/// <summary>A new file for a save: the control, the cleaned file name and the content before compression.</summary>
public sealed record AtlasNewAttachment(string ControlName, string FileName, byte[] Content);

/// <summary>The content of a file for a download.</summary>
public sealed record AtlasAttachmentContent(string FileName, string ContentType, byte[] Content);

/// <summary>Opens a stored file of a platform app for a download. A module of an app with attachments has it.</summary>
public interface IAtlasAttachmentSource
{
    /// <summary>Returns the file with this ID, after the access check of its record.</summary>
    /// <exception cref="AtlasProblemException">
    /// 404 for an unknown or removed file, a removed record or a file that a rule hides; 403 with no View access.
    /// </exception>
    AtlasAttachmentContent OpenAttachment(int id);
}

/// <summary>
/// Lists, adds, removes and reads the stored files of the records of an app.
/// </summary>
/// <remarks>
/// The methods do no access check; the caller checks the access to the record first.
/// Add and Remove use the current transaction of the context, so they commit or roll back with the record.
/// </remarks>
public interface IAtlasAttachmentService
{
    /// <summary>Returns the files of a record that are not removed, oldest first. The content is not read.</summary>
    IReadOnlyList<AtlasAttachmentInfo> List(AtlasAttachmentTable table, int recordId);

    /// <summary>Returns one file that is not removed, or null. The content is not read.</summary>
    AtlasAttachmentInfo? Find(AtlasAttachmentTable table, int id);

    /// <summary>Stores a new file of a record, compressed. Returns the ID of the file.</summary>
    /// <param name="now">The time of the save, with the SystemTime of the app.</param>
    int Add(AtlasAttachmentTable table, int recordId, AtlasNewAttachment file, DateTime now);

    /// <summary>Marks files of a record as removed. Returns the number of files that the call removed.</summary>
    /// <remarks>A file of another record, or a file that is already removed, is not changed.</remarks>
    int Remove(AtlasAttachmentTable table, int recordId, IReadOnlyCollection<int> ids, DateTime now);

    /// <summary>Returns the content of a file that is not removed, not compressed, or null.</summary>
    byte[]? ReadContent(AtlasAttachmentTable table, int id);
}

/// <summary>
/// Stores the files in the attachment table of the app, compressed with GZip, as the current CDT files.
/// </summary>
/// <remarks>
/// The names in the SQL come from the table metadata, never from a request. No file is written to a disk.
/// </remarks>
public sealed class AtlasAttachmentService(CPSharepointContext context, IAtlasUserContext user) : IAtlasAttachmentService
{
    private static readonly byte[] GZipMagic = [0x1F, 0x8B];

    /// <inheritdoc/>
    public IReadOnlyList<AtlasAttachmentInfo> List(AtlasAttachmentTable table, int recordId)
    {
        ArgumentNullException.ThrowIfNull(table);
        string sql = SelectInfo(table) + $" WHERE {Q(table.RecordId)} = @record AND {NotDeleted(table)} ORDER BY {Q(table.Id)};";
        return AtlasSql.Query(context, sql, ReadInfo, new SqlParameter("@record", recordId));
    }

    /// <inheritdoc/>
    public AtlasAttachmentInfo? Find(AtlasAttachmentTable table, int id)
    {
        ArgumentNullException.ThrowIfNull(table);
        string sql = SelectInfo(table) + $" WHERE {Q(table.Id)} = @id AND {NotDeleted(table)};";
        return AtlasSql.Query(context, sql, ReadInfo, new SqlParameter("@id", id)).SingleOrDefault();
    }

    /// <inheritdoc/>
    public int Add(AtlasAttachmentTable table, int recordId, AtlasNewAttachment file, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(file);
        List<(AtlasColumn Column, SqlParameter Value)> values =
        [
            (table.RecordId, new SqlParameter("@record", recordId)),
            (table.ControlName, Text("@control", file.ControlName)),
            (table.FileName, Text("@name", file.FileName)),
            (table.FileContent, new SqlParameter("@content", SqlDbType.VarBinary, -1) { Value = Compress(file.Content) }),
            (table.IsDeleted, new SqlParameter("@deleted", false))
        ];
        Optional(values, table.ContentType, "@type", AtlasAttachmentRules.ContentTypeOf(file.FileName));
        Optional(values, table.SizeBytes, "@size", (long)file.Content.Length);
        Optional(values, table.CreatedBy, "@createdBy", table.CreatedBy?.Cut(user.DisplayName) ?? "");
        Optional(values, table.CreatedByLanId, "@createdByLanId", user.LanId);
        Optional(values, table.CreatedDate, "@createdDate", now);
        Optional(values, table.ModifiedBy, "@modifiedBy", table.ModifiedBy?.Cut(user.DisplayName) ?? "");
        Optional(values, table.ModifiedByLanId, "@modifiedByLanId", user.LanId);
        Optional(values, table.ModifiedDate, "@modifiedDate", now);

        string sql = $"INSERT INTO {Name(table)} ({string.Join(", ", values.Select(x => Q(x.Column)))}) "
            + $"VALUES ({string.Join(", ", values.Select(x => x.Value.ParameterName))}); SELECT CAST(SCOPE_IDENTITY() AS int);";
        return AtlasSql.Query(context, sql, reader => reader.GetInt32(0), [.. values.Select(x => x.Value)]).Single();
    }

    /// <inheritdoc/>
    public int Remove(AtlasAttachmentTable table, int recordId, IReadOnlyCollection<int> ids, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
            return 0;

        List<SqlParameter> parameters = [new("@record", recordId), new("@deleted", true)];
        List<string> sets = [$"{Q(table.IsDeleted)} = @deleted"];
        void Set(AtlasColumn? column, string name, object value)
        {
            if (column == null)
                return;
            sets.Add($"{Q(column)} = {name}");
            parameters.Add(value is string text ? Text(name, text) : new SqlParameter(name, value));
        }

        Set(table.ModifiedBy, "@modifiedBy", table.ModifiedBy?.Cut(user.DisplayName) ?? "");
        Set(table.ModifiedByLanId, "@modifiedByLanId", user.LanId);
        Set(table.ModifiedDate, "@modifiedDate", now);

        List<string> names = [];
        foreach (int id in ids.Distinct())
        {
            string name = "@id" + names.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            names.Add(name);
            parameters.Add(new SqlParameter(name, id));
        }

        string sql = $"UPDATE {Name(table)} SET {string.Join(", ", sets)} WHERE {Q(table.RecordId)} = @record "
            + $"AND {Q(table.Id)} IN ({string.Join(", ", names)}) AND {NotDeleted(table)}; SELECT @@ROWCOUNT;";
        return AtlasSql.Query(context, sql, reader => reader.GetInt32(0), [.. parameters]).Single();
    }

    /// <inheritdoc/>
    public byte[]? ReadContent(AtlasAttachmentTable table, int id)
    {
        ArgumentNullException.ThrowIfNull(table);
        string sql = $"SELECT {Q(table.FileContent)} FROM {Name(table)} WHERE {Q(table.Id)} = @id AND {NotDeleted(table)};";
        List<byte[]?> rows = AtlasSql.Query(context, sql, reader => reader.IsDBNull(0) ? null : (byte[])reader.GetValue(0), new SqlParameter("@id", id));
        return rows.Count == 0 ? null : Decompress(rows[0] ?? []);
    }

    /// <summary>Returns the content compressed with GZip.</summary>
    public static byte[] Compress(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using MemoryStream output = new();
        using (GZipStream zip = new(output, CompressionLevel.Optimal, leaveOpen: true))
            zip.Write(content);
        return output.ToArray();
    }

    /// <summary>
    /// Returns the content not compressed.
    /// </summary>
    /// <remarks>
    /// Content with no GZip header is returned as it is, so a file that another tool stored with no compression opens.
    /// </remarks>
    public static byte[] Decompress(byte[] stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (!stored.AsSpan().StartsWith(GZipMagic))
            return stored;
        using MemoryStream input = new(stored);
        using GZipStream zip = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        zip.CopyTo(output);
        return output.ToArray();
    }

    private static string SelectInfo(AtlasAttachmentTable table)
    {
        StringBuilder sql = new("SELECT ");
        sql.Append(string.Join(", ",
            Q(table.Id), Q(table.RecordId), Q(table.ControlName), Q(table.FileName),
            OrNull(table.SizeBytes), OrNull(table.CreatedBy), OrNull(table.CreatedByLanId), OrNull(table.CreatedDate)));
        return sql.Append(" FROM ").Append(Name(table)).ToString();
    }

    private static AtlasAttachmentInfo ReadInfo(DbDataReader reader) => new(
        reader.GetInt32(0),
        reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
        reader.NullableString(2) ?? "",
        reader.NullableString(3) ?? "",
        reader.IsDBNull(4) ? null : Convert.ToInt64(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture),
        reader.NullableString(5),
        reader.NullableString(6),
        reader.IsDBNull(7) ? null : reader.GetDateTime(7));

    private static void Optional(List<(AtlasColumn Column, SqlParameter Value)> values, AtlasColumn? column, string name, object value)
    {
        if (column != null)
            values.Add((column, value is string text ? Text(name, text) : new SqlParameter(name, value)));
    }

    private static SqlParameter Text(string name, string value) => new(name, SqlDbType.NVarChar, -1) { Value = value };

    private static string NotDeleted(AtlasAttachmentTable table) => $"ISNULL({Q(table.IsDeleted)}, 0) = 0";

    private static string OrNull(AtlasColumn? column) => column == null ? "NULL" : Q(column);

    private static string Q(AtlasColumn column) => AtlasTabQueryBuilder.Quote(column.Name);

    private static string Name(AtlasAttachmentTable table) =>
        AtlasTabQueryBuilder.Quote(table.Table.Schema) + "." + AtlasTabQueryBuilder.Quote(table.Table.Name);
}
