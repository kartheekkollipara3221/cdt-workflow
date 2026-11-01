using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.Service.Platform.Rules;

/// <summary>The inputs of a rule (spec 04, 4.1): role names + flags (@Read, @Write, @Admin), states, owner.</summary>
public sealed record AtlasRuleContext(IReadOnlyList<string> RoleTokens, IReadOnlyList<string> States, bool IsOwner);

/// <summary>
/// What the rules changed in a section (review FABLE F01): the fields that the section showed (or enabled) before the
/// rules and the rules hid (or disabled). Names are the output names of <see cref="AtlasFormFields"/>: the control
/// Name, or the column field of a SingleRowFields grid. A field that the JSON hides is not in HiddenByRule.
/// </summary>
public sealed record AtlasRuleOutcome(IReadOnlySet<string> HiddenByRule, IReadOnlySet<string> DisabledByRule);

/// <summary>The result for one target (spec 04, 4.1).</summary>
public sealed class AtlasRuleResult
{
    public static AtlasRuleResult NoChange { get; } = new() { Matched = false };

    /// <summary>False: no rule matched and RuleDefault is Keep (or the target has no rules). Do not change the target.</summary>
    public bool Matched { get; init; }

    public bool Visible { get; init; } = true;

    public bool Disabled { get; init; }

    public IReadOnlySet<string> Show { get; init; } = EmptySet;

    public IReadOnlySet<string> Hide { get; init; } = EmptySet;

    public IReadOnlySet<string> Enable { get; init; } = EmptySet;

    public IReadOnlySet<string> Disable { get; init; } = EmptySet;

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// One rule engine for all targets (spec 04, 4.1). FirstMatch = the current behaviour; MostPermissive = all matching
/// rules count (4.2). RuleDefault applies when a target has rules but none matches (4.3).
/// </summary>
public interface IAtlasRuleEngine
{
    AtlasRuleResult Evaluate(IReadOnlyList<CPRule>? rules, AtlasRuleContext context, AtlasRuleMode mode, AtlasRuleDefault fallback);

    /// <summary>
    /// Applies the rules to the section-level controls (their own Rules, else the app-level rules), their child
    /// controls and the buttons. The rules can hide or disable; a child control that the JSON disables stays
    /// disabled unless a rule names it in EnableControls. Returns the fields that the rules hid or disabled.
    /// </summary>
    AtlasRuleOutcome ApplyToSection(CPApplicationSection section, IReadOnlyList<CPRule>? appRules, AtlasRuleContext context,
        AtlasRuleMode mode, AtlasRuleDefault fallback);
}

public sealed class AtlasRuleEngine : IAtlasRuleEngine
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public AtlasRuleResult Evaluate(IReadOnlyList<CPRule>? rules, AtlasRuleContext context, AtlasRuleMode mode, AtlasRuleDefault fallback)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (rules is not { Count: > 0 })
            return AtlasRuleResult.NoChange;

        List<CPRule> matched = [.. rules.Where(rule => Matches(rule, context))];
        if (matched.Count == 0)
        {
            return fallback switch
            {
                AtlasRuleDefault.ReadOnly => new AtlasRuleResult { Matched = true, Visible = true, Disabled = true },
                AtlasRuleDefault.Hidden => new AtlasRuleResult { Matched = true, Visible = false, Disabled = true },
                _ => AtlasRuleResult.NoChange
            };
        }

        if (mode == AtlasRuleMode.FirstMatch)
        {
            CPRule first = matched[0];
            return new AtlasRuleResult
            {
                Matched = true,
                Visible = first.Visible,
                Disabled = first.Disabled,
                Show = Set(first.ShowControls),
                Hide = Set(first.HideControls),
                Enable = Set(first.EnableControls),
                Disable = Set(first.DisableControls)
            };
        }

        HashSet<string> show = Set(matched.SelectMany(x => x.ShowControls ?? []));
        HashSet<string> enable = Set(matched.SelectMany(x => x.EnableControls ?? []));
        HashSet<string> hide = Set(matched.SelectMany(x => x.HideControls ?? []));
        HashSet<string> disable = Set(matched.SelectMany(x => x.DisableControls ?? []));
        hide.ExceptWith(show);
        disable.ExceptWith(enable);

