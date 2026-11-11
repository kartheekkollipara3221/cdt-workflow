// STAND-IN: do not copy to the target.
// Target: Data/Data/DatabaseExtension.cs (ExecuteDataReader) and Data/Data/CommenExtension.cs (ToDictionaryList).
// Only the members that the platform uses. Same signatures.
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.Data
{
    /// <summary>Runs stored procedures on the CP Atlas database (a copy of the target method).</summary>
    public static class DatabaseExtension
    {
        /// <summary>
        /// Runs the stored procedure with the parameters and returns a reader.
        /// The connection closes when the caller disposes the reader.
        /// </summary>
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

    /// <summary>Converts data tables (a copy of the target method).</summary>
    public static class CommonExtension
    {
        /// <summary>Returns one dictionary for each row: column name to value.</summary>
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
