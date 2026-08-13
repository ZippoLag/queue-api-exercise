using CmsWebhook.Domain;

namespace CmsWebhook.Application;

/// <summary>
/// Applies a single recorded <see cref="CmsEvent"/> to the entity store.
/// </summary>
/// <remarks>
/// The write side of the event pipeline: the outbox worker resolves this contract per event and advances
/// the event's status to <see cref="CmsEventStatus.Processed"/> or <see cref="CmsEventStatus.Failed"/>
/// (design D5). The rule logic itself lives in <see cref="CmsEventProcessingRules"/>.
/// </remarks>
public interface ICmsEventProcessor
{
    /// <summary>
    /// Processes the given event, updating the entity store and the event's status.
    /// </summary>
    /// <param name="event">The pending event to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessAsync(CmsEvent @event, CancellationToken cancellationToken);
}
