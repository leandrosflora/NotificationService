using System.Text.Json;

namespace NotificationService.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = default!;
    public string MessageType { get; private set; } = default!;
    public string AggregateKey { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(string topic, string aggregateKey, object message)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            MessageType = message.GetType().Name,
            AggregateKey = aggregateKey,
            Payload = JsonSerializer.Serialize(message, message.GetType()),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void ScheduleRetry(string error, DateTimeOffset nextAttemptAt)
    {
        Attempts++;
        LastError = error.Length <= 1000 ? error : error[..1000];
        NextAttemptAt = nextAttemptAt;
    }
}
