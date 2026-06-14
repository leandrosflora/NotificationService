namespace NotificationService.Contracts;

public sealed record KafkaEventEnvelope<TPayload>(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string Producer,
    TPayload Payload);

public sealed record OrderCreatedPayload(
    Guid OrderId,
    Guid BuyerId);

public sealed record ShipmentCreatedPayload(
    Guid ShipmentId,
    Guid BuyerId,
    string? TrackingCode,
    DateOnly? EstimatedDeliveryDate);

public sealed record ShipmentStatusUpdatedPayload(
    Guid ShipmentId,
    Guid BuyerId,
    string? TrackingCode,
    string CurrentStatus,
    DateOnly? EstimatedDeliveryDate,
    string? ExceptionCode);
