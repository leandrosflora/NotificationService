using NotificationService.Domain;

namespace NotificationService.Providers;

public sealed class NotificationSenderFactory
{
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationChannelSender> _senders;

    public NotificationSenderFactory(IEnumerable<INotificationChannelSender> senders)
    {
        _senders = senders.ToDictionary(x => x.Channel);
    }

    public INotificationChannelSender GetRequired(NotificationChannel channel)
    {
        return _senders.TryGetValue(channel, out var sender)
            ? sender
            : throw new InvalidOperationException($"No provider registered for {channel}");
    }
}
