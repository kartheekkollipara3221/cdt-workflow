using System.Text.Json;
using System.Text.RegularExpressions;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Demo;

namespace WorkflowEngine.Tests;

public sealed record ParsedCommand(
    int LineNumber,
    string ReadmeSection,
    string CommandName,
    string[] Arguments,
    string RawLine);

public sealed class ReadmeValidationTests
{
    private const string Prefix = "dotnet run --project src/WorkflowEngine.Console -- ";

    private static readonly string[] LockedResultCodes =
    [
        "Success",
        "ValidationFailed",
        "Forbidden",
        "InvalidTransition",
        "ConcurrencyConflict",
        "WorkflowDefinitionVersionMismatch",
        "IdempotencyConflict",
        "AmbiguousRoleMatch",
        "ActorRolesMismatch",
        "AlreadyExists",
        "WorkflowInstanceNotFound"
    ];

    [Fact]
    public void AllCommandsHaveKnownNames()
    {
        var registry = new CommandRegistry();
        foreach (var command in ExtractCommands())
        {
            Assert.True(registry.IsKnown(command.CommandName), $"{command.RawLine} uses unknown command '{command.CommandName}'.");
        }
    }

    [Fact]
    public void AllCommandsParseSuccessfully()
    {
        foreach (var command in ExtractCommands())
        {
            var result = CommandArgumentParser.Parse(command.CommandName, command.Arguments);
            Assert.True(result.Success, $"README line {command.LineNumber} failed parser: {result.ErrorMessage}{Environment.NewLine}{command.RawLine}");
        }
    }

