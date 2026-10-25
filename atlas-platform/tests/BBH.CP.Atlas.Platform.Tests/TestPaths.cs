namespace BBH.CP.Atlas.Platform.Tests;

internal static class TestPaths
{
    /// <summary>The repository root (the folder that has target-implementation-read-only/ and atlas-platform/).</summary>
    public static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "target-implementation-read-only"))
                && Directory.Exists(Path.Combine(directory.FullName, "atlas-platform")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root was not found above the test folder.");
    }

    // The read-only copy of the target code in this repository.
    public static string TargetCopyRoot() => Path.Combine(RepositoryRoot(), "target-implementation-read-only");

    public static string AtlasPlatformRoot() => Path.Combine(RepositoryRoot(), "atlas-platform");

    public static string TargetConfigFile(string fileName) =>
        Path.Combine(TargetCopyRoot(), "Atlasapp", "Atlasapp", "Config", fileName);
}
