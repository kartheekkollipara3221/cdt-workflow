using System.Globalization;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// The server checks of one field (spec 01, 10.3). The client (WebUi platform/form/atlas-validators.ts) uses the
/// same rules and texts; the cases in atlas-validator-cases.ts are checked on both sides (review SV-04):
/// a value of only spaces is empty; Pattern is anchored as Angular Validators.pattern does; Integer, then a number
/// (no exponent), then Min and Max; Validator.Message, when set, is the text of each failure.
/// </summary>
public static partial class AtlasFieldValidator
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    public static CPPageError? Validate(AtlasFormField field, string? value)
    {
        ArgumentNullException.ThrowIfNull(field);
        CPValidator? validator = field.Validator;
        string label = field.Label ?? field.Name;
        string Text(string defaultText) => validator?.Message ?? defaultText;

        if (string.IsNullOrWhiteSpace(value))
            return field.Required && !field.IsGridField ? Error(field.Name, "Required", Text($"{label} is required.")) : null;

        if (validator == null)
            return null;
        if (validator.MaxLength is { } maxLength && value.Length > maxLength)
            return Error(field.Name, "MaxLength", Text($"{label} can have at most {maxLength} characters."));
        if (validator.MinLength is { } minLength && value.Length < minLength)
            return Error(field.Name, "MinLength", Text($"{label} must have at least {minLength} characters."));
        if (!string.IsNullOrEmpty(validator.Pattern) && !PatternMatches(validator.Pattern, value))
            return Error(field.Name, "Pattern", Text($"{label} has a format that is not valid."));

        if (validator.Integer || validator.Min != null || validator.Max != null)
        {
            string text = value.Trim();
            if (validator.Integer && !IntegerText().IsMatch(text))
                return Error(field.Name, "Integer", Text($"{label} must be a whole number."));
            if (!NumberText().IsMatch(text)
                || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number))
                return Error(field.Name, "InvalidNumber", Text($"{label} must be a number."));
            if (validator.Min is { } min && number < min)
                return Error(field.Name, "Min", Text($"{label} must be {min.ToString(CultureInfo.InvariantCulture)} or more."));
            if (validator.Max is { } max && number > max)
                return Error(field.Name, "Max", Text($"{label} must be {max.ToString(CultureInfo.InvariantCulture)} or less."));
        }

        return null;
    }

    /// <summary>The anchoring of Angular Validators.pattern(string): ^ and $ are added when they are not there.</summary>
    public static string AnchoredPattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return (pattern.StartsWith('^') ? "" : "^") + pattern + (pattern.EndsWith('$') ? "" : "$");
    }

    /// <summary>A pattern that takes too long is a failed check (fail closed), not a server error.</summary>
    private static bool PatternMatches(string pattern, string value)
    {
        try
        {
            return Regex.IsMatch(value, AnchoredPattern(pattern), RegexOptions.None, PatternTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static CPPageError Error(string field, string code, string message) => new() { Field = field, Code = code, Message = message };

    [GeneratedRegex(@"^[+-]?\d+$")]
    private static partial Regex IntegerText();

    [GeneratedRegex(@"^[+-]?(\d+\.?\d*|\.\d+)$")]
    private static partial Regex NumberText();
}
