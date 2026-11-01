namespace BBH.CP.Atlas.Platform.Tests;

internal static class TestPaths
{
    /// <summary>The repository root (the folder that has atlas-platform/).</summary>
    public static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "atlas-platform")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root was not found above the test folder.");
    }

    // The read-only copy of the target code. It is git-ignored, so a fresh clone does not have it.
    public static string TargetCopyRoot() => Path.Combine(RepositoryRoot(), "target-implementation-read-only");

    public static bool HasTargetCopy() => Directory.Exists(TargetCopyRoot());

    public static string AtlasPlatformRoot() => Path.Combine(RepositoryRoot(), "atlas-platform");

    public static string TargetConfigFile(string fileName) =>
        Path.Combine(TargetCopyRoot(), "Atlasapp", "Atlasapp", "Config", fileName);
}

/// <summary>A fact that reads the target copy; skipped when the folder is absent.</summary>
internal sealed class TargetCopyFactAttribute : FactAttribute
{
    public TargetCopyFactAttribute()
    {
        if (!TestPaths.HasTargetCopy())
            Skip = "target-implementation-read-only is absent (git-ignored).";
    }
}

/// <summary>A theory that reads the target copy; skipped when the folder is absent.</summary>
internal sealed class TargetCopyTheoryAttribute : TheoryAttribute
{
    public TargetCopyTheoryAttribute()
    {
        if (!TestPaths.HasTargetCopy())
            Skip = "target-implementation-read-only is absent (git-ignored).";
    }
}
