namespace NotificationService.Domain;

public enum NotificationType
{
    OrderConfirmed = 1,
    ShipmentCreated = 2,
    OutForDelivery = 3,
    Delivered = 4,
    DeliveryException = 5,
    OrderCancelled = 6,
    PaymentFailed = 7
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
