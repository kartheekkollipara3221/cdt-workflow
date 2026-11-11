// STAND-IN: do not copy to the target.
// Target: Data/Data/DTO/CPAtlasApplicationConfig.cs. Same classes, properties and namespaces.
// Differences from the target copy:
//   * The classes are "partial". The platform adds its new optional properties in Platform/ partial files.
//     Integration: add the word "partial" to these class declarations at the target.
//   * CPWorkflowNotification.Request is left out (workflow engine type; release P1 has no workflow).
//   * Only the extension methods that the platform uses are copied.
using BBH.CP.Atlas.Data.Enumerations;

namespace BBH.CP.Atlas.Data.DTO
{
    public partial class CPAtlasApplicationConfig
    {
        public string? Name { get; set; }
        public CPApplicationCode? Code { get; set; }
        public CPApplicationConfigType Type { get; set; }
        public List<CPApplicationStep>? Steps { get; set; }
        public CPApplicationSection? Section { get; set; }
        public Dictionary<string, Dictionary<string, string>>? Options { get; set; }
    }

    public partial class CPApplicationStep
    {
        public string? ID { get; set; }
        public string? Label { get; set; }
        public string? Status { get; set; }
        public bool Active { get; set; }
        public bool Disabled { get; set; }
        public string? RouterLink { get; set; }
        public string? StepClass { get; set; }
        public List<CPApplicationControl>? Controls { get; set; }
        public List<CPButton>? Buttons { get; set; }
        public List<string>? States { get; set; }
        public List<string>? Roles { get; set; }
        public CPWorkflow? Workflow { get; set; }
        public List<CPSkipValidation>? SkipValidations { get; set; }
        public string? CurrentState { get; set; }
        public int Count { get; set; }
        public string? HeaderName { get; set; }
        public List<CPRule>? Rules { get; set; }
    }

