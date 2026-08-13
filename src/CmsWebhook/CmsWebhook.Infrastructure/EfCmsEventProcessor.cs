using CmsWebhook.Application;
using CmsWebhook.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CmsWebhook.Infrastructure;

/// <summary>
/// Processes one recorded <see cref="CmsEvent"/> against the entity store inside a single transaction.
/// </summary>
/// <remarks>
/// Design D5: per event the worker loads the current entity, computes the rules outcome, applies it, and
/// advances the event to <see cref="CmsEventStatus.Processed"/> — all in one transaction, so the entity
/// state and the event status never diverge. A thrown failure rolls the transaction back, marks the event
/// <see cref="CmsEventStatus.Failed"/> with its error, and is logged at Error (observability requirement:
/// "log processed events, including failing ones"); failed events are not retried automatically.
/// </remarks>
public class EfCmsEventProcessor : ICmsEventProcessor
{
    private readonly CmsDbContext _dbContext;
    private readonly ICmsEntityRepository _entities;
    private readonly ICmsEventLogRepository _eventLog;
    private readonly ILogger<EfCmsEventProcessor> _logger;

    /// <summary>
    /// Creates the processor with the shared context and the repository ports.
    /// </summary>
    /// <param name="dbContext">The context used to begin the per-event transaction.</param>
    /// <param name="entities">The entity store port.</param>
    /// <param name="eventLog">The event log port.</param>
    /// <param name="logger">The logger.</param>
    public EfCmsEventProcessor(
        CmsDbContext dbContext,
        ICmsEntityRepository entities,
        ICmsEventLogRepository eventLog,
        ILogger<EfCmsEventProcessor> logger)
    {
        _dbContext = dbContext;
        _entities = entities;
        _eventLog = eventLog;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessAsync(CmsEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var current = await _entities.GetByIdAsync(@event.EntityId, cancellationToken);
            var identicalTupleProcessed = await _eventLog.ExistsProcessedAsync(
                @event.EntityId, @event.Type, @event.Version, cancellationToken);

            var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed);
            switch (outcome.Kind)
            {
                case OutcomeKind.Upsert:
                    await _entities.UpsertAsync(outcome.Entity!, cancellationToken);
                    break;
                case OutcomeKind.Delete:
                    await _entities.DeleteAsync(outcome.EntityId!, cancellationToken);
                    break;
            }

            await _eventLog.MarkProcessedAsync(@event.Id, DateTimeOffset.UtcNow, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Processed event {EventId} ({EventType}) for entity {EntityId} version {Version}.",
                @event.Id, @event.Type, @event.EntityId, @event.Version);
        }
        catch (Exception exception)
        {
            // The transaction has been disposed (await using) by the time this catch runs. Marking the
            // event failed is best-effort: if the store itself is unreachable the failure propagates and
            // the worker's per-event guard keeps the queue moving.
            try
            {
                await _eventLog.MarkFailedAsync(@event.Id, exception.Message, DateTimeOffset.UtcNow, cancellationToken);
            }
            catch (Exception markFailedException)
            {
                _logger.LogError(markFailedException, "Could not record failure for event {EventId}.", @event.Id);
            }

            _logger.LogError(exception, "Failed to process event {EventId} for entity {EntityId}.", @event.Id, @event.EntityId);
        }
    }
}
