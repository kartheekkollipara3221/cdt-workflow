using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Platform.Tests.Security;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public class UserContextSqlTests(SqlServerDatabaseFixture database)
{
    [Fact]
    public async Task Roles_come_from_the_active_roles_view_for_the_app_only()
    {
        await database.SeedUserAsync("uc.both", "Both Roles", "TTDV2", "Read", "Prepare");
        await using CPSharepointContext context = Context();

        AtlasAppRoles roles = new AtlasUserContext(new FixedUser("uc.both"), context, new EmptyConfigStore()).RolesFor("ttdv2");

        Assert.Equal(["Prepare", "Read"], roles.RoleNames.Order());
        Assert.True(roles.IsWrite);
        Assert.False(roles.IsAdmin);
        Assert.False(new AtlasUserContext(new FixedUser("uc.both"), context, new EmptyConfigStore()).RolesFor("TTD").HasAnyRole);
    }

    [Fact]
    public async Task A_revoked_role_is_not_active()
    {
        await database.SeedUserAsync("uc.revoked", "Revoked User", "TTDV2", "Owner");
        await database.ScalarAsync("""
            UPDATE ue SET IsDeleted = 1 FROM dbo.CP_Atlas_UserEntitlement AS ue
            INNER JOIN dbo.CP_Atlas_User AS u ON u.ID = ue.CP_Atlas_UserID WHERE u.LanID = 'uc.revoked';
            """);
        await using CPSharepointContext context = Context();

        Assert.False(new AtlasUserContext(new FixedUser("uc.revoked"), context, new EmptyConfigStore()).RolesFor("TTDV2").HasAnyRole);
    }

    [Fact]
    public async Task Directory_lists_users_in_roles_with_LAN_ID_values_and_display_names()
    {
        await database.SeedUserAsync("uc.prep", "Pat Preparer", "TTDV2", "Prepare");
        await database.SeedUserAsync("uc.read", "Riley Reader", "TTDV2", "Read");
        await using CPSharepointContext context = Context();
        AtlasUserDirectory directory = new(context, new MemoryCache(new MemoryCacheOptions()));

        IReadOnlyList<(string LanId, string DisplayName)> users = directory.UsersInRoles("TTDV2", ["Prepare", "Owner"]);

        Assert.Contains(("uc.prep", "Pat Preparer"), users);
        Assert.DoesNotContain(users, x => x.LanId == "uc.read");
        Assert.Equal("Riley Reader", directory.DisplayName("uc.read"));
        Assert.Equal("nobody", directory.DisplayName("nobody"));
    }

    [Fact]
    public async Task Display_names_of_many_users_are_read_together_and_kept_in_the_cache()
    {
        // Review DA-09: a dashboard with 200 preparers must not make 200 round trips.
        await database.SeedUserAsync("uc.many1", "Many One", "TTDV2", "Read");
        await database.SeedUserAsync("uc.many2", "Many Two", "TTDV2", "Read");
        await using CPSharepointContext context = Context();
        MemoryCache cache = new(new MemoryCacheOptions());
        AtlasUserDirectory directory = new(context, cache);

        IReadOnlyDictionary<string, string> names = directory.DisplayNames(["uc.many1", "UC.MANY2", "uc.unknown", "uc.many1"]);

        Assert.Equal("Many One", names["uc.many1"]);
        Assert.Equal("Many Two", names["uc.many2"]);
        Assert.Equal("uc.unknown", names["uc.unknown"]);
        Assert.Equal("Many Two", directory.DisplayName("uc.many2")); // from the cache
    }

    private CPSharepointContext Context() =>
        new(new DbContextOptionsBuilder<CPSharepointContext>().UseSqlServer(database.ConnectionString).Options,
            new ConfigurationBuilder().Build());

    private sealed class FixedUser(string lanId) : IAtlasCurrentUserSource
    {
        public CPUser GetCurrentUser() => new() { LanID = lanId, DisplayName = lanId };
    }
}
