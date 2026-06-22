namespace NotificationService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ConsumerGroupId { get; set; } = "notification-service";
    public KafkaTopicOptions Topics { get; set; } = new();
}

public sealed class KafkaTopicOptions
{
    public string OrderCreated { get; set; } = "order.created";
    public string OrderConfirmed { get; set; } = "order.confirmed";
    public string OrderCancelled { get; set; } = "order.cancelled";
    public string PaymentRejected { get; set; } = "payment.rejected";
    public string ShipmentCreated { get; set; } = "shipment.created";
    public string ShipmentStatusUpdated { get; set; } = "shipment.status.updated";
    public string ShipmentCancelled { get; set; } = "shipment.cancelled";
}
