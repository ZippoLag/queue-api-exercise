namespace CmsWebhook.Domain;

/// <summary>
/// The operation a <see cref="CmsEvent"/> performed upon an external CMS entity.
/// </summary>
/// <remarks>
/// Mirrors the event types of the initial requirements' schema. The wire value is the exact
/// case-sensitive string the CMS sends, hence "unPublish" keeps its capital P (spec:
/// "Validates and sanitizes events" requires a case-sensitive match).
/// </remarks>
public enum CmsEventType
{
    /// <summary>Marks the entity as published and updates it with the newer details.</summary>
    Publish = 0,

    /// <summary>Replaces the entity's content with the newer details without modifying its published flag.</summary>
    Update = 1,

    /// <summary>Marks the entity as not published so it is no longer visible by any User, and updates its contents.</summary>
    UnPublish = 2,

    /// <summary>Removes the entity from the persistence layer (hard delete).</summary>
    Delete = 3,
}
