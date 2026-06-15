using NotificationService.Domain;

namespace NotificationService.UnitTests;

public sealed class DomainTests
{
    [Fact]
    public void Notification_Create_InitializesPendingNotificationWithoutDeliveries()
    {
        var sourceEventId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var notification = Notification.Create(sourceEventId, recipientId, NotificationType.OrderConfirmed, NotificationPriority.High, "pt-BR");

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(sourceEventId, notification.SourceEventId);
        Assert.Equal(recipientId, notification.RecipientId);
        Assert.Equal(NotificationType.OrderConfirmed, notification.Type);
        Assert.Equal(NotificationPriority.High, notification.Priority);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal("pt-BR", notification.Locale);
        Assert.Empty(notification.Deliveries);
    }

    [Fact]
    public void Notification_RefreshStatus_MarksPartiallySent_WhenSomeDeliveriesAreAcceptedAndOthersFailed()
    {
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.DeliveryException, NotificationPriority.Critical, "pt-BR");
        notification.AddDelivery(NotificationChannel.Email, "buyer@example.com", Guid.NewGuid(), 1, "Subject", "Body", DateTimeOffset.UtcNow);
        notification.AddDelivery(NotificationChannel.Sms, "+5511999999999", Guid.NewGuid(), 1, null, "Body", DateTimeOffset.UtcNow);
        notification.Deliveries[0].MarkAccepted("provider-message-1", DateTimeOffset.UtcNow);
        notification.Deliveries[1].MarkFailed("permanent failure");

        notification.RefreshStatus();

        Assert.Equal(NotificationStatus.PartiallySent, notification.Status);
    }

    [Fact]
    public void NotificationDelivery_MarkBounced_TruncatesLongProviderReason()
    {
        var delivery = NotificationDelivery.Create(Guid.NewGuid(), NotificationChannel.Email, "buyer@example.com", Guid.NewGuid(), 1, "Subject", "Body", DateTimeOffset.UtcNow);

        delivery.MarkBounced(new string('x', 1_200));

        Assert.Equal(DeliveryStatus.Bounced, delivery.Status);
        Assert.Equal(1_000, delivery.LastError?.Length);
    }

    [Theory]
    [InlineData(NotificationChannel.Email, "buyer@example.com")]
    [InlineData(NotificationChannel.Sms, "+5511999999999")]
    [InlineData(NotificationChannel.Push, "push-token")]
    public void RecipientContact_ResolveDestination_ReturnsDestinationForRequestedChannel(NotificationChannel channel, string expected)
    {
        var contact = RecipientContact.Upsert(Guid.NewGuid(), "", "buyer@example.com", "+5511999999999", "push-token");

        Assert.Equal(expected, contact.ResolveDestination(channel));
        Assert.Equal("pt-BR", contact.Locale);
    }
}
