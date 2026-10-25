// STAND-IN: do not copy to the target.
// Target: Data/Data/DTO/CPAtlasPage.cs, CPLogin.cs (CPUser), CPAtlasLog.cs (CPAuditLog), CPExternalModel.cs (CPPBAccount),
// CPAtlasApplication.cs (CPApplication). Same classes and properties; CPAtlasPage and CPAuditLog are "partial" (INTEGRATION.md).
using BBH.CP.Atlas.Data.Enumerations;

namespace BBH.CP.Atlas.Data.DTO
{
    public partial class CPAtlasPage
    {
        public CPApplicationCode? Code { get; set; }
        public string? Step { get; set; }
        public int ID { get; set; }
        public List<CPPageOutput>? Outputs { get; set; }
    }

    public class CPPageOutput
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

    public class CPUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DisplayName { get; set; }
        public string? EmailAddress { get; set; }
        public string? LanID { get; set; }
        public string[]? UserGroups { get; set; }
        public string? Department { get; set; }
        public string? Token { get; set; }
        public List<string>? Roles { get; set; }
    }

    public partial class CPAuditLog
    {
        public required string AppCode { get; set; }
        public required int RefID { get; set; }
        public required string CreatedBy { get; set; }
        public required string CreatedDate { get; set; }
        public object? LogMessage { get; set; }
    }

    public class CPPBAccount
    {
        public string? AccountNumber { get; set; }
        public string? AccountShortName { get; set; }
        public string? BranchNumber { get; set; }
        public string? BranchName { get; set; }
        public string? RelationshipManager { get; set; }
        public string? BBHAffiliateAccount { get; set; }
    }

    public class CPApplication
    {
        public int ID { get; set; }
        public string? Name { get; set; }
        public List<CPApplication>? Children { get; set; }
    }
}
