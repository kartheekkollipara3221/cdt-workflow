namespace BBH.CP.Atlas.Data.Platform;

/// <summary>Maps SQL Server column types to CLR types and EF Core store types.</summary>
public static class AtlasSqlTypes
{
    /// <summary>Returns the SQL type name in lowercase, with no spaces at the start or end.</summary>
    public static string Normalize(string sqlType) => sqlType.Trim().ToLowerInvariant();

    /// <summary>Returns true for a rowversion (timestamp) type.</summary>
    public static bool IsRowVersion(string sqlType) => Normalize(sqlType) is "timestamp" or "rowversion";

    /// <summary>Returns true for a varbinary or binary type (bytes, for example a file).</summary>
    public static bool IsBinary(string sqlType) => Normalize(sqlType) is "varbinary" or "binary";

    /// <summary>Returns true for a char, varchar, nchar or nvarchar type.</summary>
    public static bool IsText(string sqlType) => Normalize(sqlType) is "char" or "varchar" or "nchar" or "nvarchar";

    /// <summary>Returns true if the platform can map the type of the column.</summary>
    public static bool IsSupported(AtlasColumn column) => ClrType(column) != null;

    /// <summary>
    /// Returns the CLR type of the column, or null if the platform does not support the SQL type.
    /// A nullable column of a value type gives a <see cref="Nullable{T}"/> type.
    /// </summary>
    public static Type? ClrType(AtlasColumn column)
    {
        Type? baseType = Normalize(column.SqlType) switch
        {
            "int" => typeof(int),
            "bigint" => typeof(long),
            "smallint" => typeof(short),
            "tinyint" => typeof(byte),
            "bit" => typeof(bool),
            "decimal" or "numeric" or "money" or "smallmoney" => typeof(decimal),
            "date" or "datetime" or "datetime2" or "smalldatetime" => typeof(DateTime),
            "char" or "varchar" or "nchar" or "nvarchar" => typeof(string),
            "uniqueidentifier" => typeof(Guid),
            "timestamp" or "rowversion" or "varbinary" or "binary" => typeof(byte[]),
            _ => null
        };

        if (baseType == null)
            return null;

        return baseType.IsValueType && column.IsNullable ? typeof(Nullable<>).MakeGenericType(baseType) : baseType;
    }

    /// <summary>Returns the EF Core store type of the column, with its length, precision or scale.</summary>
    public static string StoreType(AtlasColumn column)
    {
        string type = Normalize(column.SqlType);
        return type switch
        {
            "char" or "varchar" or "nchar" or "nvarchar" =>
                $"{type}({(column.MaxLength is null or < 0 ? "max" : column.MaxLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))})",
            "decimal" or "numeric" => $"{type}({column.Precision},{column.Scale})",
            "datetime2" => $"datetime2({column.Scale})",
            "varbinary" or "binary" =>
                $"{type}({(column.MaxLength is null or < 0 ? "max" : column.MaxLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))})",
            "timestamp" => "rowversion",
            _ => type
        };
    }
}
