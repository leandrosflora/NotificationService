using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NotificationService.Application;
using NotificationService.Contracts;
using NotificationService.Domain;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.UnitTests;

public sealed class ApplicationTests
{
    [Fact]
    public void TemplateRenderer_Render_ReplacesContractPlaceholders()
    {
        var renderer = new TemplateRenderer();

        var result = renderer.Render(
            "Pedido {{orderId}} enviado para {{buyer.name}} via {{tracking-code}}.",
            new Dictionary<string, string>
            {
                ["orderId"] = "ORD-1",
                ["buyer.name"] = "Ana",
                ["tracking-code"] = "TRK123"
            });

        Assert.Equal("Pedido ORD-1 enviado para Ana via TRK123.", result);
    }

    [Fact]
    public void TemplateRenderer_Render_ThrowsWhenRequiredValueIsMissing()
    {
        var renderer = new TemplateRenderer();

        var exception = Assert.Throws<InvalidOperationException>(() => renderer.Render("Olá {{buyerId}}", new Dictionary<string, string>()));

        Assert.Contains("buyerId", exception.Message);
    }

    [Fact]
    public void NotificationPolicyCatalog_Get_ReturnsContractedShipmentCreatedPolicy()
    {
        var catalog = new NotificationPolicyCatalog();

        var policy = catalog.Get(NotificationType.ShipmentCreated);

        Assert.Equal(NotificationType.ShipmentCreated, policy.Type);
        Assert.Equal(NotificationPriority.Normal, policy.Priority);
        Assert.True(policy.CanUserOptOut);
        Assert.Equal([NotificationChannel.Email, NotificationChannel.Push], policy.DefaultChannels);
    }

    [Fact]
    public async Task NotificationPlanner_HandleShipmentCreated_CreatesNotificationDeliveriesFromTemplatesWithoutExternalDependencies()
    {
        await using var dbContext = CreateDbContext();
        var buyerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await dbContext.RecipientContacts.AddAsync(RecipientContact.Upsert(buyerId, "pt-BR", "buyer@example.com", null, "push-token"));
        await dbContext.NotificationTemplates.AddRangeAsync(
            NotificationTemplate.Create(NotificationType.ShipmentCreated, NotificationChannel.Email, "pt-BR", 1, "Envio {{trackingCode}}", "Pedido {{orderId}} criado"),
            NotificationTemplate.Create(NotificationType.ShipmentCreated, NotificationChannel.Push, "pt-BR", 2, null, "Rastreio {{trackingCode}}"));
        await dbContext.SaveChangesAsync();
        var planner = new NotificationPlanner(dbContext, new NotificationPolicyCatalog(), new TemplateRenderer());
        var integrationEvent = new KafkaEventEnvelope<ShipmentCreatedPayload>(
            eventId,
            "shipment.created",
            "1.0",
            DateTimeOffset.Parse("2026-06-14T10:00:00Z"),
            "corr-1",
            "ShipmentService",
            new ShipmentCreatedPayload(Guid.NewGuid(), Guid.NewGuid(), buyerId, "TRK123", DateOnly.Parse("2026-06-20"), DateTimeOffset.Parse("2026-06-14T10:00:00Z")));

        await planner.HandleAsync(integrationEvent, CancellationToken.None);

        var notification = await dbContext.Notifications.Include(x => x.Deliveries).SingleAsync(x => x.SourceEventId == eventId);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(NotificationType.ShipmentCreated, notification.Type);
        Assert.Equal(NotificationPriority.Normal, notification.Priority);
        Assert.Collection(
            notification.Deliveries.OrderBy(x => x.Channel),
            email =>
            {
                Assert.Equal(NotificationChannel.Email, email.Channel);
                Assert.Equal("buyer@example.com", email.Destination);
                Assert.Equal("Envio TRK123", email.Subject);
                Assert.Contains(integrationEvent.Payload.OrderId.ToString(), email.Body);
            },
            push =>
            {
                Assert.Equal(NotificationChannel.Push, push.Channel);
                Assert.Equal("push-token", push.Destination);
                Assert.Null(push.Subject);
                Assert.Equal("Rastreio TRK123", push.Body);
            });
        Assert.True(await dbContext.InboxMessages.AnyAsync(x => x.MessageId == eventId && x.MessageType == "shipment.created"));
    }

    [Fact]
    public async Task NotificationPlanner_HandleTrackingStatus_DoesNotDuplicateInboxMessage()
    {
        await using var dbContext = CreateDbContext();
        var buyerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await dbContext.RecipientContacts.AddAsync(RecipientContact.Upsert(buyerId, "pt-BR", "buyer@example.com", null, "push-token"));
        await dbContext.NotificationTemplates.AddRangeAsync(
            NotificationTemplate.Create(NotificationType.Delivered, NotificationChannel.Email, "pt-BR", 1, "Entregue", "Entrega {{trackingCode}}"),
            NotificationTemplate.Create(NotificationType.Delivered, NotificationChannel.Push, "pt-BR", 1, null, "Entrega {{trackingCode}}"));
        await dbContext.SaveChangesAsync();
        var planner = new NotificationPlanner(dbContext, new NotificationPolicyCatalog(), new TemplateRenderer());
        var integrationEvent = new TrackingStatusChangedIntegrationEvent(eventId, Guid.NewGuid(), buyerId, "TRK123", "Delivered", DateTimeOffset.Parse("2026-06-14T10:00:00Z"), DateOnly.Parse("2026-06-20"), null);

        await planner.HandleAsync(integrationEvent, CancellationToken.None);
        await planner.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Equal(1, await dbContext.Notifications.CountAsync(x => x.SourceEventId == eventId));
        Assert.Equal(1, await dbContext.InboxMessages.CountAsync(x => x.MessageId == eventId));
    }

    [Fact]
    public async Task NotificationPlanner_HandleOrderCreated_SuppressesNotificationWhenRecipientHasNoContact()
    {
        await using var dbContext = CreateDbContext();
        var eventId = Guid.NewGuid();
        var integrationEvent = new KafkaEventEnvelope<OrderCreatedPayload>(
            eventId,
            "order.created",
            "1.0",
            DateTimeOffset.Parse("2026-06-14T10:00:00Z"),
            "corr-1",
            "OrderService",
            new OrderCreatedPayload(Guid.NewGuid(), Guid.NewGuid()));
        var planner = new NotificationPlanner(dbContext, new NotificationPolicyCatalog(), new TemplateRenderer());

        await planner.HandleAsync(integrationEvent, CancellationToken.None);

        var notification = await dbContext.Notifications.Include(x => x.Deliveries).SingleAsync(x => x.SourceEventId == eventId);
        Assert.Equal(NotificationStatus.Suppressed, notification.Status);
        Assert.Empty(notification.Deliveries);
    }

    private static NotificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new NotificationDbContext(options);
    }
}
