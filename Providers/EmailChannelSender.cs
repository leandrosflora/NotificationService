using System.Net;
using System.Net.Http.Json;
using NotificationService.Domain;

namespace NotificationService.Providers;

public sealed class EmailChannelSender : INotificationChannelSender
{
    private readonly HttpClient _httpClient;

    public NotificationChannel Channel => NotificationChannel.Email;

    public EmailChannelSender(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/emails")
        {
            Content = JsonContent.Create(new
            {
                to = request.Destination,
                subject = request.Subject,
                body = request.Body
            })
        };

        httpRequest.Headers.Add("Idempotency-Key", request.DeliveryId.ToString("N"));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PermanentProviderException(error);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Email provider returned {response.StatusCode}");
        }

        var providerResponse = await response.Content.ReadFromJsonAsync<ProviderResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty provider response");

        return new ProviderSendResult(providerResponse.MessageId, providerResponse.AcceptedAt);
    }

    private sealed record ProviderResponse(string MessageId, DateTimeOffset AcceptedAt);
}
