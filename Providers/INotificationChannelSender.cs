using NotificationService.Domain;

namespace NotificationService.Providers;

public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }
    Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken);
}

public sealed record ProviderSendRequest(Guid DeliveryId, string Destination, string? Subject, string Body);

public sealed record ProviderSendResult(string ProviderMessageId, DateTimeOffset AcceptedAt);

public sealed class PermanentProviderException : Exception
{
    public PermanentProviderException(string message) : base(message)
    {
    }
}
