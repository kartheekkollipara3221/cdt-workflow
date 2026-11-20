namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// The real column names of an attachment table. Each property is optional; null = the platform name.
/// </summary>
/// <remarks>
/// The platform names are ID, RecordId, ControlName, FileName, ContentType, SizeBytes, FileContent, CreatedBy,
/// CreatedByLanId, CreatedDate, ModifiedBy, ModifiedByLanId, ModifiedDate and IsDeleted.
/// The ID and IsDeleted columns always have these names.
/// </remarks>
public sealed class AtlasAttachmentColumns
{
    /// <summary>The column with the ID of the record of the file.</summary>
    public string? RecordId { get; set; }

    /// <summary>The column with the name of the file picker control.</summary>
    public string? ControlName { get; set; }

    /// <summary>The column with the file name.</summary>
    public string? FileName { get; set; }

    /// <summary>The column with the content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>The column with the size in bytes before compression.</summary>
    public string? SizeBytes { get; set; }

    /// <summary>The column with the compressed content.</summary>
    public string? FileContent { get; set; }

    /// <summary>The column with the display name of the person who added the file.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>The column with the LAN ID of the person who added the file.</summary>
    public string? CreatedByLanId { get; set; }

    /// <summary>The column with the time when the file was added.</summary>
    public string? CreatedDate { get; set; }

    /// <summary>The column with the display name of the person who changed the file row last.</summary>
    public string? ModifiedBy { get; set; }

    /// <summary>The column with the LAN ID of the person who changed the file row last.</summary>
    public string? ModifiedByLanId { get; set; }

    /// <summary>The column with the time of the last change of the file row.</summary>
    public string? ModifiedDate { get; set; }
}
