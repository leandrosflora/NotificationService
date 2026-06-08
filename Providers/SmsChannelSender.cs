using System.Net;
using System.Net.Http.Json;
using NotificationService.Domain;

namespace NotificationService.Providers;

public sealed class SmsChannelSender : INotificationChannelSender
{
    private readonly HttpClient _httpClient;

    public NotificationChannel Channel => NotificationChannel.Sms;

    public SmsChannelSender(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/sms")
        {
            Content = JsonContent.Create(new
            {
                to = request.Destination,
                text = request.Body
            })
        };

        httpRequest.Headers.Add("Idempotency-Key", request.DeliveryId.ToString("N"));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            throw new PermanentProviderException(await response.Content.ReadAsStringAsync(cancellationToken));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"SMS provider returned {response.StatusCode}");
        }

        var providerResponse = await response.Content.ReadFromJsonAsync<ProviderResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty provider response");

        return new ProviderSendResult(providerResponse.MessageId, providerResponse.AcceptedAt);
    }

    private sealed record ProviderResponse(string MessageId, DateTimeOffset AcceptedAt);
}
