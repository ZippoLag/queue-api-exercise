namespace QueueApi.Domain;

/// <summary>
/// Represents a publish event from the CMS.
/// New data is only available upon publishing the event.
/// </summary>
public sealed record EntityPublished(
    string Id,
    string PayloadJson,
    int Version,
    DateTimeOffset OccurredAt
);