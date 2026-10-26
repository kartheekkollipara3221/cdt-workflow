using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>Review SV-08: the save steps without a database.</summary>
public class SectionStepTests
{
    private static readonly AtlasTable Table = new("TTDV2", "General",
    [
        new AtlasColumn("ID", "int", false, null, 10, 0, true, false),
        new AtlasColumn("Category", "varchar", true, 20, 0, 0, false, false),
        new AtlasColumn("Amount", "decimal", true, null, 19, 2, false, false),
        new AtlasColumn("MondayMinute", "int", true, null, 10, 0, false, false),
        new AtlasColumn("TuesdayMinute", "int", true, null, 10, 0, false, false),
        new AtlasColumn("WeeklyTotal", "int", true, null, 10, 0, false, false),
        new AtlasColumn("Note", "varchar", true, 100, 0, 0, false, false)
    ]);

    [Theory]
    [InlineData("Sum(MondayMinute, TuesdayMinute)", 150)]
    [InlineData("count(MondayMinute, TuesdayMinute)", 2)]
    [InlineData("Min(MondayMinute,TuesdayMinute)", 60)]
    [InlineData("Max(MondayMinute, TuesdayMinute)", 90)]
    public void Computed_functions_use_one_table(string expression, int expected)
    {
        Dictionary<string, object?> record = new(StringComparer.OrdinalIgnoreCase) { ["MondayMinute"] = 60, ["TuesdayMinute"] = 90 };

        Assert.Null(AtlasComputedFields.Compute(Table, record, "WeeklyTotal", expression));
        Assert.Equal(expected, record["WeeklyTotal"]);
    }

    [Theory]
    [InlineData("Avg(MondayMinute)")]
    [InlineData("Sum MondayMinute")]
    [InlineData("")]
    public void An_unknown_Computed_expression_is_an_error(string expression)
    {
        Assert.False(AtlasComputedFields.TryParse(expression, out _, out _));
        Assert.Equal("ComputedInvalid", AtlasComputedFields.Compute(Table, [], "WeeklyTotal", expression)!.Code);
    }

    [Fact]
    public void Min_of_no_value_is_null()
    {
        Dictionary<string, object?> record = new(StringComparer.OrdinalIgnoreCase) { ["WeeklyTotal"] = 5 };

        Assert.Null(AtlasComputedFields.Compute(Table, record, "WeeklyTotal", "Min(MondayMinute)"));
        Assert.Null(record["WeeklyTotal"]);
    }

    [Theory]
    [InlineData("{User.LanID}", "jdoe")]
    [InlineData("{user.displayname}", "John Doe")]
    [InlineData("{Today}", "2026-10-25")]
    [InlineData("{Options[1]}", "B")]
    [InlineData("{Options[5]}", null)]
    [InlineData("Draft", "Draft")]
    public void Default_tokens_resolve(string token, string? expected)
    {
        CPKeyValue[] options = [new() { Value = "A", Label = "A" }, new() { Value = "B", Label = "B" }];

        string? value = AtlasDefaultTokens.Resolve(token, "jdoe", "John Doe", new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero), options);

        Assert.Equal(expected, value);
        Assert.True(AtlasDefaultTokens.IsValid(token));
    }

    [Fact]
    public void An_unknown_token_is_not_valid()
    {
        Assert.False(AtlasDefaultTokens.IsValid("{Todya}"));
        Assert.False(AtlasDefaultTokens.IsValid("{Options[x]}"));
    }

    [Fact]
    public void An_effect_shows_and_sets_but_cannot_show_a_field_that_a_rule_hid()
    {
        AtlasFormFields fields = Fields(
            Control("Category", effects: [new CPEffect { When = new CPEffectCondition { Equals = "OTHER" }, Show = ["Note", "Amount"], Set = new() { ["MondayMinute"] = "0" } }]),
            Control("Note", visible: false),
            Control("Amount", visible: false),
            Control("MondayMinute"));
        fields.Find("Amount")!.HiddenByRule = true;

        HashSet<string> derived = AtlasServerEffects.Apply(fields, field => field.Name == "Category" ? "other" : null);

        Assert.True(fields.Find("Note")!.Visible);
        Assert.False(fields.Find("Amount")!.Visible);
        Assert.Equal(["MondayMinute"], derived);
        Assert.Equal([("MondayMinute", (string?)"0")], AtlasServerEffects.Values(fields, field => field.Name == "Category" ? "OTHER" : null, _ => []).ToList());
    }

    [Theory]
    [InlineData(null, "x", true)]
    [InlineData(true, "", false)]
    [InlineData(false, "", true)]
    public void Matches_checks_HasValue(bool? hasValue, string value, bool expected)
    {
        CPEffectCondition? when = hasValue == null ? null : new CPEffectCondition { HasValue = hasValue };
        Assert.Equal(expected, AtlasServerEffects.Matches(when, value));
    }

    [Fact]
    public void The_allow_list_ignores_server_set_and_unchanged_values_and_refuses_changed_disabled_values()
    {
        AtlasFormFields fields = Fields(Control("Category"), Control("Amount", disabled: true), Control("Note", disabled: true), Control("WeeklyTotal"));
        Dictionary<string, string?> previous = new(StringComparer.OrdinalIgnoreCase) { ["Amount"] = "1250000.50", ["Note"] = "old" };
        Dictionary<string, string?> submitted = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Category"] = "OTHER", ["Amount"] = "1250000.5", ["Note"] = "new", ["WeeklyTotal"] = "999"
        };
        List<CPPageError> errors = [];
        List<string> ignored = [];

        Dictionary<AtlasFormField, string?> accepted = AtlasAllowList.Check(submitted, fields, Table, previous,
            field => field.Field == "WeeklyTotal", (_, _, _) => null, errors, (field, reason) => ignored.Add($"{field.Name} {reason}"));

        Assert.Equal(["Category"], accepted.Keys.Select(x => x.Name));
        Assert.Equal(["Amount Unchanged", "WeeklyTotal ServerSet"], ignored.Order());
        CPPageError error = Assert.Single(errors);
        Assert.Equal(("Note", "NotAllowed"), (error.Field, error.Code));
    }

    private static AtlasFormFields Fields(params CPApplicationControl[] controls) =>
        AtlasFormFields.From(new CPApplicationSection { Controls = [.. controls] });

    private static CPApplicationControl Control(string name, bool visible = true, bool disabled = false, List<CPEffect>? effects = null) =>
        new() { Name = name, Label = name, Type = "text", Field = name, Visible = visible, Disabled = disabled, Effects = effects };
}
