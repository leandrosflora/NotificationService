namespace NotificationService.Application.Ports;

public interface IOutboxWriter
{
    Task AddAsync(string topic, string aggregateKey, object message, CancellationToken cancellationToken);
}
