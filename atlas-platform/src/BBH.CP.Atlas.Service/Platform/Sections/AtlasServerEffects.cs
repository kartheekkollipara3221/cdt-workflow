using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// The server part of the control Effects (spec 01, 5.2; review SV-03, SV-04): the same actions as the client
/// (WebUi platform/form/atlas-effects.ts). An effect cannot show a field that a rule hid or enable a field that a
/// rule disabled.
/// </summary>
public static class AtlasServerEffects
{
    /// <summary>
    /// Applies Show/Hide/Require/Unrequire/Enable/Disable to the fields. Returns the names that the server sets: the
    /// SetFromOption targets, and the Set and Clear targets of a matching effect.
    /// </summary>
    public static HashSet<string> Apply(AtlasFormFields fields, Func<AtlasFormField, string?> current)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(current);
        HashSet<string> derived = new(StringComparer.OrdinalIgnoreCase);
        foreach (AtlasFormField source in Sources(fields))
        {
            string? value = current(source);
            foreach (CPEffect effect in source.Control.Effects!)
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
    /// The server-set values: SetFromOption from the Data of the selected option, and the Set and Clear values of a
    /// matching effect. Later effects win, as on the client. <paramref name="optionsOf"/> gives the options of a control.
    /// </summary>
    public static IEnumerable<(string Target, string? Value)> Values(AtlasFormFields fields, Func<AtlasFormField, string?> current,
        Func<CPApplicationControl, IReadOnlyList<CPKeyValue>> optionsOf)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(optionsOf);
        foreach (AtlasFormField source in Sources(fields))
        {
            string? value = current(source);
            foreach (CPEffect effect in source.Control.Effects!)
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

    /// <summary>The When of an effect: HasValue, Equals, NotEquals and In (ignore case). No When = always.</summary>
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

    private static IEnumerable<AtlasFormField> Sources(AtlasFormFields fields) =>
        fields.All.Where(x => x.Control.Effects is { Count: > 0 } && !x.IsGridField);

    private static IEnumerable<AtlasFormField> Targets(AtlasFormFields fields, IEnumerable<string>? names) =>
        (names ?? []).SelectMany(name => fields.Find(name) is { } field ? new[] { field } : fields.FieldsOf(name));
}
