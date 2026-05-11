namespace WorkflowEngine.Tests;

internal sealed class SmtpFactAttribute : FactAttribute
{
    public SmtpFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_SMTP_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Set RUN_SMTP_TESTS=1 and run with --filter Category=Smtp after starting local smtp4dev on SMTP localhost:3025 and API http://localhost:5000.";
        }
    }
}
