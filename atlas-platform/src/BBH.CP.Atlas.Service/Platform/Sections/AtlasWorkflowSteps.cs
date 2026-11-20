using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// Reads the state of a record and the submit step of a form with steps.
/// </summary>
/// <remarks>
/// The state always comes from the stored record, never from the client.
/// A form with no workflow binding has no state, so its rules see an empty state list.
/// </remarks>
public static class AtlasWorkflowSteps
{
    /// <summary>The action type of the submit button.</summary>
    public const string SaveAction = "Save";

    /// <summary>Returns true when the section has a workflow binding with steps.</summary>
    public static bool HasSteps(CPApplicationSection? section) => section?.Workflow?.Steps is { Count: > 0 };

    /// <summary>
    /// Returns the state of the record: the value of the state column, or the initial state for a new record.
    /// </summary>
    /// <returns>Null when the form has no state column, or when the stored record has no state value.</returns>
    public static string? StateOf(CPApplicationSection? section, AtlasTable table, IReadOnlyDictionary<string, object?>? record, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(table);
        CPWorkflow? workflow = section?.Workflow;
        if (workflow == null || string.IsNullOrWhiteSpace(workflow.StateField))
            return null;
        if (isNew)
            return string.IsNullOrWhiteSpace(workflow.InitialState) ? null : workflow.InitialState.Trim();

        if (record == null || table.Find(workflow.StateField) is not { } column)
            return null;
        string? value = Convert.ToString(record.GetValueOrDefault(column.Name), System.Globalization.CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Returns the state list for the rules: one state, or an empty list.</summary>
    public static IReadOnlyList<string> States(string? state) => state == null ? [] : [state];

    /// <summary>Returns the step of a state, or null when the state has no step. The compare ignores case.</summary>
    public static CPWorkflowStep? StepFor(CPApplicationSection? section, string? state)
    {
        if (state == null || section?.Workflow?.Steps is not { } steps)
            return null;
        return steps.FirstOrDefault(x => string.Equals(x.Key, state, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>Returns the buttons with the action type Save. A form with steps must have exactly one.</summary>
    public static IReadOnlyList<CPButton> SaveButtons(CPApplicationSection? section) =>
        [.. (section?.Buttons ?? []).Where(x => string.Equals(x.Action?.Type, SaveAction, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Sets the submit button for the state of the record: hidden when the state has no step, else the label of the step.
    /// </summary>
    /// <remarks>
    /// Does nothing for a form with no steps. Thus the button rules need only the roles.
    /// </remarks>
    public static void ApplyToSubmitButton(CPApplicationSection section, string? state)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!HasSteps(section) || SaveButtons(section) is not [CPButton button])
            return;

        CPWorkflowStep? step = StepFor(section, state);
        if (step == null)
            button.Visible = false;
        else if (!string.IsNullOrWhiteSpace(step.Label))
            button.Label = step.Label;
    }

    /// <summary>Returns the status text of a state, or the state key when it has no label.</summary>
    public static string? LabelOf(CPApplicationSection? section, string? state)
    {
        if (state == null)
            return null;
        return section?.Workflow?.StatusLabels?.FirstOrDefault(x => string.Equals(x.Key, state, StringComparison.OrdinalIgnoreCase)).Value ?? state;
    }
}
