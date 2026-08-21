using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using WorkflowEngine.Sqms.LiveAcceptance;

try
{
    var configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
    var builder = new SqlConnectionStringBuilder(connectionString);
    if (!string.Equals(
            builder.InitialCatalog,
            AcceptanceConstants.DatabaseName,
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Refusing database '{builder.InitialCatalog}'. Expected '{AcceptanceConstants.DatabaseName}'.");
    }

    Console.Out.WriteLine($"SQMS live acceptance runner ready for {AcceptanceConstants.DatabaseName}.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
