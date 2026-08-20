namespace WorkflowEngine.Sqms.Configuration;

public sealed class SqmsOptions
{
    public const string SectionName = "Sqms";

    public bool Enabled { get; set; }
    public SqmsLinkOptions Links { get; set; } = new();
    public SqmsSchedulingOptions Scheduling { get; set; } = new();
}

public sealed class SqmsLinkOptions
{
    public string? OpenBiddingDashboardUrl { get; set; }
    public string? SellerApprovalBidsDashboardUrl { get; set; }
    public string? BidFormUrl { get; set; }
}

public sealed class SqmsSchedulingOptions
{
    public string TimeZoneId { get; set; } = "Eastern Standard Time";
    public int BiddingDays { get; set; } = 15;
    public int SellerResponseDays { get; set; } = 5;
}
