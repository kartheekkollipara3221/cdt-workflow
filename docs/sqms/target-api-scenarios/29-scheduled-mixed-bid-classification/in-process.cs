using WorkflowEngine.Sqms.Contracts;

SqmsScheduledRunResult result = await sqmsScheduledService.RunAsync(cancellationToken);
Console.WriteLine($"review={result.BidsMovedToIpgReview}, notAllocated={result.BidsNotAllocated}");
// For the documented isolated fixture: review=2, notAllocated=1.
