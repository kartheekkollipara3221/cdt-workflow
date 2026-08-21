using System.Globalization;
using WorkflowEngine.Sqms;

namespace WorkflowEngine.Tests;

public sealed class SqmsValueParserTests
{
    [Theory]
    [InlineData("1000", "1000")]
    [InlineData("1,000.00", "1000.00")]
    [InlineData("$1,000.00", "1000.00")]
    [InlineData("  $1,000.00  ", "1000.00")]
    public void ParseAmountAcceptsApprovedInvariantFormats(string input, string expected)
    {
        var result = SqmsValueParser.ParseAmount(input);

        Assert.True(result.Success, result.Error);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not money")]
    [InlineData("-1")]
    [InlineData("($1.00)")]
    public void ParseAmountReturnsStructuredFailureForMissingMalformedOrNegativeValues(string? input)
    {
        var result = SqmsValueParser.ParseAmount(input);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Theory]
    [InlineData("Fair Value", SqmsBidCategory.FairValue)]
    [InlineData("FAIRVALUE", SqmsBidCategory.FairValue)]
    [InlineData("  fair value  ", SqmsBidCategory.FairValue)]
    [InlineData("Premium", SqmsBidCategory.Premium)]
    [InlineData("Discount", SqmsBidCategory.Discount)]
    public void ParseBidCategoryNormalizesApprovedValues(string input, SqmsBidCategory expected)
    {
        var result = SqmsValueParser.ParseBidCategory(input);

        Assert.True(result.Success, result.Error);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("At Par")]
    public void ParseBidCategoryRejectsMissingAndUnknownValues(string? input)
    {
        var result = SqmsValueParser.ParseBidCategory(input);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}
