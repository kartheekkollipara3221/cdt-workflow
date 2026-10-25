namespace BBH.CP.Atlas.Platform.Tests;

internal static class TestPaths
{
    // The read-only copy of the target code in this repository (target-implementation-read-only/).
    public static string TargetCopyRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "target-implementation-read-only");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("target-implementation-read-only was not found above the test folder.");
    }

    public static string TargetConfigFile(string fileName) =>
        Path.Combine(TargetCopyRoot(), "Atlasapp", "Atlasapp", "Config", fileName);
}
