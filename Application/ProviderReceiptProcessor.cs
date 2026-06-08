using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Ports;
using NotificationService.Contracts;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application;

public sealed class ProviderReceiptProcessor
{
    private readonly NotificationDbContext _dbContext;
    private readonly IOutboxWriter _outbox;

    public ProviderReceiptProcessor(NotificationDbContext dbContext, IOutboxWriter outbox)
    {
        _dbContext = dbContext;
        _outbox = outbox;
    }

    public async Task ProcessAsync(string provider, ProviderDeliveryReceipt receipt, CancellationToken cancellationToken)
    {
        var delivery = await _dbContext.NotificationDeliveries
            .SingleOrDefaultAsync(x => x.ProviderMessageId == receipt.ProviderMessageId, cancellationToken);

        if (delivery is null)
        {
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (receipt.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
        {
            delivery.MarkDelivered(receipt.OccurredAt);
            await _outbox.AddAsync(
                "notification.events",
                delivery.NotificationId.ToString(),
                new NotificationDeliveredIntegrationEvent(Guid.NewGuid(), delivery.NotificationId, delivery.Id, delivery.Channel, receipt.OccurredAt),
                cancellationToken);
        }
        else if (receipt.Status.Equals("Bounced", StringComparison.OrdinalIgnoreCase) || receipt.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
        {
            delivery.MarkBounced($"Provider {provider} reported {receipt.Status}");
        }

        var notification = await _dbContext.Notifications
            .Include(x => x.Deliveries)
            .SingleAsync(x => x.Id == delivery.NotificationId, cancellationToken);
        notification.RefreshStatus();

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
