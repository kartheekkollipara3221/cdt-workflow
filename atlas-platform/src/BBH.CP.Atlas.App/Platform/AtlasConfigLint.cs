using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Typo checks of the JSON files of a platform app (review SV-07), part of the start-up check and --atlas-check.
/// The loader stays tolerant (unknown members are ignored at run time, B2); this check reports them:
/// unknown properties (warnings, with the JSON path), and names, tokens, patterns and action types that the
/// platform does not know (errors).
/// </summary>
public static partial class AtlasConfigLint
{
    /// <summary>The highest manifest SchemaVersion that this platform reads.</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly string[] ActionTypes = ["Save", "Close", "Delete", "Execute", "Navigate"];
    private static readonly string[] ClientActionTypes = ["Save", "Close", "Delete"];

    /// <summary>Unknown properties of a JSON file, as warnings: "file: path is not a property of Type".</summary>
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

    /// <summary>Errors of the names and values in a form.</summary>
    public static void CheckForm(AtlasAppManifest manifest, CPAtlasApplicationConfig form, Action<string> error, Action<string> warning)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(form);
        if (manifest.SchemaVersion is { } version && version > SchemaVersion)
            error($"the manifest SchemaVersion {version} is newer than this platform ({SchemaVersion}).");

        List<CPApplicationControl> controls = [.. (form.Section?.Controls ?? []).SelectMany(x => new[] { x }.Concat(x.ChildControls ?? []))];
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

        void Names(string where, IEnumerable<string>? list)
        {
            foreach (string name in list ?? [])
                if (!names.Contains(name))
                    error($"{where} names '{name}', which is not a control or field of the form.");
        }

        foreach (CPApplicationControl control in controls)
        {
            foreach (CPEffect effect in control.Effects ?? [])
            {
                string where = $"an effect of '{control.Name}'";
                Names(where, effect.Show);
                Names(where, effect.Hide);
                Names(where, effect.Require);
                Names(where, effect.Unrequire);
                Names(where, effect.Enable);
                Names(where, effect.Disable);
                Names(where, effect.Clear);
                Names(where, effect.Set?.Keys);
                Names(where, effect.SetFromOption?.Keys);
            }

            if (control.Validator?.Pattern is { Length: > 0 } pattern && !Compiles(pattern))
                error($"the Pattern of '{control.Name}' is not a valid regular expression.");
        }

        // Review 3 RV-15: a value change runs the effects of the changed control; Set, Clear and SetFromOption change
        // other controls, whose effects run in turn. A cycle (A sets B, B sets A, or A sets A) never ends in the client.
        Dictionary<string, HashSet<string>> writes = new(StringComparer.OrdinalIgnoreCase);
        foreach (CPApplicationControl control in controls.Where(x => x.Effects is { Count: > 0 }))
            writes[control.Name] = new(control.Effects!.SelectMany(x =>
                (x.Clear ?? []).Concat((IEnumerable<string>?)x.Set?.Keys ?? []).Concat((IEnumerable<string>?)x.SetFromOption?.Keys ?? [])), StringComparer.OrdinalIgnoreCase);
        foreach (string start in writes.Keys)
            if (ReachesItself(start))
                error($"the effects of '{start}' change a control whose effects change '{start}' again (a Set/Clear/SetFromOption cycle).");

        bool ReachesItself(string start)
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

        foreach (CPRule rule in (form.Rules ?? []).Concat(controls.SelectMany(x => x.Rules ?? [])))
        {
            Names("a rule", rule.ShowControls);
            Names("a rule", rule.HideControls);
            Names("a rule", rule.EnableControls);
            Names("a rule", rule.DisableControls);
        }

        foreach ((string name, string token) in form.Defaults ?? new Dictionary<string, string>())
        {
            Names("Defaults", [name]);
            if (!AtlasDefaultTokens.IsValid(token))
                error($"the default token '{token}' of '{name}' is not known (known: {string.Join(", ", AtlasDefaultTokens.Fixed)}, {{Options[n]}}).");
        }

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
