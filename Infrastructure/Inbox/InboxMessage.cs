namespace NotificationService.Infrastructure.Inbox;

public sealed class InboxMessage
{
    public Guid MessageId { get; private set; }
    public string MessageType { get; private set; } = default!;
    public DateTimeOffset ProcessedAt { get; private set; }

    private InboxMessage()
    {
    }

    public InboxMessage(Guid messageId, string messageType)
    {
        MessageId = messageId;
        MessageType = messageType;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
