using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NotificationService.Application;
using NotificationService.Contracts;
using NotificationService.Domain;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.UnitTests;

public sealed class NotificationPlannerTests
{
    [Fact]
    public async Task HandleAsync_WhenContactAndTemplatesExist_CreatesDeliveriesFromCanonicalOrderEvent()
    {
        await using var dbContext = CreateDbContext();
        var buyerId = Guid.NewGuid();
        await dbContext.RecipientContacts.AddAsync(RecipientContact.Upsert(buyerId, "pt-BR", "buyer@example.com", null, "push-token"));
        await dbContext.NotificationTemplates.AddRangeAsync(
            NotificationTemplate.Create(NotificationType.OrderConfirmed, NotificationChannel.Email, "pt-BR", 1, "Pedido {{orderId}}", "Olá {{buyerId}}"),
            NotificationTemplate.Create(NotificationType.OrderConfirmed, NotificationChannel.Push, "pt-BR", 1, null, "Pedido {{orderId}} confirmado"));
        await dbContext.SaveChangesAsync();
        var planner = CreatePlanner(dbContext);
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await planner.HandleAsync(new KafkaEventEnvelope<OrderCreatedPayload>(
            eventId,
            "orders.order-created.v1",
            "1.0",
            DateTimeOffset.Parse("2026-06-14T12:00:00Z"),
            "corr-1",
            "OrderService",
            new OrderCreatedPayload(orderId, buyerId)), CancellationToken.None);

        var notification = await dbContext.Notifications.Include(x => x.Deliveries).SingleAsync();
        Assert.Equal(eventId, notification.SourceEventId);
        Assert.Equal(NotificationType.OrderConfirmed, notification.Type);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Collection(notification.Deliveries.OrderBy(x => x.Channel),
            email =>
            {
                Assert.Equal(NotificationChannel.Email, email.Channel);
                Assert.Equal("buyer@example.com", email.Destination);
                Assert.Equal($"Pedido {orderId}", email.Subject);
                Assert.Equal($"Olá {buyerId}", email.Body);
            },
            push =>
            {
                Assert.Equal(NotificationChannel.Push, push.Channel);
                Assert.Equal("push-token", push.Destination);
                Assert.Equal($"Pedido {orderId} confirmado", push.Body);
            });
        Assert.True(await dbContext.InboxMessages.AnyAsync(x => x.MessageId == eventId && x.MessageType == "orders.order-created.v1"));
    }

    [Fact]
    public async Task HandleAsync_WhenRecipientOptedOut_SkipsOptionalChannel()
    {
        await using var dbContext = CreateDbContext();
        var buyerId = Guid.NewGuid();
        await dbContext.RecipientContacts.AddAsync(RecipientContact.Upsert(buyerId, "pt-BR", "buyer@example.com", null, "push-token"));
        await dbContext.NotificationPreferences.AddAsync(NotificationPreference.Create(buyerId, NotificationType.ShipmentCreated, NotificationChannel.Push, enabled: false));
        await dbContext.NotificationTemplates.AddRangeAsync(
            NotificationTemplate.Create(NotificationType.ShipmentCreated, NotificationChannel.Email, "pt-BR", 1, "Envio", "Rastreio {{trackingCode}}"),
            NotificationTemplate.Create(NotificationType.ShipmentCreated, NotificationChannel.Push, "pt-BR", 1, null, "Push {{trackingCode}}"));
        await dbContext.SaveChangesAsync();
        var planner = CreatePlanner(dbContext);

        await planner.HandleAsync(new KafkaEventEnvelope<ShipmentCreatedPayload>(
            Guid.NewGuid(), "shipments.shipment-created.v1", "1.0", DateTimeOffset.UtcNow, "corr-1", "ShippingService",
            new ShipmentCreatedPayload(Guid.NewGuid(), Guid.NewGuid(), buyerId, "TRK123", null, DateTimeOffset.UtcNow)), CancellationToken.None);

        var notification = await dbContext.Notifications.Include(x => x.Deliveries).SingleAsync();
        var delivery = Assert.Single(notification.Deliveries);
        Assert.Equal(NotificationChannel.Email, delivery.Channel);
        Assert.Equal("Rastreio TRK123", delivery.Body);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageWasAlreadyProcessed_DoesNotCreateDuplicateNotification()
    {
        await using var dbContext = CreateDbContext();
        var eventId = Guid.NewGuid();
        await dbContext.InboxMessages.AddAsync(new NotificationService.Infrastructure.Inbox.InboxMessage(eventId, "shipments.shipment-status-updated.v1"));
        await dbContext.SaveChangesAsync();
        var planner = CreatePlanner(dbContext);

        await planner.HandleAsync(new KafkaEventEnvelope<ShipmentStatusUpdatedPayload>(
            eventId, "shipments.shipment-status-updated.v1", "1.0", DateTimeOffset.UtcNow, "corr-1", "TrackingService",
            new ShipmentStatusUpdatedPayload(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TRK123", "carrier", null, "delivered", DateTimeOffset.UtcNow, null, null)), CancellationToken.None);

        Assert.Empty(await dbContext.Notifications.ToListAsync());
    }

    private static NotificationPlanner CreatePlanner(NotificationDbContext dbContext) => new(dbContext, new NotificationPolicyCatalog(), new TemplateRenderer());

    private static NotificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new NotificationDbContext(options);
    }
}
