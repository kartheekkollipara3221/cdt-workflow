using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// Applies the control effects of a form on the server.
/// </summary>
/// <remarks>
/// The server uses the same effect actions as the client (see atlas-effects.ts).
/// A rule has priority over an effect. An effect cannot show a field that a rule hides.
/// An effect cannot enable a field that a rule disables.
/// </remarks>
public static class AtlasServerEffects
{
    /// <summary>
    /// Applies Show, Hide, Require, Unrequire, Enable and Disable to the fields.
    /// </summary>
    /// <returns>
    /// The names of the fields that the server sets: the SetFromOption targets, and the Set and Clear targets of a
    /// matching effect.
    /// </returns>
    public static HashSet<string> Apply(AtlasFormFields fields, Func<AtlasFormField, string?> current)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(current);
        HashSet<string> derived = new(StringComparer.OrdinalIgnoreCase);
        foreach (AtlasFormField source in Sources(fields))
        {
            string? value = current(source);
            foreach (CPEffect effect in ServerEffects(source))
            {
                foreach (string target in effect.SetFromOption?.Keys ?? Enumerable.Empty<string>())
                    derived.Add(target);

                if (!Matches(effect.When, value))
                    continue;

                foreach (AtlasFormField target in Targets(fields, effect.Show).Where(x => !x.HiddenByRule)) target.Visible = true;
                foreach (AtlasFormField target in Targets(fields, effect.Hide)) target.Visible = false;
                foreach (AtlasFormField target in Targets(fields, effect.Require)) target.Required = true;
                foreach (AtlasFormField target in Targets(fields, effect.Unrequire)) target.Required = false;
                foreach (AtlasFormField target in Targets(fields, effect.Enable).Where(x => !x.DisabledByRule)) target.Disabled = false;
                foreach (AtlasFormField target in Targets(fields, effect.Disable)) target.Disabled = true;
                foreach (string target in (effect.Set?.Keys ?? Enumerable.Empty<string>()).Concat(effect.Clear ?? []))
                    derived.Add(target);
            }
        }

