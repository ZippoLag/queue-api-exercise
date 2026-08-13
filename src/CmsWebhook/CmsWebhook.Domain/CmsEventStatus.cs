namespace CmsWebhook.Domain;

/// <summary>
/// The lifecycle status of a <see cref="CmsEvent"/> in the event log (outbox).
/// </summary>
/// <remarks>
/// Every accepted event is recorded as <see cref="Pending"/> and the outbox worker advances it to
/// <see cref="Processed"/> or <see cref="Failed"/> (design D5: failed events are not retried
/// automatically; the startup/periodic sweeps only re-process <see cref="Pending"/> rows).
/// </remarks>
public enum CmsEventStatus
{
    /// <summary>Recorded and waiting for asynchronous processing.</summary>
    Pending = 0,

    /// <summary>Processing completed successfully.</summary>
    Processed = 1,

    /// <summary>Processing threw; the error is recorded on the event.</summary>
    Failed = 2,
}
