using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Tests;

[Trait("Category", "FreshClone")]
public sealed class FreshCloneWalkthroughTests
{
    [Fact]
    public void DatabaseResetSucceeds()
    {
        using var dbContext = CreateBurritoContext();

        dbContext.Database.EnsureDeleted();

        Assert.False(dbContext.Database.CanConnect());
    }

    [Fact]
    public void BuildSucceedsWithWarningsAsErrors()
    {
        var result = Run("dotnet", "build src\\WorkflowEngine.sln -warnaserror -m:1");

        AssertSuccess(result);
    }

    [Fact]
    public void InitDb_RunsSuccessfully()
    {
        using var dbContext = CreateBurritoContext();
        dbContext.Database.EnsureDeleted();

        var result = Run("dotnet", "run --project src\\WorkflowEngine.Console -- init-db");

        AssertSuccess(result);
        Assert.Contains("Database initialized", result.Stdout, StringComparison.Ordinal);
        Assert.True(dbContext.Database.CanConnect());
    }

    [Fact]
    public void VerifySeed_PassesAfterInitDb()
    {
        ResetAndInit();

        var result = Run("dotnet", "run --project src\\WorkflowEngine.Console -- verify-seed");

        AssertSuccess(result);
        Assert.Contains("Seed verification passed", result.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Demo_RunsSuccessfullyAfterInitDb()
    {
        ResetAndInit();

        var result = Run("dotnet", "run --project src\\WorkflowEngine.Console -- demo");

        AssertSuccess(result);
        Assert.Contains("Demo completed successfully", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("To explore Return-to-Maker branches", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickstartSequence_ProducesExpectedOutputs()
    {
        using var dbContext = CreateBurritoContext();
        dbContext.Database.EnsureDeleted();

        AssertSuccess(Run("dotnet", "build src\\WorkflowEngine.sln -warnaserror -m:1"));
        var init = Run("dotnet", "run --project src\\WorkflowEngine.Console -- init-db");
        AssertSuccess(init);
        Assert.Contains("Database initialized", init.Stdout, StringComparison.Ordinal);
        var verify = Run("dotnet", "run --project src\\WorkflowEngine.Console -- verify-seed");
        AssertSuccess(verify);
        Assert.Contains("Seed verification passed", verify.Stdout, StringComparison.OrdinalIgnoreCase);
        var demo = Run("dotnet", "run --project src\\WorkflowEngine.Console -- demo");
        AssertSuccess(demo);
        Assert.Contains("Demo completed successfully", demo.Stdout, StringComparison.Ordinal);
        Assert.Contains("To explore Return-to-Maker branches", demo.Stdout, StringComparison.Ordinal);
    }

    private static void ResetAndInit()
    {
        using var dbContext = CreateBurritoContext();
        dbContext.Database.EnsureDeleted();
        AssertSuccess(Run("dotnet", "run --project src\\WorkflowEngine.Console -- init-db"));
    }

    private static WorkflowDbContext CreateBurritoContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito"))
            .Options;
        return new WorkflowDbContext(options);
    }

    private static ProcessResult Run(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = TestSqlConnectionFactory.ConnectionString("burrito");
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName} {arguments}.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static void AssertSuccess(ProcessResult result)
    {
        Assert.True(result.ExitCode == 0, $"Exit code: {result.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{result.Stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{result.Stderr}");
    }

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

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
