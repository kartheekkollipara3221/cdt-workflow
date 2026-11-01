using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;

namespace BBH.CP.Atlas.Platform.Tests.Security;

// The TTD access table (spec 01, 10.5), with the TTDV2 Permissions block.
public class AccessServiceTests
{
    private static readonly CPAtlasApplicationConfig Form = new()
    {
        OwnerFields = ["CreatedBy", "TaxPreparerLanId"],
        Permissions = new CPPermissions
        {
            View = ["@Read"],
            Create = ["Prepare", "Owner"],
            Edit = new CPEditPermission { Own = ["Prepare"], All = ["Owner"] },
            Delete = ["Owner"],
            History = ["@Read"]
        }
    };

    private static readonly Dictionary<string, object?> OwnRecord = new() { ["CreatedBy"] = "me", ["TaxPreparerLanId"] = "other" };
    private static readonly Dictionary<string, object?> PreparerRecord = new() { ["CreatedBy"] = "other", ["TaxPreparerLanId"] = "ME" };
    private static readonly Dictionary<string, object?> OtherRecord = new() { ["CreatedBy"] = "other", ["TaxPreparerLanId"] = "other2" };

    public static TheoryData<string, AtlasOperation, string, bool> Matrix => new()
    {
        // role,     operation,             record,     allowed
        { "Read",    AtlasOperation.View,   "other",    true },
        { "Read",    AtlasOperation.Create, "new",      false },
        { "Read",    AtlasOperation.Edit,   "own",      false },
        { "Read",    AtlasOperation.Delete, "own",      false },
        { "Prepare", AtlasOperation.View,   "other",    true },
        { "Prepare", AtlasOperation.Create, "new",      true },
        { "Prepare", AtlasOperation.Edit,   "own",      true },
        { "Prepare", AtlasOperation.Edit,   "preparer", true },
        { "Prepare", AtlasOperation.Edit,   "other",    false },
        { "Prepare", AtlasOperation.Delete, "own",      false },
        { "Owner",   AtlasOperation.View,   "other",    true },
        { "Owner",   AtlasOperation.Create, "new",      true },
        { "Owner",   AtlasOperation.Edit,   "other",    true },
        { "Owner",   AtlasOperation.Delete, "other",    true },
        { "None",    AtlasOperation.View,   "other",    false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Access_table_for_each_role(string role, AtlasOperation operation, string record, bool allowed)
    {
        AtlasAccessService service = Service(role);

        AtlasAccessDecision decision = service.Evaluate(TestManifests.Ttdv2(), Form, operation, Record(record));

        Assert.Equal(allowed, decision.Allowed);
    }

    [Fact]
    public void Read_plus_Prepare_can_edit_own_records()
    {
        AtlasAccessService service = new(new FakeUserContext("me", Ttdv2Roles.Read, Ttdv2Roles.Prepare), new ListLogger<AtlasAccessService>());

        Assert.True(service.Evaluate(TestManifests.Ttdv2(), Form, AtlasOperation.Edit, OwnRecord).Allowed);
    }

    [Fact]
    public void An_owner_field_name_in_another_case_still_finds_the_column()
    {
        // Review SV-09: the start-up check ignores case, so the access check must too.
        CPAtlasApplicationConfig form = new() { OwnerFields = ["TaxPreparerLanID"], Permissions = Form.Permissions };
        Dictionary<string, object?> record = new() { ["CreatedBy"] = "other", ["TaxPreparerLanId"] = "me" };

        Assert.True(Service("Prepare").IsOwner(form, record));
        Assert.True(Service("Prepare").Evaluate(TestManifests.Ttdv2(), form, AtlasOperation.Edit, record).Allowed);
    }

    [Fact]
    public void Roles_of_another_app_give_no_access()
    {
        AtlasAccessService service = new(new FakeUserContext("me", new AtlasRole("TTD", "Owner", true, true, true)), new ListLogger<AtlasAccessService>());

        Assert.False(service.Evaluate(TestManifests.Ttdv2(), Form, AtlasOperation.View, OtherRecord).Allowed);
    }

    [Fact]
    public void Enforce_mode_throws_403_and_logs()
    {
        ListLogger<AtlasAccessService> log = new();
        AtlasAccessService service = new(new FakeUserContext("me", Ttdv2Roles.Read), log);

        AtlasProblemException error = Assert.Throws<AtlasProblemException>(() =>
            service.Demand(TestManifests.Ttdv2(AtlasAccessMode.Enforce), Form, AtlasOperation.Delete, OtherRecord, 10001));

        Assert.Equal(403, error.StatusCode);
        Assert.Contains(log.Entries, x => x.EventId.Name == "Atlas.AccessDenied" && x.Message.Contains("Record=10001"));
    }

    [Fact]
    public void LogOnly_mode_logs_and_continues()
    {
        ListLogger<AtlasAccessService> log = new();
        AtlasAccessService service = new(new FakeUserContext("me", Ttdv2Roles.Read), log);

        service.Demand(TestManifests.Ttdv2(AtlasAccessMode.LogOnly), Form, AtlasOperation.Edit, OtherRecord, 10001);

        Assert.Contains(log.Entries, x => x.EventId.Name == "Atlas.AccessDenied" && x.Message.Contains("Mode=LogOnly"));
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Prepare")]
    [InlineData("None")]
    public void LogOnly_mode_still_refuses_a_delete_with_403(string role)
    {
        ListLogger<AtlasAccessService> log = new();
        AtlasAccessService service = new(new FakeUserContext("me", role switch
        {
            "Read" => [Ttdv2Roles.Read],
            "Prepare" => [Ttdv2Roles.Prepare],
            _ => []
        }), log);

        AtlasProblemException error = Assert.Throws<AtlasProblemException>(() =>
            service.Demand(TestManifests.Ttdv2(AtlasAccessMode.LogOnly), Form, AtlasOperation.Delete, OtherRecord, 10001));

        Assert.Equal(403, error.StatusCode);
        Assert.Contains(log.Entries, x => x.EventId.Name == "Atlas.AccessDenied" && x.Message.Contains("Operation=Delete"));
    }

    [Fact]
    public void Role_tokens_match_the_role_flags()
    {
        AtlasAppRoles writer = new([Ttdv2Roles.Prepare]);
        AtlasAppRoles reader = new([Ttdv2Roles.Read]);

        Assert.True(writer.MatchesAny(["@Read"]));
        Assert.False(reader.MatchesAny(["@Write"]));
    }

    [Fact]
    public void User_permissions_for_the_buttons()
    {
        CPUserPermissions preparer = Service("Prepare").UserPermissions(TestManifests.Ttdv2(), Form, OtherRecord);

        Assert.True(preparer.CanCreate);
        Assert.False(preparer.CanEdit);
        Assert.False(preparer.CanDelete);
        Assert.True(preparer.CanViewHistory);
    }

    private static AtlasAccessService Service(string role)
    {
        AtlasRole[] roles = role switch
        {
            "Read" => [Ttdv2Roles.Read],
            "Prepare" => [Ttdv2Roles.Prepare],
            "Owner" => [Ttdv2Roles.Owner],
            _ => []
        };
        return new AtlasAccessService(new FakeUserContext("me", roles), new ListLogger<AtlasAccessService>());
    }

    private static Dictionary<string, object?>? Record(string name) => name switch
    {
        "own" => OwnRecord,
        "preparer" => PreparerRecord,
        "other" => OtherRecord,
        _ => null
    };
}
