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
    Guid OrderId,
    Guid BuyerId,
    string? TrackingCode,
    DateOnly? EstimatedDeliveryDate,
    DateTimeOffset CreatedAt);

public sealed record ShipmentStatusUpdatedPayload(
    Guid ShipmentId,
    Guid OrderId,
    Guid BuyerId,
    string? TrackingCode,
    string? CarrierCode,
    string? PreviousStatus,
    string CurrentStatus,
    DateTimeOffset StatusDate,
    DateOnly? EstimatedDeliveryDate,
    string? ExceptionCode);
