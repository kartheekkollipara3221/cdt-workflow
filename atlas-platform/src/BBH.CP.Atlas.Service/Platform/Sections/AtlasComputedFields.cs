using System.Globalization;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// The Computed fields of a form (spec 01, 10.2): <c>"Target": "Sum(A, B, ...)"</c>. One function table for the save
/// and the start-up check (review SV-08). Empty sources are left out; Min and Max of no value give null.
/// </summary>
public static partial class AtlasComputedFields
{
    private static readonly Dictionary<string, Func<decimal[], decimal?>> FunctionTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sum"] = values => values.Sum(),
        ["Count"] = values => values.Length,
        ["Min"] = values => values.Length == 0 ? null : values.Min(),
        ["Max"] = values => values.Length == 0 ? null : values.Max()
    };

    /// <summary>The names of the functions, for messages.</summary>
    public static IReadOnlyCollection<string> Functions => FunctionTable.Keys;

    /// <summary>Splits "Function(A, B)". False if the text is not a call of a known function.</summary>
    public static bool TryParse(string? expression, out string function, out IReadOnlyList<string> sources)
    {
        function = "";
        sources = [];
        Match match = expression == null ? Match.Empty : Expression().Match(expression);
        if (!match.Success || !FunctionTable.ContainsKey(match.Groups[1].Value))
            return false;

        function = match.Groups[1].Value;
        sources = match.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return true;
    }

    /// <summary>The result of a function for the values that are present.</summary>
    public static decimal? Evaluate(string function, IEnumerable<decimal> values) =>
        FunctionTable.TryGetValue(function, out Func<decimal[], decimal?>? evaluate)
            ? evaluate([.. values])
            : throw new ArgumentException($"'{function}' is not a Computed function.", nameof(function));

    /// <summary>Sets the target column of the record. Returns an error, or null.</summary>
    public static CPPageError? Compute(AtlasTable table, Dictionary<string, object?> record, string target, string expression)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(record);
        AtlasColumn? column = table.Find(target);
        if (column == null || !TryParse(expression, out string function, out IReadOnlyList<string> sources))
            return new CPPageError { Field = "", Code = "ComputedInvalid", Message = $"Computed field '{target}' has the expression '{expression}', which is not valid." };

        IEnumerable<decimal> present = sources
            .Select(name => table.Find(name) is { } source && record.GetValueOrDefault(source.Name) is { } value
                ? Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                : (decimal?)null)
            .Where(x => x != null)
            .Select(x => x!.Value);

        decimal? result = Evaluate(function, present);
        AtlasConversionResult converted = AtlasValueConverter.FromWire(result?.ToString(CultureInfo.InvariantCulture), column);
        if (!converted.Success)
            return new CPPageError { Field = target, Code = converted.ErrorCode!, Message = converted.Message! };

        record[column.Name] = converted.Value;
        return null;
    }

    [GeneratedRegex(@"^\s*(\w+)\s*\((.*)\)\s*$")]
    private static partial Regex Expression();
}
