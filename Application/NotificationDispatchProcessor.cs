using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Ports;
using NotificationService.Contracts;
using NotificationService.Domain;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Providers;

namespace NotificationService.Application;

public sealed class NotificationDispatchProcessor
{
    private const int MaximumAttempts = 8;

    private readonly NotificationDbContext _dbContext;
    private readonly NotificationSenderFactory _senderFactory;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<NotificationDispatchProcessor> _logger;

    public NotificationDispatchProcessor(NotificationDbContext dbContext, NotificationSenderFactory senderFactory, IOutboxWriter outbox, ILogger<NotificationDispatchProcessor> logger)
    {
        _dbContext = dbContext;
        _senderFactory = senderFactory;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        var delivery = await _dbContext.NotificationDeliveries.SingleOrDefaultAsync(x => x.Id == deliveryId, cancellationToken);

        if (delivery is null || delivery.Status != DeliveryStatus.Sending)
        {
            return;
        }

        var sender = _senderFactory.GetRequired(delivery.Channel);

        try
        {
            var result = await sender.SendAsync(
                new ProviderSendRequest(delivery.Id, delivery.Destination, delivery.Subject, delivery.Body),
                cancellationToken);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            delivery.MarkAccepted(result.ProviderMessageId, result.AcceptedAt);

            await _outbox.AddAsync(
                topic: "notification.events",
                aggregateKey: delivery.NotificationId.ToString(),
                message: new NotificationAcceptedIntegrationEvent(Guid.NewGuid(), delivery.NotificationId, delivery.Id, delivery.Channel, result.ProviderMessageId, result.AcceptedAt),
                cancellationToken);

            await RefreshNotificationAsync(delivery.NotificationId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PermanentProviderException exception)
        {
            await MarkFailedAsync(delivery, exception.Message, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Notification delivery {DeliveryId} failed", delivery.Id);

            if (delivery.Attempts >= MaximumAttempts)
            {
                await MarkFailedAsync(delivery, "Maximum attempts exceeded", cancellationToken);
                return;
            }

            delivery.ScheduleRetry(exception.Message, DateTimeOffset.UtcNow.Add(CalculateBackoff(delivery.Attempts)));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task MarkFailedAsync(NotificationDelivery delivery, string error, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        delivery.MarkFailed(error);

        await _outbox.AddAsync(
            "notification.events",
            delivery.NotificationId.ToString(),
            new NotificationDeliveryFailedIntegrationEvent(Guid.NewGuid(), delivery.NotificationId, delivery.Id, delivery.Channel, error, DateTimeOffset.UtcNow),
            cancellationToken);

        await RefreshNotificationAsync(delivery.NotificationId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RefreshNotificationAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await _dbContext.Notifications
            .Include(x => x.Deliveries)
            .SingleAsync(x => x.Id == notificationId, cancellationToken);

        notification.RefreshStatus();
    }

    private static TimeSpan CalculateBackoff(int attempt)
    {
        var seconds = Math.Min(300, Math.Pow(2, attempt));
        var jitter = Random.Shared.Next(0, 1000);
        return TimeSpan.FromSeconds(seconds).Add(TimeSpan.FromMilliseconds(jitter));
    }
}
