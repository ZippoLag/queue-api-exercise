namespace QueueApi.Domain;

/// <summary>
/// Represents an unpublish event from the CMS.
/// Unpublishing an entity disables it but keeps the data in the persistence layer.
/// </summary>
public sealed record EntityUnpublished(
    string Id,
    int Version,
    DateTimeOffset OccurredAt
);
