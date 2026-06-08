namespace NotificationService.Domain;

public sealed class RecipientContact
{
    public Guid RecipientId { get; private set; }
    public string Locale { get; private set; } = "pt-BR";
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? PushToken { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RecipientContact()
    {
    }

    public static RecipientContact Upsert(Guid recipientId, string locale, string? email, string? phoneNumber, string? pushToken)
    {
        return new RecipientContact
        {
            RecipientId = recipientId,
            Locale = string.IsNullOrWhiteSpace(locale) ? "pt-BR" : locale,
            Email = email,
            PhoneNumber = phoneNumber,
            PushToken = pushToken,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public string? ResolveDestination(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => Email,
            NotificationChannel.Sms => PhoneNumber,
            NotificationChannel.Push => PushToken,
            _ => null
        };
    }
}
