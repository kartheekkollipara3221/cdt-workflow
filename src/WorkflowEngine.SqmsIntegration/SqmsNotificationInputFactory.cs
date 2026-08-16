using System.Globalization;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.SqmsIntegration;

public sealed record SqmsSaleCycleNotificationData(
    DateOnly EffectiveDateForSale,
    DateOnly BidResponseDueDate,
    string OpportunitiesLink);

public sealed record SqmsOpportunityNotificationData(
    DateOnly EffectiveDateForSale,
    DateOnly SellerResponseDueDate,
    string AccountNumber,
    string AccountName,
    string FundName,
    string Vehicle,
    decimal SaleAmount,
    string SellerApprovalLink,
    string OpportunityRelationshipManagerEmail,
    IReadOnlyList<string> SellerRmTeamEmails);

public sealed record SqmsBidNotificationData(
    DateOnly EffectiveDateForSale,
    string BuyerAccountNumber,
    string BuyerAccountName,
    string FundName,
    string Vehicle,
    decimal PurchaseAmount,
    string BidLink,
    string BidRelationshipManagerEmail,
    IReadOnlyList<string> BuyerRmTeamEmails);

public sealed class SqmsNotificationInputFactory
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    public WorkflowNotificationInput CreateOpenBiddingInput(SqmsSaleCycleNotificationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new WorkflowNotificationInput
        {
            TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["quarter"] = Quarter(data.EffectiveDateForSale),
                ["year"] = data.EffectiveDateForSale.Year.ToString(CultureInfo.InvariantCulture),
                ["responseDueDate"] = DisplayDate(data.BidResponseDueDate),
                ["viewUrl"] = Required(data.OpportunitiesLink, nameof(data.OpportunitiesLink))
            }
        };
    }

    public WorkflowNotificationInput CreateSellerApprovalInput(SqmsOpportunityNotificationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new WorkflowNotificationInput
        {
            TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["quarter"] = Quarter(data.EffectiveDateForSale),
                ["year"] = data.EffectiveDateForSale.Year.ToString(CultureInfo.InvariantCulture),
                ["responseDueDate"] = DisplayDate(data.SellerResponseDueDate),
                ["accountNumber"] = Required(data.AccountNumber, nameof(data.AccountNumber)),
                ["accountName"] = Required(data.AccountName, nameof(data.AccountName)),
                ["fundName"] = Required(data.FundName, nameof(data.FundName)),
                ["vehicle"] = Required(data.Vehicle, nameof(data.Vehicle)),
                ["saleAmount"] = DisplayCurrency(data.SaleAmount),
                ["viewUrl"] = Required(data.SellerApprovalLink, nameof(data.SellerApprovalLink))
            },
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["opportunityRelationshipManager"] = Recipients(
                    [Required(data.OpportunityRelationshipManagerEmail, nameof(data.OpportunityRelationshipManagerEmail))]),
                ["sellerRmTeam"] = Recipients(data.SellerRmTeamEmails)
            }
        };
    }

    public WorkflowNotificationInput CreateBidDispositionInput(SqmsBidNotificationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new WorkflowNotificationInput
        {
            TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["quarter"] = Quarter(data.EffectiveDateForSale),
                ["year"] = data.EffectiveDateForSale.Year.ToString(CultureInfo.InvariantCulture),
                ["effectiveDateForSale"] = DisplayDate(data.EffectiveDateForSale),
                ["buyerAccountNumber"] = Required(data.BuyerAccountNumber, nameof(data.BuyerAccountNumber)),
                ["buyerAccountName"] = Required(data.BuyerAccountName, nameof(data.BuyerAccountName)),
                ["fundName"] = Required(data.FundName, nameof(data.FundName)),
                ["vehicle"] = Required(data.Vehicle, nameof(data.Vehicle)),
                ["purchaseAmount"] = DisplayCurrency(data.PurchaseAmount),
                ["viewUrl"] = Required(data.BidLink, nameof(data.BidLink))
            },
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["bidRelationshipManager"] = Recipients(
                    [Required(data.BidRelationshipManagerEmail, nameof(data.BidRelationshipManagerEmail))]),
                ["buyerRmTeam"] = Recipients(data.BuyerRmTeamEmails)
            }
        };
    }

    private static string Quarter(DateOnly effectiveDateForSale)
    {
        var quarter = ((effectiveDateForSale.Month - 1) / 3) + 1;
        return quarter.ToString(CultureInfo.InvariantCulture);
    }

    private static string DisplayDate(DateOnly date)
    {
        return date.ToString("MM/dd/yyyy", DisplayCulture);
    }

    private static string DisplayCurrency(decimal amount)
    {
        return amount.ToString("C2", DisplayCulture);
    }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{parameterName} is required.", parameterName)
            : value.Trim();
    }

    private static IReadOnlyList<WorkflowRecipientInput> Recipients(IEnumerable<string> emailAddresses)
    {
        ArgumentNullException.ThrowIfNull(emailAddresses);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<WorkflowRecipientInput>();
        foreach (var rawAddress in emailAddresses)
        {
            if (string.IsNullOrWhiteSpace(rawAddress))
            {
                continue;
            }

            var address = rawAddress.Trim();
            if (seen.Add(address))
            {
                result.Add(new WorkflowRecipientInput(null, address, null));
            }
        }

        return result;
    }
}