        return derived;
    }

    /// <summary>
    /// Applies Show, Hide, Require, Unrequire, Enable and Disable to controls that are not fields, such as file pickers.
    /// </summary>
    /// <remarks>
    /// The order and the rule priority are the same as in <see cref="Apply"/>. An effect names such a control by its Name.
    /// </remarks>
    public static void ApplyToControls(AtlasFormFields fields, Func<AtlasFormField, string?> current, IReadOnlyCollection<AtlasControlState> controls)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(controls);
        if (controls.Count == 0)
            return;

        foreach (AtlasFormField source in Sources(fields))
        {
            string? value = current(source);
            foreach (CPEffect effect in ServerEffects(source).Where(x => Matches(x.When, value)))
            {
                foreach (AtlasControlState target in Named(controls, effect.Show).Where(x => !x.HiddenByRule)) target.Visible = true;
                foreach (AtlasControlState target in Named(controls, effect.Hide)) target.Visible = false;
                foreach (AtlasControlState target in Named(controls, effect.Require)) target.Required = true;
                foreach (AtlasControlState target in Named(controls, effect.Unrequire)) target.Required = false;
                foreach (AtlasControlState target in Named(controls, effect.Enable).Where(x => !x.DisabledByRule)) target.Disabled = false;
                foreach (AtlasControlState target in Named(controls, effect.Disable)) target.Disabled = true;
            }
        }
    }

    /// <summary>
    /// Returns the values that the server sets.
    /// </summary>
    /// <remarks>
    /// SetFromOption takes the value from the Data of the selected option.
    /// A matching effect also gives its Set and Clear values. A later effect wins, as on the client.
    /// Only a source field that the user can change (visible and enabled) gives values. When the source field is
    /// hidden or disabled, its targets keep their saved values.
    /// </remarks>
    /// <param name="optionsOf">Returns the options of a control.</param>
    public static IEnumerable<(string Target, string? Value)> Values(AtlasFormFields fields, Func<AtlasFormField, string?> current,
        Func<CPApplicationControl, IReadOnlyList<CPKeyValue>> optionsOf)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(optionsOf);
        foreach (AtlasFormField source in Sources(fields).Where(CanChange))
        {
            string? value = current(source);
            foreach (CPEffect effect in ServerEffects(source))
            {
                if (!Matches(effect.When, value))
                    continue;

                foreach ((string target, string text) in effect.Set ?? [])
                    yield return (target, text);
                foreach (string target in effect.Clear ?? [])
                    yield return (target, null);
                if (effect.SetFromOption is not { Count: > 0 })
                    continue;

                CPKeyValue? option = source.Control.OptionsSource == null
                    ? null
                    : optionsOf(source.Control).FirstOrDefault(x => string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase));
                foreach ((string target, string key) in effect.SetFromOption)
                {
                    if (option?.Data != null && option.Data.TryGetValue(key, out string? data))
                        yield return (target, data);
                    else if (string.IsNullOrEmpty(value))
                        yield return (target, null);
                }
            }
        }
    }

    /// <summary>
    /// Returns true when the value matches the When of an effect: HasValue, Equals, NotEquals and In (ignore case).
    /// An effect with no When always matches.
    /// </summary>
    public static bool Matches(CPEffectCondition? when, string? value)
    {
        if (when == null)
            return true;
        bool hasValue = !string.IsNullOrEmpty(value);
        if (when.HasValue != null && when.HasValue.Value != hasValue)
            return false;
        if (when.Equals != null && !string.Equals(when.Equals, value, StringComparison.OrdinalIgnoreCase))
            return false;
        if (when.NotEquals != null && string.Equals(when.NotEquals, value, StringComparison.OrdinalIgnoreCase))
            return false;
        if (when.In != null && !when.In.Contains(value ?? "", StringComparer.OrdinalIgnoreCase))
            return false;
        return true;
    }

    /// <summary>Returns true when the user can change the field: it is visible and enabled after the rules and effects.</summary>
    public static bool CanChange(AtlasFormField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field.Visible && !field.Disabled && !field.HiddenByRule && !field.DisabledByRule;
    }

    /// <summary>Returns true when the effect runs only on a user change (On "Change"). The server does not apply it.</summary>
    public static bool IsChangeOnly(CPEffect effect) =>
        string.Equals(effect.On, "Change", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<CPEffect> ServerEffects(AtlasFormField source) =>
        source.Control.Effects!.Where(x => !IsChangeOnly(x));

    private static IEnumerable<AtlasFormField> Sources(AtlasFormFields fields) =>
        fields.All.Where(x => x.Control.Effects is { Count: > 0 } && !x.IsGridField);

    private static IEnumerable<AtlasFormField> Targets(AtlasFormFields fields, IEnumerable<string>? names) =>
        (names ?? []).SelectMany(name => fields.Find(name) is { } field ? new[] { field } : fields.FieldsOf(name));

    private static IEnumerable<AtlasControlState> Named(IReadOnlyCollection<AtlasControlState> controls, IEnumerable<string>? names) =>
        (names ?? []).SelectMany(name => controls.Where(x => string.Equals(x.Control.Name, name, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>
/// The effective state of a control that is not a field, such as a file picker, after the rules and the server effects.
/// </summary>
public sealed class AtlasControlState
{
    /// <summary>The control.</summary>
    public required CPApplicationControl Control { get; init; }

    /// <summary>True when the control shows.</summary>
    public bool Visible { get; set; }

    /// <summary>True when the user cannot change the control.</summary>
    public bool Disabled { get; set; }

    /// <summary>True when the control needs a value.</summary>
    public bool Required { get; set; }

    /// <summary>True when the JSON shows the control and a rule hides it. An effect cannot show it.</summary>
    public bool HiddenByRule { get; init; }

    /// <summary>True when the JSON enables the control and a rule disables it. An effect cannot enable it.</summary>
    public bool DisabledByRule { get; init; }

    /// <summary>True when the user can change the control: it shows and it is enabled.</summary>
    public bool CanChange => Visible && !Disabled;
}
