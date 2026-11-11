using System.Text.RegularExpressions;

namespace BBH.CP.Atlas.Platform.Tests;

/// <summary>
/// Checks the comment rules of AGENTS.md ("Code comments (platform)") on the platform source files.
/// </summary>
/// <remarks>
/// Comments tell what the code does and why. The links from code to specs, reviews and decisions are in
/// docs/atlas-platform/CODE_TRACE.md. Test files and the scripts 050 and 090 are not checked.
/// </remarks>
public sealed partial class CodeCommentRulesTests
{
    private const string TraceFile = "docs/atlas-platform/CODE_TRACE.md";

    private static readonly string[] SkippedFiles = ["050-ttdv2-copy-from-ttd.sql", "090-ttdv2-rollback.sql"];

    [Fact]
    public void Comments_HaveNoReviewSpecOrDecisionReferences()
    {
        List<string> files = SourceFiles().ToList();
        Assert.True(files.Count > 50, $"Only {files.Count} source files were found; the scan folders are wrong.");

        List<string> problems = [];
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach ((int line, string comment) in Comments(file, text))
            {
                Match match = ReferencePattern().Match(comment);
                if (match.Success)
                    problems.Add($"{Relative(file)}:{line}: \"{match.Value}\" in: {comment.Trim()}");
            }
        }

