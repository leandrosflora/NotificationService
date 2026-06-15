using Xunit;
using NotificationService.Domain;

namespace NotificationService.UnitTests;

public sealed class NotificationDomainTests
{
    [Fact]
    public void RefreshStatus_WithNoDeliveries_SuppressesNotification()
    {
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.Delivered, NotificationPriority.Normal, "pt-BR");

        notification.RefreshStatus();

        Assert.Equal(NotificationStatus.Suppressed, notification.Status);
    }

    [Fact]
    public void RefreshStatus_WhenEveryDeliveryIsDelivered_MarksNotificationAsSent()
    {
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.Delivered, NotificationPriority.Normal, "pt-BR");
        notification.AddDelivery(NotificationChannel.Email, "buyer@example.com", Guid.NewGuid(), 1, "Assunto", "Corpo", DateTimeOffset.UtcNow);
        notification.AddDelivery(NotificationChannel.Push, "push-token", Guid.NewGuid(), 1, null, "Corpo", DateTimeOffset.UtcNow);

        foreach (var delivery in notification.Deliveries)
        {
            delivery.MarkDelivered(DateTimeOffset.UtcNow);
        }

        notification.RefreshStatus();

        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public void ScheduleRetry_LimitsStoredErrorMessage()
    {
        var delivery = NotificationDelivery.Create(Guid.NewGuid(), NotificationChannel.Email, "buyer@example.com", Guid.NewGuid(), 1, "Assunto", "Corpo", DateTimeOffset.UtcNow);

        delivery.ScheduleRetry(new string('x', 1_500), DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.Equal(DeliveryStatus.RetryScheduled, delivery.Status);
        Assert.Equal(1_000, delivery.LastError!.Length);
    }
}
