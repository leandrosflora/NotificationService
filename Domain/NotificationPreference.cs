namespace NotificationService.Domain;

public sealed class NotificationPreference
{
    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public NotificationType NotificationType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private NotificationPreference()
    {
    }

    public static NotificationPreference Create(Guid recipientId, NotificationType notificationType, NotificationChannel channel, bool enabled)
    {
        return new NotificationPreference
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            NotificationType = notificationType,
            Channel = channel,
            Enabled = enabled,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Change(bool enabled)
    {
        Enabled = enabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
