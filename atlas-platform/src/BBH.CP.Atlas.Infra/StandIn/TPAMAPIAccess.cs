// STAND-IN: do not copy to the target.
// Target: Infra/Infra/TPAMAPIAccess.cs. The target gets the database password from TPAM.
// Locally, the connection string comes from the settings (Windows authentication, no password).
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Infra
{
    /// <summary>Gives the database connection strings.</summary>
    public class TPAMAPIAccess
    {
        /// <summary>Returns the connection string with the key from the settings.</summary>
        /// <exception cref="InvalidOperationException">The settings have no connection string with the key.</exception>
        public static string GetConnectionString(IConfiguration configuration, string connectionKey)
        {
            string? connectionString = configuration["ConnectionStrings:" + connectionKey];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"ConnectionStrings:{connectionKey} is not set.");

            return connectionString;
        }
    }
}
