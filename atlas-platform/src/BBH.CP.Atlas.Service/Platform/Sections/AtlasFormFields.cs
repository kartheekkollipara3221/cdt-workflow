using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// One field of the form: a control with a Field, or a column of a grid with <c>BindMode: SingleRowFields</c>
/// (spec 01, 10.2). <see cref="Name"/> is the name in <c>Outputs</c>; <see cref="Field"/> is the table column.
/// </summary>
public sealed class AtlasFormField
{
    public required string Name { get; init; }

    public required string Field { get; init; }

    public string? Label { get; init; }

    /// <summary>The control. For a grid field: the grid control.</summary>
    public required CPApplicationControl Control { get; init; }

    public CPGridColumn? GridColumn { get; init; }

    public bool IsGridField => GridColumn != null;

    /// <summary>Effective state after the rules and the server effects.</summary>
    public bool Visible { get; set; }

    public bool Disabled { get; set; }

    public bool Required { get; set; }

    public CPValidator? Validator => Control.Validator;
}

/// <summary>The fields of a section, found one time for each request.</summary>
public sealed class AtlasFormFields
{
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

    public IReadOnlyList<AtlasFormField> All => _fields;

    /// <summary>The grid controls with SingleRowFields.</summary>
    public IReadOnlyList<CPApplicationControl> Grids { get; }

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

    public static bool IsSingleRowGrid(CPApplicationControl control) =>
        control.Grid != null && string.Equals(control.Grid.BindMode, SingleRowFields, StringComparison.OrdinalIgnoreCase);

    /// <summary>Finds a field by its output name, else by its column name.</summary>
    public AtlasFormField? Find(string name) =>
        _fields.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? _fields.FirstOrDefault(x => string.Equals(x.Field, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Any control by name (section, child or grid).</summary>
    public CPApplicationControl? Control(string name) => _controls.GetValueOrDefault(name);

    public IEnumerable<AtlasFormField> FieldsOf(string controlName) =>
        _fields.Where(x => string.Equals(x.Control.Name, controlName, StringComparison.OrdinalIgnoreCase));
}
