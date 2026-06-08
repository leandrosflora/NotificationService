namespace NotificationService.Domain;

public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid SourceEventId { get; private set; }
    public Guid RecipientId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string Locale { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<NotificationDelivery> Deliveries { get; private set; } = [];

    private Notification()
    {
    }

    public static Notification Create(Guid sourceEventId, Guid recipientId, NotificationType type, NotificationPriority priority, string locale)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            SourceEventId = sourceEventId,
            RecipientId = recipientId,
            Type = type,
            Priority = priority,
            Locale = locale,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void AddDelivery(NotificationChannel channel, string destination, Guid templateId, int templateVersion, string? subject, string body, DateTimeOffset notBefore)
    {
        Deliveries.Add(NotificationDelivery.Create(Id, channel, destination, templateId, templateVersion, subject, body, notBefore));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSuppressed()
    {
        Status = NotificationStatus.Suppressed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RefreshStatus()
    {
        if (Deliveries.Count == 0)
        {
            Status = NotificationStatus.Suppressed;
            UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        var sent = Deliveries.Count(x => x.Status is DeliveryStatus.Accepted or DeliveryStatus.Delivered);
        var failed = Deliveries.Count(x => x.Status is DeliveryStatus.Failed or DeliveryStatus.Bounced or DeliveryStatus.Suppressed);

        if (sent == Deliveries.Count)
        {
            Status = NotificationStatus.Sent;
        }
        else if (sent > 0 && sent + failed == Deliveries.Count)
        {
            Status = NotificationStatus.PartiallySent;
        }
        else if (failed == Deliveries.Count)
        {
            Status = NotificationStatus.Failed;
        }
        else
        {
            Status = NotificationStatus.Processing;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
