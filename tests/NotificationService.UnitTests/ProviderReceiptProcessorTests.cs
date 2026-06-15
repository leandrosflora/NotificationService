using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NotificationService.Application;
using NotificationService.Application.Ports;
using NotificationService.Contracts;
using NotificationService.Domain;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.UnitTests;

public sealed class ProviderReceiptProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenReceiptIsDelivered_MarksDeliveryDeliveredAndPublishesDomainEvent()
    {
        await using var dbContext = CreateDbContext();
        var outbox = new RecordingOutboxWriter();
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.Delivered, NotificationPriority.Normal, "pt-BR");
        notification.AddDelivery(NotificationChannel.Email, "buyer@example.com", Guid.NewGuid(), 1, "Assunto", "Corpo", DateTimeOffset.UtcNow);
        var delivery = notification.Deliveries.Single();
        var acceptedAt = DateTimeOffset.Parse("2026-06-14T12:00:00Z");
        delivery.MarkAccepted("provider-message-1", acceptedAt);
        await dbContext.Notifications.AddAsync(notification);
        await dbContext.SaveChangesAsync();
        var processor = new ProviderReceiptProcessor(dbContext, outbox);
        var deliveredAt = DateTimeOffset.Parse("2026-06-14T12:05:00Z");

        await processor.ProcessAsync("email-provider", new ProviderDeliveryReceipt("provider-event-1", "provider-message-1", "Delivered", deliveredAt, "signature"), CancellationToken.None);

        var persisted = await dbContext.NotificationDeliveries.SingleAsync(x => x.Id == delivery.Id);
        Assert.Equal(DeliveryStatus.Delivered, persisted.Status);
        Assert.Equal(deliveredAt, persisted.DeliveredAt);
        Assert.Equal(NotificationStatus.Sent, (await dbContext.Notifications.SingleAsync()).Status);
        var message = Assert.Single(outbox.Messages);
        Assert.Equal("notification.events", message.Topic);
        Assert.Equal(notification.Id.ToString(), message.AggregateKey);
        var domainEvent = Assert.IsType<NotificationDeliveredIntegrationEvent>(message.Message);
        Assert.Equal(notification.Id, domainEvent.NotificationId);
        Assert.Equal(delivery.Id, domainEvent.DeliveryId);
    }

    [Fact]
    public async Task ProcessAsync_WhenReceiptIsBounced_MarksDeliveryBouncedWithoutOutboxEvent()
    {
        await using var dbContext = CreateDbContext();
        var outbox = new RecordingOutboxWriter();
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.Delivered, NotificationPriority.Normal, "pt-BR");
        notification.AddDelivery(NotificationChannel.Email, "buyer@example.com", Guid.NewGuid(), 1, "Assunto", "Corpo", DateTimeOffset.UtcNow);
        var delivery = notification.Deliveries.Single();
        delivery.MarkAccepted("provider-message-2", DateTimeOffset.UtcNow);
        await dbContext.Notifications.AddAsync(notification);
        await dbContext.SaveChangesAsync();
        var processor = new ProviderReceiptProcessor(dbContext, outbox);

        await processor.ProcessAsync("email-provider", new ProviderDeliveryReceipt("provider-event-2", "provider-message-2", "Bounced", DateTimeOffset.UtcNow, "signature"), CancellationToken.None);

        var persisted = await dbContext.NotificationDeliveries.SingleAsync(x => x.Id == delivery.Id);
        Assert.Equal(DeliveryStatus.Bounced, persisted.Status);
        Assert.Contains("email-provider", persisted.LastError);
        Assert.Equal(NotificationStatus.Failed, (await dbContext.Notifications.SingleAsync()).Status);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task ProcessAsync_WhenProviderMessageIsUnknown_DoesNothing()
    {
        await using var dbContext = CreateDbContext();
        var outbox = new RecordingOutboxWriter();
        var processor = new ProviderReceiptProcessor(dbContext, outbox);

        await processor.ProcessAsync("email-provider", new ProviderDeliveryReceipt("provider-event-3", "missing", "Delivered", DateTimeOffset.UtcNow, "signature"), CancellationToken.None);

        Assert.Empty(outbox.Messages);
        Assert.Empty(await dbContext.Notifications.ToListAsync());
    }

    private static NotificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new NotificationDbContext(options);
    }

    private sealed class RecordingOutboxWriter : IOutboxWriter
    {
        public List<(string Topic, string AggregateKey, object Message)> Messages { get; } = [];

        public Task AddAsync(string topic, string aggregateKey, object message, CancellationToken cancellationToken)
        {
            Messages.Add((topic, aggregateKey, message));
            return Task.CompletedTask;
        }
    }
}