    public partial class CPApplicationSection
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? HeaderName { get; set; }
        public CPWorkflow? Workflow { get; set; }
        public List<CPApplicationControl>? Controls { get; set; }
        public List<CPButton>? Buttons { get; set; }
    }

    public partial class CPApplicationControl
    {
        public required string Name { get; set; }
        public required string Label { get; set; }
        public required string Type { get; set; }
        public List<CPKeyValue>? Options { get; set; }
        public int? Row { get; set; }
        public int? Column { get; set; }
        public string? Value { get; set; }
        public bool Disabled { get; set; }
        public bool Visible { get; set; } = true;
        public string? PlaceHolder { get; set; }
        public string? DivClass { get; set; }
        public string? Class { get; set; }
        public CPValidator? Validator { get; set; }
        public CPGrid? Grid { get; set; }
        public List<CPAttachment>? Files { get; set; }
        public List<CPApplicationControl>? ChildControls { get; set; }
        public Dictionary<string, List<CPKeyValue>>? ChildOptions { get; set; }
        public string? Field { get; set; }
        public List<CPRule>? Rules { get; set; }
        public bool Multiple { get; set; }
    }

    public partial class CPValidator
    {
        public bool Required { get; set; }
        public string? Message { get; set; }
        public string? Pattern { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
    }

    public partial class CPKeyValue
    {
        public string? Label { get; set; }
        public string? Value { get; set; }
    }

    public partial class CPGrid
    {
        public List<CPGridColumn>? Columns { get; set; }
        public List<Dictionary<string, string>>? Data { get; set; }
        public bool Paginate { get; set; }
    }

    public partial class CPGridColumn
    {
        public string? headerName { get; set; }
        public int width { get; set; }
        public string? field { get; set; }
        public bool wrapHeaderText { get; set; }
        public bool autoHeaderHeight { get; set; }
        public bool unSortIcon { get; set; }
        public bool filter { get; set; }
        public string? cellEditor { get; set; }
        public string? cellRenderer { get; set; }
        public CPCellRendererParam? cellRendererParams { get; set; }
    }

    public partial class CPCellRendererParam
    {
        public List<string>? options { get; set; }
        public bool disabled { get; set; }
        public string? code { get; set; }
        public string? ctrlname { get; set; }
    }

    public partial class CPAttachment
    {
        public string? ID { get; set; }
        public string? Name { get; set; }
        public string? URL { get; set; }
        public string? Type { get; set; }
        public byte[]? FileByte { get; set; }
        public bool Disabled { get; set; }
    }

    public partial class CPWorkflow
    {
        public required string Key { get; set; }
        public required List<string> Roles { get; set; }
        public required Dictionary<string, List<CPWorkflowCondition>> Actions { get; set; }
        public Dictionary<string, List<CPWorkflowCondition>>? AssignRoles { get; set; }
        public Dictionary<string, CPWorkflowNotification>? Notifications { get; set; }
        public Dictionary<string, List<string>>? AdditionalInfo { get; set; }
    }

    public partial class CPWorkflowCondition
    {
        public required string Name { get; set; }
        public string? Value { get; set; }
    }

    public partial class CPWorkflowNotification
    {
        public string? ActionKey { get; set; }
        public bool IsSLA { get; set; }
        public bool IsExecuteInvoke { get; set; } = true;
    }

    public partial class CPButton
    {
        public string? Name { get; set; }
        public string? Label { get; set; }
        public List<CPRule>? Rules { get; set; }
        public bool Disabled { get; set; }
        public bool Visible { get; set; }
    }

    public partial class CPSkipValidation
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

    public partial class CPRule
    {
        public List<string>? Roles { get; set; }
        public List<string>? States { get; set; }
        public bool? IsOwner { get; set; }
        public bool Visible { get; set; } = true;
        public bool Disabled { get; set; } = false;
        public List<string>? EnableControls { get; set; }
        public List<string>? DisableControls { get; set; }
        public List<string>? ShowControls { get; set; }
        public List<string>? HideControls { get; set; }
    }

    /// <summary>Finds the controls and buttons of a form section (a copy of the target methods).</summary>
    public static class CPApplicationConfigExtension
    {
        /// <summary>
        /// Returns the child controls whose Field is equal to <paramref name="field"/> (ignore case).
        /// Returns null if the section has no controls.
        /// </summary>
        public static List<CPApplicationControl>? FindControlByField(this CPApplicationSection section, string field)
        {
            List<CPApplicationControl>? lstResult = [];
            if (section != null && section.Controls != null && section.Controls.Count > 0)
            {
                foreach (CPApplicationControl control in section.Controls)
                {
                    if (control != null && control.ChildControls != null && control.ChildControls.Count > 0
                        && control.ChildControls.Any(x => string.Equals(field, x.Field, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        lstResult.AddRange(control.ChildControls.Where(x =>
                            string.Equals(field, x.Field, StringComparison.InvariantCultureIgnoreCase)));
                    }
                }
                return lstResult;
            }
            else
                return null;
        }

        /// <summary>
        /// Returns the control or child control with the name (ignore case). The last match wins.
        /// Returns null if there is no match or the section has no controls.
        /// </summary>
        public static CPApplicationControl? FindControlByName(this CPApplicationSection section, string name)
        {
            CPApplicationControl? result = null;
            if (section != null && section.Controls != null && section.Controls.Count > 0)
            {
                foreach (CPApplicationControl control in section.Controls)
                {
                    if (control != null && control.ChildControls != null && control.ChildControls.Count > 0
                        && control.ChildControls.Any(x => string.Equals(name, x.Name, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        result = control.ChildControls.Single(x =>
                            string.Equals(name, x.Name, StringComparison.InvariantCultureIgnoreCase));
                    }
                    if (control != null && string.Equals(control.Name, name, StringComparison.InvariantCultureIgnoreCase))
                        result = control;
                }
                return result;
            }
            else
                return null;
        }

        /// <summary>Returns the button with the name (ignore case), or null if there is no such button.</summary>
        /// <exception cref="InvalidOperationException">The section has more than one button with the name.</exception>
        public static CPButton? FindButton(this CPApplicationSection section, string name)
        {
            if (section != null && section.Buttons != null && section.Buttons.Count > 0
                && section.Buttons.Any(x => string.Equals(name, x.Name, StringComparison.InvariantCultureIgnoreCase)))
                return section.Buttons.Single(x => string.Equals(name, x.Name, StringComparison.InvariantCultureIgnoreCase));
            else
                return null;
        }
    }
}
