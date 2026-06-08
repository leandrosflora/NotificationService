using Microsoft.EntityFrameworkCore;
using NotificationService.Domain;
using NotificationService.Infrastructure.Inbox;
using NotificationService.Infrastructure.Outbox;

namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<RecipientContact> RecipientContacts => Set<RecipientContact>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SourceEventId).IsUnique();
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.SourceEventId).HasColumnName("source_event_id");
            entity.Property(x => x.RecipientId).HasColumnName("recipient_id");
            entity.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(80);
            entity.Property(x => x.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasMany(x => x.Deliveries).WithOne().HasForeignKey(x => x.NotificationId);
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("notification_deliveries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.NotificationId).HasColumnName("notification_id");
            entity.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Destination).HasColumnName("destination");
            entity.Property(x => x.TemplateId).HasColumnName("template_id");
            entity.Property(x => x.TemplateVersion).HasColumnName("template_version");
            entity.Property(x => x.Subject).HasColumnName("subject");
            entity.Property(x => x.Body).HasColumnName("body");
            entity.Property(x => x.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(300);
            entity.Property(x => x.Attempts).HasColumnName("attempts");
            entity.Property(x => x.NotBefore).HasColumnName("not_before");
            entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(x => x.ProcessingToken).HasColumnName("processing_token");
            entity.Property(x => x.ProcessingLeaseUntil).HasColumnName("processing_lease_until");
            entity.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
            entity.HasIndex(x => new { x.Status, x.NotBefore, x.NextAttemptAt, x.ProcessingLeaseUntil });
            entity.HasIndex(x => x.ProviderMessageId);
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("notification_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(80);
            entity.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(20);
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.SubjectTemplate).HasColumnName("subject_template");
            entity.Property(x => x.BodyTemplate).HasColumnName("body_template");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => new { x.Type, x.Channel, x.Locale, x.Version }).IsUnique();
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("notification_preferences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.RecipientId).HasColumnName("recipient_id");
            entity.Property(x => x.NotificationType).HasColumnName("notification_type").HasConversion<string>().HasMaxLength(80);
            entity.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Enabled).HasColumnName("enabled");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => new { x.RecipientId, x.NotificationType, x.Channel }).IsUnique();
        });

        modelBuilder.Entity<RecipientContact>(entity =>
        {
            entity.ToTable("recipient_contacts");
            entity.HasKey(x => x.RecipientId);
            entity.Property(x => x.RecipientId).HasColumnName("recipient_id");
            entity.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(20);
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            entity.Property(x => x.PushToken).HasColumnName("push_token");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(x => x.MessageId);
            entity.Property(x => x.MessageId).HasColumnName("message_id");
            entity.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(200);
            entity.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(200);
            entity.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(200);
            entity.Property(x => x.AggregateKey).HasColumnName("aggregate_key").HasMaxLength(100);
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            entity.Property(x => x.Attempts).HasColumnName("attempts");
            entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(x => x.LastError).HasColumnName("last_error");
            entity.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt, x.CreatedAt });
        });
    }
}
