using WorkflowEngine.Sqms.Contracts;

// Sqms:Enabled is false in server configuration.
SqmsScheduledRunResult result = await sqmsScheduledService.RunAsync(cancellationToken);
if (!result.Success || result.OpportunitiesClosed != 0 || result.BidsMovedToIpgReview != 0
    || result.BidsNotAllocated != 0 || result.OpenNotificationsQueued != 0
    || result.ReminderNotificationsQueued != 0 || result.AlreadyQueued != 0)
{
    throw new InvalidOperationException("Disabled SQMS must return successful zero work.");
}
