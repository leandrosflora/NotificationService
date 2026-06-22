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
    Guid CheckoutId,
    Guid BuyerId,
    Guid SellerId,
    string ShippingPromiseId,
    string RouteId,
    string CarrierCode,
    string ServiceLevelCode,
    Guid OriginNodeId,
    DateOnly PromisedDeliveryDate,
    AddressPayload Destination,
    IReadOnlyList<PackagePayload> Packages,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed record OrderConfirmedPayload(
    Guid OrderId,
    Guid CheckoutId,
    Guid BuyerId,
    Guid SellerId,
    DateTimeOffset ConfirmedAt);

public sealed record OrderCancelledPayload(
    Guid OrderId,
    Guid CheckoutId,
    Guid BuyerId,
    Guid SellerId,
    string CancellationReason,
    DateTimeOffset CancelledAt);

public sealed record PaymentRejectedPayload(
    Guid OrderId,
    Guid PaymentId,
    Guid BuyerId,
    string RejectionCode,
    DateTimeOffset RejectedAt);

public sealed record ShipmentCreatedPayload(
    Guid ShipmentId,
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    string CarrierCode,
    string ServiceLevelCode,
    string ExternalShipmentId,
    string TrackingCode,
    string LabelObjectKey,
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

public sealed record ShipmentCancelledPayload(
    Guid ShipmentId,
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    string CancellationReason,
    DateTimeOffset CancelledAt);

public sealed record AddressPayload(
    string Street,
    string Number,
    string City,
    string State,
    string ZipCode,
    string Country);

public sealed record PackagePayload(
    string PackageId,
    decimal WeightKg,
    decimal HeightCm,
    decimal WidthCm,
    decimal LengthCm,
    IReadOnlyList<PackageItemPayload> Items);

public sealed record PackageItemPayload(
    Guid SkuId,
    int Quantity);
