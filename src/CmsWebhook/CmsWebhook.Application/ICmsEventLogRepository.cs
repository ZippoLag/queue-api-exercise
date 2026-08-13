using CmsWebhook.Domain;

namespace CmsWebhook.Application;

/// <summary>
/// Port for persisting <see cref="CmsEvent"/> rows in the event log (outbox) and advancing their status.
/// </summary>
/// <remarks>
/// Clean architecture: the Application layer depends on this port, never on EF Core. The outbox is an
/// audit log — every accepted delivery is appended (no insert-time dedup); idempotency is enforced at
/// processing time (design D5/D8).
/// </remarks>
public interface ICmsEventLogRepository
{
    /// <summary>
    /// Records the given events with status <see cref="CmsEventStatus.Pending"/>.
    /// </summary>
    /// <param name="events">The validated events to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(IReadOnlyCollection<CmsEvent> events, CancellationToken cancellationToken);

    /// <summary>
    /// Loads all events still awaiting processing, oldest first.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pending events ordered by their log id.</returns>
    Task<IReadOnlyList<CmsEvent>> GetPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns whether an event with the same entity id, type and version has already been processed.
    /// </summary>
    /// <param name="entityId">The entity the event refers to.</param>
    /// <param name="type">The event type.</param>
    /// <param name="version">The event version, or <see langword="null"/> for delete events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when an identical tuple was already processed.</returns>
    Task<bool> ExistsProcessedAsync(string entityId, CmsEventType type, int? version, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the given event as processed.
    /// </summary>
    /// <param name="eventId">The event's log id.</param>
    /// <param name="processedAt">When processing completed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkProcessedAsync(long eventId, DateTimeOffset processedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the given event as failed, recording the error.
    /// </summary>
    /// <param name="eventId">The event's log id.</param>
    /// <param name="error">The failure message to record.</param>
    /// <param name="processedAt">When processing completed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkFailedAsync(long eventId, string error, DateTimeOffset processedAt, CancellationToken cancellationToken);
}
