using NotificationService.Domain;

namespace NotificationService.Application;

public sealed record NotificationPolicy(
    NotificationType Type,
    IReadOnlyList<NotificationChannel> DefaultChannels,
    bool CanUserOptOut,
    NotificationPriority Priority);

public sealed class NotificationPolicyCatalog
{
    private readonly IReadOnlyDictionary<NotificationType, NotificationPolicy> _policies;

    public NotificationPolicyCatalog()
    {
        _policies = new Dictionary<NotificationType, NotificationPolicy>
        {
            [NotificationType.OrderCreated] = new(NotificationType.OrderCreated, [NotificationChannel.Email, NotificationChannel.Push], CanUserOptOut: false, NotificationPriority.High),
            [NotificationType.OrderConfirmed] = new(NotificationType.OrderConfirmed, [NotificationChannel.Email, NotificationChannel.Push], CanUserOptOut: false, NotificationPriority.High),
            [NotificationType.OutForDelivery] = new(NotificationType.OutForDelivery, [NotificationChannel.Push, NotificationChannel.Email], CanUserOptOut: true, NotificationPriority.High),
            [NotificationType.Delivered] = new(NotificationType.Delivered, [NotificationChannel.Push, NotificationChannel.Email], CanUserOptOut: true, NotificationPriority.Normal),
            [NotificationType.DeliveryException] = new(NotificationType.DeliveryException, [NotificationChannel.Push, NotificationChannel.Email, NotificationChannel.Sms], CanUserOptOut: false, NotificationPriority.Critical),
            [NotificationType.PaymentFailed] = new(NotificationType.PaymentFailed, [NotificationChannel.Email, NotificationChannel.Push], CanUserOptOut: false, NotificationPriority.Critical),
            [NotificationType.ShipmentCreated] = new(NotificationType.ShipmentCreated, [NotificationChannel.Email, NotificationChannel.Push], CanUserOptOut: true, NotificationPriority.Normal),
            [NotificationType.ShipmentCancelled] = new(NotificationType.ShipmentCancelled, [NotificationChannel.Email, NotificationChannel.Push], CanUserOptOut: false, NotificationPriority.High),
            [NotificationType.OrderCancelled] = new(NotificationType.OrderCancelled, [NotificationChannel.Email, NotificationChannel.Push], CanUserOptOut: false, NotificationPriority.High)
        };
    }

    public NotificationPolicy Get(NotificationType type)
    {
        return _policies.TryGetValue(type, out var policy)
            ? policy
            : throw new InvalidOperationException($"No notification policy configured for {type}");
    }
}
