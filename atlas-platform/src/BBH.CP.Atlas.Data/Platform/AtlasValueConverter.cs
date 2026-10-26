using System.Globalization;

namespace BBH.CP.Atlas.Data.Platform;

/// <summary>The result of one value conversion. On failure, <see cref="ErrorCode"/> is the field error code (spec 01, 5.11).</summary>
public readonly record struct AtlasConversionResult(bool Success, object? Value, string? ErrorCode, string? Message)
{
    public static AtlasConversionResult Ok(object? value) => new(true, value, null, null);

    public static AtlasConversionResult Fail(string code, string message) => new(false, null, code, message);
}

/// <summary>
/// Converts between the wire format (text in <c>CPPageOutput.Value</c> and <c>CPApplicationControl.Value</c>) and the
/// column value, by the SQL column type, with the invariant culture (spec 05, 6.4). The server culture has no effect.
/// </summary>
public static class AtlasValueConverter
{
    public const string Required = "Required";
    public const string InvalidNumber = "InvalidNumber";
    public const string OutOfRange = "OutOfRange";
    public const string InvalidDate = "InvalidDate";
    public const string InvalidBoolean = "InvalidBoolean";
    public const string InvalidGuid = "InvalidGuid";
    public const string TooLong = "TooLong";
    public const string NotWritable = "NotWritable";
    public const string UnsupportedType = "UnsupportedType";

