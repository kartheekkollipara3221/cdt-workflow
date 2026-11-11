// STAND-IN: do not copy to the target.
// Target: Service/CPAtlasExternalService.cs. The target reads the PB accounts from an internal API.
// Locally, GetPBAccount returns fixed sample accounts.
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service
{
    /// <summary>The local stand-in of the external service that reads the PB accounts.</summary>
    public class CPAtlasExternalService
    {
        /// <summary>Creates the service. The stand-in does not use the configuration.</summary>
        public CPAtlasExternalService(IConfiguration configuration)
        {
        }

        /// <summary>Creates the service. The stand-in does not use the parameters.</summary>
        public CPAtlasExternalService(IConfiguration configuration, CPPBContext? pbContext, CPUser user)
        {
        }

        /// <summary>Returns fixed sample PB accounts.</summary>
        public List<CPPBAccount> GetPBAccount()
        {
            return
            [
                new() { AccountNumber = "100100", AccountShortName = "Smith Family Trust", RelationshipManager = "J. Doe" },
                new() { AccountNumber = "100200", AccountShortName = "Brown Foundation", RelationshipManager = "A. Lee" },
                new() { AccountNumber = "100300", AccountShortName = "Green Holdings LLC", RelationshipManager = "J. Doe" },
                new() { AccountNumber = "100400", AccountShortName = "White Estate", RelationshipManager = "M. Patel" },
                new() { AccountNumber = "100500", AccountShortName = "Black River Partners", RelationshipManager = "A. Lee" }
            ];
        }
    }
}
