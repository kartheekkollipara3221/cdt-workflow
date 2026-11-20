using System.Text.RegularExpressions;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>How the access check acts on a refusal: it only logs it, or it refuses the request.</summary>
public enum AtlasAccessMode { LogOnly, Enforce }

/// <summary>Which side writes the audit entries: the client or the server.</summary>
public enum AtlasAuditMode { Client, Server }

/// <summary>How the rule engine combines the rules that match: the first match, or the most permissive result.</summary>
public enum AtlasRuleMode { FirstMatch, MostPermissive }

/// <summary>The result when no rule matches: keep the field as it is, make it read-only, or hide it.</summary>
public enum AtlasRuleDefault { Keep, ReadOnly, Hidden }

/// <summary>The time zone of the stored times: UTC or the server local time.</summary>
public enum AtlasAuditTime { Utc, Local }

/// <summary>
/// The app manifest, the file &lt;Code&gt;.app.json. It names the other files and holds the settings of the app.
/// </summary>
/// <remarks>
/// Each environment can change the settings in AppSettings:Modules:&lt;Code&gt;:&lt;Setting&gt;.
/// </remarks>
public sealed class AtlasAppManifest
{
    /// <summary>The app code, for example TTDV2.</summary>
    public string Code { get; set; } = "";

    /// <summary>For the logs only. The menu uses CP_Atlas_Application.Name.</summary>
    public string? Name { get; set; }

    /// <summary>SECTION, STEPPER or FORM. FORM is the platform form component.</summary>
    public string FormType { get; set; } = "FORM";

    /// <summary>The code of the parent app of a family of apps, or null.</summary>
    public string? Parent { get; set; }

    /// <summary>The form config file, or null when the app has no form.</summary>
    public string? Form { get; set; }

    /// <summary>The dashboard config file, or null.</summary>
    public string? Dashboard { get; set; }

    /// <summary>The name of the record table of the app.</summary>
    public string? Table { get; set; }

    /// <summary>The name of the audit table of the app.</summary>
    public string? AuditTable { get; set; }

    /// <summary>The name of the attachment table of the app.</summary>
    public string? AttachmentTable { get; set; }

    /// <summary>
    /// The real column names of the attachment table, when they are not the platform names. Null = the platform names.
    /// </summary>
    public AtlasAttachmentColumns? AttachmentColumns { get; set; }

    /// <summary>
    /// The tables that the Table option provider of this app can read ("Schema.Name").
    /// </summary>
    /// <remarks>The start-up reads their columns. The platform never writes to a lookup table.</remarks>
    public List<string>? LookupTables { get; set; }

    /// <summary>
    /// The ReferenceType values that History reads from the AuditTable, for example ["TTDV2", "TTD"].
    /// </summary>
    /// <remarks>
    /// When this is null and Parent is set, History reads only ReferenceType = Code (a shared family audit table).
    /// When this is null and Parent is not set, History reads all entries of the record.
    /// </remarks>
    public List<string>? AuditRefTypes { get; set; }

    /// <summary>Returns the ReferenceType filter of the History read, or null for no filter.</summary>
    public IReadOnlyList<string>? AuditReadRefTypes() =>
        AuditRefTypes is { Count: > 0 } list ? list : Parent != null ? [Code] : null;

    /// <summary>The key of a custom service class. Null means ConfigSectionService.</summary>
    public string? Service { get; set; }

    /// <summary>The access mode. LogOnly logs a refusal; Enforce refuses the request.</summary>
    public AtlasAccessMode AccessMode { get; set; } = AtlasAccessMode.LogOnly;

    /// <summary>The side that writes the audit entries.</summary>
    public AtlasAuditMode AuditMode { get; set; } = AtlasAuditMode.Server;

    /// <summary>The time zone of the audit CreatedDate.</summary>
    public AtlasAuditTime AuditTime { get; set; } = AtlasAuditTime.Utc;

