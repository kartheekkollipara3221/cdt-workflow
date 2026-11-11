// STAND-IN: do not copy to the target.
// Target: Data/Data/Data extended/CPSharepointContext.cs (scaffolded). Only the members that the platform uses.
// Copy-error note: the model file has "Lanid", the mapping has "LanId". The stand-in uses "LanId" (check at the target).
using BBH.CP.Atlas.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Data;

/// <summary>The EF Core context of the CP Atlas database (the user, role and application tables).</summary>
public partial class CPSharepointContext : DbContext
{
    public IConfiguration Configuration { get; }

    public CPSharepointContext(DbContextOptions<CPSharepointContext> options, IConfiguration configuration)
        : base(options)
    {
        Configuration = configuration;
    }

    public virtual DbSet<CPAtlasApplication> CPAtlasApplications { get; set; }

    public virtual DbSet<CPAtlasEntitlement> CPAtlasEntitlements { get; set; }

    public virtual DbSet<CPAtlasUser> CPAtlasUsers { get; set; }

    public virtual DbSet<CPAtlasUserEntitlement> CPAtlasUserEntitlements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CPAtlasApplication>(entity =>
        {
            entity.ToTable("CP_Atlas_Application", "dbo");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Code).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedBy).HasMaxLength(50);
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.ParentId).HasColumnName("ParentID");
        });

        modelBuilder.Entity<CPAtlasEntitlement>(entity =>
        {
            entity.ToTable("CP_Atlas_Entitlement", "dbo");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CpAtlasApplicationId).HasColumnName("CP_Atlas_ApplicationID");
            entity.Property(e => e.Name).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedBy).HasMaxLength(50);
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.HasOne(d => d.CpAtlasApplication).WithMany(p => p.CPAtlasEntitlements)
                .HasForeignKey(d => d.CpAtlasApplicationId);
        });

        modelBuilder.Entity<CPAtlasUser>(entity =>
        {
            entity.ToTable("CP_Atlas_User", "dbo");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.LanId).HasMaxLength(50).HasColumnName("LanID");
            entity.Property(e => e.FirstName).HasMaxLength(500);
            entity.Property(e => e.LastName).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(500);
            entity.Property(e => e.DisplayName).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedBy).HasMaxLength(50);
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<CPAtlasUserEntitlement>(entity =>
        {
            entity.ToTable("CP_Atlas_UserEntitlement", "dbo");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CpAtlasUserId).HasColumnName("CP_Atlas_UserID");
            entity.Property(e => e.CpAtlasEntitlementId).HasColumnName("CP_Atlas_EntitlementID");
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedBy).HasMaxLength(50);
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.HasOne(d => d.CpAtlasUser).WithMany(p => p.CpAtlasUserEntitlements)
                .HasForeignKey(d => d.CpAtlasUserId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
