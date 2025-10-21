using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Listo.Notification.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for the Notification service with tenant scoping and query filters.
/// </summary>
public class NotificationDbContext : DbContext
{
    private readonly Guid? _tenantId;

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Constructor that accepts tenant ID for scoping queries
    /// </summary>
    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options,
        Guid? tenantId)
        : base(options)
    {
        _tenantId = tenantId;
    }

    // Tenant-Scoped DbSets
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<PreferenceEntity> Preferences => Set<PreferenceEntity>();
    public DbSet<CostTrackingEntity> CostTracking => Set<CostTrackingEntity>();

    // Global DbSets (not tenant-scoped)
    public DbSet<NotificationQueueEntity> NotificationQueue => Set<NotificationQueueEntity>();
    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<RetryPolicyEntity> RetryPolicies => Set<RetryPolicyEntity>();
    public DbSet<RateLimitingEntity> RateLimiting => Set<RateLimitingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Notifications table
        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ServiceOrigin).HasConversion<string>();
            entity.Property(e => e.Channel).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Priority).HasConversion<string>();

            // Tenant scoping index
            entity.HasIndex(e => new { e.TenantId, e.UserId, e.CreatedAt })
                .HasDatabaseName("IX_Notifications_TenantId_UserId_CreatedAt");

            entity.HasIndex(e => new { e.TenantId, e.ServiceOrigin, e.CreatedAt })
                .HasDatabaseName("IX_Notifications_TenantId_ServiceOrigin_CreatedAt");

            entity.HasIndex(e => e.CorrelationId)
                .HasDatabaseName("IX_Notifications_CorrelationId");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_Notifications_TenantId");

            // Global query filter for tenant isolation
            entity.HasQueryFilter(e => _tenantId == null || e.TenantId == _tenantId);
        });

        // Configure NotificationQueue table
        modelBuilder.Entity<NotificationQueueEntity>(entity =>
        {
            entity.ToTable("NotificationQueue");
            entity.HasKey(e => e.QueueId);

            entity.Property(e => e.ServiceOrigin).HasConversion<string>();
            entity.Property(e => e.Channel).HasConversion<string>();

            entity.HasIndex(e => new { e.ScheduledAt, e.Attempts })
                .HasDatabaseName("IX_NotificationQueue_ScheduledAt_Attempts")
                .HasFilter("NextAttemptAt IS NOT NULL");

            entity.HasIndex(e => e.EmailHash)
                .HasDatabaseName("IX_NotificationQueue_EmailHash")
                .HasFilter("EmailHash IS NOT NULL");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_NotificationQueue_CreatedAt");
        });

        // Configure Templates table
        modelBuilder.Entity<TemplateEntity>(entity =>
        {
            entity.ToTable("Templates");
            entity.HasKey(e => e.TemplateId);

            entity.Property(e => e.Channel).HasConversion<string>();

            entity.HasIndex(e => new { e.TemplateKey, e.Channel, e.Locale })
                .HasDatabaseName("IX_Templates_TemplateKey_Channel_Locale")
                .HasFilter("IsActive = 1");

            // Unique constraint
            entity.HasIndex(e => new { e.TemplateKey, e.Channel, e.Locale, e.Version })
                .IsUnique()
                .HasDatabaseName("UX_Templates_TemplateKey_Channel_Locale_Version");
        });

        // Configure Preferences table
        modelBuilder.Entity<PreferenceEntity>(entity =>
        {
            entity.ToTable("Preferences");
            entity.HasKey(e => e.PreferenceId);

            entity.Property(e => e.Channel).HasConversion<string>();

            entity.HasIndex(e => new { e.TenantId, e.UserId })
                .HasDatabaseName("IX_Preferences_TenantId_UserId");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_Preferences_TenantId");

            // Unique constraint
            entity.HasIndex(e => new { e.TenantId, e.UserId, e.Channel })
                .IsUnique()
                .HasDatabaseName("UX_Preferences_TenantId_UserId_Channel");

            // Global query filter for tenant isolation
            entity.HasQueryFilter(e => _tenantId == null || e.TenantId == _tenantId);
        });

        // Configure CostTracking table
        modelBuilder.Entity<CostTrackingEntity>(entity =>
        {
            entity.ToTable("CostTracking");
            entity.HasKey(e => e.CostId);

            entity.Property(e => e.ServiceOrigin).HasConversion<string>();
            entity.Property(e => e.Channel).HasConversion<string>();

            entity.HasIndex(e => new { e.TenantId, e.ServiceOrigin, e.OccurredAt })
                .HasDatabaseName("IX_CostTracking_TenantId_ServiceOrigin_OccurredAt");

            entity.HasIndex(e => new { e.TenantId, e.Channel, e.OccurredAt })
                .HasDatabaseName("IX_CostTracking_TenantId_Channel_OccurredAt");

            entity.HasIndex(e => new { e.ServiceOrigin, e.OccurredAt })
                .HasDatabaseName("IX_CostTracking_ServiceOrigin_OccurredAt");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_CostTracking_TenantId");

            // Global query filter for tenant isolation
            entity.HasQueryFilter(e => _tenantId == null || e.TenantId == _tenantId);
        });

        // Configure Devices table
        modelBuilder.Entity<DeviceEntity>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(e => e.DeviceId);

            entity.Property(e => e.DeviceToken).HasMaxLength(512).IsRequired();
            entity.Property(e => e.DeviceInfo).HasMaxLength(1024);
            entity.Property(e => e.AppVersion).HasMaxLength(50);
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();

            entity.HasIndex(e => new { e.TenantId, e.UserId, e.Active })
                .HasDatabaseName("IX_Devices_TenantId_UserId_Active");

            entity.HasIndex(e => new { e.TenantId, e.Platform, e.Active })
                .HasDatabaseName("IX_Devices_TenantId_Platform_Active");

            entity.HasIndex(e => e.LastSeen)
                .HasDatabaseName("IX_Devices_LastSeen");

            // Unique constraint on device token (globally unique per requirements)
            entity.HasIndex(e => e.DeviceToken)
                .IsUnique()
                .HasDatabaseName("UX_Devices_DeviceToken");

            // Global query filter for tenant isolation
            entity.HasQueryFilter(e => _tenantId == null || e.TenantId == _tenantId);
        });

        // Configure AuditLog table
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(e => e.AuditId);

            entity.Property(e => e.ServiceOrigin).HasConversion<string?>();
            entity.Property(e => e.ActorType).HasConversion<string>();

            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt })
                .HasDatabaseName("IX_AuditLog_EntityType_EntityId_OccurredAt");

            entity.HasIndex(e => new { e.UserId, e.OccurredAt })
                .HasDatabaseName("IX_AuditLog_UserId_OccurredAt")
                .HasFilter("UserId IS NOT NULL");

            entity.HasIndex(e => e.OccurredAt)
                .HasDatabaseName("IX_AuditLog_OccurredAt");
        });

        // Configure Conversations table
        modelBuilder.Entity<ConversationEntity>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(e => e.ConversationId);

            entity.Property(e => e.ServiceOrigin).HasConversion<string>();

            entity.HasIndex(e => e.LastMessageAt)
                .HasDatabaseName("IX_Conversations_LastMessageAt");

            // Navigation property
            entity.HasMany(e => e.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Messages table
        modelBuilder.Entity<MessageEntity>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(e => e.MessageId);

            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasIndex(e => new { e.ConversationId, e.SentAt })
                .HasDatabaseName("IX_Messages_ConversationId_SentAt");

            entity.HasIndex(e => new { e.RecipientUserId, e.Status })
                .HasDatabaseName("IX_Messages_RecipientUserId_Status")
                .HasFilter("RecipientUserId IS NOT NULL");
        });

        // Configure RetryPolicy table
        modelBuilder.Entity<RetryPolicyEntity>(entity =>
        {
            entity.ToTable("RetryPolicy");
            entity.HasKey(e => e.PolicyId);

            // Unique constraint on ServiceOrigin and Channel
            entity.HasIndex(e => new { e.ServiceOrigin, e.Channel })
                .IsUnique()
                .HasDatabaseName("UX_RetryPolicy_ServiceOrigin_Channel");
        });

        // Configure RateLimiting table
        modelBuilder.Entity<RateLimitingEntity>(entity =>
        {
            entity.ToTable("RateLimiting");
            entity.HasKey(e => e.ConfigId);

            // Unique constraint on TenantId, ServiceOrigin, and Channel
            entity.HasIndex(e => new { e.TenantId, e.ServiceOrigin, e.Channel })
                .IsUnique()
                .HasDatabaseName("UX_RateLimiting_TenantId_ServiceOrigin_Channel");
        });

        // Configure ConversationType and DevicePlatform enums
        modelBuilder.Entity<ConversationEntity>()
            .Property(e => e.Type)
            .HasConversion<string>();

        modelBuilder.Entity<DeviceEntity>()
            .Property(e => e.Platform)
            .HasConversion<string>();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Automatically set timestamps on save
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is NotificationEntity notification)
                {
                    notification.CreatedAt = DateTime.UtcNow;
                    notification.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is NotificationQueueEntity queue)
                {
                    queue.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is TemplateEntity template)
                {
                    template.CreatedAt = DateTime.UtcNow;
                    template.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is DeviceEntity device)
                {
                    device.CreatedAt = DateTime.UtcNow;
                    device.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is ConversationEntity conversation)
                {
                    conversation.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is MessageEntity message)
                {
                    message.SentAt = DateTime.UtcNow;
                }
                else if (entry.Entity is CostTrackingEntity cost)
                {
                    cost.OccurredAt = DateTime.UtcNow;
                }
                else if (entry.Entity is AuditLogEntity audit)
                {
                    audit.OccurredAt = DateTime.UtcNow;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is NotificationEntity notification)
                {
                    notification.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is TemplateEntity template)
                {
                    template.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is DeviceEntity device)
                {
                    device.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is PreferenceEntity preference)
                {
                    preference.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
