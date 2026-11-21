using System.Globalization;
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Attachments;

/// <summary>
/// The limits and the checks of the files of a file picker: types, sizes, names and file signatures.
/// </summary>
/// <remarks>
/// The client (see atlas-files.ts) uses the same limits and texts. The server checks each file again.
/// </remarks>
public static class AtlasAttachmentRules
{
    /// <summary>The control type of a file picker.</summary>
    public const string FilePickerType = "filepicker";

    /// <summary>The separator between the control name and the file name in the name of a file part.</summary>
    public const string PartSeparator = "#$#";

    /// <summary>The platform limit for one file, in MB.</summary>
    public const int MaxFileMb = 10;

    /// <summary>The platform limit for the new files of one save, in MB.</summary>
    public const int MaxSaveMb = 25;

    /// <summary>The longest file name.</summary>
    public const int MaxNameLength = 255;

    /// <summary>The file types that the platform accepts. A control can only make the list shorter.</summary>
    public static IReadOnlyList<string> DefaultAccept { get; } = [".pdf", ".docx", ".xlsx", ".csv", ".txt", ".msg", ".png", ".jpg"];

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".csv"] = "text/csv",
        [".txt"] = "text/plain",
        [".msg"] = "application/vnd.ms-outlook",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg"
    };

    // The first bytes of each type that has a signature. A type with no entry has no signature check.
    private static readonly Dictionary<string, byte[]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "%PDF"u8.ToArray(),
        [".docx"] = [0x50, 0x4B, 0x03, 0x04],
        [".xlsx"] = [0x50, 0x4B, 0x03, 0x04],
        [".msg"] = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1],
        [".png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        [".jpg"] = [0xFF, 0xD8, 0xFF]
    };

    /// <summary>Returns true for a file picker control.</summary>
    public static bool IsFilePicker(CPApplicationControl? control) =>
        string.Equals(control?.Type, FilePickerType, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the file types that the control accepts: its Accept list, or the platform list.</summary>
    public static IReadOnlyList<string> AcceptOf(CPApplicationControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.Accept is { Count: > 0 } accept
            ? [.. accept.Select(Extension).Where(x => DefaultAccept.Contains(x, StringComparer.OrdinalIgnoreCase))]
            : DefaultAccept;
    }

    /// <summary>Returns the largest file of the control in bytes: its MaxSizeMb, at most the platform limit.</summary>
    public static long MaxBytesOf(CPApplicationControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        int mb = control.MaxSizeMb is > 0 and <= MaxFileMb ? control.MaxSizeMb.Value : MaxFileMb;
        return mb * 1024L * 1024L;
    }

    /// <summary>Returns the largest file of the control in MB.</summary>
    public static int MaxMbOf(CPApplicationControl control) => (int)(MaxBytesOf(control) / (1024L * 1024L));

    /// <summary>
    /// Splits the name of a file part into the control name and the file name.
    /// </summary>
    /// <remarks>
    /// The file name is the part after the last / or \, so it is never a path.
    /// </remarks>
    /// <returns>False when the part has no separator.</returns>
    public static bool TrySplitPartName(string? partName, out string controlName, out string fileName)
    {
        controlName = "";
        fileName = "";
        int index = partName?.IndexOf(PartSeparator, StringComparison.Ordinal) ?? -1;
        if (index <= 0)
            return false;

        controlName = partName![..index].Trim();
        string name = partName[(index + PartSeparator.Length)..];
        int slash = name.LastIndexOfAny(['/', '\\']);
        fileName = (slash >= 0 ? name[(slash + 1)..] : name).Trim();
        return true;
    }

    /// <summary>
    /// Returns true when a name has a control character (for example CR, LF, tab or NUL) or a text direction character.
    /// </summary>
    /// <remarks>
    /// Such a character can make a name show as a different name, or write a false line in a log or a list.
    /// </remarks>
    public static bool HasBadCharacter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Any(x => char.IsControl(x) || x is >= '‪' and <= '‮' or >= '⁦' and <= '⁩');
    }

    /// <summary>Returns a name for a log entry: each control or text direction character becomes "?".</summary>
    public static string LogText(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return HasBadCharacter(name) ? string.Concat(name.Select(x => HasBadCharacter(x.ToString()) ? '?' : x)) : name;
    }

    /// <summary>Returns the extension of a file name or an Accept item in lowercase, with the dot.</summary>
    public static string Extension(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string trimmed = name.Trim();
        string extension = trimmed.StartsWith('.') && trimmed.IndexOf('.', 1) < 0 ? trimmed : Path.GetExtension(trimmed);
        return extension.ToLowerInvariant();
    }

    /// <summary>Returns the content type of a file name, from its extension. An unknown type is application/octet-stream.</summary>
    public static string ContentTypeOf(string fileName) =>
        ContentTypes.TryGetValue(Extension(fileName), out string? type) ? type : "application/octet-stream";

    /// <summary>Returns true when the content starts with the signature of its type, or the type has no signature.</summary>
    public static bool SignatureMatches(string fileName, ReadOnlySpan<byte> content) =>
        !Signatures.TryGetValue(Extension(fileName), out byte[]? signature) || content.StartsWith(signature);

    /// <summary>Returns the text of the type error, the same as the client text.</summary>
    public static string TypeText(string label, string fileName, IReadOnlyList<string> accept) =>
        $"{label}: the type of the file '{fileName}' is not permitted. Permitted types: {string.Join(", ", accept)}.";

    /// <summary>Returns the text of the size error, the same as the client text.</summary>
    public static string SizeText(string label, string fileName, int maxMb) =>
        $"{label}: the file '{fileName}' is larger than {maxMb.ToString(CultureInfo.InvariantCulture)} MB.";

    /// <summary>Returns the text of the empty file error, the same as the client text.</summary>
    public static string EmptyText(string label, string fileName) => $"{label}: the file '{fileName}' is empty.";

    /// <summary>Returns the text of the count error, the same as the client text.</summary>
    public static string CountText(string label, int maxFiles) =>
        maxFiles == 1 ? $"{label} can have only one file." : $"{label} can have at most {maxFiles.ToString(CultureInfo.InvariantCulture)} files.";

    /// <summary>The text of the error for too many MB in one save, the same as the client text.</summary>
    public static string SaveSizeText => $"Save at most {MaxSaveMb.ToString(CultureInfo.InvariantCulture)} MB at one time.";
}
