namespace QueueApi.Domain;

/// <summary>
/// Represents the current state of an entity in the system.
/// Entities must keep track of the latest data version.
/// Entities must allow admins to disable entities without affecting CMS data.
/// </summary>
public sealed class EntityState
{
    /// <summary>
    /// The unique identifier of the entity.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The entity's payload data serialized as JSON.
    /// </summary>
    public string PayloadJson { get; }

    /// <summary>
    /// The latest version number of the entity, as provided by the CMS.
    /// </summary>
    public int LatestVersion { get; }

    /// <summary>
    /// Indicates whether the entity has been published and is active.
    /// </summary>
    public bool IsPublished { get; }

    /// <summary>
    /// Indicates whether an admin has disabled this entity via the API.
    /// This is an overwrite that does not affect the CMS data.
    /// </summary>
    public bool IsAdminDisabled { get; }

    private EntityState(
        string id,
        string payloadJson,
        int latestVersion,
        bool isPublished,
        bool isAdminDisabled)
    {
        Id = id;
        PayloadJson = payloadJson;
        LatestVersion = latestVersion;
        IsPublished = isPublished;
        IsAdminDisabled = isAdminDisabled;
    }

    /// <summary>
    /// Applies a publish event to the current entity state, creating a new state with the event's data.
    /// When current is null, creates a new published entity state.
    /// This implements the event processing logic where new data is only available upon publishing.
    /// </summary>
    public static EntityState Apply(EntityState? current, EntityPublished published)
    {
        return new EntityState(
            id: published.Id,
            payloadJson: published.PayloadJson,
            latestVersion: published.Version,
            isPublished: true,
            isAdminDisabled: false
        );
    }

    /// <summary>
    /// Applies an unpublish event to the current entity state, creating a new state with the entity marked as unpublished.
    /// According to requirements: "unpublish should still keep the data in your persistence layer".
    /// The entity data (payload, version) is preserved but IsPublished is set to false.
    /// </summary>
    public static EntityState Apply(EntityState current, EntityUnpublished unpublished)
    {
        return new EntityState(
            id: current.Id,
            payloadJson: current.PayloadJson,
            latestVersion: current.LatestVersion,
            isPublished: false,
            isAdminDisabled: current.IsAdminDisabled
        );
    }

    /// <summary>
    /// Applies a delete event to the current entity state, returning null to indicate hard deletion.
    /// According to requirements: "Deleted entities should be removed (hard-delete)".
    /// </summary>
    public static EntityState? Apply(EntityState current, EntityDeleted deleted)
    {
        return null;
    }

    /// <summary>
    /// Applies an admin disable event to the current entity state, marking it as admin-disabled.
    /// According to requirements: "an admin can disable them from the API - this will not affect the CMS, it's an overwrite that does not affect CMS data".
    /// The entity data is preserved, IsPublished remains unchanged, and IsAdminDisabled is set to true.
    /// </summary>
    public static EntityState Apply(EntityState current, EntityAdminDisabled adminDisabled)
    {
        return new EntityState(
            id: current.Id,
            payloadJson: current.PayloadJson,
            latestVersion: current.LatestVersion,
            isPublished: current.IsPublished,
            isAdminDisabled: true
        );
    }
}