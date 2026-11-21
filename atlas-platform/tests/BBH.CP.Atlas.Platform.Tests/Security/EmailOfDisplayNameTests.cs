using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Platform.Tests.Security;

/// <summary>
/// The name match of a recipient field (module 25, 10.6): the email of exactly one active user with the display name.
/// The compare ignores case and the spaces at the start, at the end and doubled.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class EmailOfDisplayNameTests(SqlServerDatabaseFixture database) : IAsyncLifetime
{
    public async Task InitializeAsync() => await database.ScalarAsync("""
        IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_User WHERE LanID LIKE 'nm.%')
            INSERT INTO dbo.CP_Atlas_User (LanID, DisplayName, Email, IsDeleted) VALUES
            ('nm.one', N'  nora   MATCH ', N' nora@test.local ', 0),
            ('nm.twin1', N'Tess Twin', N'tess1@test.local', 0),
            ('nm.twin2', N'tess  twin', N'tess2@test.local', 0),
            ('nm.nomail', N'Nils Nomail', NULL, 0),
            ('nm.blank', N'Bea Blank', N'   ', 0),
            ('nm.gone', N'Gina Gone', N'gina.old@test.local', 1),
            ('nm.back', N'Gina Gone', N'gina@test.local', 0);
        SELECT 1;
        """);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Nora Match")]
    [InlineData("nora match")]
    [InlineData("  NORA    Match  ")]
    public void One_user_gives_the_email_with_any_case_and_spaces(string name)
    {
        AtlasNameMatch match = Directory().EmailOfDisplayName(name);

        Assert.Equal(new AtlasNameMatch("nora@test.local", null), match);
    }

    [Theory]
    [InlineData("Nobody Here", "no user")]
    [InlineData("Tess Twin", "2 users")]
    [InlineData("Nils Nomail", "no email")]
    [InlineData("Bea Blank", "no email")]
    [InlineData("", "no name")]
    [InlineData("   ", "no name")]
    public void No_user_two_users_or_no_email_give_no_email_and_the_reason(string name, string problem)
    {
        AtlasNameMatch match = Directory().EmailOfDisplayName(name);

        Assert.Null(match.Email);
        Assert.Equal(problem, match.Problem);
    }

    [Fact]
    public void A_deleted_user_is_not_a_second_user()
    {
        Assert.Equal("gina@test.local", Directory().EmailOfDisplayName("Gina Gone").Email);
    }

    private AtlasUserDirectory Directory() =>
        new(new CPSharepointContext(new DbContextOptionsBuilder<CPSharepointContext>().UseSqlServer(database.ConnectionString).Options,
            new ConfigurationBuilder().Build()), new MemoryCache(new MemoryCacheOptions()));
}
