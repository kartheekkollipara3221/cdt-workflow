using System.Text.Json;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>Review FABLE 6.1: the server runs the same effect and Computed cases as the client spec (atlas-effect-cases.ts).</summary>
public class EffectParityTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static TheoryData<int> EffectCases() => Indexes(Load<EffectCase>("ATLAS_EFFECT_CASES").Count);

    public static TheoryData<int> ComputedCases() => Indexes(Load<ComputedCase>("ATLAS_COMPUTED_CASES").Count);

    [Theory]
    [MemberData(nameof(EffectCases))]
    public void The_server_matches_an_effect_condition_as_the_client(int index)
    {
        EffectCase item = Load<EffectCase>("ATLAS_EFFECT_CASES")[index];

        Assert.Equal(item.Matches, AtlasServerEffects.Matches(item.When, item.Value));
    }

    [Theory]
    [MemberData(nameof(ComputedCases))]
    public void The_server_computes_the_same_value_as_the_client(int index)
    {
        ComputedCase item = Load<ComputedCase>("ATLAS_COMPUTED_CASES")[index];
        AtlasTable table = new("dbo", "T", [Column("ID"), Column("A"), Column("B"), Column("C"), Column("Result")]);
        Dictionary<string, object?> record = new(StringComparer.OrdinalIgnoreCase) { ["Result"] = null };
        foreach ((string name, string value) in item.Values)
            record[name] = value.Length == 0 ? null : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Null(AtlasComputedFields.Compute(table, record, "Result", item.Expression));
        Assert.Equal(item.Result, (int?)record["Result"]);
    }

    private static AtlasColumn Column(string name) => new(name, "int", true, null, 10, 0, false, false);

    private static TheoryData<int> Indexes(int count)
    {
        TheoryData<int> data = [];
        for (int i = 0; i < count; i++)
            data.Add(i);
        return data;
    }

    private sealed record EffectCase(CPEffectCondition? When, string Value, bool Matches);

    private sealed record ComputedCase(string Expression, Dictionary<string, string> Values, int? Result);

    /// <summary>The JSON array after "NAME: ...[] = " up to its "];".</summary>
    private static List<T> Load<T>(string name)
    {
        string path = Path.Combine(TestPaths.AtlasPlatformRoot(), "WebUi", "src", "app", "platform", "form", "atlas-effect-cases.ts");
        string text = File.ReadAllText(path);
        int at = text.IndexOf("export const " + name, StringComparison.Ordinal);
        int start = text.IndexOf("= [", at, StringComparison.Ordinal) + 2;
        int end = text.IndexOf("];", start, StringComparison.Ordinal) + 1;
        return JsonSerializer.Deserialize<List<T>>(text[start..end], Options)!;
    }
}
