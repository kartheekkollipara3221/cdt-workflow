// STAND-IN: do not copy to the target.
// Target: Data/Data/Models/CpAtlasApplication.cs, CpAtlasEntitlement.cs, CpAtlasUser.cs, CpAtlasUserEntitlement.cs (scaffolded).
namespace BBH.CP.Atlas.Data.Models;

public partial class CPAtlasApplication
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool? IsDeleted { get; set; }
    public int? ParentId { get; set; }
    public virtual ICollection<CPAtlasEntitlement> CPAtlasEntitlements { get; set; } = new List<CPAtlasEntitlement>();
}

public partial class CPAtlasEntitlement
{
    public int Id { get; set; }
    public int? CpAtlasApplicationId { get; set; }
    public string? Name { get; set; }
    public bool? IsAdmin { get; set; }
    public bool? IsWrite { get; set; }
    public bool? IsRead { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool? IsDeleted { get; set; }
    public virtual CPAtlasApplication? CpAtlasApplication { get; set; }
}

public partial class CPAtlasUser
{
    public int Id { get; set; }
    public string? LanId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool? IsDeleted { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public virtual ICollection<CPAtlasUserEntitlement> CpAtlasUserEntitlements { get; set; } = new List<CPAtlasUserEntitlement>();
}

public partial class CPAtlasUserEntitlement
{
    public int Id { get; set; }
    public int? CpAtlasUserId { get; set; }
    public int? CpAtlasEntitlementId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool? IsDeleted { get; set; }
    public virtual CPAtlasUser? CpAtlasUser { get; set; }
}
