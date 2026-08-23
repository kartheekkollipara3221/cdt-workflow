using WorkflowEngine.Sqms.Contracts;

SqmsScheduledRunResult result = await sqmsScheduledService.RunAsync(cancellationToken);
Console.WriteLine($"closed={result.OpportunitiesClosed}, review={result.BidsMovedToIpgReview}, notAllocated={result.BidsNotAllocated}, open={result.OpenNotificationsQueued}, reminders={result.ReminderNotificationsQueued}");
