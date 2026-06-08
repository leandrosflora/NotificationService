using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Ports;

namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _dbContext;

    public NotificationRepository(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> ClaimPendingAsync(int limit, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid();
        var leaseUntil = DateTimeOffset.UtcNow.AddMinutes(2);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE notification_deliveries
            SET status = 'Sending',
                processing_token = {token},
                processing_lease_until = {leaseUntil},
                attempts = attempts + 1,
                updated_at = NOW()
            WHERE id IN
            (
                SELECT id
                FROM notification_deliveries
                WHERE
                    not_before <= NOW()
                    AND
                    (
                        status = 'Pending'
                        OR (status = 'RetryScheduled' AND next_attempt_at <= NOW())
                        OR (status = 'Sending' AND processing_lease_until < NOW())
                    )
                ORDER BY
                    CASE channel
                        WHEN 'Push' THEN 1
                        WHEN 'Sms' THEN 2
                        ELSE 3
                    END,
                    created_at
                FOR UPDATE SKIP LOCKED
                LIMIT {limit}
            )
            """, cancellationToken);

        var ids = await _dbContext.NotificationDeliveries
            .Where(x => x.ProcessingToken == token)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ids;
    }
}