    /// <summary>
    /// The time zone of CreatedDate, ModifiedDate and of the date-time values on the wire.
    /// </summary>
    /// <remarks>
    /// Null means Utc, with 'Z'. Local means the server local time, with no 'Z'.
    /// The current apps write local time. Use Local for an existing table that the platform takes over.
    /// </remarks>
    public AtlasAuditTime? SystemTime { get; set; }

    /// <summary>True unless SystemTime is Local.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool SystemTimeIsUtc => SystemTime != AtlasAuditTime.Local;

    /// <summary>How the rule engine combines the rules that match.</summary>
    public AtlasRuleMode RuleMode { get; set; } = AtlasRuleMode.MostPermissive;

    /// <summary>The result of a field when no rule matches.</summary>
    public AtlasRuleDefault RuleDefault { get; set; } = AtlasRuleDefault.ReadOnly;

    /// <summary>False disables the app (the rollback switch).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// True when an update of a table with a rowversion column must send the Version from the load. The default is true.
    /// </summary>
    /// <remarks>
    /// False lets a caller that does not send the Version overwrite the changes of other users (last write wins).
    /// </remarks>
    public bool RequireVersion { get; set; } = true;

    /// <summary>
    /// The optional version of the config contract. Null or 1 is this platform. A higher value is a start-up error.
    /// </summary>
    public int? SchemaVersion { get; set; }

    /// <summary>The file that the manifest came from (not in the JSON).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SourceFile { get; set; }

    /// <summary>Environment settings that are present but not valid (not in the JSON). The start-up check reports them.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> SettingErrors { get; } = [];
}

/// <summary>Checks and converts app codes.</summary>
public static partial class AtlasAppCode
{
    /// <summary>Returns true when the code matches the app code pattern. The Code column is varchar(20).</summary>
    public static bool IsValid(string? code) => code != null && Pattern().IsMatch(code);

    /// <summary>Returns the code with no spaces at the ends, in upper case.</summary>
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    /// <summary>
    /// Returns the app code from a request.
    /// </summary>
    /// <remarks>
    /// The request has the text code, or an old caller sends a number (the enum value).
    /// The method changes a known number to the enum name. It returns other text with no change.
    /// </remarks>
    public static string FromRequest(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string text = code.Trim();
        if (int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number)
            && Enum.IsDefined(typeof(BBH.CP.Atlas.Data.Enumerations.CPApplicationCode), number))
            return ((BBH.CP.Atlas.Data.Enumerations.CPApplicationCode)number).ToString();
        return text;
    }

    /// <summary>
    /// Returns the enum value for the current if chains at the target.
    /// Use it in place of <c>Enum.Parse&lt;CPApplicationCode&gt;(code, true)</c>.
    /// </summary>
    /// <remarks>
    /// A platform app that is not available (Enabled = false, or disabled at start-up) gives 404.
    /// Enum.Parse gives an ArgumentException, which the target logs as a 500.
    /// Any other code gives the same result as Enum.Parse, so the current callers see no change.
    /// </remarks>
    /// <exception cref="AtlasProblemException">400 when the code is empty. 404 when the platform app is not available.</exception>
    public static BBH.CP.Atlas.Data.Enumerations.CPApplicationCode ToLegacy(string? code, IAtlasConfigStore? configStore)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw AtlasProblemException.BadRequest("The request has no app code.");
        string text = FromRequest(code);
        if (!IsLegacy(text) && configStore?.FindManifest(Normalize(text)) != null)
            throw AtlasProblemException.NotFound($"App '{Normalize(text)}' is not available.");
        return Enum.Parse<BBH.CP.Atlas.Data.Enumerations.CPApplicationCode>(text, true);
    }

    /// <summary>Returns true when the code is one of the current (enum) apps.</summary>
    public static bool IsLegacy(string? code) =>
        code != null && Enum.TryParse(code.Trim(), true, out BBH.CP.Atlas.Data.Enumerations.CPApplicationCode _) && !int.TryParse(code, out _);

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,19}$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