        Assert.True(problems.Count == 0,
            "Comments must not hold references. Move them to " + TraceFile + ":\n" + string.Join('\n', problems));
    }

    [Fact]
    public void CodeTrace_RowsNameFilesAndMembersThatExist()
    {
        string tracePath = Path.Combine(TestPaths.RepositoryRoot(), TraceFile);
        Assert.True(File.Exists(tracePath), TraceFile + " is missing.");

        List<string> problems = [];
        int rows = 0;
        foreach (string row in File.ReadLines(tracePath))
        {
            Match match = TraceRowPattern().Match(row);
            if (!match.Success)
                continue;

            rows++;
            string file = match.Groups["file"].Value;
            string member = match.Groups["member"].Value;
            string path = Path.Combine(TestPaths.AtlasPlatformRoot(), file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                problems.Add($"{file}: the file does not exist.");
                continue;
            }

            if (member != "(file)" && !File.ReadAllText(path).Contains(member, StringComparison.Ordinal))
                problems.Add($"{file}: the member \"{member}\" does not occur in the file.");
        }

        Assert.True(rows > 0, TraceFile + " has no rows.");
        Assert.True(problems.Count == 0, TraceFile + " has rows that do not match the code:\n" + string.Join('\n', problems));
    }

    private static IEnumerable<string> SourceFiles()
    {
        string root = TestPaths.AtlasPlatformRoot();
        IEnumerable<string> server = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories);
        IEnumerable<string> client = new[] { "*.ts", "*.html", "*.scss" }
            .SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(root, "WebUi", "src"), pattern, SearchOption.AllDirectories))
            .Where(file => !file.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase));
        IEnumerable<string> sql = Directory.EnumerateFiles(Path.Combine(root, "sql"), "*.sql", SearchOption.AllDirectories)
            .Where(file => !SkippedFiles.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase));

        return server.Concat(client).Concat(sql).Where(file => !IsBuildOutput(file));
    }

    private static bool IsBuildOutput(string file)
    {
        string normalized = file.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Relative(string file) => Path.GetRelativePath(TestPaths.AtlasPlatformRoot(), file).Replace('\\', '/');

    /// <summary>
    /// Returns each comment of a file with the line where it starts. Text in string literals is not a comment.
    /// </summary>
    private static IEnumerable<(int Line, string Text)> Comments(string file, string text)
    {
        string extension = Path.GetExtension(file).ToLowerInvariant();
        return extension switch
        {
            ".html" => HtmlComments(text),
            ".sql" => SqlComments(text),
            _ => CStyleComments(text, csharp: extension == ".cs"),
        };
    }

    private static IEnumerable<(int Line, string Text)> HtmlComments(string text)
    {
        foreach (Match match in HtmlCommentPattern().Matches(text))
            yield return (LineOf(text, match.Index), match.Value);
    }

    private static IEnumerable<(int Line, string Text)> SqlComments(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (Starts(text, i, "--"))
            {
                int end = IndexOrEnd(text, "\n", i);
                yield return (LineOf(text, i), text[i..end]);
                i = end;
            }
            else if (Starts(text, i, "/*"))
            {
                int end = Math.Min(text.Length, IndexOrEnd(text, "*/", i + 2) + 2);
                yield return (LineOf(text, i), text[i..end]);
                i = end;
            }
            else if (text[i] == '\'')
            {
                int j = i + 1;
                while (j < text.Length)
                {
                    if (text[j] != '\'')
                        j++;
                    else if (j + 1 < text.Length && text[j + 1] == '\'')
                        j += 2;
                    else
                        break;
                }

                i = j + 1;
            }
            else
            {
                i++;
            }
        }
    }

    private static IEnumerable<(int Line, string Text)> CStyleComments(string text, bool csharp)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (Starts(text, i, "//"))
            {
                int end = IndexOrEnd(text, "\n", i);
                yield return (LineOf(text, i), text[i..end]);
                i = end;
                continue;
            }

            if (Starts(text, i, "/*"))
            {
                int end = Math.Min(text.Length, IndexOrEnd(text, "*/", i + 2) + 2);
                yield return (LineOf(text, i), text[i..end]);
                i = end;
                continue;
            }

            i = csharp ? SkipCSharpLiteral(text, i) : SkipScriptLiteral(text, i);
        }
    }

    /// <summary>Returns the index after a C# string or char literal at <paramref name="i"/>, or i + 1.</summary>
    private static int SkipCSharpLiteral(string text, int i)
    {
        int start = i;
        while (i < text.Length && text[i] == '$')
            i++;

        if (Starts(text, i, "\"\"\""))
        {
            int quotes = 0;
            while (i + quotes < text.Length && text[i + quotes] == '"')
                quotes++;
            string close = new('"', quotes);
            return Math.Min(text.Length, IndexOrEnd(text, close, i + quotes) + quotes);
        }

        bool verbatim = false;
        if (i < text.Length && text[i] == '@')
        {
            verbatim = true;
            i++;
            while (i < text.Length && text[i] == '$')
                i++;
        }

        if (i >= text.Length || (text[i] != '"' && !(text[i] == '\'' && i == start)))
            return start + 1;

        char quote = text[i++];
        while (i < text.Length)
        {
            if (verbatim && text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"')
            {
                i += 2;
                continue;
            }

            if (!verbatim && text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (text[i] == quote || (!verbatim && text[i] == '\n'))
                return i + 1;
            i++;
        }

        return i;
    }

    /// <summary>Returns the index after a TypeScript string or template literal at <paramref name="i"/>, or i + 1.</summary>
    private static int SkipScriptLiteral(string text, int i)
    {
        char quote = text[i];
        if (quote != '"' && quote != '\'' && quote != '`')
            return i + 1;

        i++;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (text[i] == quote || (quote != '`' && text[i] == '\n'))
                return i + 1;
            i++;
        }

        return i;
    }

    private static bool Starts(string text, int i, string value) => string.CompareOrdinal(text, i, value, 0, value.Length) == 0;

    private static int IndexOrEnd(string text, string value, int from)
    {
        int index = text.IndexOf(value, from, StringComparison.Ordinal);
        return index < 0 ? text.Length : index;
    }

    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++)
        {
            if (text[i] == '\n')
                line++;
        }

        return line;
    }

    // Review, defect, decision and question IDs; spec, module and document references; history words of reviews.
    [GeneratedRegex(@"\b(DA|RV|SV|HO|NG|AR|SE|CD)-L?\d+\b|\b[Rr]eview\b|\bFABLE\b|\b[Ss]pecs?\s+\d{1,2}\b|\b[Mm]odules?\s+\d{2}\b|\b\d{2}-[a-z0-9-]+\.md\b|\bINTEGRATION\.md\b|\bPBDEV\.md\b|\b[Dd]ecisions?\s+D\d+\b|\b[BQ]\d{2}\b|\b[Ww]ave\s+\d\b")]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"^\|\s*`(?<file>[^`]+)`\s*\|\s*`(?<member>[^`]+)`\s*\|")]
    private static partial Regex TraceRowPattern();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentPattern();
}
