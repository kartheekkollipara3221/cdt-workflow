using WorkflowEngine.Sqms.Contracts;

SqmsScheduledRunResult result = await sqmsScheduledService.RunAsync(cancellationToken);
Console.WriteLine($"reminders={result.ReminderNotificationsQueued}, replay={result.AlreadyQueued}");
