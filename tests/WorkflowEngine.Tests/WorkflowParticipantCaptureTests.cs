using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.WorkflowStart;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowParticipantCaptureTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public void CaptureStoresRequiredMakerOnceAndReturnsOnlyActionInputs()
    {
        using var dbContext = CreateContextWithInstance();
        var input = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["originalMaker"] =
                [
                    new("maker-1", " maker-value ", "Maker One")
                ],
                ["selectedCcUsers"] =
                [
                    new("manager-1", "manager@example.test", "Manager One")
                ]
            },
            TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["referenceNumber"] = "1050"
            }
        };

        var result = CreateService(dbContext).Capture(
            PortfolioTradeVersionId(dbContext),
            "WF-PT-CAPTURE",
            input);
        dbContext.SaveChanges();

        Assert.Empty(result.Errors);
        var participant = Assert.Single(dbContext.WorkflowParticipants);
        Assert.Equal("originalMaker", participant.ParticipantKey);
        Assert.Equal("maker-1", participant.UserId);
        Assert.Equal(" maker-value ", participant.EmailAddress);
        Assert.Equal("Maker One", participant.DisplayName);
        Assert.Equal(1, participant.SortOrder);
        Assert.Equal(Now, participant.CreatedAtUtc);
        Assert.DoesNotContain("originalMaker", result.ActionNotificationInput.RecipientInputs.Keys);
        Assert.Single(result.ActionNotificationInput.RecipientInputs["selectedCcUsers"]);
        Assert.Equal("1050", result.ActionNotificationInput.TemplateData["referenceNumber"]);
    }

    [Fact]
    public void CaptureRejectsMissingOrMultipleRequiredSingleParticipant()
    {
        using var dbContext = CreateContextWithInstance();
        var service = CreateService(dbContext);

        var missing = service.Capture(
            PortfolioTradeVersionId(dbContext),
            "WF-PT-CAPTURE",
            new WorkflowNotificationInput());
        var multiple = service.Capture(
            PortfolioTradeVersionId(dbContext),
            "WF-PT-CAPTURE",
            new WorkflowNotificationInput
            {
                RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
                {
                    ["originalMaker"] =
                    [
                        new("maker-1", "first", null),
                        new("maker-2", "second", null)
                    ]
                }
            });

        Assert.Contains(missing.Errors, error => error.Contains("originalMaker", StringComparison.Ordinal));
        Assert.Contains(multiple.Errors, error => error.Contains("exactly one", StringComparison.Ordinal));
        Assert.Empty(dbContext.ChangeTracker.Entries<WorkflowParticipant>());
    }

    [Fact]
    public void CaptureStoresNullAddressAsEmptyOpaqueValueAndCannotReplaceImmutableMaker()
    {
        using var dbContext = CreateContextWithInstance();
        var service = CreateService(dbContext);
        var first = service.Capture(
            PortfolioTradeVersionId(dbContext),
            "WF-PT-CAPTURE",
            MakerInput(null!));
        dbContext.SaveChanges();

        var replacement = service.Capture(
            PortfolioTradeVersionId(dbContext),
            "WF-PT-CAPTURE",
            MakerInput("replacement"));

        Assert.Empty(first.Errors);
        Assert.Equal(string.Empty, Assert.Single(dbContext.WorkflowParticipants).EmailAddress);
        Assert.Contains(replacement.Errors, error => error.Contains("immutable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(string.Empty, Assert.Single(dbContext.WorkflowParticipants).EmailAddress);
    }

    private static WorkflowNotificationInput MakerInput(string address)
    {
        return new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["originalMaker"] = [new("maker-1", address, "Maker One")]
            }
        };
    }

    private static WorkflowParticipantCaptureService CreateService(WorkflowDbContext dbContext)
    {
        return new WorkflowParticipantCaptureService(dbContext, new FixedTimeProvider(Now));
    }

    private static WorkflowDbContext CreateContextWithInstance()
    {
        var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = "WF-PT-CAPTURE",
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-CAPTURE",
            CurrentStateKey = "Draft",
            Status = "Active",
            StartedAtUtc = Now,
            StartedByUserId = "maker-1",
            Version = 1
        });
        dbContext.SaveChanges();
        return dbContext;
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definitionId = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade").Id;
        return dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definitionId && row.IsActive).Id;
    }
}
