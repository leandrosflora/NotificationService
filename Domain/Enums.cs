namespace NotificationService.Domain;

public enum NotificationType
{
    OrderCreated = 1,
    OrderConfirmed = 2,
    ShipmentCreated = 3,
    OutForDelivery = 4,
    Delivered = 5,
    DeliveryException = 6,
    OrderCancelled = 7,
    PaymentFailed = 8,
    ShipmentCancelled = 9
}

public enum NotificationChannel
{
    Email = 1,
    Sms = 2,
    Push = 3
}

public enum NotificationPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

public enum NotificationStatus
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    PartiallySent = 4,
    Suppressed = 5,
    Failed = 6
}

public enum DeliveryStatus
{
    Pending = 1,
    Sending = 2,
    RetryScheduled = 3,
    Accepted = 4,
    Delivered = 5,
    Bounced = 6,
    Suppressed = 7,
    Failed = 8
}
