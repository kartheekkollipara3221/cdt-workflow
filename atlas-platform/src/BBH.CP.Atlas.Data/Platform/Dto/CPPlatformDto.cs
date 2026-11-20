// Platform additions to the existing DTO classes (partial). All properties are optional (null = current behaviour).
// The target classes need the word "partial".
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Data.DTO
{
    /// <summary>The platform settings of an app configuration.</summary>
    public partial class CPAtlasApplicationConfig
    {
        /// <summary>Header for an existing record, for example "{Name} - {ID}".</summary>
        public string? HeaderNameWithId { get; set; }

        /// <summary>App-level rules: the default for each section-level control with no Rules.</summary>
        public List<CPRule>? Rules { get; set; }

        /// <summary>Fields that name the owner of a record, compared by LAN ID.</summary>
        public List<string>? OwnerFields { get; set; }

        /// <summary>Operation permissions by role name or flag (@Read, @Write, @Admin).</summary>
        public CPPermissions? Permissions { get; set; }

        /// <summary>Default values for a new record or an empty value: control name to token or text.</summary>
        public Dictionary<string, string>? Defaults { get; set; }

        /// <summary>Fields that the server calculates on each save: field to "Sum(A, B, ...)".</summary>
        public Dictionary<string, string>? Computed { get; set; }

        /// <summary>Soft delete: RemovePage sets this field to true.</summary>
        public CPSoftDelete? SoftDelete { get; set; }
    }

    /// <summary>The platform data of a form section.</summary>
    public partial class CPApplicationSection
    {
        /// <summary>The app code as text.</summary>
        public string? AppCode { get; set; }

        /// <summary>Version token (base64 rowversion). The client sends it back on save.</summary>
        public string? Version { get; set; }

        /// <summary>What the current user can do with this record (for the buttons and menus).</summary>
        public CPUserPermissions? UserPermissions { get; set; }

        /// <summary>The Computed fields of the app (field to "Sum(...)"), so the client can show the value while the user types.
        /// The server calculates the saved value.</summary>
        public Dictionary<string, string>? Computed { get; set; }
    }

    /// <summary>The platform settings of a form control.</summary>
    public partial class CPApplicationControl
    {
        /// <summary>Options from a registered option provider.</summary>
        public CPOptionsSource? OptionsSource { get; set; }

        /// <summary>Value-based client rules. The server applies Show/Hide/Require/Unrequire again on save.</summary>
        public List<CPEffect>? Effects { get; set; }

        /// <summary>If the saved value is not in the options, add it at the top.</summary>
        public bool KeepSavedValue { get; set; }

        /// <summary>False: the audit writes "***" for this field. Null = audited.</summary>
        public bool? Audit { get; set; }
    }

    /// <summary>The platform checks of a control value.</summary>
    public partial class CPValidator
    {
        /// <summary>The value must be a whole number.</summary>
        public bool Integer { get; set; }

        /// <summary>The lowest permitted number. Null = no lower limit.</summary>
        public decimal? Min { get; set; }

        /// <summary>The highest permitted number. Null = no upper limit.</summary>
        public decimal? Max { get; set; }

        /// <summary>The number must be a multiple of this value. Null = each number is permitted.</summary>
        public decimal? Step { get; set; }

        /// <summary>The value must be true, for example a confirmation checkbox. An empty value fails.</summary>
        public bool MustBeTrue { get; set; }
    }

    /// <summary>The platform settings of the workflow binding of a section.</summary>
    /// <remarks>
    /// The platform does not read the target properties Roles and Actions.
    /// </remarks>
    public partial class CPWorkflow
    {
        /// <summary>The column that holds the state key of the record. The rules match their States against it.</summary>
        public string? StateField { get; set; }

        /// <summary>The column that holds the status text of the record, from <see cref="StatusLabels"/>.</summary>
        public string? StatusField { get; set; }

        /// <summary>The state of a new record.</summary>
        public string? InitialState { get; set; }

        /// <summary>The status text of each state key.</summary>
        public Dictionary<string, string>? StatusLabels { get; set; }

        /// <summary>
        /// The submit step of each state: state key to step. A state with no entry has no submit.
        /// </summary>
        /// <remarks>
        /// A form with steps has one submit button. Each save of such a form runs the step of the stored state.
        /// </remarks>
        public Dictionary<string, CPWorkflowStep>? Steps { get; set; }
    }

    /// <summary>One submit step of a form with steps.</summary>
    public class CPWorkflowStep
    {
        /// <summary>The text of the submit button in this state. Null = the button keeps its text.</summary>
        public string? Label { get; set; }

        /// <summary>
        /// The values that the server sets at this submit: control name or column name to token or text.
        /// </summary>
        /// <remarks>
        /// The tokens are the default tokens and {Now}. A client value for such a field is not used.
        /// </remarks>
        public Dictionary<string, string>? Set { get; set; }
    }

    /// <summary>The platform data of an option.</summary>
    public partial class CPKeyValue
    {
        /// <summary>Structured option data for SetFromOption.</summary>
        public Dictionary<string, string>? Data { get; set; }
    }

    /// <summary>The platform settings of a grid.</summary>
    public partial class CPGrid
    {
        /// <summary>"SingleRowFields": one row; each column field is a field of the record.</summary>
        public string? BindMode { get; set; }
    }

    /// <summary>The platform settings of a grid column.</summary>
    public partial class CPGridColumn
    {
        /// <summary>The header shows a date from a date control of the form, for example the 7 days before
        /// "WeekEnding" (as the TTD form at the target). Null = the fixed headerName.</summary>
        public CPHeaderFromDate? HeaderFromDate { get; set; }
    }

    /// <summary>The platform settings of a button.</summary>
    public partial class CPButton
    {
        /// <summary>Button action. Release P1 supports Save, Close and Delete.</summary>
        public CPButtonAction? Action { get; set; }

        /// <summary>Navigation target of a dashboard button.</summary>
        public CPButtonTarget? Target { get; set; }
    }

    /// <summary>The platform data of a page request and response.</summary>
    public partial class CPAtlasPage
    {
        /// <summary>The app code as text. The server uses AppCode, else Code.</summary>
        public string? AppCode { get; set; }

        /// <summary>Version token from the load response.</summary>
        public string? Version { get; set; }

        /// <summary>Save errors. On error, ID = 0 and the HTTP status stays 200.</summary>
        public List<CPPageError>? Errors { get; set; }
    }

    /// <summary>The platform data of an audit log row.</summary>
    public partial class CPAuditLog
    {
        /// <summary>The name of the audit event.</summary>
        public string? Event { get; set; }

        /// <summary>ISO 8601 UTC time.</summary>
        public string? CreatedDateUtc { get; set; }

        /// <summary>"App" or "Workflow".</summary>
        public string? Source { get; set; }
    }

    /// <summary>The platform data of a dashboard.</summary>
    public partial class CPAtlasDashboard
    {
        /// <summary>The app code as text (the enum Code cannot hold a new app code).</summary>
        public string? AppCode { get; set; }
    }

    /// <summary>The platform settings of a dashboard tab.</summary>
    public partial class CPAtlasTab
    {
        /// <summary>"Table": the platform makes the list query from Columns and Filter.</summary>
        public string? Source { get; set; }

        /// <summary>The columns of the tab grid.</summary>
        public List<CPTabColumn>? Columns { get; set; }

        /// <summary>The row filter of the tab. All items must match (AND).</summary>
        public List<CPTabFilter>? Filter { get; set; }

        /// <summary>For example ["ModifiedDate DESC", "CreatedDate DESC"]. Default "ID DESC". ID DESC is always the last item.</summary>
        public List<string>? OrderBy { get; set; }

        /// <summary>Optional upper limit of rows for the tab: TOP (MaxRows). The platform logs a full tab.</summary>
        public int? MaxRows { get; set; }
    }

    /// <summary>The platform data of a menu item.</summary>
    public partial class CPMenu
    {
        /// <summary>The text of the menu item.</summary>
        public string? Label { get; set; }
    }

    /// <summary>The roles or flags that can do each operation on the records of an app.</summary>
    public class CPPermissions
    {
        /// <summary>Roles that can open a record.</summary>
        public List<string>? View { get; set; }
        /// <summary>Roles that can make a new record.</summary>
        public List<string>? Create { get; set; }
        /// <summary>Roles that can edit own records or all records.</summary>
        public CPEditPermission? Edit { get; set; }
        /// <summary>Roles that can delete a record.</summary>
        public List<string>? Delete { get; set; }
        /// <summary>Roles that can see the history of a record.</summary>
        public List<string>? History { get; set; }
        /// <summary>Reserved. The platform does not read it in release P1.</summary>
        public Dictionary<string, List<string>>? Execute { get; set; }
    }

    /// <summary>The roles that can edit records.</summary>
    public class CPEditPermission
    {
        /// <summary>Roles that can edit own records (OwnerFields).</summary>
        public List<string>? Own { get; set; }

        /// <summary>Roles that can edit all records.</summary>
        public List<string>? All { get; set; }
    }

    /// <summary>What the current user can do with one record.</summary>
    public class CPUserPermissions
    {
        /// <summary>True if the user can make a new record.</summary>
        public bool CanCreate { get; set; }
        /// <summary>True if the user can edit this record.</summary>
        public bool CanEdit { get; set; }
        /// <summary>True if the user can delete this record.</summary>
        public bool CanDelete { get; set; }
        /// <summary>True if the user can see the history of this record.</summary>
        public bool CanViewHistory { get; set; }
    }

    /// <summary>The soft-delete setting of an app.</summary>
    public class CPSoftDelete
    {
        /// <summary>The bit field that marks a record as deleted. Default "IsDeleted".</summary>
        public string Field { get; set; } = "IsDeleted";
    }

    /// <summary>A grid column header that shows a date from a date control of the form.</summary>
    public class CPHeaderFromDate
    {
        /// <summary>The control with the date (yyyy-MM-dd).</summary>
        public required string Control { get; set; }

        /// <summary>Days to add to the date, for example -6 for the first day of a week that ends on the date.</summary>
        public int AddDays { get; set; }

        /// <summary>Angular date format. Default "EEEE dd-MMM-yyyy" (for example "Monday 19-Oct-2026", as the target).</summary>
        public string? Format { get; set; }
    }

    /// <summary>The registered option provider that gives the options of a control.</summary>
    public class CPOptionsSource
    {
        /// <summary>The name of the registered option provider.</summary>
        public required string Provider { get; set; }
        /// <summary>The roles whose users the provider returns, for a provider of users.</summary>
        public List<string>? Roles { get; set; }
        /// <summary>Other values that the provider reads.</summary>
        public Dictionary<string, string>? Parameters { get; set; }
    }

    /// <summary>A control effect: when the condition matches, the actions change other controls.</summary>
    public class CPEffect
    {
        /// <summary>
        /// When the effect runs. Null: when the form loads and at each change. "Change": only when the user changes the value.
        /// </summary>
        /// <remarks>
        /// The server does not apply a "Change" effect. It is a help for the user, not a rule.
        /// </remarks>
        public string? On { get; set; }

        /// <summary>The condition on the value of the control. Null = always.</summary>
        public CPEffectCondition? When { get; set; }
        /// <summary>Controls to show.</summary>
        public List<string>? Show { get; set; }
        /// <summary>Controls to hide.</summary>
        public List<string>? Hide { get; set; }
        /// <summary>Controls that become required.</summary>
        public List<string>? Require { get; set; }
        /// <summary>Controls that become not required.</summary>
        public List<string>? Unrequire { get; set; }
        /// <summary>Controls to enable.</summary>
        public List<string>? Enable { get; set; }
        /// <summary>Controls to disable.</summary>
        public List<string>? Disable { get; set; }
        /// <summary>Control name to the value to set.</summary>
        public Dictionary<string, string>? Set { get; set; }
        /// <summary>Controls whose value to clear.</summary>
        public List<string>? Clear { get; set; }

        /// <summary>Target control name to the key in CPKeyValue.Data of the selected option.</summary>
        public Dictionary<string, string>? SetFromOption { get; set; }

        /// <summary>Reserved. The platform does not read it in release P1.</summary>
        public string? OptionsFromChild { get; set; }
    }

    /// <summary>The condition of a control effect. Each property that is set must match.</summary>
    public class CPEffectCondition
    {
        // The JSON name is "Equals". "new": the property hides object.Equals(object) by name only.
        /// <summary>The value must be equal to this text (ignore case).</summary>
        public new string? Equals { get; set; }
        /// <summary>The value must not be equal to this text (ignore case).</summary>
        public string? NotEquals { get; set; }
        /// <summary>The value must be one of these texts (ignore case).</summary>
        public List<string>? In { get; set; }
        /// <summary>True: the value must not be empty. False: the value must be empty.</summary>
        public bool? HasValue { get; set; }
    }

    /// <summary>The action of a button.</summary>
    public class CPButtonAction
    {
        /// <summary>Save, Close, Delete, Execute, Navigate.</summary>
        public required string Type { get; set; }
        /// <summary>Reserved. The platform does not read it in release P1.</summary>
        public Dictionary<string, string>? Set { get; set; }
    }

    /// <summary>The record that a dashboard button opens.</summary>
    public class CPButtonTarget
    {
        /// <summary>The app code of the form to open.</summary>
        public required string AppCode { get; set; }
        /// <summary>The record ID.</summary>
        public int Id { get; set; }
    }

    /// <summary>One save error.</summary>
    public class CPPageError
    {
        /// <summary>Control name; empty for a page error.</summary>
        public string Field { get; set; } = "";
        /// <summary>The error code, for example a field error code of <see cref="BBH.CP.Atlas.Data.Platform.AtlasValueConverter"/>.</summary>
        public required string Code { get; set; }
        /// <summary>The error text for the user.</summary>
        public required string Message { get; set; }
    }

    /// <summary>One column of a dashboard tab grid.</summary>
    public class CPTabColumn
    {
        /// <summary>The column name in the table.</summary>
        public required string Name { get; set; }
        /// <summary>The column header text.</summary>
        public string? Label { get; set; }

        /// <summary>string, number, date, currency, user (LAN ID shown as display name).</summary>
        public string? Type { get; set; }
        /// <summary>The display format of the value.</summary>
        public string? Format { get; set; }
        /// <summary>True if the grid does not show the column.</summary>
        public bool Hidden { get; set; }
        /// <summary>The column width.</summary>
        public int? Width { get; set; }

        /// <summary>"Edit": the cell opens the form of the row.</summary>
        public string? Link { get; set; }

        /// <summary>The grid shows the total of this column.</summary>
        public bool Sum { get; set; }
    }

    /// <summary>One condition of a dashboard tab filter, or a group of conditions.</summary>
    public class CPTabFilter
    {
        /// <summary>The column to compare.</summary>
        public string? Column { get; set; }

        /// <summary>Eq, NotEq, In, IsNull, NotNull, Gt, Lt.</summary>
        public string? Op { get; set; }
        /// <summary>The value for Eq, NotEq, Gt and Lt.</summary>
        public string? Value { get; set; }
        /// <summary>The values for In.</summary>
        public List<string>? Values { get; set; }

        /// <summary>A group: the items are joined with OR (one level only).</summary>
        public List<CPTabFilter>? Any { get; set; }
    }
}
