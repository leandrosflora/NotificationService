namespace NotificationService.Infrastructure.Persistence;

public sealed class MockNotificationRepositoryOptions
{
    public const string SectionName = "Mocks:NotificationRepository";

    public IReadOnlyList<Guid> PendingDeliveryIds { get; init; } = [];
}
