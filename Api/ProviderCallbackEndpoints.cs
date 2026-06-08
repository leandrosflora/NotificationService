using NotificationService.Application;
using NotificationService.Contracts;

namespace NotificationService.Api;

public static class ProviderCallbackEndpoints
{
    public static IEndpointRouteBuilder MapProviderCallbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/providers").WithTags("Provider Callbacks");

        group.MapPost("/{provider}/receipts", async (
            string provider,
            ProviderDeliveryReceipt receipt,
            ProviderReceiptProcessor processor,
            CancellationToken cancellationToken) =>
        {
            await processor.ProcessAsync(provider, receipt, cancellationToken);
            return Results.Accepted();
        });

        return app;
    }
}
