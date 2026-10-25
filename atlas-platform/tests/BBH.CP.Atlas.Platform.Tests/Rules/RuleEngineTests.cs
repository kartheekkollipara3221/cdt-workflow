using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Rules;

namespace BBH.CP.Atlas.Platform.Tests.Rules;

public class RuleEngineTests
{
    private readonly AtlasRuleEngine _engine = new();

    // The TTD rules (spec 01, 10.5).
    private static readonly List<CPRule> TtdRules =
    [
        new() { Roles = ["Read"], Disabled = true },
        new() { Roles = ["Prepare"], IsOwner = false, Disabled = true },
        new() { Roles = ["Prepare"], IsOwner = true, Disabled = false },
        new() { Roles = ["Owner"], Disabled = false }
    ];

    [Theory]
    [InlineData(new[] { "Read" }, false, true)]
    [InlineData(new[] { "Prepare" }, true, false)]
    [InlineData(new[] { "Prepare" }, false, true)]
    [InlineData(new[] { "Owner" }, false, false)]
    [InlineData(new[] { "Read", "Prepare" }, true, false)]
    public void MostPermissive_gives_the_TTD_access_table(string[] roles, bool isOwner, bool disabled)
    {
        AtlasRuleResult result = _engine.Evaluate(TtdRules, new(roles, [], isOwner), AtlasRuleMode.MostPermissive, AtlasRuleDefault.ReadOnly);

        Assert.True(result.Matched);
        Assert.Equal(disabled, result.Disabled);
    }

    [Fact]
    public void FirstMatch_keeps_the_current_behaviour_where_the_first_rule_wins()
    {
        // RE-02: AIN Admin + Read gets the first matching rule (read-only).
        AtlasRuleResult result = _engine.Evaluate(TtdRules, new(["Read", "Prepare"], [], true), AtlasRuleMode.FirstMatch, AtlasRuleDefault.Keep);

        Assert.True(result.Disabled);
    }

    [Theory]
    [InlineData(AtlasRuleDefault.Keep, false, true, false)]
    [InlineData(AtlasRuleDefault.ReadOnly, true, true, true)]
    [InlineData(AtlasRuleDefault.Hidden, true, false, true)]
    public void RuleDefault_applies_when_no_rule_matches(AtlasRuleDefault fallback, bool matched, bool visible, bool disabled)
    {
        AtlasRuleResult result = _engine.Evaluate(TtdRules, new(["Guest"], [], false), AtlasRuleMode.MostPermissive, fallback);

        Assert.Equal(matched, result.Matched);
        if (matched)
        {
            Assert.Equal(visible, result.Visible);
            Assert.Equal(disabled, result.Disabled);
        }
    }

    [Fact]
    public void A_target_with_no_rules_is_not_changed()
    {
        Assert.False(_engine.Evaluate(null, new(["Read"], [], false), AtlasRuleMode.MostPermissive, AtlasRuleDefault.Hidden).Matched);
    }

    [Fact]
    public void Flags_and_states_match()
    {
        List<CPRule> rules = [new() { Roles = ["@Write"], States = ["Draft"], Disabled = false }];

        Assert.False(_engine.Evaluate(rules, new(["Prepare", "@Read", "@Write"], ["draft"], false), AtlasRuleMode.MostPermissive, AtlasRuleDefault.ReadOnly).Disabled);
        Assert.True(_engine.Evaluate(rules, new(["Prepare", "@Read", "@Write"], ["Submitted"], false), AtlasRuleMode.MostPermissive, AtlasRuleDefault.ReadOnly).Disabled);
    }

    [Fact]
    public void MostPermissive_merges_the_name_lists()
    {
        List<CPRule> rules =
        [
            new() { Roles = ["A"], Disabled = true, HideControls = ["X"], DisableControls = ["Y"] },
            new() { Roles = ["B"], Disabled = true, ShowControls = ["X"], EnableControls = ["Y"] }
        ];

        AtlasRuleResult result = _engine.Evaluate(rules, new(["A", "B"], [], false), AtlasRuleMode.MostPermissive, AtlasRuleDefault.Keep);

        Assert.Contains("X", result.Show);
        Assert.DoesNotContain("X", result.Hide);
        Assert.Contains("Y", result.Enable);
        Assert.DoesNotContain("Y", result.Disable);
    }

    [Fact]
    public void ApplyToSection_uses_app_rules_keeps_json_disabled_controls_and_hides_buttons()
    {
        CPApplicationSection section = new()
        {
            Controls =
            [
                new() { Name = "Main", Label = "Main", Type = "section", ChildControls =
                    [
                        new() { Name = "Category", Label = "Category", Type = "select" },
                        new() { Name = "AccountNumber", Label = "No", Type = "text", Disabled = true }
                    ] },
                new() { Name = "WeeklyHour", Label = "Hours", Type = "grid",
                    Grid = new CPGrid { Columns = [new() { field = "MondayMinute", cellRendererParams = new() }] } }
            ],
            Buttons =
            [
                new() { Name = "Submit", Visible = true, Rules =
                    [ new() { Roles = ["Prepare"], IsOwner = true, Visible = true }, new() { Roles = ["Owner"], Visible = true } ] },
                new() { Name = "Close", Visible = true }
            ]
        };

        _engine.ApplyToSection(section, TtdRules, new(["Prepare"], [], IsOwner: false), AtlasRuleMode.MostPermissive, AtlasRuleDefault.ReadOnly);

        Assert.True(section.Controls![0].ChildControls![0].Disabled);
        Assert.True(section.Controls[1].Disabled);
        Assert.True(section.Controls[1].Grid!.Columns![0].cellRendererParams!.disabled);
        Assert.False(section.Buttons![0].Visible);
        Assert.True(section.Buttons[1].Visible);

        CPApplicationSection owner = new()
        {
            Controls = [new() { Name = "Main", Label = "Main", Type = "section", ChildControls =
                [ new() { Name = "Category", Label = "C", Type = "select" }, new() { Name = "AccountNumber", Label = "N", Type = "text", Disabled = true } ] }]
        };
        _engine.ApplyToSection(owner, TtdRules, new(["Owner"], [], false), AtlasRuleMode.MostPermissive, AtlasRuleDefault.ReadOnly);

        Assert.False(owner.Controls![0].ChildControls![0].Disabled);
        Assert.True(owner.Controls[0].ChildControls![1].Disabled);
    }
}
