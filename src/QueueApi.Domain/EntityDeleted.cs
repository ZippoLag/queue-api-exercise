namespace QueueApi.Domain;

/// <summary>
/// Represents a delete event from the CMS.
/// Deleting an entity removes it completely from the system (hard-delete).
/// </summary>
public sealed record EntityDeleted(
    string Id,
    DateTimeOffset OccurredAt
);
