using System.Globalization;
using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// Checks the value of one field on the server.
/// </summary>
/// <remarks>
/// The client (see atlas-validators.ts) uses the same rules and texts.
/// Tests check the cases in atlas-validator-cases.ts on the two sides.
/// A value of only spaces is empty. Pattern is anchored as Angular Validators.pattern does it.
/// The checks run in this order: Required, MustBeTrue, the length checks, Pattern, Integer, number (no exponent),
/// Min, Max and Step.
/// When Validator.Message is set, it is the text of each failure.
/// </remarks>
public static partial class AtlasFieldValidator
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Returns the first error of the value, or null when the value is valid.</summary>
    public static CPPageError? Validate(AtlasFormField field, string? value)
    {
        ArgumentNullException.ThrowIfNull(field);
        CPValidator? validator = field.Validator;
        string label = field.Label ?? field.Name;
        string Text(string defaultText) => validator?.Message ?? defaultText;

        if (string.IsNullOrWhiteSpace(value) && field.Required && !field.IsGridField)
            return Error(field.Name, "Required", Text($"{label} is required."));
        if (validator?.MustBeTrue == true && !IsTrue(value))
            return Error(field.Name, "MustBeTrue", Text($"Select {label} before you submit."));
        if (string.IsNullOrWhiteSpace(value) || validator == null)
            return null;
        if (validator.MaxLength is { } maxLength && value.Length > maxLength)
            return Error(field.Name, "MaxLength", Text($"{label} can have at most {maxLength} characters."));
        if (validator.MinLength is { } minLength && value.Length < minLength)
            return Error(field.Name, "MinLength", Text($"{label} must have at least {minLength} characters."));
        if (!string.IsNullOrEmpty(validator.Pattern) && !PatternMatches(validator.Pattern, value))
            return Error(field.Name, "Pattern", Text($"{label} has a format that is not valid."));

        if (validator.Integer || validator.Min != null || validator.Max != null || validator.Step != null)
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
            if (validator.Step is { } step && step > 0 && number % step != 0)
                return Error(field.Name, "Step", Text($"{label} must be a multiple of {step.ToString(CultureInfo.InvariantCulture)}."));
        }

        return null;
    }

    /// <summary>
    /// Returns the pattern anchored as Angular Validators.pattern(string) does it. It adds ^ and $ when they are missing.
    /// </summary>
    public static string AnchoredPattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return (pattern.StartsWith('^') ? "" : "^") + pattern + (pattern.EndsWith('$') ? "" : "$");
    }

    /// <summary>Returns true when the value matches the pattern.</summary>
    /// <remarks>A pattern that takes too long is a failed check (fail closed), not a server error.</remarks>
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

    /// <summary>Returns true for the texts that a bit column reads as true: true, yes or 1 (any case).</summary>
    private static bool IsTrue(string? value) =>
        value?.Trim().ToLowerInvariant() is "true" or "yes" or "1";

    private static CPPageError Error(string field, string code, string message) => new() { Field = field, Code = code, Message = message };

    [GeneratedRegex(@"^[+-]?\d+$")]
    private static partial Regex IntegerText();

    [GeneratedRegex(@"^[+-]?(\d+\.?\d*|\.\d+)$")]
    private static partial Regex NumberText();
}
