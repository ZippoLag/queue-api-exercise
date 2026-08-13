namespace CmsWebhook.Domain;

/// <summary>
/// An event published by the external CMS, recorded in the event log for asynchronous processing.
/// </summary>
/// <remarks>
/// Glossary: a <b>CmsEvent</b> is an event published by the external CMS which notifies our system of
/// something that has already happened to an entity, and contains the full details of the received
/// <b>CmsRequest</b> (except headers). Every accepted delivery is stored in the <c>cms_event_log</c>
/// outbox with status <see cref="CmsEventStatus.Pending"/> and is then advanced by the outbox worker
/// (design D2/D5). <see cref="Timestamp"/> is when the event happened in the CMS, whereas
/// <see cref="ReceivedAt"/> is when our system recorded it.
/// </remarks>
public class CmsEvent
{
    /// <summary>
    /// The event log's primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The external entity's id the event refers to.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// The operation performed upon the entity.
    /// </summary>
    public CmsEventType Type { get; set; }

    /// <summary>
    /// The entity's version from the external system; <see langword="null"/> for <see cref="CmsEventType.Delete"/> events.
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// The entity's payload as a raw JSON object string; <see langword="null"/> for delete events.
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// When the event happened in the external CMS (ISO 8601 date-time).
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// When our system recorded the event.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>
    /// The event's lifecycle status in the outbox.
    /// </summary>
    public CmsEventStatus Status { get; set; } = CmsEventStatus.Pending;

    /// <summary>
    /// The recorded failure message when <see cref="Status"/> is <see cref="CmsEventStatus.Failed"/>.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// When processing finished (successfully or not).
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }
}
