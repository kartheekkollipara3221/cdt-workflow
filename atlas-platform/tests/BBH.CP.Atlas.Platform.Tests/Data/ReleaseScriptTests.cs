namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>The release folder has the same text as the module scripts (sql/release-p1/README.md).</summary>
public class ReleaseScriptTests
{
    [Theory]
    [InlineData("020-script-log.sql", "11-script-log.sql")]
    [InlineData("030-active-user-roles-view.sql", "06-active-user-roles-view.sql")]
    [InlineData("040-ttdv2-app.sql", "12-ttdv2-app.sql")]
    [InlineData("050-ttdv2-copy-from-ttd.sql", "12-ttdv2-copy-from-ttd.sql")]
    [InlineData("090-ttdv2-rollback.sql", "12-ttdv2-rollback.sql")]
    public void A_release_script_is_the_same_as_its_module_script(string release, string module)
    {
        string Read(string path) => File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.Equal(
            Read(Path.Combine(TestPaths.RepositoryRoot(), "docs", "atlas-platform", "sql", module)),
            Read(Path.Combine(TestPaths.AtlasPlatformRoot(), "sql", "release-p1", release)));
    }
}
