// STAND-IN: do not copy to the target.
// Target: Service/CPAtlasExternalService.cs. The target reads the PB accounts from an internal API.
// Locally, GetPBAccount returns fixed sample accounts.
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service
{
    public class CPAtlasExternalService
    {
        public CPAtlasExternalService(IConfiguration configuration)
        {
        }

        public CPAtlasExternalService(IConfiguration configuration, CPPBContext? pbContext, CPUser user)
        {
        }

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