    [Fact]
    public void AllJsonReferencesResolve()
    {
        foreach (var command in ExtractCommands())
        {
            foreach (var arg in command.Arguments.Where(arg => arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.StartsWith("./samples/workflow-actions/", arg, StringComparison.Ordinal);
                Assert.True(File.Exists(ResolvePath(arg)), $"README line {command.LineNumber} references missing sample file: {arg}");
            }
        }
    }

    [Fact]
    public void OverrideFlagsArePaired()
    {
        foreach (var command in ExtractCommands())
        {
            var hasBusinessObject = command.Arguments.Contains("--business-object");
            var hasWorkflowInstance = command.Arguments.Contains("--workflow-instance");
            Assert.True(hasBusinessObject == hasWorkflowInstance, $"README line {command.LineNumber} must pair --business-object and --workflow-instance.");
        }
    }

    [Fact]
    public void ReservedIdentifiersAreScopedCorrectly()
    {
        foreach (var command in ExtractCommands())
        {
            var text = string.Join(" ", command.Arguments);
            Assert.False(text.Contains("PT-DEMO", StringComparison.Ordinal) && !string.Equals(command.CommandName, "demo", StringComparison.OrdinalIgnoreCase));
            Assert.False(text.Contains("PT-1002", StringComparison.Ordinal) && !command.ReadmeSection.Contains("Checker", StringComparison.OrdinalIgnoreCase));
            Assert.False(text.Contains("PT-1003", StringComparison.Ordinal) && !command.ReadmeSection.Contains("Trading", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AllCapabilitiesHaveExamples()
    {
        var commandNames = ExtractCommands().Select(command => command.CommandName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new CommandRegistry().KnownCommands)
        {
            Assert.Contains(required, commandNames);
        }
    }

    [Fact]
    public void BranchScriptCommands_MatchTestFixtures()
    {
        AssertBlockMatches("HappyPath_Section25_1", BranchScriptDefinitions.HappyPath_Section25_1);
        AssertBlockMatches("CheckerReturnBranch_Section25_2", BranchScriptDefinitions.CheckerReturnBranch_Section25_2);
        AssertBlockMatches("TradingReturnBranch_Section25_3", BranchScriptDefinitions.TradingReturnBranch_Section25_3);
    }

    [Fact]
    public void ResultCodeList_MatchesLockedSet()
    {
        var readme = File.ReadAllText(ReadmePath());
        var resultSection = Regex.Match(readme, "(?s)### Result codes\\r?\\n(?<body>.*?)(?=\\r?\\n### )").Groups["body"].Value;
        var codes = Regex.Matches(resultSection, @"^- `(?<code>[^`]+)`:", RegexOptions.Multiline)
            .Select(match => match.Groups["code"].Value)
            .ToArray();

        Assert.Equal(LockedResultCodes, codes);
    }

    [Fact]
    public void SpecSectionReferencesResolve()
    {
        var project = File.ReadAllText(ProjectPath());
        var headings = Regex.Matches(project, @"^##+ (?<number>\d+(?:\.\d+)?)", RegexOptions.Multiline)
            .Select(match => match.Groups["number"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var readme = File.ReadAllText(ReadmePath());
        foreach (Match match in Regex.Matches(readme, @"§(?<number>\d+(?:\.\d+)?)"))
        {
            Assert.Contains(match.Groups["number"].Value, headings);
        }
    }

    [Fact]
    public void ConfigurationExamplesDeserialize()
    {
        var readme = File.ReadAllText(ReadmePath());
        foreach (Match match in Regex.Matches(readme, "(?s)```json\\r?\\n(?<json>.*?)\\r?\\n```"))
        {
            using var _ = JsonDocument.Parse(match.Groups["json"].Value);
        }
    }

    [Fact]
    public void ExtendingSectionPointsToStopAndAsk()
    {
        var readme = File.ReadAllText(ReadmePath());
        var extending = Regex.Match(readme, "(?s)### Extending the engine\\r?\\n(?<body>.*?)(?=\\r?\\n### )").Groups["body"].Value;
        Assert.Contains("PROJECT.md` §22", extending, StringComparison.Ordinal);
    }

    private static void AssertBlockMatches(string blockName, IReadOnlyList<ConsoleCommand> commands)
    {
        var actual = ExtractTaggedBlock(blockName);
        var expected = commands.Select(BranchScriptDefinitions.ToReadmeLine).ToArray();
        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static string[] ExtractTaggedBlock(string blockName)
    {
        var readme = File.ReadAllText(ReadmePath());
        var match = Regex.Match(
            readme,
            $"(?s)<!-- BEGIN: {Regex.Escape(blockName)} -->\\s*```bash\\r?\\n(?<body>.*?)\\r?\\n```\\s*<!-- END: {Regex.Escape(blockName)} -->");
        Assert.True(match.Success, $"README block {blockName} was not found.");
        return match.Groups["body"].Value
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static ParsedCommand[] ExtractCommands()
    {
        var lines = File.ReadAllLines(ReadmePath());
        var commands = new List<ParsedCommand>();
        var inFence = false;
        var section = string.Empty;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("##", StringComparison.Ordinal))
            {
                section = line.TrimStart('#').Trim();
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (!inFence || !line.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var tokens = Tokenize(line[Prefix.Length..]);
            commands.Add(new ParsedCommand(index + 1, section, tokens[0], tokens.Skip(1).ToArray(), line));
        }

        return commands.ToArray();
    }

    private static string[] Tokenize(string command)
    {
        var tokens = new List<string>();
        var current = new List<char>();
        var inQuotes = false;

        foreach (var ch in command)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Count > 0)
                {
                    tokens.Add(new string(current.ToArray()));
                    current.Clear();
                }

                continue;
            }

            current.Add(ch);
        }

        if (current.Count > 0)
        {
            tokens.Add(new string(current.ToArray()));
        }

        return tokens.ToArray();
    }

    private static string[] Normalize(IEnumerable<string> lines)
    {
        return lines.Select(line => line.TrimEnd()).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
    }

    private static string ResolvePath(string relativePath)
    {
        var repoRoot = Path.GetDirectoryName(ReadmePath())!;
        return Path.Combine(repoRoot, relativePath.Replace("./", string.Empty).Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ProjectPath() => Path.Combine(Path.GetDirectoryName(ReadmePath())!, "PROJECT.md");

    private static string ReadmePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "README.md");
            var project = Path.Combine(directory.FullName, "PROJECT.md");
            if (File.Exists(candidate) && File.Exists(project))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return "README.md";
    }
}