        return new AtlasRuleResult
        {
            Matched = true,
            Visible = matched.Any(x => x.Visible),
            Disabled = matched.All(x => x.Disabled),
            Show = show,
            Hide = hide,
            Enable = enable,
            Disable = disable
        };
    }

    public AtlasRuleOutcome ApplyToSection(CPApplicationSection section, IReadOnlyList<CPRule>? appRules, AtlasRuleContext context,
        AtlasRuleMode mode, AtlasRuleDefault fallback)
    {
        ArgumentNullException.ThrowIfNull(section);
        HashSet<string> hidden = Set(null);
        HashSet<string> disabled = Set(null);

        foreach (CPApplicationControl control in section.Controls ?? [])
        {
            IReadOnlyList<CPRule>? rules = control.Rules is { Count: > 0 } own ? own : appRules;
            AtlasRuleResult result = Evaluate(rules, context, mode, fallback);
            if (!result.Matched)
                continue;

            (bool visibleBefore, bool disabledBefore) = (control.Visible, control.Disabled);
            List<FieldState> before = FieldStates(control, true, false);
            control.Visible = control.Visible && result.Visible;
            control.Disabled = control.Disabled || result.Disabled;
            DisableGrid(control);
            Compare(before, FieldStates(control, true, false), hidden, disabled);

            foreach (CPApplicationControl child in control.ChildControls ?? [])
            {
                before = FieldStates(child, visibleBefore, disabledBefore);
                bool visible = child.Visible && result.Visible;
                if (result.Hide.Contains(child.Name))
                    visible = false;
                if (result.Show.Contains(child.Name) && result.Visible)
                    visible = true;
                child.Visible = visible;

                if (child.Visible)
                {
                    child.Disabled = !result.Enable.Contains(child.Name)
                        && (child.Disabled || result.Disabled || result.Disable.Contains(child.Name));
                    DisableGrid(child);
                }

                Compare(before, FieldStates(child, control.Visible, control.Disabled), hidden, disabled);
            }
        }

        foreach (CPButton button in section.Buttons ?? [])
        {
            AtlasRuleResult result = Evaluate(button.Rules, context, mode, fallback == AtlasRuleDefault.Keep ? AtlasRuleDefault.Keep : AtlasRuleDefault.Hidden);
            if (!result.Matched)
                continue;

            button.Visible = button.Visible && result.Visible;
            button.Disabled = button.Disabled || result.Disabled;
        }

        return new AtlasRuleOutcome(hidden, disabled);
    }

    private readonly record struct FieldState(string Name, bool Visible, bool Disabled);

    /// <summary>The fields of a control with their effective state, as <see cref="AtlasFormFields.From(CPApplicationSection)"/> finds them.</summary>
    private static List<FieldState> FieldStates(CPApplicationControl control, bool parentVisible, bool parentDisabled)
    {
        bool visible = parentVisible && control.Visible;
        bool disabled = parentDisabled || control.Disabled;
        if (AtlasFormFields.IsSingleRowGrid(control))
            return [.. (control.Grid!.Columns ?? [])
                .Where(column => !string.IsNullOrWhiteSpace(column.field))
                .Select(column => new FieldState(column.field!, visible, disabled || column.cellRendererParams?.disabled == true))];

        return string.IsNullOrWhiteSpace(control.Field) ? [] : [new FieldState(control.Name, visible, disabled)];
    }

    private static void Compare(List<FieldState> before, List<FieldState> after, HashSet<string> hidden, HashSet<string> disabled)
    {
        for (int i = 0; i < before.Count; i++)
        {
            if (before[i].Visible && !after[i].Visible)
                hidden.Add(before[i].Name);
            if (!before[i].Disabled && after[i].Disabled)
                disabled.Add(before[i].Name);
        }
    }

    private static bool Matches(CPRule rule, AtlasRuleContext context)
    {
        bool roleCondition = rule.Roles is not { Count: > 0 } || rule.Roles.Intersect(context.RoleTokens, Comparer).Any();
        bool stateCondition = rule.States is not { Count: > 0 } || rule.States.Intersect(context.States, Comparer).Any();
        bool ownerCondition = rule.IsOwner == null || rule.IsOwner.Value == context.IsOwner;
        return roleCondition && stateCondition && ownerCondition;
    }

    private static void DisableGrid(CPApplicationControl control)
    {
        if (!control.Disabled || control.Grid?.Columns == null)
            return;

        foreach (CPGridColumn column in control.Grid.Columns)
            if (column.cellRendererParams != null)
                column.cellRendererParams.disabled = true;
    }

    private static HashSet<string> Set(IEnumerable<string>? names) => new(names ?? [], Comparer);
}
