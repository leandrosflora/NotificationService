using Microsoft.Extensions.Options;
using NotificationService.Application.Ports;

namespace NotificationService.Infrastructure.Persistence;

public sealed class MockNotificationRepository : INotificationRepository
{
    private readonly IReadOnlyList<Guid> _pendingDeliveryIds;
    private readonly ILogger<MockNotificationRepository> _logger;

    public MockNotificationRepository(
        IOptions<MockNotificationRepositoryOptions> options,
        ILogger<MockNotificationRepository> logger)
    {
        _pendingDeliveryIds = options.Value.PendingDeliveryIds;
        _logger = logger;
    }

    public Task<IReadOnlyList<Guid>> ClaimPendingAsync(int limit, CancellationToken cancellationToken)
    {
        var claimedDeliveryIds = _pendingDeliveryIds
            .Take(limit)
            .ToArray();

        _logger.LogInformation(
            "Mock notification repository claimed {DeliveryCount} pending deliveries with limit {Limit}",
            claimedDeliveryIds.Length,
            limit);

        return Task.FromResult<IReadOnlyList<Guid>>(claimedDeliveryIds);
    }
}
