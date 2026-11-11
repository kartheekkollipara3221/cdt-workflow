using System.Data;
using System.Data.Common;
using BBH.CP.Atlas.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BBH.CP.Atlas.Service.Platform.Security;

/// <summary>Runs small parameterized queries on the connection of <see cref="CPSharepointContext"/>.</summary>
internal static class AtlasSql
{
    /// <summary>Runs a query and maps each row. The query uses the current transaction of the context.</summary>
    public static List<T> Query<T>(CPSharepointContext context, string sql, Func<DbDataReader, T> map, params SqlParameter[] parameters)
    {
        context.Database.OpenConnection();
        try
        {
            using DbCommand command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.Parameters.AddRange(parameters);

            List<T> result = [];
            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
                result.Add(map(reader));
            return result;
        }
        finally
        {
            context.Database.CloseConnection();
        }
    }

    /// <summary>Returns the text of a column, or null for a database NULL.</summary>
    public static string? NullableString(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
