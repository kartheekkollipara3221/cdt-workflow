using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;

namespace BBH.CP.Atlas.Service.Platform.Attachments;

/// <summary>
/// The attachment table of an app, with the real column of each platform name.
/// </summary>
/// <remarks>
/// ID, RecordId, ControlName, FileName, FileContent and IsDeleted are necessary. The other columns are optional.
/// </remarks>
public sealed class AtlasAttachmentTable
{
    private AtlasAttachmentTable(AtlasTable table) => Table = table;

    /// <summary>The table.</summary>
    public AtlasTable Table { get; }

    /// <summary>The key column.</summary>
    public AtlasColumn Id { get; private set; } = null!;

    /// <summary>The column with the ID of the record.</summary>
    public AtlasColumn RecordId { get; private set; } = null!;

    /// <summary>The column with the name of the file picker control.</summary>
    public AtlasColumn ControlName { get; private set; } = null!;

    /// <summary>The column with the file name.</summary>
    public AtlasColumn FileName { get; private set; } = null!;

    /// <summary>The column with the compressed content.</summary>
    public AtlasColumn FileContent { get; private set; } = null!;

    /// <summary>The soft-delete column.</summary>
    public AtlasColumn IsDeleted { get; private set; } = null!;

    /// <summary>The optional column with the content type.</summary>
    public AtlasColumn? ContentType { get; private set; }

    /// <summary>The optional column with the size in bytes.</summary>
    public AtlasColumn? SizeBytes { get; private set; }

    /// <summary>The optional column with the display name of the person who added the file.</summary>
    public AtlasColumn? CreatedBy { get; private set; }

    /// <summary>The optional column with the LAN ID of the person who added the file.</summary>
    public AtlasColumn? CreatedByLanId { get; private set; }

    /// <summary>The optional column with the time when the file was added.</summary>
    public AtlasColumn? CreatedDate { get; private set; }

    /// <summary>The optional column with the display name of the person who changed the row last.</summary>
    public AtlasColumn? ModifiedBy { get; private set; }

    /// <summary>The optional column with the LAN ID of the person who changed the row last.</summary>
    public AtlasColumn? ModifiedByLanId { get; private set; }

    /// <summary>The optional column with the time of the last change of the row.</summary>
    public AtlasColumn? ModifiedDate { get; private set; }

    /// <summary>Returns the attachment table with its columns, and the errors of the columns.</summary>
    /// <returns>Null when a necessary column is missing or has a wrong type; the errors tell why.</returns>
    public static AtlasAttachmentTable? Resolve(AtlasTable table, AtlasAttachmentColumns? map, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(errors);
        int before = errors.Count;
        AtlasAttachmentTable result = new(table);

        AtlasColumn? Column(string platformName, string? realName, Func<AtlasColumn, bool> fits, string kind, bool required)
        {
            string name = string.IsNullOrWhiteSpace(realName) ? platformName : realName.Trim();
            AtlasColumn? column = table.Find(name);
            if (column == null)
            {
                // A name in the map is a statement that the column exists.
                if (required || !string.IsNullOrWhiteSpace(realName))
                    errors.Add($"the attachment table {table.FullName} has no column '{name}' ({platformName}).");
                return null;
            }

            if (!fits(column))
            {
                errors.Add($"the column '{column.Name}' ({platformName}) of {table.FullName} must be {kind} (it is {column.SqlType}).");
                return null;
            }

            return column;
        }

        static bool IsInt(AtlasColumn x) => AtlasSqlTypes.Normalize(x.SqlType) == "int";
        static bool IsText(AtlasColumn x) => AtlasSqlTypes.IsText(x.SqlType);
        static bool IsWhen(AtlasColumn x) => AtlasSqlTypes.Normalize(x.SqlType) is "datetime" or "datetime2" or "smalldatetime";

        result.Id = Column(AtlasTable.KeyColumnName, null, x => IsInt(x) && x.IsIdentity, "an int identity column", true)!;
        result.RecordId = Column("RecordId", map?.RecordId, IsInt, "an int column", true)!;
        result.ControlName = Column("ControlName", map?.ControlName, IsText, "a text column", true)!;
        result.FileName = Column("FileName", map?.FileName, IsText, "a text column", true)!;
        result.FileContent = Column("FileContent", map?.FileContent, x => AtlasSqlTypes.IsBinary(x.SqlType), "a varbinary column", true)!;
        result.IsDeleted = Column(AtlasTable.SoftDeleteColumnName, null, x => AtlasSqlTypes.Normalize(x.SqlType) == "bit", "a bit column", true)!;
        result.ContentType = Column("ContentType", map?.ContentType, IsText, "a text column", false);
        result.SizeBytes = Column("SizeBytes", map?.SizeBytes, x => AtlasSqlTypes.Normalize(x.SqlType) is "bigint" or "int", "a bigint or int column", false);
        result.CreatedBy = Column("CreatedBy", map?.CreatedBy, IsText, "a text column", false);
        result.CreatedByLanId = Column("CreatedByLanId", map?.CreatedByLanId, IsText, "a text column", false);
        result.CreatedDate = Column("CreatedDate", map?.CreatedDate, IsWhen, "a datetime column", false);
        result.ModifiedBy = Column("ModifiedBy", map?.ModifiedBy, IsText, "a text column", false);
        result.ModifiedByLanId = Column("ModifiedByLanId", map?.ModifiedByLanId, IsText, "a text column", false);
        result.ModifiedDate = Column("ModifiedDate", map?.ModifiedDate, IsWhen, "a datetime column", false);

        return errors.Count == before ? result : null;
    }
}
