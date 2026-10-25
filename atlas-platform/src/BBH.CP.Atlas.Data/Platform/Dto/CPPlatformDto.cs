// Platform additions to the existing DTO classes (partial). All properties are optional (null = current behaviour).
// The target classes need the word "partial" (INTEGRATION.md, section 2).
// Specs: 01 (5.1-5.13, 10.2, 10.5), 03 (4.4-4.7), 06 (4.3), 07 (4.4), 09 (4.4), 12.
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Data.DTO
{
    public partial class CPAtlasApplicationConfig
    {
        /// <summary>Header for an existing record, for example "{Name} - {ID}" (spec 01, 5.7).</summary>
        public string? HeaderNameWithId { get; set; }

        /// <summary>App-level rules: the default for each section-level control with no Rules (spec 01, 10.5 example).</summary>
        public List<CPRule>? Rules { get; set; }

        /// <summary>Fields that name the owner of a record, compared by LAN ID (spec 01, 10.5; spec 04, 4.4).</summary>
        public List<string>? OwnerFields { get; set; }

        /// <summary>Operation permissions by role name or flag (@Read, @Write, @Admin) (spec 06, 4.3).</summary>
        public CPPermissions? Permissions { get; set; }

        /// <summary>Default values for a new record or an empty value: control name to token or text (spec 01, 5.7, 10.2).</summary>
        public Dictionary<string, string>? Defaults { get; set; }

        /// <summary>Fields that the server calculates on each save: field to "Sum(A, B, ...)" (spec 01, 10.2).</summary>
        public Dictionary<string, string>? Computed { get; set; }

        /// <summary>Soft delete: RemovePage sets this field to true (spec 01, 10.2).</summary>
        public CPSoftDelete? SoftDelete { get; set; }
    }

    public partial class CPApplicationSection
    {
        /// <summary>The app code as text (spec 07, 4.4).</summary>
        public string? AppCode { get; set; }

        /// <summary>Version token (base64 rowversion). The client sends it back on save (spec 01, 5.13).</summary>
        public string? Version { get; set; }

        /// <summary>What the current user can do with this record (for the buttons and menus).</summary>
        public CPUserPermissions? UserPermissions { get; set; }

        /// <summary>The Computed fields of the app (field to "Sum(...)"), so the client can show the value while the user types.
        /// The server calculates the saved value (spec 01, 10.2).</summary>
        public Dictionary<string, string>? Computed { get; set; }
    }

    public partial class CPApplicationControl
    {
        /// <summary>Options from a registered option provider (spec 01, 10.2).</summary>
        public CPOptionsSource? OptionsSource { get; set; }

        /// <summary>Value-based client rules; the server applies Show/Hide/Require/Unrequire again on save (spec 01, 5.1).</summary>
        public List<CPEffect>? Effects { get; set; }

        /// <summary>If the saved value is not in the options, add it at the top (spec 01, 10.2).</summary>
        public bool KeepSavedValue { get; set; }

        /// <summary>False: the audit writes "***" for this field (spec 09, 4.2). Null = audited.</summary>
        public bool? Audit { get; set; }
    }

    public partial class CPValidator
    {
        /// <summary>The value must be a whole number (spec 01, 10.3).</summary>
        public bool Integer { get; set; }

        public decimal? Min { get; set; }

        public decimal? Max { get; set; }
    }

    public partial class CPKeyValue
    {
        /// <summary>Structured option data for SetFromOption (spec 01, 5.2).</summary>
        public Dictionary<string, string>? Data { get; set; }
    }

    public partial class CPGrid
    {
        /// <summary>"SingleRowFields": one row; each column field is a field of the record (spec 01, 10.2).</summary>
        public string? BindMode { get; set; }
    }

    public partial class CPButton
    {
        /// <summary>Button action (spec 01, 5.3). P1: Save, Close, Delete.</summary>
        public CPButtonAction? Action { get; set; }

        /// <summary>Navigation target of a dashboard button (spec 03, 4.5).</summary>
        public CPButtonTarget? Target { get; set; }
    }

    public partial class CPAtlasPage
    {
        /// <summary>The app code as text. The server uses AppCode, else Code (spec 07, 4.4).</summary>
        public string? AppCode { get; set; }

        /// <summary>Version token from the load response (spec 01, 5.13).</summary>
        public string? Version { get; set; }

        /// <summary>Save errors. On error, ID = 0 and the HTTP status stays 200 (spec 01, 5.11).</summary>
        public List<CPPageError>? Errors { get; set; }
    }

    public partial class CPAuditLog
    {
        public string? Event { get; set; }

        /// <summary>ISO 8601 UTC time (spec 09, 4.5).</summary>
        public string? CreatedDateUtc { get; set; }

        /// <summary>"App" or "Workflow" (spec 09, 4.4).</summary>
        public string? Source { get; set; }
    }

    public partial class CPAtlasDashboard
    {
        /// <summary>The app code as text (the enum Code cannot hold a new app code).</summary>
        public string? AppCode { get; set; }
    }

    public partial class CPAtlasTab
    {
        /// <summary>"Table": the platform makes the list query from Columns and Filter (spec 03, 4.7).</summary>
        public string? Source { get; set; }

        public List<CPTabColumn>? Columns { get; set; }

        public List<CPTabFilter>? Filter { get; set; }

        /// <summary>For example ["ModifiedDate DESC", "CreatedDate DESC"]. Default "ID DESC".</summary>
        public List<string>? OrderBy { get; set; }
    }

    public partial class CPMenu
    {
        public string? Label { get; set; }
    }

    public class CPPermissions
    {
        public List<string>? View { get; set; }
        public List<string>? Create { get; set; }
        public CPEditPermission? Edit { get; set; }
        public List<string>? Delete { get; set; }
        public List<string>? History { get; set; }
        public Dictionary<string, List<string>>? Execute { get; set; }
    }

    public class CPEditPermission
    {
        /// <summary>Roles that can edit own records (OwnerFields).</summary>
        public List<string>? Own { get; set; }

        /// <summary>Roles that can edit all records.</summary>
        public List<string>? All { get; set; }
    }

    public class CPUserPermissions
    {
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanViewHistory { get; set; }
    }

    public class CPSoftDelete
    {
        public string Field { get; set; } = "IsDeleted";
    }

    public class CPOptionsSource
    {
        public required string Provider { get; set; }
        public List<string>? Roles { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }
    }

    public class CPEffect
    {
        public CPEffectCondition? When { get; set; }
        public List<string>? Show { get; set; }
        public List<string>? Hide { get; set; }
        public List<string>? Require { get; set; }
        public List<string>? Unrequire { get; set; }
        public List<string>? Enable { get; set; }
        public List<string>? Disable { get; set; }
        public Dictionary<string, string>? Set { get; set; }
        public List<string>? Clear { get; set; }

        /// <summary>Target control name to the key in CPKeyValue.Data of the selected option.</summary>
        public Dictionary<string, string>? SetFromOption { get; set; }

        public string? OptionsFromChild { get; set; }
    }

    public class CPEffectCondition
    {
        // JSON name "Equals" (spec 01, 5.1). "new": the property hides object.Equals(object) by name only.
        public new string? Equals { get; set; }
        public string? NotEquals { get; set; }
        public List<string>? In { get; set; }
        public bool? HasValue { get; set; }
    }

    public class CPButtonAction
    {
        /// <summary>Save, Close, Delete, Execute, Navigate.</summary>
        public required string Type { get; set; }
        public Dictionary<string, string>? Set { get; set; }
    }

    public class CPButtonTarget
    {
        public required string AppCode { get; set; }
        public int Id { get; set; }
    }

    public class CPPageError
    {
        /// <summary>Control name; empty for a page error.</summary>
        public string Field { get; set; } = "";
        public required string Code { get; set; }
        public required string Message { get; set; }
    }

    public class CPTabColumn
    {
        public required string Name { get; set; }
        public string? Label { get; set; }

        /// <summary>string, number, date, currency, user (LAN ID shown as display name).</summary>
        public string? Type { get; set; }
        public string? Format { get; set; }
        public bool Hidden { get; set; }
        public int? Width { get; set; }

        /// <summary>"Edit": the cell opens the form of the row.</summary>
        public string? Link { get; set; }

        /// <summary>The grid shows the total of this column.</summary>
        public bool Sum { get; set; }
    }

    public class CPTabFilter
    {
        public string? Column { get; set; }

        /// <summary>Eq, NotEq, In, IsNull, NotNull, Gt, Lt.</summary>
        public string? Op { get; set; }
        public string? Value { get; set; }
        public List<string>? Values { get; set; }

        /// <summary>A group: the items are joined with OR (one level only, spec 12, 3.2).</summary>
        public List<CPTabFilter>? Any { get; set; }
    }
}
