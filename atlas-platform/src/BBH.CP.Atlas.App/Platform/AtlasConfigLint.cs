using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Finds typing errors in the JSON files of a platform app.
/// </summary>
/// <remarks>
/// The start-up check and --atlas-check use these checks.
/// The loader ignores unknown members at run time, so this check reports them.
/// An unknown property gives a warning with the JSON path.
/// An unknown name, token, pattern or action type gives an error.
/// </remarks>
public static partial class AtlasConfigLint
{
    /// <summary>The highest manifest SchemaVersion that this platform reads.</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly string[] ActionTypes = ["Save", "Close", "Delete", "Execute", "Navigate"];
    private static readonly string[] ClientActionTypes = ["Save", "Close", "Delete"];

    /// <summary>
    /// Reports each unknown property of a JSON file as a warning: "file: path is not a property of Type".
    /// </summary>
    /// <remarks>A missing file or a file that is not valid JSON gives no warning. The loader reports it.</remarks>
    public static void CheckUnknownProperties(string path, Type type, Action<string> warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        if (!File.Exists(path))
            return;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            Walk(document.RootElement, type, "", Path.GetFileName(path), warning);
        }
        catch (JsonException)
        {
            // Not valid JSON: the loader reports it.
        }
    }

    /// <summary>Reports the errors in the names and values of a form, and the button actions that the form does not do.</summary>
    /// <param name="table">The main table of the app. A SetFromOption target can be one of its columns (a data-only target).</param>
    public static void CheckForm(AtlasAppManifest manifest, CPAtlasApplicationConfig form, Action<string> error, Action<string> warning,
        AtlasTable? table = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(form);
        if (manifest.SchemaVersion is { } version && version > SchemaVersion)
            error($"the manifest SchemaVersion {version} is newer than this platform ({SchemaVersion}).");

        List<CPApplicationControl> controls = [.. (form.Section?.Controls ?? []).SelectMany(x => new[] { x }.Concat(x.ChildControls ?? []))];
        HashSet<string> names = DuplicateNames(controls, error);
        EffectTargets(controls, names, table, error);
        EffectCycles(controls, error);
        RuleTargets(form, controls, names, error);
        Defaults(form, names, error);
        ButtonActions(form, error, warning);
        WorkflowSteps(form, names, table, error);
    }

    /// <summary>
    /// Checks the workflow binding of a form with steps: the state columns, the states, the Set of each step and the
    /// one submit button.
    /// </summary>
    /// <remarks>A form with no Steps is not checked here.</remarks>
    private static void WorkflowSteps(CPAtlasApplicationConfig form, HashSet<string> names, AtlasTable? table, Action<string> error)
    {
        CPWorkflow? workflow = form.Section?.Workflow;
        if (workflow?.Steps is not { Count: > 0 } steps)
            return;

        Column(workflow.StateField, "StateField", table, error, required: true);
        Column(workflow.StatusField, "StatusField", table, error, required: false);
        HashSet<string> states = new((IEnumerable<string>?)workflow.StatusLabels?.Keys ?? [], StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(workflow.InitialState))
            error("the Workflow has Steps but no InitialState.");
        else if (!states.Contains(workflow.InitialState))
            error($"the Workflow InitialState '{workflow.InitialState}' is not a key of StatusLabels.");

        foreach ((string state, CPWorkflowStep step) in steps)
        {
            if (!states.Contains(state))
                error($"the Workflow step '{state}' is not a key of StatusLabels.");
            foreach ((string name, string token) in step?.Set ?? new Dictionary<string, string>())
            {
                string where = $"the Set of the Workflow step '{state}'";
                if (!names.Contains(name) && table?.HasColumn(name) != true)
                    error($"{where} names '{name}', which is not a control or field of the form, or a column of the table.");
                else if (ConfigSectionService.SystemColumns.Contains(name))
                    error($"{where} names the system column '{name}', which only the server writes.");
                if (!string.Equals(token, AtlasDefaultTokens.Now, StringComparison.OrdinalIgnoreCase) && !AtlasDefaultTokens.IsValid(token))
                    error($"{where} has the token '{token}' for '{name}', which is not known (known: {AtlasDefaultTokens.Now}, {string.Join(", ", AtlasDefaultTokens.Fixed)}).");
            }
        }

        int saveButtons = AtlasWorkflowSteps.SaveButtons(form.Section).Count;
        if (saveButtons != 1)
            error($"the form has Workflow Steps and {saveButtons} buttons with the action Save; a form with steps has exactly one submit button.");
    }

    private static void Column(string? name, string key, AtlasTable? table, Action<string> error, bool required)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (required)
                error($"the Workflow has Steps but no {key}.");
        }
        else if (table != null && !table.HasColumn(name))
            error($"the Workflow {key} '{name}' is not a column of {table.FullName}.");
    }

    /// <summary>Reports a control name that occurs more than one time. Returns the names of the controls, grid columns and fields.</summary>
    private static HashSet<string> DuplicateNames(List<CPApplicationControl> controls, Action<string> error)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (CPApplicationControl control in controls)
        {
            if (!names.Add(control.Name))
                error($"the control name '{control.Name}' is used more than one time.");
            foreach (CPGridColumn column in control.Grid?.Columns ?? [])
                if (!string.IsNullOrWhiteSpace(column.field))
                    names.Add(column.field);
        }
        foreach (CPApplicationControl control in controls.Where(x => !string.IsNullOrWhiteSpace(x.Field)))
            names.Add(control.Field!);
        return names;
    }

    private static void Names(HashSet<string> names, string where, IEnumerable<string>? list, Action<string> error)
    {
        foreach (string name in list ?? [])
            if (!names.Contains(name))
                error($"{where} names '{name}', which is not a control or field of the form.");
    }

    /// <summary>Checks the targets of the effects, the HeaderFromDate controls and the Pattern of each control.</summary>
    private static void EffectTargets(List<CPApplicationControl> controls, HashSet<string> names, AtlasTable? table, Action<string> error)
    {
        foreach (CPApplicationControl control in controls)
        {
            foreach (CPEffect effect in control.Effects ?? [])
            {
                string where = $"an effect of '{control.Name}'";
                if (effect.On != null && !AtlasServerEffects.IsChangeOnly(effect))
                    error($"{where} has On '{effect.On}'. Use \"Change\" or leave it out.");
                Names(names, where, effect.Show, error);
                Names(names, where, effect.Hide, error);
                Names(names, where, effect.Require, error);
                Names(names, where, effect.Unrequire, error);
                Names(names, where, effect.Enable, error);
                Names(names, where, effect.Disable, error);
                Names(names, where, effect.Clear, error);
                Names(names, where, effect.Set?.Keys, error);
                foreach (string target in (IEnumerable<string>?)effect.SetFromOption?.Keys ?? [])
                    if (!names.Contains(target) && table?.HasColumn(target) != true)
                        error($"{where} names '{target}', which is not a control or field of the form, or a column of the table.");
                    else if (!names.Contains(target) && ConfigSectionService.SystemColumns.Contains(target))
                        error($"{where} names the system column '{target}', which only the server writes.");
            }

            foreach (CPGridColumn column in control.Grid?.Columns ?? [])
                if (column.HeaderFromDate is { } header)
                    Names(names, $"the HeaderFromDate of the column '{column.field}'", [header.Control ?? ""], error);

            if (control.Validator?.Pattern is { Length: > 0 } pattern && !Compiles(pattern))
                error($"the Pattern of '{control.Name}' is not a valid regular expression.");
            if (control.Validator?.Step is <= 0)
                error($"the Step of '{control.Name}' must be more than 0.");
        }
    }

    /// <summary>
    /// Reports each control whose Set, Clear or SetFromOption effects come back to the same control.
    /// </summary>
    /// <remarks>
    /// A value change runs the effects of the changed control. Set, Clear and SetFromOption change other controls.
    /// The effects of these controls run in turn. A cycle (A sets B, B sets A, or A sets A) never ends in the client.
    /// </remarks>
    private static void EffectCycles(List<CPApplicationControl> controls, Action<string> error)
    {
        Dictionary<string, HashSet<string>> writes = new(StringComparer.OrdinalIgnoreCase);
        foreach (CPApplicationControl control in controls.Where(x => x.Effects is { Count: > 0 }))
            writes[control.Name] = new(control.Effects!.SelectMany(x =>
                (x.Clear ?? []).Concat((IEnumerable<string>?)x.Set?.Keys ?? []).Concat((IEnumerable<string>?)x.SetFromOption?.Keys ?? [])), StringComparer.OrdinalIgnoreCase);
        foreach (string start in writes.Keys)
            if (ReachesItself(writes, start))
                error($"the effects of '{start}' change a control whose effects change '{start}' again (a Set/Clear/SetFromOption cycle).");
    }

    private static bool ReachesItself(Dictionary<string, HashSet<string>> writes, string start)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        Stack<string> open = new(writes[start]);
        while (open.Count > 0)
        {
            string name = open.Pop();
            if (string.Equals(name, start, StringComparison.OrdinalIgnoreCase))
                return true;
            if (seen.Add(name) && writes.TryGetValue(name, out HashSet<string>? next))
                foreach (string target in next)
                    open.Push(target);
        }
        return false;
    }

    private static void RuleTargets(CPAtlasApplicationConfig form, List<CPApplicationControl> controls, HashSet<string> names, Action<string> error)
    {
        foreach (CPRule rule in (form.Rules ?? []).Concat(controls.SelectMany(x => x.Rules ?? [])))
        {
            Names(names, "a rule", rule.ShowControls, error);
            Names(names, "a rule", rule.HideControls, error);
            Names(names, "a rule", rule.EnableControls, error);
            Names(names, "a rule", rule.DisableControls, error);
        }
    }

    private static void Defaults(CPAtlasApplicationConfig form, HashSet<string> names, Action<string> error)
    {
        foreach ((string name, string token) in form.Defaults ?? new Dictionary<string, string>())
        {
            Names(names, "Defaults", [name], error);
            if (!AtlasDefaultTokens.IsValid(token))
                error($"the default token '{token}' of '{name}' is not known (known: {string.Join(", ", AtlasDefaultTokens.Fixed)}, {AtlasDefaultTokens.TodayNext}, {{Options[n]}}).");
        }
    }

    private static void ButtonActions(CPAtlasApplicationConfig form, Action<string> error, Action<string> warning)
    {
        foreach (CPButton button in form.Section?.Buttons ?? [])
        {
            if (button.Action?.Type is not { } type)
                continue;
            if (!ActionTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                error($"the button '{button.Name}' has the action '{type}' (known: {string.Join(", ", ActionTypes)}).");
            else if (!ClientActionTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                warning($"the button '{button.Name}' has the action '{type}', which the platform form does not do yet (it closes the form).");
        }
    }

    private static bool Compiles(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Walk(JsonElement element, Type type, string path, string file, Action<string> warning)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (element.ValueKind == JsonValueKind.Array && ElementType(type) is { } item)
        {
            int index = 0;
            foreach (JsonElement child in element.EnumerateArray())
                Walk(child, item, $"{path}[{index++}]", file, warning);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object || !IsDto(type))
            return;

        if (DictionaryValueType(type) is { } valueType)
        {
            foreach (JsonProperty property in element.EnumerateObject())
                Walk(property.Value, valueType, $"{path}.{property.Name}", file, warning);
            return;
        }

        Dictionary<string, PropertyInfo> properties = Properties(type);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string at = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
            if (properties.TryGetValue(property.Name, out PropertyInfo? info))
                Walk(property.Value, info.PropertyType, at, file, warning);
            else
                warning($"{file}: '{at}' is not a property of {type.Name}; it is ignored (check the spelling).");
        }
    }

    private static Dictionary<string, PropertyInfo> Properties(Type type)
    {
        Dictionary<string, PropertyInfo> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always } || property.SetMethod == null)
                continue;
            map[property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name] = property;
        }
        return map;
    }

    private static bool IsDto(Type type) =>
        type.IsClass && type != typeof(string) && type != typeof(object) && type != typeof(JsonElement)
        && (type.Namespace?.StartsWith("BBH.CP.Atlas", StringComparison.Ordinal) == true || DictionaryValueType(type) != null);

    private static Type? ElementType(Type type) =>
        type.IsArray ? type.GetElementType()
        : type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type) && DictionaryValueType(type) == null ? type.GetGenericArguments()[0]
        : null;

    private static Type? DictionaryValueType(Type type) =>
        type.IsGenericType && type.GetGenericArguments() is [var key, var value] && key == typeof(string)
            && typeof(IDictionary).IsAssignableFrom(type) && IsDtoOrCollection(value) ? value : null;

    private static bool IsDtoOrCollection(Type type) =>
        type.Namespace?.StartsWith("BBH.CP.Atlas", StringComparison.Ordinal) == true || (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string));
}
