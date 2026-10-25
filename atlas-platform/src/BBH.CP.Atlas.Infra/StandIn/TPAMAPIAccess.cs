// STAND-IN: do not copy to the target.
// Target: Infra/Infra/TPAMAPIAccess.cs. The target gets the database password from TPAM.
// Locally, the connection string comes from the settings (Windows authentication, no password).
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Infra
{
    public class TPAMAPIAccess
    {
        public static string GetConnectionString(IConfiguration configuration, string connectionKey)
        {
            string? connectionString = configuration["ConnectionStrings:" + connectionKey];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"ConnectionStrings:{connectionKey} is not set.");

            return connectionString;
        }
    }
}
