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
    public string ShipmentCreated { get; set; } = "shipment.created";
    public string ShipmentStatusUpdated { get; set; } = "shipment.status.updated";
}
