using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using WorkflowEngine.SqmsIntegration;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsNotificationMetadataTests
{
    [Fact]
    public void EndToEndMetadataSeedIsRerunnableAndPassesIndependentVerification()
    {
        using var host = new TestConsoleHost("workflowengine_sqms_notification_metadata_tests");
        host.ResetDatabase();

        try
        {
            Assert.Equal(0, host.Execute("init-db").ExitCode);
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/001_seed_sqms_transition_metadata.sql");
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/004_seed_sqms_end_to_end_metadata.sql");
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/004_seed_sqms_end_to_end_metadata.sql");
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/002_verify_sqms_transition_metadata.sql");
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/005_verify_sqms_end_to_end_metadata.sql");
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void NotificationInputFactoryFormatsCycleSellerAndBidDataExactly()
    {
        var factory = new SqmsNotificationInputFactory();
        var cycle = factory.CreateOpenBiddingInput(new SqmsSaleCycleNotificationData(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 5, 15),
            "https://example.test/sqms/opportunities/open-to-bidding"));

        Assert.Equal("Q3 2026", cycle.TemplateData["quarterLabel"]);
        Assert.Equal("05/15/2026", cycle.TemplateData["bidResponseDueDate"]);
        Assert.Equal(
            "https://example.test/sqms/opportunities/open-to-bidding",
            cycle.TemplateData["opportunitiesLink"]);
        Assert.Empty(cycle.RecipientInputs);

        var seller = factory.CreateSellerApprovalInput(new SqmsOpportunityNotificationData(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 5, 24),
            "ACC-100",
            "Seller Account",
            "Private Fund I",
            "Vehicle A",
            123456666m,
            "https://example.test/sqms/opportunities/10000/seller-review",
            "seller.rm@example.test",
            ["seller.team1@example.test", "seller.team2@example.test"]));

        Assert.Equal("Q3 2026", seller.TemplateData["quarterLabel"]);
        Assert.Equal("05/24/2026", seller.TemplateData["sellerResponseDueDate"]);
        Assert.Equal("$123,456,666.00", seller.TemplateData["saleAmount"]);
        Assert.Equal("Private Fund I", seller.TemplateData["fundName"]);
        Assert.Equal(
            ["seller.rm@example.test"],
            seller.RecipientInputs["opportunityRelationshipManager"].Select(item => item.EmailAddress).ToArray());
        Assert.Equal(
            ["seller.team1@example.test", "seller.team2@example.test"],
            seller.RecipientInputs["sellerRmTeam"].Select(item => item.EmailAddress).ToArray());

        var bid = factory.CreateBidDispositionInput(new SqmsBidNotificationData(
            new DateOnly(2026, 7, 1),
            "BUY-200",
            "Buyer Account",
            "Private Fund I",
            "Vehicle A",
            987654.5m,
            "https://example.test/sqms/bids/10000",
            "buyer.rm@example.test",
            ["buyer.team1@example.test", "buyer.team2@example.test"]));

        Assert.Equal("07/01/2026", bid.TemplateData["effectiveDateForSale"]);
        Assert.Equal("$987,654.50", bid.TemplateData["purchaseAmount"]);
        Assert.Equal("BUY-200", bid.TemplateData["buyerAccountNumber"]);
        Assert.Equal(
            ["buyer.rm@example.test"],
            bid.RecipientInputs["bidRelationshipManager"].Select(item => item.EmailAddress).ToArray());
        Assert.Equal(
            ["buyer.team1@example.test", "buyer.team2@example.test"],
            bid.RecipientInputs["buyerRmTeam"].Select(item => item.EmailAddress).ToArray());
    }

    private static void ExecuteSqlScript(string connectionString, string relativePath)
    {
        var script = File.ReadAllText(ResolveRepositoryFile(relativePath));
        var batches = Regex.Split(
            script,
            @"^\s*GO\s*(?:--.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        using var connection = new SqlConnection(connectionString);
        connection.Open();
        foreach (var batch in batches.Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 60;
            command.ExecuteNonQuery();
        }
    }

    private static string ResolveRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Repository file was not found: {relativePath}", relativePath);
    }
}
