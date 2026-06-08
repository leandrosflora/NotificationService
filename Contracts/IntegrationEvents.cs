using NotificationService.Domain;

namespace NotificationService.Contracts;

public sealed record TrackingStatusChangedIntegrationEvent(
    Guid MessageId,
    Guid ShipmentId,
    Guid BuyerId,
    string TrackingCode,
    string CurrentStatus,
    DateTimeOffset OccurredAt,
    DateOnly? EstimatedDeliveryDate,
    string? ExceptionCode);

public sealed record NotificationAcceptedIntegrationEvent(
    Guid MessageId,
    Guid NotificationId,
    Guid DeliveryId,
    NotificationChannel Channel,
    string ProviderMessageId,
    DateTimeOffset AcceptedAt);

public sealed record NotificationDeliveryFailedIntegrationEvent(
    Guid MessageId,
    Guid NotificationId,
    Guid DeliveryId,
    NotificationChannel Channel,
    string Reason,
    DateTimeOffset FailedAt);

public sealed record NotificationDeliveredIntegrationEvent(
    Guid MessageId,
    Guid NotificationId,
    Guid DeliveryId,
    NotificationChannel Channel,
    DateTimeOffset DeliveredAt);
