namespace NotificationService.Application.Ports;

public interface INotificationRepository
{
    Task<IReadOnlyList<Guid>> ClaimPendingAsync(int limit, CancellationToken cancellationToken);
}
