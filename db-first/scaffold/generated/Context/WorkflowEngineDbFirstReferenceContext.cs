using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.DatabaseFirst.Generated.Entities;

namespace WorkflowEngine.DatabaseFirst.Generated.Context;

public partial class WorkflowEngineDbFirstReferenceContext : DbContext
{
    public WorkflowEngineDbFirstReferenceContext(DbContextOptions<WorkflowEngineDbFirstReferenceContext> options)
        : base(options)
    {
    }

    public virtual DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    public virtual DbSet<MockSectionDatum> MockSectionData { get; set; }

    public virtual DbSet<NotificationOutbox> NotificationOutboxes { get; set; }

    public virtual DbSet<NotificationRoute> NotificationRoutes { get; set; }

    public virtual DbSet<SlaPolicy> SlaPolicies { get; set; }

    public virtual DbSet<WorkflowActionDefinition> WorkflowActionDefinitions { get; set; }

    public virtual DbSet<WorkflowAudit> WorkflowAudits { get; set; }

    public virtual DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }

    public virtual DbSet<WorkflowDefinitionVersion> WorkflowDefinitionVersions { get; set; }

    public virtual DbSet<WorkflowInstance> WorkflowInstances { get; set; }

    public virtual DbSet<WorkflowRoleDefinition> WorkflowRoleDefinitions { get; set; }

    public virtual DbSet<WorkflowStateDefinition> WorkflowStateDefinitions { get; set; }

    public virtual DbSet<WorkflowTask> WorkflowTasks { get; set; }

    public virtual DbSet<WorkflowTaskDefinition> WorkflowTaskDefinitions { get; set; }

    public virtual DbSet<WorkflowTransitionDefinition> WorkflowTransitionDefinitions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecord");

            entity.HasIndex(e => e.IdempotencyKey, "IX_IdempotencyRecord_IdempotencyKey").IsUnique();

            entity.Property(e => e.ActionKey).HasMaxLength(100);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.RequestHash).HasMaxLength(128);
            entity.Property(e => e.ResultHash).HasMaxLength(128);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(150);

            entity.HasOne(d => d.WorkflowInstance).WithMany(p => p.IdempotencyRecords)
                .HasPrincipalKey(p => p.WorkflowInstanceId)
                .HasForeignKey(d => d.WorkflowInstanceId);
        });

        modelBuilder.Entity<MockSectionDatum>(entity =>
        {
            entity.HasIndex(e => new { e.BusinessObjectType, e.BusinessObjectId, e.SectionKey, e.SavedAtUtc, e.Id }, "IX_MockSectionData_BusinessObjectType_BusinessObjectId_SectionKey_SavedAtUtc_Id");

            entity.Property(e => e.BusinessObjectId).HasMaxLength(150);
            entity.Property(e => e.BusinessObjectType).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(150);
            entity.Property(e => e.SavedByUserId).HasMaxLength(200);
            entity.Property(e => e.SectionKey).HasMaxLength(100);
        });

        modelBuilder.Entity<NotificationOutbox>(entity =>
        {
            entity.ToTable("NotificationOutbox");

            entity.HasIndex(e => new { e.WorkflowTaskId, e.NotificationType }, "IX_NotificationOutbox_WorkflowTaskId_NotificationType")
                .IsUnique()
                .HasFilter("([NotificationType]='SlaTaskOverdue')");

            entity.Property(e => e.BusinessObjectId).HasMaxLength(150);
            entity.Property(e => e.BusinessObjectType).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(150);
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.Property(e => e.NotificationType).HasMaxLength(100);
            entity.Property(e => e.ProcessingToken).HasMaxLength(100);
            entity.Property(e => e.RecipientRoleKey).HasMaxLength(100);
            entity.Property(e => e.RecipientUserId).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.Subject).HasMaxLength(300);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(150);

            entity.HasOne(d => d.WorkflowInstance).WithMany(p => p.NotificationOutboxes)
                .HasPrincipalKey(p => p.WorkflowInstanceId)
                .HasForeignKey(d => d.WorkflowInstanceId);
        });

        modelBuilder.Entity<NotificationRoute>(entity =>
        {
            entity.ToTable("NotificationRoute");

            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.RouteKey }, "IX_NotificationRoute_WorkflowDefinitionVersionId_RouteKey").IsUnique();

            entity.Property(e => e.NotificationType).HasMaxLength(100);
            entity.Property(e => e.RecipientRoleKey).HasMaxLength(100);
            entity.Property(e => e.RecipientUserId).HasMaxLength(200);
            entity.Property(e => e.RouteKey).HasMaxLength(100);
            entity.Property(e => e.SubjectTemplate).HasMaxLength(300);
        });

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.ToTable("SlaPolicy");

            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.SlaPolicyKey }, "IX_SlaPolicy_WorkflowDefinitionVersionId_SlaPolicyKey").IsUnique();

            entity.Property(e => e.SlaPolicyKey).HasMaxLength(100);
        });

        modelBuilder.Entity<WorkflowActionDefinition>(entity =>
        {
            entity.ToTable("WorkflowActionDefinition");

            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.ActionKey }, "IX_WorkflowActionDefinition_WorkflowDefinitionVersionId_ActionKey").IsUnique();

            entity.Property(e => e.ActionKey).HasMaxLength(100);
            entity.Property(e => e.ButtonKey).HasMaxLength(100);
            entity.Property(e => e.ButtonLabel).HasMaxLength(200);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Intent).HasMaxLength(100);
            entity.Property(e => e.SectionKey).HasMaxLength(100);
        });

        modelBuilder.Entity<WorkflowAudit>(entity =>
        {
            entity.ToTable("WorkflowAudit");

            entity.HasIndex(e => e.WorkflowInstanceId, "IX_WorkflowAudit_WorkflowInstanceId");

            entity.Property(e => e.ActionKey).HasMaxLength(100);
            entity.Property(e => e.BusinessObjectId).HasMaxLength(150);
            entity.Property(e => e.BusinessObjectType).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(150);
            entity.Property(e => e.FromStateKey).HasMaxLength(100);
            entity.Property(e => e.PerformedByRoleKey).HasMaxLength(100);
            entity.Property(e => e.PerformedByUserId).HasMaxLength(200);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ToStateKey).HasMaxLength(100);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(150);

            entity.HasOne(d => d.WorkflowInstance).WithMany(p => p.WorkflowAudits)
                .HasPrincipalKey(p => p.WorkflowInstanceId)
                .HasForeignKey(d => d.WorkflowInstanceId);
        });

        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.ToTable("WorkflowDefinition");

            entity.HasIndex(e => e.WorkflowKey, "IX_WorkflowDefinition_WorkflowKey").IsUnique();

            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.WorkflowKey).HasMaxLength(100);
        });

        modelBuilder.Entity<WorkflowDefinitionVersion>(entity =>
        {
            entity.ToTable("WorkflowDefinitionVersion");

            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.VersionNumber }, "IX_WorkflowDefinitionVersion_WorkflowDefinitionId_VersionNumber").IsUnique();

            entity.HasOne(d => d.WorkflowDefinition).WithMany(p => p.WorkflowDefinitionVersions)
                .HasForeignKey(d => d.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("WorkflowInstance");

            entity.HasIndex(e => e.WorkflowInstanceId, "IX_WorkflowInstance_WorkflowInstanceId").IsUnique();

            entity.HasIndex(e => new { e.WorkflowKey, e.BusinessObjectType, e.BusinessObjectId }, "IX_WorkflowInstance_WorkflowKey_BusinessObjectType_BusinessObjectId").IsUnique();

            entity.Property(e => e.BusinessObjectId).HasMaxLength(150);
            entity.Property(e => e.BusinessObjectType).HasMaxLength(100);
            entity.Property(e => e.CurrentStateKey).HasMaxLength(100);
            entity.Property(e => e.StartedByUserId).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(150);
            entity.Property(e => e.WorkflowKey).HasMaxLength(100);
        });

        modelBuilder.Entity<WorkflowRoleDefinition>(entity =>
        {
            entity.ToTable("WorkflowRoleDefinition");

            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.RoleKey }, "IX_WorkflowRoleDefinition_WorkflowDefinitionVersionId_RoleKey").IsUnique();

            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.RoleKey).HasMaxLength(100);
        });

        modelBuilder.Entity<WorkflowStateDefinition>(entity =>
        {
            entity.ToTable("WorkflowStateDefinition");

            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.StateKey }, "IX_WorkflowStateDefinition_WorkflowDefinitionVersionId_StateKey").IsUnique();

            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.StateKey).HasMaxLength(100);
        });

        modelBuilder.Entity<WorkflowTask>(entity =>
        {
            entity.ToTable("WorkflowTask");

            entity.HasIndex(e => e.WorkflowInstanceId, "IX_WorkflowTask_WorkflowInstanceId")
                .IsUnique()
                .HasFilter("([Status]='Pending')");

            entity.Property(e => e.AssignedRoleKey).HasMaxLength(100);
            entity.Property(e => e.AssignedUserId).HasMaxLength(200);
            entity.Property(e => e.ClosedByActionKey).HasMaxLength(100);
            entity.Property(e => e.CompletedByUserId).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.TaskKey).HasMaxLength(100);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(150);

            entity.HasOne(d => d.WorkflowInstance).WithOne(p => p.WorkflowTask)
                .HasPrincipalKey<WorkflowInstance>(p => p.WorkflowInstanceId)
                .HasForeignKey<WorkflowTask>(d => d.WorkflowInstanceId);
        });

        modelBuilder.Entity<WorkflowTaskDefinition>(entity =>
        {
            entity.ToTable("WorkflowTaskDefinition");

            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.TaskKey }, "IX_WorkflowTaskDefinition_WorkflowDefinitionVersionId_TaskKey").IsUnique();

            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.OwnerRoleKey).HasMaxLength(100);
            entity.Property(e => e.SlaPolicyKey).HasMaxLength(100);
            entity.Property(e => e.TaskKey).HasMaxLength(100);
        });

        modelBuilder.Entity<WorkflowTransitionDefinition>(entity =>
        {
            entity.ToTable("WorkflowTransitionDefinition");

            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.FromStateKey, e.ActionKey, e.AllowedRoleKey }, "IX_WorkflowTransitionDefinition_WorkflowDefinitionVersionId_FromStateKey_ActionKey_AllowedRoleKey").IsUnique();

            entity.Property(e => e.ActionKey).HasMaxLength(100);
            entity.Property(e => e.AllowedRoleKey).HasMaxLength(100);
            entity.Property(e => e.AuditLabel).HasMaxLength(200);
            entity.Property(e => e.FromStateKey).HasMaxLength(100);
            entity.Property(e => e.NotificationRouteKey).HasMaxLength(100);
            entity.Property(e => e.TaskToCloseKey).HasMaxLength(100);
            entity.Property(e => e.TaskToCreateKey).HasMaxLength(100);
            entity.Property(e => e.ToStateKey).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
