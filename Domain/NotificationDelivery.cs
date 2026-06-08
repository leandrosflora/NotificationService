namespace NotificationService.Domain;

public sealed class NotificationDelivery
{
    public Guid Id { get; private set; }
    public Guid NotificationId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public string Destination { get; private set; } = default!;
    public Guid TemplateId { get; private set; }
    public int TemplateVersion { get; private set; }
    public string? Subject { get; private set; }
    public string Body { get; private set; } = default!;
    public string? ProviderMessageId { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NotBefore { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public Guid? ProcessingToken { get; private set; }
    public DateTimeOffset? ProcessingLeaseUntil { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }

    private NotificationDelivery()
    {
    }

    public static NotificationDelivery Create(Guid notificationId, NotificationChannel channel, string destination, Guid templateId, int templateVersion, string? subject, string body, DateTimeOffset notBefore)
    {
        return new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            Channel = channel,
            Destination = destination,
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            Subject = subject,
            Body = body,
            Status = DeliveryStatus.Pending,
            NotBefore = notBefore,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkAccepted(string providerMessageId, DateTimeOffset acceptedAt)
    {
        Status = DeliveryStatus.Accepted;
        ProviderMessageId = providerMessageId;
        AcceptedAt = acceptedAt;
        ProcessingToken = null;
        ProcessingLeaseUntil = null;
        NextAttemptAt = null;
        LastError = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ScheduleRetry(string error, DateTimeOffset nextAttemptAt)
    {
        Status = DeliveryStatus.RetryScheduled;
        LastError = Limit(error);
        NextAttemptAt = nextAttemptAt;
        ProcessingToken = null;
        ProcessingLeaseUntil = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = DeliveryStatus.Failed;
        LastError = Limit(error);
        ProcessingToken = null;
        ProcessingLeaseUntil = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDelivered(DateTimeOffset deliveredAt)
    {
        Status = DeliveryStatus.Delivered;
        DeliveredAt = deliveredAt;
        ProcessingToken = null;
        ProcessingLeaseUntil = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkBounced(string reason)
    {
        Status = DeliveryStatus.Bounced;
        LastError = Limit(reason);
        ProcessingToken = null;
        ProcessingLeaseUntil = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string Limit(string value) => value.Length <= 1000 ? value : value[..1000];
}
