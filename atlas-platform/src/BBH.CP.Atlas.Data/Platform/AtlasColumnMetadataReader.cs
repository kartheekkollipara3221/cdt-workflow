using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Data.Platform;

/// <summary>
/// Reads the columns of the registered tables from sys.columns (spec 05, 6.2 and 6.8).
/// The API login needs no VIEW DEFINITION grant: SQL Server shows the metadata of the objects on
/// which the login has a permission (spec 05, question 4).
/// </summary>
public sealed class AtlasColumnMetadataReader
{
    private const string Query = """
        SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName,
               TYPE_NAME(c.system_type_id) AS TypeName, c.is_nullable, c.max_length,
               c.precision, c.scale, c.is_identity, c.is_computed
        FROM sys.tables AS t
        INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        INNER JOIN sys.columns AS c ON c.object_id = t.object_id
        WHERE s.name = @Schema AND t.name = @Table
        ORDER BY c.column_id;
        """;

    /// <summary>Returns the table, or null if the table does not exist (or the login cannot see it).</summary>
    public async Task<AtlasTable?> ReadAsync(SqlConnection connection, string fullName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        (string schema, string name) = AtlasTable.ParseFullName(fullName);

        bool opened = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            opened = true;
        }

        try
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = Query;
            command.Parameters.Add(new SqlParameter("@Schema", System.Data.SqlDbType.NVarChar, 128) { Value = schema });
            command.Parameters.Add(new SqlParameter("@Table", System.Data.SqlDbType.NVarChar, 128) { Value = name });

            List<AtlasColumn> columns = [];
            string? actualSchema = null, actualName = null;
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                actualSchema ??= reader.GetString(0);
                actualName ??= reader.GetString(1);
                string typeName = reader.GetString(3);
                short maxLengthBytes = reader.GetInt16(5);
                columns.Add(new AtlasColumn(
                    Name: reader.GetString(2),
                    SqlType: typeName,
                    IsNullable: reader.GetBoolean(4),
                    MaxLength: ToCharacterLength(typeName, maxLengthBytes),
                    Precision: reader.GetByte(6),
                    Scale: reader.GetByte(7),
                    IsIdentity: reader.GetBoolean(8),
                    IsComputed: reader.GetBoolean(9)));
            }

            return columns.Count == 0 ? null : new AtlasTable(actualSchema!, actualName!, columns);
        }
        finally
        {
            if (opened)
                await connection.CloseAsync();
        }
    }

    /// <summary>Reads each table and adds it to the registry. Returns the names that were not found.</summary>
    public async Task<IReadOnlyList<string>> LoadAsync(SqlConnection connection, AtlasTableRegistry registry,
        IEnumerable<string> fullNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        List<string> missing = [];
        foreach (string fullName in fullNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AtlasTable? table = await ReadAsync(connection, fullName, cancellationToken);
            if (table == null)
                missing.Add(fullName);
            else
                registry.Add(table);
        }

        return missing;
    }

    private static int? ToCharacterLength(string typeName, short maxLengthBytes)
    {
        string type = AtlasSqlTypes.Normalize(typeName);
        if (maxLengthBytes == -1 && type is "varchar" or "nvarchar" or "varbinary")
            return -1;

        return type switch
        {
            "nchar" or "nvarchar" => maxLengthBytes / 2,
            "char" or "varchar" or "binary" or "varbinary" => maxLengthBytes,
            _ => null
        };
    }
}
