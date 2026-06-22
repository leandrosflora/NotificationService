using Microsoft.EntityFrameworkCore;
using NotificationService.Contracts;
using NotificationService.Domain;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Api;

public static class PreferenceEndpoints
{
    public static IEndpointRouteBuilder MapPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/notification-preferences").WithTags("Notification Preferences");

        group.MapPut("/{recipientId:guid}/{type}/{channel}", async (
            Guid recipientId,
            NotificationType type,
            NotificationChannel channel,
            UpdatePreferenceRequest request,
            NotificationDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var preference = await dbContext.NotificationPreferences
                .SingleOrDefaultAsync(x => x.RecipientId == recipientId && x.NotificationType == type && x.Channel == channel, cancellationToken);

            if (preference is null)
            {
                preference = NotificationPreference.Create(recipientId, type, channel, request.Enabled);
                await dbContext.NotificationPreferences.AddAsync(preference, cancellationToken);
            }
            else
            {
                preference.Change(request.Enabled);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
