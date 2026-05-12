using WorkflowEngine.Api.Options;
using WorkflowEngine.Application.Persistence;

namespace WorkflowEngine.Api.Services;

public sealed class WorkflowDatabaseReadinessService(
    IWorkflowDatabaseInitializer databaseInitializer,
    ISeedVerificationService seedVerificationService,
    ILogger<WorkflowDatabaseReadinessService> logger)
{
    public WorkflowDatabaseReadinessResult Run(WorkflowDatabaseStartupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.ApplyMigrationsAndSeedOnStartup && !options.VerifySeedOnStartup)
        {
            logger.LogInformation("Workflow database startup migration, seed, and verification are disabled.");
            return new WorkflowDatabaseReadinessResult(
                MigrationsAndSeedApplied: false,
                SeedVerificationRan: false,
                SeedVerificationPassed: false,
                SeedVerificationErrors: [],
                Message: "Workflow database startup work is disabled.");
        }

        var migrationsAndSeedApplied = false;
        if (options.ApplyMigrationsAndSeedOnStartup)
        {
            logger.LogInformation("Applying workflow database migrations and seed data at startup.");
            var message = databaseInitializer.Initialize();
            migrationsAndSeedApplied = true;
            logger.LogInformation("Workflow database initialization completed: {Message}", message);
        }

        if (!options.VerifySeedOnStartup)
        {
            return new WorkflowDatabaseReadinessResult(
                migrationsAndSeedApplied,
                SeedVerificationRan: false,
                SeedVerificationPassed: false,
                SeedVerificationErrors: [],
                Message: "Workflow database startup completed without seed verification.");
        }

        logger.LogInformation("Verifying workflow seed data at startup.");
        var verification = seedVerificationService.Verify();
        if (!verification.Passed)
        {
            logger.LogError(
                "Workflow seed verification failed with {ErrorCount} error(s): {Errors}",
                verification.Errors.Count,
                string.Join("; ", verification.Errors));

            if (options.FailStartupWhenSeedVerificationFails)
            {
                throw new InvalidOperationException(
                    "Workflow seed verification failed: " + string.Join("; ", verification.Errors));
            }
        }
        else
        {
            logger.LogInformation("Workflow seed verification passed.");
        }

        return new WorkflowDatabaseReadinessResult(
            migrationsAndSeedApplied,
            SeedVerificationRan: true,
            SeedVerificationPassed: verification.Passed,
            SeedVerificationErrors: verification.Errors,
            Message: verification.Passed
                ? "Workflow seed verification passed."
                : "Workflow seed verification failed.");
    }
}
