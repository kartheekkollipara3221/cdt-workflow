using System.Text.Json;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>Review SV-04: the server runs the same validator cases as the client spec (atlas-validator-cases.ts).</summary>
public class ValidatorParityTests
{
    public static TheoryData<int> Cases()
    {
        TheoryData<int> data = [];
        for (int i = 0; i < Load().Count; i++)
            data.Add(i);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_server_gives_the_same_result_as_the_client(int index)
    {
        Case item = Load()[index];
        AtlasFormField field = new()
        {
            Name = "Field",
            Field = "Field",
            Label = "Field",
            Control = new CPApplicationControl { Name = "Field", Label = "Field", Type = "text", Validator = item.Validator },
            Required = item.Validator.Required
        };

        CPPageError? error = AtlasFieldValidator.Validate(field, item.Value);

        Assert.Equal(item.Code, error?.Code);
        Assert.Equal(item.Message, error?.Message);
    }

    [Theory]
    [InlineData(@"\d{4}", @"^\d{4}$")]
    [InlineData("^A", "^A$")]
    [InlineData("B$", "^B$")]
    [InlineData("^C$", "^C$")]
    public void A_pattern_is_anchored_as_Angular_does(string pattern, string anchored) =>
        Assert.Equal(anchored, AtlasFieldValidator.AnchoredPattern(pattern));

    private sealed record Case(CPValidator Validator, string Value, string? Code, string? Message);

    private static List<Case> Load()
    {
        string path = Path.Combine(TestPaths.AtlasPlatformRoot(), "WebUi", "src", "app", "platform", "form", "atlas-validator-cases.ts");
        string text = File.ReadAllText(path);
        int start = text.IndexOf("= [", StringComparison.Ordinal) + 2;
        int end = text.LastIndexOf("];", StringComparison.Ordinal) + 1;
        return JsonSerializer.Deserialize<List<Case>>(text[start..end], new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
