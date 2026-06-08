using Microsoft.EntityFrameworkCore;
using NotificationService.Application;
using NotificationService.Contracts;
using NotificationService.Domain;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Api;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications").WithTags("Notifications");

        group.MapGet("/{notificationId:guid}", async (Guid notificationId, NotificationDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var notification = await dbContext.Notifications
                .AsNoTracking()
                .Include(x => x.Deliveries)
                .SingleOrDefaultAsync(x => x.Id == notificationId, cancellationToken);

            if (notification is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                notification.Id,
                notification.RecipientId,
                Type = notification.Type.ToString(),
                Status = notification.Status.ToString(),
                notification.Priority,
                notification.CreatedAt,
                Deliveries = notification.Deliveries.Select(x => new
                {
                    x.Id,
                    Channel = x.Channel.ToString(),
                    Status = x.Status.ToString(),
                    x.TemplateVersion,
                    x.Attempts,
                    x.AcceptedAt,
                    x.DeliveredAt,
                    Destination = MaskDestination(x.Channel, x.Destination)
                })
            });
        });

        group.MapPost("/tracking-status-changed", async (TrackingStatusChangedIntegrationEvent integrationEvent, NotificationPlanner planner, CancellationToken cancellationToken) =>
        {
            await planner.HandleAsync(integrationEvent, cancellationToken);
            return Results.Accepted();
        });

        return app;
    }

    private static string MaskDestination(NotificationChannel channel, string destination)
    {
        if (channel == NotificationChannel.Email)
        {
            var parts = destination.Split('@');
            return parts.Length == 2 && parts[0].Length > 0 ? $"{parts[0][0]}***@{parts[1]}" : "***";
        }

        return destination.Length > 4 ? $"***{destination[^4..]}" : "***";
    }
}
