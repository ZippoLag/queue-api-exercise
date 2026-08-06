namespace QueueApi.Domain;

/// <summary>
/// Represents an admin disable event from the API.
/// Admin disabling an entity marks it as disabled but does not affect the CMS data.
/// This is an overwrite that does not affect the underlying CMS data.
/// </summary>
public sealed record EntityAdminDisabled(
    string Id,
    DateTimeOffset OccurredAt
);
