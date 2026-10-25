namespace BBH.CP.Atlas.Data.Platform;

/// <summary>Maps SQL Server column types to CLR types and EF Core store types (spec 05, 6.2 and 6.4).</summary>
public static class AtlasSqlTypes
{
    public static string Normalize(string sqlType) => sqlType.Trim().ToLowerInvariant();

    public static bool IsRowVersion(string sqlType) => Normalize(sqlType) is "timestamp" or "rowversion";

    public static bool IsText(string sqlType) => Normalize(sqlType) is "char" or "varchar" or "nchar" or "nvarchar";

    public static bool IsSupported(AtlasColumn column) => ClrType(column) != null;

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
            "timestamp" or "rowversion" => typeof(byte[]),
            _ => null
        };

        if (baseType == null)
            return null;

        return baseType.IsValueType && column.IsNullable ? typeof(Nullable<>).MakeGenericType(baseType) : baseType;
    }

    public static string StoreType(AtlasColumn column)
    {
        string type = Normalize(column.SqlType);
        return type switch
        {
            "char" or "varchar" or "nchar" or "nvarchar" =>
                $"{type}({(column.MaxLength is null or < 0 ? "max" : column.MaxLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))})",
            "decimal" or "numeric" => $"{type}({column.Precision},{column.Scale})",
            "datetime2" => $"datetime2({column.Scale})",
            "timestamp" => "rowversion",
            _ => type
        };
    }
}
