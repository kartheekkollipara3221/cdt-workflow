// STAND-IN: do not copy to the target.
// Target: the Oracle PB data warehouse context (registered with UseOracle in program.cs).
// Locally there is no Oracle database. The platform reads PB accounts only through
// CPAtlasExternalService (the stand-in returns sample accounts).
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.Data;

/// <summary>The EF Core context of the PB data warehouse. Locally it has no entities.</summary>
public partial class CPPBContext : DbContext
{
    public CPPBContext(DbContextOptions<CPPBContext> options)
        : base(options)
    {
    }
}
