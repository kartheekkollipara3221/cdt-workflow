using WorkflowEngine.Sqms.Contracts;

// Implemented operator-facing entry point; do not expose as a browser route.
SqmsScheduledRunResult result = await sqmsScheduledService.RunAsync(cancellationToken);
Console.WriteLine($"open={result.OpenNotificationsQueued}, replay={result.AlreadyQueued}");
