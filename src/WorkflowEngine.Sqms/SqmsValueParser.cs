using System.Globalization;

namespace WorkflowEngine.Sqms;

public enum SqmsBidCategory
{
    FairValue,
    Premium,
    Discount
}

public sealed record SqmsParseResult<T>(bool Success, T Value, string? Error)
{
    public static SqmsParseResult<T> Succeeded(T value) => new(true, value, null);

    public static SqmsParseResult<T> Failed(string error) => new(false, default!, error);
}

public static class SqmsValueParser
{
    private static readonly NumberFormatInfo InvariantDollarFormat = CreateInvariantDollarFormat();

    public static SqmsParseResult<decimal> ParseAmount(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return SqmsParseResult<decimal>.Failed("Amount is required.");
        }

        if (!decimal.TryParse(
                input,
                NumberStyles.Currency,
                InvariantDollarFormat,
                out var value) || value < 0)
        {
            return SqmsParseResult<decimal>.Failed(
                $"Amount '{input}' is not a non-negative invariant currency value.");
        }

        return SqmsParseResult<decimal>.Succeeded(value);
    }

    public static SqmsParseResult<SqmsBidCategory> ParseBidCategory(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return SqmsParseResult<SqmsBidCategory>.Failed("Bid category is required.");
        }

        var normalized = string.Concat(input.Where(character => !char.IsWhiteSpace(character)));
        if (normalized.Equals("FairValue", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Yes,FairValue", StringComparison.OrdinalIgnoreCase))
        {
            return SqmsParseResult<SqmsBidCategory>.Succeeded(SqmsBidCategory.FairValue);
        }

        if (normalized.Equals("Premium", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("No,Premium", StringComparison.OrdinalIgnoreCase))
        {
            return SqmsParseResult<SqmsBidCategory>.Succeeded(SqmsBidCategory.Premium);
        }

        if (normalized.Equals("Discount", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("No,Discount", StringComparison.OrdinalIgnoreCase))
        {
            return SqmsParseResult<SqmsBidCategory>.Succeeded(SqmsBidCategory.Discount);
        }

        return SqmsParseResult<SqmsBidCategory>.Failed($"Bid category '{input}' is not recognized.");
    }

    private static NumberFormatInfo CreateInvariantDollarFormat()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.CurrencySymbol = "$";
        return NumberFormatInfo.ReadOnly(format);
    }
}
