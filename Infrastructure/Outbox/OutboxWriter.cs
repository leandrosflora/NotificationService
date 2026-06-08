using NotificationService.Application.Ports;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly NotificationDbContext _dbContext;

    public OutboxWriter(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(string topic, string aggregateKey, object message, CancellationToken cancellationToken)
    {
        await _dbContext.OutboxMessages.AddAsync(
            OutboxMessage.Create(topic, aggregateKey, message),
            cancellationToken);
    }
}
