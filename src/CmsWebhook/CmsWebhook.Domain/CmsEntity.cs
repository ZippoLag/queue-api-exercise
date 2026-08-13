namespace CmsWebhook.Domain;

/// <summary>
/// The system's internal representation of an entity from the external CMS.
/// </summary>
/// <remarks>
/// Glossary: a <b>CmsEntity</b> is the internal representation of an entity from the external CMS. It
/// keeps track of the latest data version (initial requirements: "Entities must keep track of the latest
/// data version"), the published flag (set by <c>publish</c>/<c>unPublish</c>, untouched by
/// <c>update</c>) and the administrator's visibility override used by the deferred Users API
/// (defaults to visible; disabling is independent of publishing status).
/// </remarks>
public class CmsEntity
{
    /// <summary>
    /// The external entity's id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The latest version of the entity known to our system.
    /// </summary>
    public int LatestVersion { get; set; }

    /// <summary>
    /// The payload of the latest version, as a raw JSON object string.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Whether the entity is currently published and therefore visible to regular Users.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Whether an administrator has disabled this entity from the (future) Users API.
    /// </summary>
    public bool IsVisibleByAdmin { get; set; } = true;

    /// <summary>
    /// When the latest version was last updated, from the event's timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
