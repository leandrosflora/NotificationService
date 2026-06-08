namespace NotificationService.Contracts;

public sealed record ProviderDeliveryReceipt(
    string ProviderEventId,
    string ProviderMessageId,
    string Status,
    DateTimeOffset OccurredAt,
    string Signature);
