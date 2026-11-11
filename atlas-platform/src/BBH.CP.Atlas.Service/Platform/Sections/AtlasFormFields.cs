using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Rules;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// One field of the form: a control with a Field, or a column of a grid with <c>BindMode: SingleRowFields</c>.
/// </summary>
/// <remarks>
/// <see cref="Name"/> is the name in <c>Outputs</c>. <see cref="Field"/> is the table column.
/// </remarks>
public sealed class AtlasFormField
{
    /// <summary>The output name of the field.</summary>
    public required string Name { get; init; }

    /// <summary>The table column of the field.</summary>
    public required string Field { get; init; }

    /// <summary>The label of the field, for messages.</summary>
    public string? Label { get; init; }

    /// <summary>The control. For a grid field, this is the grid control.</summary>
    public required CPApplicationControl Control { get; init; }

    /// <summary>The grid column of a grid field, or null.</summary>
    public CPGridColumn? GridColumn { get; init; }

    /// <summary>True when the field is a column of a SingleRowFields grid.</summary>
    public bool IsGridField => GridColumn != null;

    /// <summary>The effective visibility after the rules and the server effects.</summary>
    public bool Visible { get; set; }

    /// <summary>The effective disabled state after the rules and the server effects.</summary>
    public bool Disabled { get; set; }

    /// <summary>The effective required state after the server effects.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// True when the JSON shows the field and a rule hides it.
    /// </summary>
    /// <remarks>
    /// A value-driven effect cannot show the field again. An effect can only narrow what the rules allow.
    /// </remarks>
    public bool HiddenByRule { get; set; }

    /// <summary>True when the JSON enables the field and a rule disables it. An effect cannot enable it.</summary>
    public bool DisabledByRule { get; set; }

    /// <summary>The validator of the control.</summary>
    public CPValidator? Validator => Control.Validator;
}

/// <summary>The fields of a section, found one time for each request.</summary>
public sealed class AtlasFormFields
{
    /// <summary>The grid BindMode that makes each column a field of the record.</summary>
    public const string SingleRowFields = "SingleRowFields";

    private readonly List<AtlasFormField> _fields;
    private readonly Dictionary<string, CPApplicationControl> _controls = new(StringComparer.OrdinalIgnoreCase);

    private AtlasFormFields(List<AtlasFormField> fields, List<CPApplicationControl> grids, IEnumerable<CPApplicationControl> controls)
    {
        _fields = fields;
        Grids = grids;
        foreach (CPApplicationControl control in controls)
            _controls.TryAdd(control.Name, control);
    }

    /// <summary>All fields of the section.</summary>
    public IReadOnlyList<AtlasFormField> All => _fields;

    /// <summary>The grid controls with SingleRowFields.</summary>
    public IReadOnlyList<CPApplicationControl> Grids { get; }

    /// <summary>Returns the fields of a section, with the state from the JSON.</summary>
    public static AtlasFormFields From(CPApplicationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        List<AtlasFormField> fields = [];
        List<CPApplicationControl> grids = [];
        List<CPApplicationControl> controls = [];

        foreach (CPApplicationControl control in section.Controls ?? [])
        {
            controls.Add(control);
            Add(control, null);
            foreach (CPApplicationControl child in control.ChildControls ?? [])
            {
                controls.Add(child);
                Add(child, control);
            }
        }

        return new AtlasFormFields(fields, grids, controls);

        void Add(CPApplicationControl control, CPApplicationControl? parent)
        {
            bool parentVisible = parent == null || parent.Visible;
            bool parentDisabled = parent != null && parent.Disabled;

            if (IsSingleRowGrid(control))
            {
                grids.Add(control);
                foreach (CPGridColumn column in control.Grid!.Columns ?? [])
                {
                    if (string.IsNullOrWhiteSpace(column.field))
                        continue;
                    fields.Add(new AtlasFormField
                    {
                        Name = column.field,
                        Field = column.field,
                        Label = column.headerName,
                        Control = control,
                        GridColumn = column,
                        Visible = parentVisible && control.Visible,
                        Disabled = parentDisabled || control.Disabled || column.cellRendererParams?.disabled == true
                    });
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(control.Field))
                return;

            fields.Add(new AtlasFormField
            {
                Name = control.Name,
                Field = control.Field,
                Label = control.Label,
                Control = control,
                Visible = parentVisible && control.Visible,
                Disabled = parentDisabled || control.Disabled,
                Required = control.Validator?.Required == true
            });
        }
    }

    /// <summary>
    /// Returns the fields of a section after the rules, with HiddenByRule and DisabledByRule from <paramref name="outcome"/>.
    /// </summary>
    public static AtlasFormFields From(CPApplicationSection section, AtlasRuleOutcome outcome)
    {
        AtlasFormFields fields = From(section);
        fields.ApplyOutcome(outcome);
        return fields;
    }

    /// <summary>Sets HiddenByRule and DisabledByRule from what the rules changed.</summary>
    public void ApplyOutcome(AtlasRuleOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        foreach (AtlasFormField field in _fields)
        {
            field.HiddenByRule = outcome.HiddenByRule.Contains(field.Name);
            field.DisabledByRule = outcome.DisabledByRule.Contains(field.Name);
        }
    }

    /// <summary>Returns true when the control is a grid with BindMode SingleRowFields.</summary>
    public static bool IsSingleRowGrid(CPApplicationControl control) =>
        control.Grid != null && string.Equals(control.Grid.BindMode, SingleRowFields, StringComparison.OrdinalIgnoreCase);

    /// <summary>Finds a field by its output name, else by its column name.</summary>
    public AtlasFormField? Find(string name) =>
        _fields.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? _fields.FirstOrDefault(x => string.Equals(x.Field, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns a control by name (section, child or grid control), or null.</summary>
    public CPApplicationControl? Control(string name) => _controls.GetValueOrDefault(name);

    /// <summary>Returns the fields of a control. A grid control can have many fields.</summary>
    public IEnumerable<AtlasFormField> FieldsOf(string controlName) =>
        _fields.Where(x => string.Equals(x.Control.Name, controlName, StringComparison.OrdinalIgnoreCase));
}
