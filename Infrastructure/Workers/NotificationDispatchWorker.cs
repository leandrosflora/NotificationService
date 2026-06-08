using NotificationService.Application;
using NotificationService.Application.Ports;

namespace NotificationService.Infrastructure.Workers;

public sealed class NotificationDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatchWorker> _logger;

    public NotificationDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Notification dispatch cycle failed");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> deliveryIds;

        using (var scope = _scopeFactory.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            deliveryIds = await repository.ClaimPendingAsync(limit: 100, cancellationToken);
        }

        await Parallel.ForEachAsync(
            deliveryIds,
            new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = cancellationToken },
            async (deliveryId, token) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationDispatchProcessor>();
                await processor.ProcessAsync(deliveryId, token);
            });
    }
}
