using Microsoft.EntityFrameworkCore;
using NotificationService.Contracts;
using NotificationService.Domain;
using NotificationService.Infrastructure.Inbox;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application;

public sealed class NotificationPlanner
{
    private readonly NotificationDbContext _dbContext;
    private readonly NotificationPolicyCatalog _policyCatalog;
    private readonly TemplateRenderer _renderer;

    public NotificationPlanner(NotificationDbContext dbContext, NotificationPolicyCatalog policyCatalog, TemplateRenderer renderer)
    {
        _dbContext = dbContext;
        _policyCatalog = policyCatalog;
        _renderer = renderer;
    }

    public Task HandleAsync(TrackingStatusChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var type = MapType(integrationEvent.CurrentStatus);

        return PlanAsync(
            sourceEventId: integrationEvent.MessageId,
            messageType: nameof(TrackingStatusChangedIntegrationEvent),
            recipientId: integrationEvent.BuyerId,
            type: type,
            templateValues: CreateTemplateValues(integrationEvent),
            cancellationToken: cancellationToken);
    }

    public Task HandleAsync(KafkaEventEnvelope<OrderCreatedPayload> integrationEvent, CancellationToken cancellationToken)
    {
        return PlanAsync(
            sourceEventId: integrationEvent.EventId,
            messageType: integrationEvent.EventType,
            recipientId: integrationEvent.Payload.BuyerId,
            type: NotificationType.OrderConfirmed,
            templateValues: new Dictionary<string, string>
            {
                ["orderId"] = integrationEvent.Payload.OrderId.ToString(),
                ["occurredAt"] = integrationEvent.OccurredAt.ToString("O")
            },
            cancellationToken: cancellationToken);
    }

    public Task HandleAsync(KafkaEventEnvelope<ShipmentCreatedPayload> integrationEvent, CancellationToken cancellationToken)
    {
        return PlanAsync(
            sourceEventId: integrationEvent.EventId,
            messageType: integrationEvent.EventType,
            recipientId: integrationEvent.Payload.BuyerId,
            type: NotificationType.ShipmentCreated,
            templateValues: new Dictionary<string, string>
            {
                ["shipmentId"] = integrationEvent.Payload.ShipmentId.ToString(),
                ["trackingCode"] = integrationEvent.Payload.TrackingCode ?? string.Empty,
                ["estimatedDeliveryDate"] = integrationEvent.Payload.EstimatedDeliveryDate?.ToString("dd/MM/yyyy") ?? "não informada"
            },
            cancellationToken: cancellationToken);
    }

    public Task HandleAsync(KafkaEventEnvelope<ShipmentStatusUpdatedPayload> integrationEvent, CancellationToken cancellationToken)
    {
        var type = MapType(integrationEvent.Payload.CurrentStatus);

        return PlanAsync(
            sourceEventId: integrationEvent.EventId,
            messageType: integrationEvent.EventType,
            recipientId: integrationEvent.Payload.BuyerId,
            type: type,
            templateValues: new Dictionary<string, string>
            {
                ["shipmentId"] = integrationEvent.Payload.ShipmentId.ToString(),
                ["trackingCode"] = integrationEvent.Payload.TrackingCode ?? string.Empty,
                ["status"] = integrationEvent.Payload.CurrentStatus,
                ["estimatedDeliveryDate"] = integrationEvent.Payload.EstimatedDeliveryDate?.ToString("dd/MM/yyyy") ?? "não informada",
                ["exceptionCode"] = integrationEvent.Payload.ExceptionCode ?? string.Empty
            },
            cancellationToken: cancellationToken);
    }

    private async Task PlanAsync(Guid sourceEventId, string messageType, Guid recipientId, NotificationType type, IReadOnlyDictionary<string, string> templateValues, CancellationToken cancellationToken)
    {
        if (await _dbContext.InboxMessages.AnyAsync(x => x.MessageId == sourceEventId, cancellationToken))
        {
            return;
        }

        var policy = _policyCatalog.Get(type);

        var contact = await _dbContext.RecipientContacts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.RecipientId == recipientId, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var notification = Notification.Create(
            sourceEventId: sourceEventId,
            recipientId: recipientId,
            type: type,
            priority: policy.Priority,
            locale: contact?.Locale ?? "pt-BR");

        if (contact is not null)
        {
            var preferences = await _dbContext.NotificationPreferences
                .AsNoTracking()
                .Where(x => x.RecipientId == recipientId && x.NotificationType == type)
                .ToListAsync(cancellationToken);

            var templates = await _dbContext.NotificationTemplates
                .AsNoTracking()
                .Where(x => x.Type == type && x.Locale == notification.Locale && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var channel in policy.DefaultChannels)
            {
                var destination = contact.ResolveDestination(channel);
                if (string.IsNullOrWhiteSpace(destination))
                {
                    continue;
                }

                if (policy.CanUserOptOut)
                {
                    var preference = preferences.FirstOrDefault(x => x.Channel == channel);
                    if (preference is { Enabled: false })
                    {
                        continue;
                    }
                }

                var template = templates
                    .Where(x => x.Channel == channel)
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefault();

                if (template is null)
                {
                    continue;
                }

                var subject = template.SubjectTemplate is null ? null : _renderer.Render(template.SubjectTemplate, templateValues);
                var body = _renderer.Render(template.BodyTemplate, templateValues);

                notification.AddDelivery(channel, destination, template.Id, template.Version, subject, body, DateTimeOffset.UtcNow);
            }
        }

        if (notification.Deliveries.Count == 0)
        {
            notification.MarkSuppressed();
        }

        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
        await _dbContext.InboxMessages.AddAsync(new InboxMessage(sourceEventId, messageType), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> CreateTemplateValues(TrackingStatusChangedIntegrationEvent source)
    {
        return new Dictionary<string, string>
        {
            ["trackingCode"] = source.TrackingCode,
            ["status"] = source.CurrentStatus,
            ["estimatedDeliveryDate"] = source.EstimatedDeliveryDate?.ToString("dd/MM/yyyy") ?? "não informada",
            ["exceptionCode"] = source.ExceptionCode ?? string.Empty
        };
    }

    private static NotificationType MapType(string status)
    {
        return status switch
        {
            "OutForDelivery" => NotificationType.OutForDelivery,
            "Delivered" => NotificationType.Delivered,
            "Exception" => NotificationType.DeliveryException,
            _ => throw new InvalidOperationException($"No policy for status {status}")
        };
    }
}
