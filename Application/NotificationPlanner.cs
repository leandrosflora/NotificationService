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

    public async Task HandleAsync(TrackingStatusChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (await _dbContext.InboxMessages.AnyAsync(x => x.MessageId == integrationEvent.MessageId, cancellationToken))
        {
            return;
        }

        var type = MapType(integrationEvent.CurrentStatus);
        var policy = _policyCatalog.Get(type);

        var contact = await _dbContext.RecipientContacts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.RecipientId == integrationEvent.BuyerId, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var notification = Notification.Create(
            sourceEventId: integrationEvent.MessageId,
            recipientId: integrationEvent.BuyerId,
            type: type,
            priority: policy.Priority,
            locale: contact?.Locale ?? "pt-BR");

        if (contact is not null)
        {
            var preferences = await _dbContext.NotificationPreferences
                .AsNoTracking()
                .Where(x => x.RecipientId == integrationEvent.BuyerId && x.NotificationType == type)
                .ToListAsync(cancellationToken);

            var templates = await _dbContext.NotificationTemplates
                .AsNoTracking()
                .Where(x => x.Type == type && x.Locale == notification.Locale && x.IsActive)
                .ToListAsync(cancellationToken);

            var values = CreateTemplateValues(integrationEvent);

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

                var subject = template.SubjectTemplate is null ? null : _renderer.Render(template.SubjectTemplate, values);
                var body = _renderer.Render(template.BodyTemplate, values);

                notification.AddDelivery(channel, destination, template.Id, template.Version, subject, body, DateTimeOffset.UtcNow);
            }
        }

        if (notification.Deliveries.Count == 0)
        {
            notification.MarkSuppressed();
        }

        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
        await _dbContext.InboxMessages.AddAsync(new InboxMessage(integrationEvent.MessageId, nameof(TrackingStatusChangedIntegrationEvent)), cancellationToken);
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
