using System.Text.RegularExpressions;

namespace BBH.CP.Atlas.Service.Platform.Registry;

public enum AtlasAccessMode { LogOnly, Enforce }

public enum AtlasAuditMode { Client, Server }

public enum AtlasRuleMode { FirstMatch, MostPermissive }

public enum AtlasRuleDefault { Keep, ReadOnly, Hidden }

public enum AtlasAuditTime { Utc, Local }

/// <summary>
/// The app manifest, &lt;Code&gt;.app.json (spec 07, 4.1). It names the other files and holds the settings of the app.
/// The settings can be changed for each environment: AppSettings:Modules:&lt;Code&gt;:&lt;Setting&gt;.
/// </summary>
public sealed class AtlasAppManifest
{
    public string Code { get; set; } = "";

    /// <summary>For the logs only. The menu uses CP_Atlas_Application.Name.</summary>
    public string? Name { get; set; }

    /// <summary>SECTION, STEPPER or FORM (FORM = the new platform form component, spec 12, question 5).</summary>
    public string FormType { get; set; } = "FORM";

    public string? Parent { get; set; }

    public string? Form { get; set; }

    public string? Dashboard { get; set; }

    public string? Table { get; set; }

    public string? AuditTable { get; set; }

    public string? AttachmentTable { get; set; }

    /// <summary>Key of a custom service class; null = ConfigSectionService (spec 07, 4.1).</summary>
    public string? Service { get; set; }

    public AtlasAccessMode AccessMode { get; set; } = AtlasAccessMode.LogOnly;

    public AtlasAuditMode AuditMode { get; set; } = AtlasAuditMode.Server;

    public AtlasAuditTime AuditTime { get; set; } = AtlasAuditTime.Utc;

    public AtlasRuleMode RuleMode { get; set; } = AtlasRuleMode.MostPermissive;

    public AtlasRuleDefault RuleDefault { get; set; } = AtlasRuleDefault.ReadOnly;

    public bool Enabled { get; set; } = true;

    /// <summary>Optional version of the config contract (review SV-07). Null or 1 = this platform; a higher value is a start-up error.</summary>
    public int? SchemaVersion { get; set; }

    /// <summary>The file that the manifest came from (not in the JSON).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SourceFile { get; set; }

    /// <summary>Environment settings that are present but not valid (not in the JSON). The start-up check reports them.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> SettingErrors { get; } = [];
}

public static partial class AtlasAppCode
{
    /// <summary>App code pattern (spec 07, 4.4). The Code column is varchar(20).</summary>
    public static bool IsValid(string? code) => code != null && Pattern().IsMatch(code);

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    /// <summary>
    /// The code from a request (spec 07, 4.4): the text code, or a number of an old caller (the enum value), which
    /// is changed to the enum name. Returns the text as it is if it is not a known number.
    /// </summary>
    public static string FromRequest(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string text = code.Trim();
        if (int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number)
            && Enum.IsDefined(typeof(BBH.CP.Atlas.Data.Enumerations.CPApplicationCode), number))
            return ((BBH.CP.Atlas.Data.Enumerations.CPApplicationCode)number).ToString();
        return text;
    }

    /// <summary>True if the code is one of the current (enum) apps.</summary>
    public static bool IsLegacy(string? code) =>
        code != null && Enum.TryParse(code.Trim(), true, out BBH.CP.Atlas.Data.Enumerations.CPApplicationCode _) && !int.TryParse(code, out _);

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,19}$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
