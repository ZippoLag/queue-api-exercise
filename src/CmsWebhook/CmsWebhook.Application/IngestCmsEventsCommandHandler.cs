using CmsWebhook.Domain;

namespace CmsWebhook.Application;

/// <summary>
/// The result of executing the ingest command, mapped by the API to <c>201</c> or <c>400</c>.
/// </summary>
/// <param name="Success">Whether every request was accepted and recorded.</param>
/// <param name="Error">The validation error when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record IngestResult(bool Success, string? Error)
{
    /// <summary>Creates a success result.</summary>
    public static IngestResult Accepted() => new(true, null);

    /// <summary>Creates a rejection result carrying the first validation error.</summary>
    public static IngestResult Rejected(string error) => new(false, error);
}

/// <summary>
/// Command (write side) that validates and durably records incoming CMS events into the outbox.
/// </summary>
public interface IIngestCmsEventsCommandHandler
{
    /// <summary>
    /// Validates and records the given requests, atomically for a batch.
    /// </summary>
    /// <param name="requests">The single request or batch to ingest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome for HTTP mapping.</returns>
    Task<IngestResult> HandleAsync(IReadOnlyCollection<CmsRequest> requests, CancellationToken cancellationToken);
}

/// <summary>
/// Validates and records incoming CMS events, then notifies the outbox worker.
/// </summary>
/// <remarks>
/// Strict CQRS: this is the only write path into the outbox. The batch is all-or-nothing — every request
/// is validated before any event is persisted, so an invalid element rejects the whole batch without
/// recording anything (spec: "Batch recording is atomic"). After a successful persistence the worker is
/// notified so processing starts immediately, without the HTTP response waiting for it.
/// </remarks>
public sealed class IngestCmsEventsCommandHandler : IIngestCmsEventsCommandHandler
{
    private readonly ICmsEventLogRepository _eventLog;
    private readonly IOutboxNotifier _notifier;

    /// <summary>
    /// Creates the handler with the given event log port and outbox notifier.
    /// </summary>
    /// <param name="eventLog">The outbox to record into.</param>
    /// <param name="notifier">The signal that wakes the outbox worker.</param>
    public IngestCmsEventsCommandHandler(ICmsEventLogRepository eventLog, IOutboxNotifier notifier)
    {
        _eventLog = eventLog;
        _notifier = notifier;
    }

    /// <inheritdoc/>
    public async Task<IngestResult> HandleAsync(
        IReadOnlyCollection<CmsRequest> requests,
        CancellationToken cancellationToken)
    {
        var events = new List<CmsEvent>(requests.Count);
        foreach (var request in requests)
        {
            if (!CmsRequestValidator.TryValidate(request, out var @event, out var error))
            {
                return IngestResult.Rejected(error!);
            }

            events.Add(@event!);
        }

        await _eventLog.AddAsync(events, cancellationToken);
        _notifier.Notify();
        return IngestResult.Accepted();
    }
}
