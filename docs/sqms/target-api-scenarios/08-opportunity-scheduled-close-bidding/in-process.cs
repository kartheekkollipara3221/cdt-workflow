using WorkflowEngine.Sqms.Contracts;

// Implemented operator-facing scheduling facade. Do not expose as a public UI route.
SqmsScheduledRunResult result = await sqmsScheduledService.RunAsync(cancellationToken);
if (!result.Success)
{
    foreach (var failure in result.Failures)
    {
        Console.Error.WriteLine($"{failure.EntityType} {failure.EntityId}: {failure.Error}");
    }
}
