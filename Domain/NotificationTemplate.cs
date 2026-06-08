namespace NotificationService.Domain;

public sealed class NotificationTemplate
{
    public Guid Id { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Locale { get; private set; } = default!;
    public int Version { get; private set; }
    public string? SubjectTemplate { get; private set; }
    public string BodyTemplate { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private NotificationTemplate()
    {
    }

    public static NotificationTemplate Create(NotificationType type, NotificationChannel channel, string locale, int version, string? subjectTemplate, string bodyTemplate, bool isActive = true)
    {
        return new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            Type = type,
            Channel = channel,
            Locale = locale,
            Version = version,
            SubjectTemplate = subjectTemplate,
            BodyTemplate = bodyTemplate,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
