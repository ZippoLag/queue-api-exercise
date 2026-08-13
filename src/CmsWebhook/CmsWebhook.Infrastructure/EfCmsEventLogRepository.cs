using CmsWebhook.Application;
using CmsWebhook.Domain;
using Microsoft.EntityFrameworkCore;

namespace CmsWebhook.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="ICmsEventLogRepository"/> over the <c>cms_event_log</c> table.
/// </summary>
/// <remarks>
/// All writes participate in the caller's ambient transaction (the outbox worker opens one per event),
/// so the entity update and the event status change commit atomically (design D5). The query for pending
/// events is indexed on <see cref="CmsEventStatus.Pending"/> and ordered by log id for FIFO processing.
/// </remarks>
public class EfCmsEventLogRepository : ICmsEventLogRepository
{
    private readonly CmsDbContext _dbContext;

    /// <summary>
    /// Creates the repository over the given context.
    /// </summary>
    /// <param name="dbContext">The context exposing the event log table.</param>
    public EfCmsEventLogRepository(CmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task AddAsync(IReadOnlyCollection<CmsEvent> events, CancellationToken cancellationToken)
    {
        _dbContext.Events.AddRange(events);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CmsEvent>> GetPendingAsync(CancellationToken cancellationToken)
        => await _dbContext.Events
            .Where(item => item.Status == CmsEventStatus.Pending)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> ExistsProcessedAsync(
        string entityId,
        CmsEventType type,
        int? version,
        CancellationToken cancellationToken)
        => await _dbContext.Events.AnyAsync(
            item => item.EntityId == entityId
                && item.Type == type
                && item.Version == version
                && item.Status == CmsEventStatus.Processed,
            cancellationToken);

    /// <inheritdoc/>
    public async Task MarkProcessedAsync(long eventId, DateTimeOffset processedAt, CancellationToken cancellationToken)
        => await UpdateStatusAsync(eventId, CmsEventStatus.Processed, processedAt, error: null, cancellationToken);

    /// <inheritdoc/>
    public async Task MarkFailedAsync(long eventId, string error, DateTimeOffset processedAt, CancellationToken cancellationToken)
        => await UpdateStatusAsync(eventId, CmsEventStatus.Failed, processedAt, error, cancellationToken);

    private async Task UpdateStatusAsync(
        long eventId,
        CmsEventStatus status,
        DateTimeOffset processedAt,
        string? error,
        CancellationToken cancellationToken)
    {
        var @event = await _dbContext.Events.FindAsync(new object[] { eventId }, cancellationToken);
        if (@event is null)
        {
            return;
        }

        @event.Status = status;
        @event.ProcessedAt = processedAt;
        @event.Error = error;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
