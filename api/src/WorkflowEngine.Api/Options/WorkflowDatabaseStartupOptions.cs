namespace WorkflowEngine.Api.Options;

public sealed class WorkflowDatabaseStartupOptions
{
    public const string SectionName = "WorkflowDatabaseStartup";

    public bool ApplyMigrationsAndSeedOnStartup { get; set; }

    public bool VerifySeedOnStartup { get; set; }

    public bool FailStartupWhenSeedVerificationFails { get; set; } = true;
}