    private static readonly DateTime SqlDateTimeMin = new(1753, 1, 1);
    private static readonly DateTime SmallDateTimeMin = new(1900, 1, 1);
    private static readonly DateTime SmallDateTimeMax = new(2079, 6, 6);

    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd"
    ];

    /// <summary>
    /// Converts a wire value to the column value. An empty or white-space value is null.
    /// A database-generated column (identity, rowversion, computed) is not writable.
    /// </summary>
    public static AtlasConversionResult FromWire(string? wire, AtlasColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (column.IsDatabaseGenerated)
            return AtlasConversionResult.Fail(NotWritable, $"{column.Name} is set by the database.");

        if (string.IsNullOrWhiteSpace(wire))
        {
            return column.IsNullable
                ? AtlasConversionResult.Ok(null)
                : AtlasConversionResult.Fail(Required, $"{column.Name} is required.");
        }

        string text = AtlasSqlTypes.IsText(column.SqlType) ? wire : wire.Trim();

        return AtlasSqlTypes.Normalize(column.SqlType) switch
        {
            "int" => Integer(text, column, int.MinValue, int.MaxValue, v => (int)v),
            "bigint" => Integer(text, column, long.MinValue, long.MaxValue, v => v),
            "smallint" => Integer(text, column, short.MinValue, short.MaxValue, v => (short)v),
            "tinyint" => Integer(text, column, byte.MinValue, byte.MaxValue, v => (byte)v),
            "decimal" or "numeric" => Decimal(text, column, column.Precision, column.Scale),
            "money" => Decimal(text, column, 19, 4),
            "smallmoney" => Decimal(text, column, 10, 4),
            "bit" => Boolean(text, column),
            "date" => Date(text, column),
            "datetime" => DateTimeValue(text, column, SqlDateTimeMin, DateTime.MaxValue),
            "smalldatetime" => DateTimeValue(text, column, SmallDateTimeMin, SmallDateTimeMax),
            "datetime2" => DateTimeValue(text, column, DateTime.MinValue, DateTime.MaxValue),
            "char" or "varchar" or "nchar" or "nvarchar" => Text(text, column),
            "uniqueidentifier" => Guid.TryParse(text, out Guid guid)
                ? AtlasConversionResult.Ok(guid)
                : AtlasConversionResult.Fail(InvalidGuid, $"{column.Name} is not a valid identifier."),
            _ => AtlasConversionResult.Fail(UnsupportedType, $"{column.Name} has the type '{column.SqlType}', which the platform does not support.")
        };
    }

    /// <summary>Converts a column value to the wire format. Null gives null. A date-time is UTC with 'Z'.</summary>
    public static string? ToWire(object? value, AtlasColumn column) => ToWire(value, column, utc: true);

    /// <summary>
    /// The same; <paramref name="utc"/> = false writes a date-time with no 'Z' (an app with SystemTime Local, review DA-11).
    /// </summary>
    public static string? ToWire(object? value, AtlasColumn column, bool utc)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (value is null or DBNull)
            return null;

        string type = AtlasSqlTypes.Normalize(column.SqlType);
        return value switch
        {
            DateTime date when type == "date" => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString(utc ? "yyyy-MM-dd'T'HH:mm:ss.fff'Z'" : "yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            byte[] bytes => Convert.ToBase64String(bytes),
            Guid guid => guid.ToString("D"),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// True if two wire values give the same column value (review DA-06): "1250000.50" and "1250000.5", or two forms of
    /// the same time. Empty is null. A value that does not convert is compared as text (ordinal).
    /// </summary>
    public static bool SameValue(string? a, string? b, AtlasColumn? column)
    {
        a = string.IsNullOrEmpty(a) ? null : a;
        b = string.IsNullOrEmpty(b) ? null : b;
        if (string.Equals(a, b, StringComparison.Ordinal))
            return true;
        if (a == null || b == null || column == null || AtlasSqlTypes.IsText(column.SqlType))
            return false;

        AtlasConversionResult left = FromWire(a, column), right = FromWire(b, column);
        return left.Success && right.Success && Equals(left.Value, right.Value);
    }

    private static AtlasConversionResult Integer(string text, AtlasColumn column, long min, long max, Func<long, object> box)
    {
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
            return AtlasConversionResult.Fail(InvalidNumber, $"{column.Name} must be a whole number.");

        if (value < min || value > max)
            return AtlasConversionResult.Fail(OutOfRange, $"{column.Name} is out of range.");

        return AtlasConversionResult.Ok(box(value));
    }

    private static AtlasConversionResult Decimal(string text, AtlasColumn column, int precision, int scale)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
            return AtlasConversionResult.Fail(InvalidNumber, $"{column.Name} must be a number (for example 1250000.50).");

        int separator = text.IndexOf('.', StringComparison.Ordinal);
        int decimals = separator < 0 ? 0 : text.Length - separator - 1;
        if (decimals > scale)
            return AtlasConversionResult.Fail(InvalidNumber, $"{column.Name} can have at most {scale} decimal places.");

        decimal limit = Pow10(precision - scale);
        if (Math.Abs(decimal.Truncate(value)) >= limit)
            return AtlasConversionResult.Fail(OutOfRange, $"{column.Name} is out of range.");

        return AtlasConversionResult.Ok(value);
    }

    private static AtlasConversionResult Boolean(string text, AtlasColumn column)
    {
        switch (text.ToLowerInvariant())
        {
            case "true" or "yes" or "1":
                return AtlasConversionResult.Ok(true);
            case "false" or "no" or "0":
                return AtlasConversionResult.Ok(false);
            default:
                return AtlasConversionResult.Fail(InvalidBoolean, $"{column.Name} must be true or false.");
        }
    }

    private static AtlasConversionResult Date(string text, AtlasColumn column)
    {
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value))
            return AtlasConversionResult.Fail(InvalidDate, $"{column.Name} must be a date in the format yyyy-MM-dd.");

        return AtlasConversionResult.Ok(value.Date);
    }

    private static AtlasConversionResult DateTimeValue(string text, AtlasColumn column, DateTime min, DateTime max)
    {
        if (!DateTimeOffset.TryParseExact(text, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset value))
            return AtlasConversionResult.Fail(InvalidDate, $"{column.Name} must be a date and time in ISO 8601 format.");

        DateTime utc = DateTime.SpecifyKind(value.UtcDateTime, DateTimeKind.Utc);
        if (utc < min || utc > max)
            return AtlasConversionResult.Fail(OutOfRange, $"{column.Name} is out of range.");

        return AtlasConversionResult.Ok(utc);
    }

    private static AtlasConversionResult Text(string text, AtlasColumn column)
    {
        if (column.MaxLength is > 0 && text.Length > column.MaxLength.Value)
            return AtlasConversionResult.Fail(TooLong, $"{column.Name} can have at most {column.MaxLength.Value} characters.");

        return AtlasConversionResult.Ok(text);
    }

    private static decimal Pow10(int exponent)
    {
        decimal result = 1m;
        for (int i = 0; i < exponent; i++)
            result *= 10m;
        return result;
    }
}
