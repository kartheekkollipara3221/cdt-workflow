using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

// Target implementation required: use the target API's real shared-connection transaction helper.
await using var transaction = await workflowDbContext.Database.BeginTransactionAsync(cancellationToken);
try
{
    var actor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
    var request = new SqmsExecuteRequest(
        10000,
        SqmsKeys.OpportunityActions.RequestSellerApproval,
        5,
        actor,
        serverResolvedRecipients);
    var result = await sqmsWorkflowService.ExecuteOpportunityActionAsync(request, cancellationToken);
    if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);

    // Testing only: the target repository test double throws here, after workflow success.
    await failingTargetRepository.SaveReturnedStateAsync(10000, result.WorkflowResult!.Result.NewState!, cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
