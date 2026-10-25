// STAND-IN: do not copy to the target.
// Target: Data/Data/DatabaseExtension.cs (ExecuteDataReader) and Data/Data/CommenExtension.cs (ToDictionaryList).
// Only the members that the platform uses. Same signatures.
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.Data
{
    public static class DatabaseExtension
    {
        public static DbDataReader ExecuteDataReader(this CPSharepointContext context, string procedure, SqlParameter[]? parameters = null)
        {
            SqlConnection connection = (SqlConnection)context.Database.GetDbConnection();
            using SqlCommand command = connection.CreateCommand();

            command.CommandText = procedure;
            command.CommandType = CommandType.StoredProcedure;

            if (parameters != null && parameters.Length > 0)
                command.Parameters.AddRange(parameters);

            if (connection.State == ConnectionState.Closed)
                connection.Open();

            return command.ExecuteReader(CommandBehavior.CloseConnection);
        }
    }

    public static class CommonExtension
    {
        public static List<Dictionary<string, object>> ToDictionaryList(this DataTable table)
        {
            List<Dictionary<string, object>> result = [];
            foreach (DataRow row in table.Rows)
            {
                Dictionary<string, object> item = [];
                foreach (DataColumn column in table.Columns)
                    item[column.ColumnName] = row[column];
                result.Add(item);
            }

            return result;
        }
    }
}
