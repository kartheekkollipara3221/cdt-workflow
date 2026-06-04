using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Persistence;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

    public DbSet<WorkflowDefinitionVersion> WorkflowDefinitionVersions => Set<WorkflowDefinitionVersion>();

    public DbSet<WorkflowStateDefinition> WorkflowStateDefinitions => Set<WorkflowStateDefinition>();

    public DbSet<WorkflowActionDefinition> WorkflowActionDefinitions => Set<WorkflowActionDefinition>();

    public DbSet<WorkflowRoleDefinition> WorkflowRoleDefinitions => Set<WorkflowRoleDefinition>();

    public DbSet<WorkflowTaskDefinition> WorkflowTaskDefinitions => Set<WorkflowTaskDefinition>();

    public DbSet<WorkflowTaskAssignmentRoleDefinition> WorkflowTaskAssignmentRoleDefinitions => Set<WorkflowTaskAssignmentRoleDefinition>();

    public DbSet<WorkflowTransitionDefinition> WorkflowTransitionDefinitions => Set<WorkflowTransitionDefinition>();

    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();

    public DbSet<MockSectionData> MockSectionData => Set<MockSectionData>();

    public DbSet<WorkflowAudit> WorkflowAudits => Set<WorkflowAudit>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<NotificationRoute> NotificationRoutes => Set<NotificationRoute>();

    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureWorkflowDefinition(modelBuilder);
        ConfigureWorkflowDefinitionVersion(modelBuilder);
        ConfigureDefinitions(modelBuilder);
        ConfigureRuntime(modelBuilder);
    }

    private static void ConfigureWorkflowDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.ToTable("WorkflowDefinition");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WorkflowKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => row.WorkflowKey).IsUnique();
        });
    }

    private static void ConfigureWorkflowDefinitionVersion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinitionVersion>(entity =>
        {
            entity.ToTable("WorkflowDefinitionVersion");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.CreatedAtUtc).IsRequired();
            entity.HasIndex(row => new { row.WorkflowDefinitionId, row.VersionNumber }).IsUnique();
            entity
                .HasOne(row => row.WorkflowDefinition)
                .WithMany(row => row.Versions)
                .HasForeignKey(row => row.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDefinitions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowStateDefinition>(entity =>
        {
            entity.ToTable("WorkflowStateDefinition");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.StateKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.StateKey }).IsUnique();
        });

        modelBuilder.Entity<WorkflowActionDefinition>(entity =>
        {
            entity.ToTable("WorkflowActionDefinition");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ActionKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(row => row.SectionKey).HasMaxLength(100);
            entity.Property(row => row.ButtonKey).HasMaxLength(100);
            entity.Property(row => row.ButtonLabel).HasMaxLength(200);
            entity.Property(row => row.Intent).HasMaxLength(100);
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.ActionKey }).IsUnique();
        });

        modelBuilder.Entity<WorkflowRoleDefinition>(entity =>
        {
            entity.ToTable("WorkflowRoleDefinition");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.RoleKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.RoleKey }).IsUnique();
        });

        modelBuilder.Entity<WorkflowTaskDefinition>(entity =>
        {
            entity.ToTable("WorkflowTaskDefinition");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.TaskKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(row => row.OwnerRoleKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.SlaPolicyKey).HasMaxLength(100);
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.TaskKey }).IsUnique();
        });

        modelBuilder.Entity<WorkflowTaskAssignmentRoleDefinition>(entity =>
        {
            entity.ToTable("WorkflowTaskAssignmentRoleDefinition");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.TaskKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.RoleKey).HasMaxLength(100).UseCollation("Latin1_General_BIN2").IsRequired();
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.TaskKey, row.RoleKey }).IsUnique();
        });

        modelBuilder.Entity<WorkflowTransitionDefinition>(entity =>
        {
            entity.ToTable("WorkflowTransitionDefinition");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.FromStateKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.ActionKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.AllowedRoleKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.ToStateKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.TaskToCloseKey).HasMaxLength(100);
            entity.Property(row => row.TaskToCreateKey).HasMaxLength(100);
            entity.Property(row => row.NotificationRouteKey).HasMaxLength(100);
            entity.Property(row => row.AuditLabel).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.FromStateKey, row.ActionKey, row.AllowedRoleKey }).IsUnique();
        });

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.ToTable("SlaPolicy");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.SlaPolicyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.SlaPolicyKey }).IsUnique();
        });

        modelBuilder.Entity<NotificationRoute>(entity =>
        {
            entity.ToTable("NotificationRoute");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.RouteKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.NotificationType).HasMaxLength(100).IsRequired();
            entity.Property(row => row.RecipientRoleKey).HasMaxLength(100);
            entity.Property(row => row.RecipientUserId).HasMaxLength(200);
            entity.Property(row => row.SubjectTemplate).HasMaxLength(300).IsRequired();
            entity.Property(row => row.BodyTemplate).IsRequired();
            entity.Property(row => row.OverdueSubjectPrefix).HasMaxLength(100).IsRequired();
            entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.RouteKey }).IsUnique();
        });
    }

    private static void ConfigureRuntime(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("WorkflowInstance");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WorkflowInstanceId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.WorkflowKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.BusinessObjectType).HasMaxLength(100).IsRequired();
            entity.Property(row => row.BusinessObjectId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.CurrentStateKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(30).IsRequired();
            entity.Property(row => row.StartedByUserId).HasMaxLength(200).IsRequired();
            entity.HasAlternateKey(row => row.WorkflowInstanceId);
            entity.HasIndex(row => new { row.WorkflowKey, row.BusinessObjectType, row.BusinessObjectId }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("CK_WorkflowInstance_Status", "[Status] IN ('Active', 'Completed')"));
        });

        modelBuilder.Entity<WorkflowTask>(entity =>
        {
            entity.ToTable("WorkflowTask");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WorkflowInstanceId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.TaskKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.AssignedRoleKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.AssignedUserId).HasMaxLength(200);
            entity.Property(row => row.Status).HasMaxLength(30).IsRequired();
            entity.Property(row => row.CompletedByUserId).HasMaxLength(200);
            entity.Property(row => row.ClosedByActionKey).HasMaxLength(100);
            entity.Property(row => row.NotificationRouteKey).HasMaxLength(100);
            entity.Property(row => row.NotificationContextJson);
            entity.HasIndex(row => row.WorkflowInstanceId)
                .IsUnique()
                .HasFilter("[Status] = 'Pending'");
            entity.HasOne<WorkflowInstance>()
                .WithMany()
                .HasForeignKey(row => row.WorkflowInstanceId)
                .HasPrincipalKey(row => row.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("CK_WorkflowTask_Status", "[Status] IN ('Pending', 'Completed')"));
        });

        modelBuilder.Entity<MockSectionData>(entity =>
        {
            entity.ToTable("MockSectionData");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.BusinessObjectType).HasMaxLength(100).IsRequired();
            entity.Property(row => row.BusinessObjectId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.SectionKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.PayloadJson).IsRequired();
            entity.Property(row => row.SavedByUserId).HasMaxLength(200).IsRequired();
            entity.Property(row => row.CorrelationId).HasMaxLength(150).IsRequired();
            entity.HasIndex(row => new { row.BusinessObjectType, row.BusinessObjectId, row.SectionKey, row.SavedAtUtc, row.Id });
        });

        modelBuilder.Entity<WorkflowAudit>(entity =>
        {
            entity.ToTable("WorkflowAudit");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WorkflowInstanceId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.BusinessObjectType).HasMaxLength(100).IsRequired();
            entity.Property(row => row.BusinessObjectId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.ActionKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.FromStateKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.ToStateKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.PerformedByUserId).HasMaxLength(200).IsRequired();
            entity.Property(row => row.PerformedByRoleKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.Reason).HasMaxLength(1000);
            entity.Property(row => row.CorrelationId).HasMaxLength(150).IsRequired();
            entity.HasIndex(row => row.WorkflowInstanceId);
            entity.HasOne<WorkflowInstance>()
                .WithMany()
                .HasForeignKey(row => row.WorkflowInstanceId)
                .HasPrincipalKey(row => row.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationOutbox>(entity =>
        {
            entity.ToTable("NotificationOutbox");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WorkflowInstanceId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.BusinessObjectType).HasMaxLength(100).IsRequired();
            entity.Property(row => row.BusinessObjectId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.NotificationType).HasMaxLength(100).IsRequired();
            entity.Property(row => row.RecipientRoleKey).HasMaxLength(100);
            entity.Property(row => row.RecipientUserId).HasMaxLength(200);
            entity.Property(row => row.Subject).HasMaxLength(300).IsRequired();
            entity.Property(row => row.Body).IsRequired();
            entity.Property(row => row.PayloadJson).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(30).IsRequired();
            entity.Property(row => row.CorrelationId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.ProcessingToken).HasMaxLength(100);
            entity.Property(row => row.LastError).HasMaxLength(1000);
            entity.HasIndex(row => new { row.WorkflowTaskId, row.NotificationType })
                .IsUnique()
                .HasFilter("[NotificationType] = 'SlaTaskOverdue'");
            entity.HasOne<WorkflowInstance>()
                .WithMany()
                .HasForeignKey(row => row.WorkflowInstanceId)
                .HasPrincipalKey(row => row.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("CK_NotificationOutbox_Status", "[Status] IN ('Pending', 'Processing', 'Sent')"));
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecord");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(row => row.WorkflowInstanceId).HasMaxLength(150).IsRequired();
            entity.Property(row => row.ActionKey).HasMaxLength(100).IsRequired();
            entity.Property(row => row.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(row => row.ResultHash).HasMaxLength(128).IsRequired();
            entity.Property(row => row.ResultJson).IsRequired();
            entity.HasIndex(row => row.IdempotencyKey).IsUnique();
            entity.HasOne<WorkflowInstance>()
                .WithMany()
                .HasForeignKey(row => row.WorkflowInstanceId)
                .HasPrincipalKey(row => row.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
