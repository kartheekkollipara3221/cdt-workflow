using System.Globalization;

namespace WorkflowEngine.Sqms;

public static class SqmsKeys
{
    public const string OpportunityWorkflow = "SqmsOpportunity";
    public const string BidWorkflow = "SqmsBid";

    public static class OpportunityStates
    {
        public const string Draft = "Draft";
        public const string Submitted = "SUBMITTED";
        public const string Pending = "PENDING";
        public const string OpenToBidding = "OPEN TO BIDDING";
        public const string ClosedToBidding = "CLOSED TO BIDDING";
        public const string PendingSellerApproval = "PENDING SELLER APPROVAL";
        public const string AwaitingAllocation = "AWAITING ALLOCATION";
        public const string PartiallyAllocated = "PARTIALLY ALLOCATED";
        public const string NotFulfilled = "NOT FULFILLED";
        public const string Completed = "COMPLETED";
    }

    public static class BidStates
    {
        public const string Draft = "Draft";
        public const string Submitted = "SUBMITTED";
        public const string NotAllocated = "NOT ALLOCATED";
        public const string IpgReview = "IPG REVIEW";
        public const string UnderReviewBySeller = "UNDER REVIEW BY SELLER";
        public const string Accepted = "ACCEPTED";
        public const string PartiallyAccepted = "PARTIALLY ACCEPTED";
        public const string NotAccepted = "NOT ACCEPTED";
    }

    public static class OpportunityActions
    {
        public const string Submit = "SubmitOpportunity";
        public const string ReturnToDraft = "ReturnOpportunityToDraft";
        public const string MarkPending = "MarkOpportunityPending";
        public const string OpenForBidding = "OpenOpportunityForBidding";
        public const string CloseBidding = "CloseOpportunityBidding";
        public const string RequestSellerApproval = "RequestSellerApproval";
        public const string MarkAwaitingAllocation = "MarkOpportunityAwaitingAllocation";
        public const string MarkPartiallyAllocated = "MarkOpportunityPartiallyAllocated";
        public const string MarkNotFulfilled = "MarkOpportunityNotFulfilled";
        public const string Complete = "CompleteOpportunity";
    }

    public static class BidActions
    {
        public const string Submit = "SubmitBid";
        public const string ReturnToDraft = "ReturnBidToDraft";
        public const string MarkNotAllocated = "MarkBidNotAllocated";
        public const string MoveToIpgReview = "MoveBidToIpgReview";
        public const string SendToSellerReview = "SendBidToSellerReview";
        public const string Accept = "AcceptBid";
        public const string PartiallyAccept = "PartiallyAcceptBid";
        public const string Reject = "RejectBid";
    }

    public static class Roles
    {
        public const string Mfo = "MFO";
        public const string Ipg = "IPG";
        public const string System = "System";
        public const string Seller = "Seller";
    }

    public static class Routes
    {
        public const string OpportunityOpen = "SqmsOpportunityOpen";
        public const string OpportunityReminder = "SqmsOpportunityReminder";
        public const string OpportunityPendingSellerApproval = "SqmsOpportunityPendingSellerApproval";
        public const string BidAccepted = "SqmsBidAccepted";
        public const string BidPartiallyAccepted = "SqmsBidPartiallyAccepted";
        public const string BidNotAccepted = "SqmsBidNotAccepted";
    }

    public static string OpportunityBusinessObjectId(int opportunityId) =>
        BuildId("SQMS-OPP-", opportunityId, nameof(opportunityId));

    public static string OpportunityWorkflowInstanceId(int opportunityId) =>
        BuildId("WF-SQMS-OPP-", opportunityId, nameof(opportunityId));

    public static string BidBusinessObjectId(int bidId) =>
        BuildId("SQMS-BID-", bidId, nameof(bidId));

    public static string BidWorkflowInstanceId(int bidId) =>
        BuildId("WF-SQMS-BID-", bidId, nameof(bidId));

    private static string BuildId(string prefix, int id, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1, parameterName);
        return prefix + id.ToString(CultureInfo.InvariantCulture);
    }
}
