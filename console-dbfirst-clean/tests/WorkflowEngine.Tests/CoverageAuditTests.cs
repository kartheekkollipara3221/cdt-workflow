using System.Reflection;
using System.Text.RegularExpressions;

namespace WorkflowEngine.Tests;

public sealed class CoverageAuditTests
{
    [Fact]
    public void AuditFile_Exists()
    {
        Assert.True(File.Exists(AuditPath()), "tests/COVERAGE_AUDIT.md does not exist.");
    }

    [Fact]
    public void Section20Bullets_AllPresent()
    {
        var result = ValidateAudit();
        Assert.True(result.MissingBullets.Count == 0, "COVERAGE_AUDIT.md is incomplete. Missing bullets:" + Environment.NewLine + string.Join(Environment.NewLine, result.MissingBullets.Select(row => "  - " + row)));
        Assert.True(result.OrphanBullets.Count == 0, "COVERAGE_AUDIT.md has rows that are not PROJECT.md section 20 bullets:" + Environment.NewLine + string.Join(Environment.NewLine, result.OrphanBullets.Select(row => "  - " + row)));
        Assert.True(result.DuplicateBullets.Count == 0, "COVERAGE_AUDIT.md has duplicate bullets:" + Environment.NewLine + string.Join(Environment.NewLine, result.DuplicateBullets.Select(row => "  - " + row)));
    }

    [Fact]
    public void AllRowsAreCovered()
    {
        var result = ValidateAudit();
        Assert.True(result.NonCoveredRows.Count == 0, "COVERAGE_AUDIT.md has rows with non-Covered status:" + Environment.NewLine + string.Join(Environment.NewLine, result.NonCoveredRows));
    }

    [Fact]
    public void AllCitedTestsExist()
    {
        var result = ValidateAudit();
        Assert.True(result.DeadCitations.Count == 0, "COVERAGE_AUDIT.md cites tests that do not exist:" + Environment.NewLine + string.Join(Environment.NewLine, result.DeadCitations));
    }

    [Fact]
    public void AuditDocumentIsValidAndComplete()
    {
        Section20Bullets_AllPresent();
        AllRowsAreCovered();
        AllCitedTestsExist();
    }

    private static AuditValidationResult ValidateAudit()
    {
        var specBullets = ExtractSection20Bullets();
        var rows = ParseAuditRows();
        var auditBullets = rows.Select(row => Normalize(row.Bullet)).ToArray();
        var compiledTests = CompiledTestNames();

        var missing = specBullets.Where(bullet => !auditBullets.Contains(bullet, StringComparer.Ordinal)).ToArray();
        var orphan = auditBullets.Where(bullet => !specBullets.Contains(bullet, StringComparer.Ordinal)).ToArray();
        var duplicates = auditBullets.GroupBy(bullet => bullet, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        var nonCovered = rows
            .Select((row, index) => (row, index))
            .Where(item => !string.Equals(item.row.Status, "Covered", StringComparison.Ordinal))
            .Select(item => $"  Row {item.index + 3}: Status = \"{item.row.Status}\" (bullet: \"{item.row.Bullet}\")")
            .ToArray();

        var dead = new List<string>();
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row.TestOrAction.StartsWith("Manual:", StringComparison.Ordinal)
                || row.TestOrAction.StartsWith("Quality gate:", StringComparison.Ordinal)
                || row.TestOrAction.StartsWith("README:", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var citation in row.TestOrAction.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!compiledTests.Contains(citation))
                {
                    dead.Add($"  Row {index + 3} cites \"{citation}\"");
                }
            }
        }

        return new AuditValidationResult(missing, orphan, duplicates, nonCovered, dead.ToArray());
    }

    private static string[] ExtractSection20Bullets()
    {
        var project = File.ReadAllText(ProjectPath());
        var section = Regex.Match(project, "(?s)## 20\\. Required tests\\r?\\n(?<body>.*?)(?=\\r?\\n## 21\\.)").Groups["body"].Value;
        return Regex.Matches(section, "(?m)^- (?<text>.+)$")
            .Select(match => Normalize(match.Groups["text"].Value))
            .ToArray();
    }

    private static AuditRow[] ParseAuditRows()
    {
        var rows = new List<AuditRow>();
        foreach (var line in File.ReadAllLines(AuditPath()))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal)
                || line.StartsWith("| §20 bullet ", StringComparison.Ordinal)
                || line.StartsWith("|---", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('|').Skip(1).Take(4).Select(cell => cell.Trim()).ToArray();
            Assert.Equal(4, cells.Length);
            rows.Add(new AuditRow(cells[0], cells[1], cells[2], cells[3]));
        }

        return rows.ToArray();
    }

    private static HashSet<string> CompiledTestNames()
    {
        return typeof(CoverageAuditTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttributes().Any(attribute =>
                    attribute is FactAttribute || attribute.GetType().FullName == "Xunit.TheoryAttribute"))
                .Select(method => $"{type.FullName}.{method.Name}"))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.Trim(), "\\s+", " ");
    }

    private static string AuditPath() => Path.Combine(RepoRoot(), "tests", "COVERAGE_AUDIT.md");

    private static string ProjectPath() => Path.Combine(RepoRoot(), "PROJECT.md");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PROJECT.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed record AuditRow(string Bullet, string Status, string TestOrAction, string Notes);

    private sealed record AuditValidationResult(
        IReadOnlyList<string> MissingBullets,
        IReadOnlyList<string> OrphanBullets,
        IReadOnlyList<string> DuplicateBullets,
        IReadOnlyList<string> NonCoveredRows,
        IReadOnlyList<string> DeadCitations);
}
